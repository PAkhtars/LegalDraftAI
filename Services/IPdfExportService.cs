namespace LegalDraftAI.Services;

public interface IPdfExportService
{
    byte[] GenerateBailApplicationPdf(Models.BailApplicationDraft draft);
    byte[] GenerateAdvocateBillPdf(Models.AdvocateBillDraft draft);
    byte[] GenerateAbsenceApplicationPdf(Models.AbsenceApplicationDraft draft);
    byte[] GenerateSummonApplicationPdf(Models.SummonApplicationDraft draft);
    byte[] GenerateCertifiedCopyApplicationPdf(Models.CertifiedCopyApplicationDraft draft);
    byte[] GenerateCurativePetitionPdf(Models.CurativePetitionDraft draft);
    byte[] GenerateComplaintNonRegistrationFirPdf(Models.ComplaintNonRegistrationFirDraft draft);
    byte[] GenerateCivilPlaintPdf(Models.CivilPlaintDraft draft, string outputLanguage = "English");
    byte[] GenerateCivilStageAApplicationPdf(Models.CivilStageAApplicationDraft draft);
    byte[] GenerateCivilStageAProposedNoticePdf(Models.CivilStageAProposedNoticeDraft draft);
    byte[] GenerateCivilStageBServiceApplicationPdf(Models.CivilStageBServiceApplicationDraft draft);
    byte[] GenerateAffidavitPdf(string affidavitText);
    byte[] GenerateFromJson(string documentType, string json, string outputLanguage = "English");
}
