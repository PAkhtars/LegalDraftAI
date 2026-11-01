namespace LegalDraftAI.Services;

public interface IFileStorageService
{
    /// <summary>Saves a file under cases/{caseId}/documents/ and returns relative path.</summary>
    Task<string> SaveCaseDocumentAsync(Guid caseId, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>Saves a PDF under cases/{caseId}/drafts/ and returns its path relative to the storage root.</summary>
    Task<string> SaveCaseDraftPdfAsync(Guid caseId, string fileName, byte[] pdfBytes, CancellationToken ct = default);

    Task<string> SaveDraftFormatMasterAsync(Guid masterId, string fileName, Stream content, CancellationToken ct = default);

    string GetAbsolutePath(string relativePath);

    Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default);

    Task DeleteCaseDocumentAsync(Guid caseId, string relativePath, CancellationToken ct = default);

    Task DeleteDraftFormatMasterAsync(Guid masterId, string relativePath, CancellationToken ct = default);
}
