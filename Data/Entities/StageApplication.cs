namespace LegalDraftAI.Data.Entities;

/// <summary>A selected stage-specific application within a case.</summary>
public class StageApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public CaseRecord Case { get; set; } = null!;
    public string StageCode { get; set; } = "";
    public string ApplicationCode { get; set; } = "";
    public string? DraftContent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<CaseDocument> Documents { get; set; } = new List<CaseDocument>();
    public ICollection<CaseDraft> Drafts { get; set; } = new List<CaseDraft>();
}
