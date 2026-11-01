namespace LegalDraftAI.Data.Entities;

/// <summary>Uploaded supporting document linked to a case.</summary>
public class CaseDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public CaseRecord Case { get; set; } = null!;
    public Guid? StageApplicationId { get; set; }
    public StageApplication? StageApplication { get; set; }
    public Guid? CaseDraftId { get; set; }

    /// <summary>Catalog code e.g. TITLE_DEED, REVENUE_RECORD</summary>
    public string DocumentCode { get; set; } = "OTHER";
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }

    /// <summary>Legacy file path or SQL locator for generated stage PDFs.</summary>
    public string StoragePath { get; set; } = "";

    /// <summary>Uploaded file bytes stored in SQL Server for new documents.</summary>
    public byte[]? FileContent { get; set; }

    /// <summary>Extracted text for AI context (may be truncated)</summary>
    public string? ExtractedText { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
