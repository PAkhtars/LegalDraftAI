using LegalDraftAI.Models;

namespace LegalDraftAI.Services;

public interface ICaseService
{
    Task<CaseSummaryDto> CreateAsync(CreateCaseRequest request, CancellationToken ct = default);
    Task<CaseDetailDto?> GetAsync(Guid caseId, CancellationToken ct = default);
    Task<IReadOnlyList<CaseSummaryDto>> ListAsync(
        string? status = null,
        string? documentType = null,
        CancellationToken ct = default);
    Task<CaseSummaryDto?> UpdateAsync(Guid caseId, UpdateCaseRequest request, CancellationToken ct = default);
    Task<CaseDetailDto?> SaveCivilPlaintDetailsAsync(
        Guid caseId,
        CivilPlaintDetailsDto details,
        CancellationToken ct = default);
    Task<CaseDetailDto?> SaveCivilPlaintSectionAsync(
        Guid caseId,
        string section,
        CivilPlaintDetailsDto details,
        CancellationToken ct = default);
    Task<CaseDocumentDto> AddDocumentAsync(
        Guid caseId,
        string documentCode,
        IFormFile file,
        CancellationToken ct = default,
        string? extractedText = null,
        string? stageCode = null,
        string? applicationCode = null);
    Task<StoredCaseDocumentFile?> GetDocumentFileAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(Guid caseId, Guid documentId, CancellationToken ct = default);
    Task<DocumentResearchResult> ResearchAsync(Guid caseId, CancellationToken ct = default);
    Task<CaseDraftDetailDto> GenerateDraftAsync(Guid caseId, GenerateCaseDraftRequest request, CancellationToken ct = default);
    Task<CaseDraftDetailDto?> GetDraftAsync(Guid caseId, Guid draftId, CancellationToken ct = default);
    Task<byte[]?> GetDraftPdfAsync(Guid caseId, Guid draftId, CancellationToken ct = default);
}
