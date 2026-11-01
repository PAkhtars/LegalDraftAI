using LegalDraftAI.Data.Entities;
using LegalDraftAI.Models;

namespace LegalDraftAI.Services;

internal static class CivilPlaintDetailsMapper
{
    public static void Apply(CaseRecord caseRecord, CivilPlaintDetailsDto details)
    {
        var courtAndSuit = caseRecord.CivilPlaintCourtAndSuit ??= new();
        courtAndSuit.CourtName = details.CourtAndSuit.CourtName;
        courtAndSuit.CaseTitle = details.CourtAndSuit.CaseTitle;
        courtAndSuit.CaseNumber = details.CourtAndSuit.CaseNumber;
        courtAndSuit.CauseOfActionDate = details.CourtAndSuit.CauseOfActionDate;
        courtAndSuit.PlaintiffName = details.CourtAndSuit.PlaintiffName;
        courtAndSuit.DefendantName = details.CourtAndSuit.DefendantName;
        courtAndSuit.PlaintiffAddress = details.CourtAndSuit.PlaintiffAddress;
        courtAndSuit.DefendantAddress = details.CourtAndSuit.DefendantAddress;

        var suitProperty = caseRecord.CivilPlaintSuitProperty ??= new();
        suitProperty.PropertyDescription = details.SuitProperty.PropertyDescription;
        suitProperty.SurveyPlotNumber = details.SuitProperty.SurveyPlotNumber;
        suitProperty.Area = details.SuitProperty.Area;
        suitProperty.Village = details.SuitProperty.Village;
        suitProperty.Tehsil = details.SuitProperty.Tehsil;
        suitProperty.District = details.SuitProperty.District;
        suitProperty.SuitValuationCourtFee = details.SuitProperty.SuitValuationCourtFee;

        var factsAndRelief = caseRecord.CivilPlaintFactsAndRelief ??= new();
        factsAndRelief.Facts = details.FactsAndRelief.Facts;
        factsAndRelief.LegalGrounds = details.FactsAndRelief.LegalGrounds;
        factsAndRelief.ReliefsSought = details.FactsAndRelief.ReliefsSought;
        factsAndRelief.Prayer = details.FactsAndRelief.Prayer;

        var filingDetails = caseRecord.CivilPlaintFilingDetails ??= new();
        filingDetails.Place = details.FilingDetails.Place;
        filingDetails.Date = details.FilingDetails.Date;
        filingDetails.AdvocateName = details.FilingDetails.AdvocateName;
        filingDetails.EnrollmentNumber = details.FilingDetails.EnrollmentNumber;
    }
}
