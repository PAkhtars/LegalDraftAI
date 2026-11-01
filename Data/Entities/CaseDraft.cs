namespace LegalDraftAI.Data.Entities;

/// <summary>Generated or saved legal draft with JSON and optional PDF storage.</summary>
public class CaseDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public CaseRecord Case { get; set; } = null!;
    public Guid? StageApplicationId { get; set; }
    public StageApplication? StageApplication { get; set; }

    /// <summary>BailApplication, CivilPlaint, AbsenceApplication, etc.</summary>
    public string DocumentType { get; set; } = "";
    public string? ApplicationCode { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "Draft";            // Draft / Final / Superseded

    /// <summary>Full structured JSON returned by AI / edited by user</summary>
    public string JsonContent { get; set; } = "{}";

    /// <summary>Generated PDF bytes stored in SQL Server for non-stage drafts.</summary>
    public byte[]? PdfContent { get; set; }

    /// <summary>Relative physical PDF path under the configured storage root.</summary>
    public string? PdfStoragePath { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
