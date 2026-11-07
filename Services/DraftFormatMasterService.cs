using LegalDraftAI.Data;
using LegalDraftAI.Data.Entities;
using LegalDraftAI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LegalDraftAI.Services;

public sealed class DraftFormatMasterService(
    AppDbContext db,
    IFileStorageService files) : IDraftFormatMasterService
{
    public async Task<IReadOnlyList<DraftFormatMasterResponse>> ListAsync(
        string? category,
        CancellationToken cancellationToken)
    {
        var query = db.DraftFormatMasters.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(item => item.Category == category);

        var items = await query
            .OrderBy(item => item.Category)
            .ThenBy(item => item.FormatName)
            .ToListAsync(cancellationToken);
        return items.Select(ToResponse).ToList();
    }

    public async Task<DraftFormatMasterResponse?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await db.DraftFormatMasters.AsNoTracking()
            .FirstOrDefaultAsync(master => master.Id == id, cancellationToken);
        return item is null ? null : ToResponse(item);
    }

    public Task<bool> NameExistsAsync(
        string category,
        string formatName,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        db.DraftFormatMasters.AnyAsync(
            item => item.Category == category &&
                    item.FormatName == formatName &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public async Task<DraftFormatMasterResponse> CreateAsync(
        CreateDraftFormatMasterRequest request,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        string path;
        await using (var stream = file.OpenReadStream())
        {
            path = await files.SaveDraftFormatMasterAsync(
                id,
                file.FileName,
                stream,
                cancellationToken);
        }

        var item = new DraftFormatMaster
        {
            Id = id,
            Category = request.Category.Trim(),
            FormatName = request.FormatName.Trim(),
            Description = NormalizeOptional(request.Description),
            DraftFormatPath = path,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileSizeBytes = file.Length
        };

        try
        {
            db.DraftFormatMasters.Add(item);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await files.DeleteDraftFormatMasterAsync(id, path, cancellationToken);
            throw;
        }

        return ToResponse(item);
    }

    public async Task<DraftFormatMasterResponse?> UpdateAsync(
        Guid id,
        UpdateDraftFormatMasterRequest request,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var item = await db.DraftFormatMasters
            .FirstOrDefaultAsync(master => master.Id == id, cancellationToken);
        if (item is null)
            return null;

        var previousPath = item.DraftFormatPath;
        string? uploadedPath = null;
        if (file is not null)
        {
            await using var stream = file.OpenReadStream();
            uploadedPath = await files.SaveDraftFormatMasterAsync(
                id,
                file.FileName,
                stream,
                cancellationToken);
        }

        item.Category = request.Category.Trim();
        item.FormatName = request.FormatName.Trim();
        item.Description = NormalizeOptional(request.Description);
        if (file is not null && uploadedPath is not null)
        {
            item.DraftFormatPath = uploadedPath;
            item.OriginalFileName = Path.GetFileName(file.FileName);
            item.ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            item.FileSizeBytes = file.Length;
        }
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (uploadedPath is not null)
                await files.DeleteDraftFormatMasterAsync(id, uploadedPath, cancellationToken);
            throw;
        }

        if (uploadedPath is not null && !string.IsNullOrWhiteSpace(previousPath))
            await files.DeleteDraftFormatMasterAsync(id, previousPath, cancellationToken);
        return ToResponse(item);
    }

    public async Task<StoredDraftFormatFile?> GetFileAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await db.DraftFormatMasters.AsNoTracking()
            .FirstOrDefaultAsync(master => master.Id == id, cancellationToken);
        if (item is null)
            return null;
        var expectedPrefix = $"draft-format-masters/{id:N}/";
        if (string.IsNullOrWhiteSpace(item.DraftFormatPath) ||
            !item.DraftFormatPath.Replace('\\', '/').StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var content = await files.ReadAsync(item.DraftFormatPath, cancellationToken);
        return content is null
            ? null
            : new StoredDraftFormatFile(content, item.OriginalFileName, item.ContentType);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.DraftFormatMasters
            .FirstOrDefaultAsync(master => master.Id == id, cancellationToken);
        if (item is null)
            return false;

        db.DraftFormatMasters.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(item.DraftFormatPath))
            await files.DeleteDraftFormatMasterAsync(id, item.DraftFormatPath, cancellationToken);
        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DraftFormatMasterResponse ToResponse(DraftFormatMaster item) =>
        new(
            item.Id,
            item.Category,
            item.FormatName,
            item.Description,
            item.OriginalFileName,
            item.DraftFormatPath,
            item.ContentType,
            item.FileSizeBytes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
}
