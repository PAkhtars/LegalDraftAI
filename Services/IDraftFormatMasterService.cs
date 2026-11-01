using LegalDraftAI.Models;
using Microsoft.AspNetCore.Http;

namespace LegalDraftAI.Services;

public interface IDraftFormatMasterService
{
    Task<IReadOnlyList<DraftFormatMasterResponse>> ListAsync(
        string? category,
        CancellationToken cancellationToken);
    Task<DraftFormatMasterResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(
        string category,
        string formatName,
        Guid? excludingId,
        CancellationToken cancellationToken);
    Task<DraftFormatMasterResponse> CreateAsync(
        CreateDraftFormatMasterRequest request,
        IFormFile file,
        CancellationToken cancellationToken);
    Task<DraftFormatMasterResponse?> UpdateAsync(
        Guid id,
        UpdateDraftFormatMasterRequest request,
        IFormFile? file,
        CancellationToken cancellationToken);
    Task<StoredDraftFormatFile?> GetFileAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record StoredDraftFormatFile(
    byte[] Content,
    string OriginalFileName,
    string ContentType);
