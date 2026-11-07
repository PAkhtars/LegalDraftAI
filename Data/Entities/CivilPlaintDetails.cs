namespace LegalDraftAI.Data.Entities;

public sealed class CivilPlaintCourtAndSuitRecord
{
    public Guid CaseId { get; set; }
    public CaseRecord Case { get; set; } = null!;
    public string? CourtName { get; set; }
    public string? CaseTitle { get; set; }
    public string? CaseNumber { get; set; }
    public string? CauseOfActionDate { get; set; }
    public string? PlaintiffName { get; set; }
    public string? DefendantName { get; set; }
    public string? PlaintiffAddress { get; set; }
    public string? DefendantAddress { get; set; }
}

public sealed class CivilPlaintSuitPropertyRecord
{
    public Guid CaseId { get; set; }
    public CaseRecord Case { get; set; } = null!;
    public string? PropertyDescription { get; set; }
    public string? SurveyPlotNumber { get; set; }
    public string? Area { get; set; }
    public string? Village { get; set; }
    public string? Tehsil { get; set; }
    public string? District { get; set; }
    public string? SuitValuationCourtFee { get; set; }
}

public sealed class CivilPlaintFactsAndReliefRecord
{
    public Guid CaseId { get; set; }
    public CaseRecord Case { get; set; } = null!;
    public string? Facts { get; set; }
    public string? LegalGrounds { get; set; }
    public string? ReliefsSought { get; set; }
    public string? Prayer { get; set; }
}

public sealed class CivilPlaintFilingDetailsRecord
{
    public Guid CaseId { get; set; }
    public CaseRecord Case { get; set; } = null!;
    public string? Place { get; set; }
    public string? Date { get; set; }
    public string? AdvocateName { get; set; }
    public string? EnrollmentNumber { get; set; }
}
