using System.ComponentModel.DataAnnotations;

namespace LegalDraftAI.Models;

public sealed class CivilPlaintDetailsDto
{
    [StringLength(100)] public string? PlaintNature { get; set; }
    [StringLength(100)] public string? Jurisdiction { get; set; }
    public CivilPlaintCourtAndSuitDto CourtAndSuit { get; set; } = new();
    public CivilPlaintSuitPropertyDto SuitProperty { get; set; } = new();
    public CivilPlaintFactsAndReliefDto FactsAndRelief { get; set; } = new();
    public CivilPlaintFilingDetailsDto FilingDetails { get; set; } = new();
}

public sealed class CivilPlaintCourtAndSuitDto
{
    [StringLength(300)] public string? CourtName { get; set; }
    [StringLength(500)] public string? CaseTitle { get; set; }
    [StringLength(100)] public string? CaseNumber { get; set; }
    [StringLength(50)] public string? CauseOfActionDate { get; set; }
    [StringLength(300)] public string? PlaintiffName { get; set; }
    [StringLength(300)] public string? DefendantName { get; set; }
    [StringLength(2000)] public string? PlaintiffAddress { get; set; }
    [StringLength(2000)] public string? DefendantAddress { get; set; }
}

public sealed class CivilPlaintSuitPropertyDto
{
    [StringLength(5000)] public string? PropertyDescription { get; set; }
    [StringLength(200)] public string? SurveyPlotNumber { get; set; }
    [StringLength(200)] public string? Area { get; set; }
    [StringLength(200)] public string? Village { get; set; }
    [StringLength(200)] public string? Tehsil { get; set; }
    [StringLength(200)] public string? District { get; set; }
    [StringLength(500)] public string? SuitValuationCourtFee { get; set; }
}

public sealed class CivilPlaintFactsAndReliefDto
{
    [StringLength(20000)] public string? Facts { get; set; }
    [StringLength(10000)] public string? LegalGrounds { get; set; }
    [StringLength(10000)] public string? ReliefsSought { get; set; }
    [StringLength(5000)] public string? Prayer { get; set; }
}

public sealed class CivilPlaintFilingDetailsDto
{
    [StringLength(200)] public string? Place { get; set; }
    [StringLength(50)] public string? Date { get; set; }
    [StringLength(300)] public string? AdvocateName { get; set; }
    [StringLength(100)] public string? EnrollmentNumber { get; set; }
}
