namespace LegalDraftAI.Data.Entities;

/// <summary>A legal matter / case file for a client.</summary>
public class CaseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "";
    public string CaseCategory { get; set; } = "Civil";      // Civil / Criminal
    public string? PlaintNature { get; set; }                // AncestralLandPossession, etc.
    public string Jurisdiction { get; set; } = "";
    public string? CourtName { get; set; }
    public string? CaseNumber { get; set; }                  // Allotted by court later
    public string Status { get; set; } = "Open";             // Open / Closed / Archived

    public string? PlaintiffName { get; set; }
    public string? PlaintiffAddress { get; set; }
    public string? DefendantName { get; set; }
    public string? DefendantAddress { get; set; }

    public string? ClientFacts { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public CivilPlaintCourtAndSuitRecord? CivilPlaintCourtAndSuit { get; set; }
    public CivilPlaintSuitPropertyRecord? CivilPlaintSuitProperty { get; set; }
    public CivilPlaintFactsAndReliefRecord? CivilPlaintFactsAndRelief { get; set; }
    public CivilPlaintFilingDetailsRecord? CivilPlaintFilingDetails { get; set; }
    public ICollection<CaseDocument> Documents { get; set; } = new List<CaseDocument>();
    public ICollection<CaseDraft> Drafts { get; set; } = new List<CaseDraft>();
    public ICollection<StageApplication> StageApplications { get; set; } = new List<StageApplication>();
}
