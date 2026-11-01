namespace LegalDraftAI.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;

    public LocalFileStorageService(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Storage:RootPath"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "storage")
            : configured;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveCaseDocumentAsync(Guid caseId, string fileName, Stream content, CancellationToken ct = default)
    {
        var safe = Sanitize(fileName);
        var relative = Path.Combine("cases", caseId.ToString("N"), "documents", $"{Guid.NewGuid():N}_{safe}");
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return relative.Replace('\\', '/');
    }

    public async Task<string> SaveCaseDraftPdfAsync(Guid caseId, string fileName, byte[] pdfBytes, CancellationToken ct = default)
    {
        var safe = Sanitize(fileName);
        var relative = Path.Combine("cases", caseId.ToString("N"), "drafts", safe);
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, pdfBytes, ct);
        return relative.Replace('\\', '/');
    }

    public async Task<string> SaveDraftFormatMasterAsync(
        Guid masterId,
        string fileName,
        Stream content,
        CancellationToken ct = default)
    {
        var safe = Sanitize(fileName);
        var relative = Path.Combine(
            "draft-format-masters",
            masterId.ToString("N"),
            $"{Guid.NewGuid():N}_{safe}");
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fileStream = File.Create(full);
        await content.CopyToAsync(fileStream, ct);
        return relative.Replace('\\', '/');
    }

    public string GetAbsolutePath(string relativePath) =>
        Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        var full = GetAbsolutePath(relativePath);
        if (!File.Exists(full)) return null;
        return await File.ReadAllBytesAsync(full, ct);
    }

    public Task DeleteCaseDocumentAsync(Guid caseId, string relativePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var normalizedRelativePath = relativePath.Replace('\\', '/');
        var expectedPrefix = $"cases/{caseId:N}/documents/";
        if (!normalizedRelativePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The document storage path does not belong to this case.");

        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(GetAbsolutePath(normalizedRelativePath));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The document storage path is outside the configured storage root.");

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task DeleteDraftFormatMasterAsync(Guid masterId, string relativePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var normalizedRelativePath = relativePath.Replace('\\', '/');
        var expectedPrefix = $"draft-format-masters/{masterId:N}/";
        if (!normalizedRelativePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The draft format storage path does not belong to this master.");

        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(GetAbsolutePath(normalizedRelativePath));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The draft format storage path is outside the configured storage root.");

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "file.bin" : name;
    }
}
