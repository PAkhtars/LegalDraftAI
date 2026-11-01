namespace LegalDraftAI.Models;

public class CreateCaseRequest
{
    public string Title { get; set; } = "";
    public string CaseCategory { get; set; } = "Civil";
    public string? PlaintNature { get; set; }
    public string Jurisdiction { get; set; } = "Delhi";
    public string? CourtName { get; set; }
    public string? CaseNumber { get; set; }
    public string? PlaintiffName { get; set; }
    public string? PlaintiffAddress { get; set; }
    public string? DefendantName { get; set; }
    public string? DefendantAddress { get; set; }
    public string? ClientFacts { get; set; }
    public string? Notes { get; set; }
}

public class UpdateCaseRequest
{
    public string? Title { get; set; }
    public string? CourtName { get; set; }
    public string? CaseNumber { get; set; }
    public string? Status { get; set; }
    public string? PlaintiffName { get; set; }
    public string? PlaintiffAddress { get; set; }
    public string? DefendantName { get; set; }
    public string? DefendantAddress { get; set; }
    public string? ClientFacts { get; set; }
    public string? Notes { get; set; }
    public string? PlaintNature { get; set; }
}

public class CaseSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string CaseCategory { get; set; } = "";
    public string? PlaintNature { get; set; }
    public string Jurisdiction { get; set; } = "";
    public string? CourtName { get; set; }
    public string? CaseNumber { get; set; }
    public string Status { get; set; } = "";
    public string? PlaintiffName { get; set; }
    public string? DefendantName { get; set; }
    public int DocumentCount { get; set; }
    public int DraftCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CaseDetailDto : CaseSummaryDto
{
    public string? PlaintiffAddress { get; set; }
    public string? DefendantAddress { get; set; }
    public string? ClientFacts { get; set; }
    public string? Notes { get; set; }
    public CivilPlaintDetailsDto? CivilPlaintDetails { get; set; }
    public List<CaseDocumentDto> Documents { get; set; } = new();
    public List<CaseDraftDto> Drafts { get; set; } = new();
    public List<StageApplicationDto> StageApplications { get; set; } = new();
}

public class CaseDocumentDto
{
    public Guid Id { get; set; }
    public Guid? StageApplicationId { get; set; }
    public Guid? CaseDraftId { get; set; }
    public string? StageCode { get; set; }
    public string? ApplicationCode { get; set; }
    public string DocumentCode { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public bool HasExtractedText { get; set; }
    public string? ExtractedText { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public sealed class StoredCaseDocumentFile
{
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
}

public class CaseDraftDto
{
    public Guid Id { get; set; }
    public Guid? StageApplicationId { get; set; }
    public string? StageCode { get; set; }
    public string DocumentType { get; set; } = "";
    public string? ApplicationCode { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "";
    public string? PdfStoragePath { get; set; }
    public bool HasPdf { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class StageApplicationDto
{
    public Guid Id { get; set; }
    public string StageCode { get; set; } = "";
    public string ApplicationCode { get; set; } = "";
    public string? DraftContent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class GenerateCaseDraftRequest
{
    public string DocumentType { get; set; } = "CivilPlaint";
    public string? StageCode { get; set; }
    public string? ApplicationCode { get; set; }
    public string? ExtraInstructions { get; set; }
    public List<Guid> ExcludedDocumentIds { get; set; } = [];
    public bool SavePdf { get; set; } = true;
}

public class CaseDraftDetailDto : CaseDraftDto
{
    public string JsonContent { get; set; } = "{}";
}
