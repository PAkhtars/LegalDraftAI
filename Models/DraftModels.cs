namespace LegalDraftAI.Models;

public class DraftRequest
{
    public string DocumentType { get; set; } = string.Empty; // BailApplication, AdvocateBill, AbsenceApplication
    public string Jurisdiction { get; set; } = "Delhi";
    public string ExtractedClientFacts { get; set; } = string.Empty;
    public Dictionary<string, string>? ExtraFields { get; set; }
    public Guid? CaseId { get; set; }
    public CivilPlaintDetailsDto? CivilPlaintDetails { get; set; }
}

public class DraftResult
{
    public bool Success { get; set; }
    public string? RawJson { get; set; }
    public object? StructuredData { get; set; }
    public string? Error { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}

public class ExportRequest
{
    public string DocumentType { get; set; } = string.Empty;
    public string Json { get; set; } = string.Empty;
}

public sealed class AffidavitPdfRequest
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>Request to export edited Civil Plaint text, optionally merged into a saved DOCX format.</summary>
public sealed class CivilPlaintDocxExportRequest
{
    /// <summary>Edited plaint text used when generating a standard DOCX without a selected template.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Optional Civil draft format master whose uploaded DOCX contains placeholders to fill.</summary>
    public Guid? DraftFormatMasterId { get; set; }

    /// <summary>Generated plaint field values used to replace matching DOCX placeholders, including document1 through document4.</summary>
    public Dictionary<string, string> TemplateValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>OCR text extracted from one uploaded image.</summary>
public sealed record ImageOcrResult(string FileName, string Text);

/// <summary>Request to create an editable DOCX from reviewed OCR text.</summary>
public sealed record ImageOcrDocxExportRequest
{
    /// <summary>Reviewed OCR text to place into the Word document.</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>Request to create a DOCX from a reviewed Stage B service application.</summary>
public sealed record StageBServiceDocxExportRequest
{
    /// <summary>Reviewed Stage B application text to place into the Word document.</summary>
    public string Text { get; init; } = string.Empty;
}

public class CivilStageAApplicationDraft
{
    public string? CourtName { get; set; }
    public string? CaseTitle { get; set; }
    public string? CaseNumber { get; set; }
    public string ApplicationTitle { get; set; } = string.Empty;
    public string? Provision { get; set; }
    public string? ApplicantName { get; set; }
    public string? RespondentName { get; set; }
    public string ApplicationType { get; set; } = string.Empty;
    public List<string> FactualBackground { get; set; } = [];
    public List<string> Grounds { get; set; } = [];
    public List<string> DocumentsReliedOn { get; set; } = [];
    public string Prayer { get; set; } = string.Empty;
    public string? SupportingAffidavit { get; set; }
    public string? Place { get; set; }
    public string? Date { get; set; }
    public string? AdvocateName { get; set; }
    public string? AdvocateEnrollmentNo { get; set; }
}

public sealed class CivilStageBServiceApplicationDraft : CivilStageAApplicationDraft
{
    public string? DefendantServiceAddress { get; set; }
}

public sealed class CivilStageAProposedNoticeDraft
{
    public string? CourtName { get; set; }
    public string? CaseTitle { get; set; }
    public string? CaseNumber { get; set; }
    public string? PlaintiffName { get; set; }
    public string? DefendantName { get; set; }
    public string? DefendantAddress { get; set; }
    public string NoticeTitle { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public List<string> NoticeParagraphs { get; set; } = [];
    public List<string> EvidenceReferences { get; set; } = [];
    public string? ResponseDirection { get; set; }
    public string? Place { get; set; }
    public string? Date { get; set; }
    public string? AdvocateName { get; set; }
    public string? AdvocateEnrollmentNo { get; set; }
}

public sealed class HindiTranslationRequest
{
    public string Text { get; set; } = string.Empty;
}

public class BailApplicationDraft
{
    public string CourtName { get; set; } = "";
    public string CaseTitle { get; set; } = "";
    public string CaseNumber { get; set; } = "";
    public string ApplicantName { get; set; } = "";
    public string RespondentName { get; set; } = "";
    public string? FirNumber { get; set; }
    public string? PoliceStation { get; set; }   // Thana
    public string? District { get; set; }
    public List<string> Sections { get; set; } = new();  // e.g. BNS / BNSS sections
    public List<string> Grounds { get; set; } = new();
    public string Prayer { get; set; } = "";
    public string Place { get; set; } = "";
    public string Date { get; set; } = "";
    public string AdvocateName { get; set; } = "";
    public string? AdvocateEnrollmentNo { get; set; }
}

public class AdvocateBillDraft
{
    public string BillNumber { get; set; } = "";
    public string CourtName { get; set; } = "";
    public string CaseNumber { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string CaseTitle { get; set; } = "";
    public string PeriodFrom { get; set; } = "";
    public string PeriodTo { get; set; } = "";
    public List<BillItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string AdvocateName { get; set; } = "";
    public string? BankDetails { get; set; }
}

public class BillItem
{
    public string Description { get; set; } = "";
    public string Date { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>
/// Application for Exemption from Personal Appearance / Absence on Hearing Date
/// </summary>
public class AbsenceApplicationDraft
{
    public string CourtName { get; set; } = "";
    public string CaseTitle { get; set; } = "";
    public string CaseNumber { get; set; } = "";
    public string ApplicantName { get; set; } = "";          // Party seeking exemption
    public string ApplicantStatus { get; set; } = "";        // e.g. "Accused", "Plaintiff", "Defendant"
    public string OppositePartyName { get; set; } = "";
    public string? FirNumber { get; set; }
    public string? PoliceStation { get; set; }   // Thana
    public string? District { get; set; }
    public List<string> Sections { get; set; } = new();  // e.g. BNS / BNSS sections
    public string HearingDate { get; set; } = "";            // Date on which absence is sought

    /// <summary>
    /// Codes selected from the dropdown (see AbsenceGrounds.Options).
    /// Example: ["MEDICAL", "OUT_OF_STATION"]
    /// </summary>
    public List<string> SelectedGroundCodes { get; set; } = new();

    /// <summary>
    /// Human-readable labels corresponding to SelectedGroundCodes (filled by the service).
    /// </summary>
    public List<string> SelectedGroundLabels { get; set; } = new();

    /// <summary>
    /// Free-text main reason (especially useful when "OTHER" is selected).
    /// </summary>
    public string ReasonForAbsence { get; set; } = "";

    /// <summary>
    /// Additional detailed grounds / paragraphs generated or provided.
    /// </summary>
    public List<string> Grounds { get; set; } = new();

    public string Prayer { get; set; } = "";
    public string Place { get; set; } = "";
    public string Date { get; set; } = "";                   // Date of application
    public string AdvocateName { get; set; } = "";
    public string? AdvocateEnrollmentNo { get; set; }
}

/// <summary>
/// Application for issuance of summons / summoning the opposite (counter) party
/// </summary>
public class SummonApplicationDraft
{
    public string CourtName { get; set; } = "";
    public string CaseTitle { get; set; } = "";
    public string CaseNumber { get; set; } = "";
    public string ApplicantName { get; set; } = "";          // Party seeking summons (Plaintiff / Petitioner)
    public string ApplicantStatus { get; set; } = "";        // e.g. "Plaintiff", "Petitioner", "Complainant"
    public string OppositePartyName { get; set; } = "";      // Party to be summoned
    public string OppositePartyAddress { get; set; } = "";   // Last known address
    public string? OppositePartyStatus { get; set; }         // e.g. "Defendant", "Respondent"
    public string? FirNumber { get; set; }
    public string? PoliceStation { get; set; }   // Thana
    public string? District { get; set; }
    public List<string> Sections { get; set; } = new();  // e.g. BNS / BNSS sections
    public string NextHearingDate { get; set; } = "";        // Date already fixed or proposed
    public List<string> Grounds { get; set; } = new();       // Why summons is required
    public string Prayer { get; set; } = "";
    public string Place { get; set; } = "";
    public string Date { get; set; } = "";
    public string AdvocateName { get; set; } = "";
    public string? AdvocateEnrollmentNo { get; set; }
}

/// <summary>
/// Application for certified copies / supply of court documents
/// (FIR, Chargesheet, Plaint, Order-sheets, etc.)
/// </summary>
public class CertifiedCopyApplicationDraft
{
    public string CourtName { get; set; } = "";
    public string CaseTitle { get; set; } = "";
    public string CaseNumber { get; set; } = "";
    public string ApplicantName { get; set; } = "";
    public string ApplicantStatus { get; set; } = "";        // Party / Advocate / Third party
    public string OppositePartyName { get; set; } = "";
    public string? FirNumber { get; set; }
    public string? PoliceStation { get; set; }   // Thana
    public string? District { get; set; }
    public List<string> Sections { get; set; } = new();  // e.g. BNS / BNSS sections

    /// <summary>
    /// Codes of documents selected from dropdown (see CourtDocumentTypes.Options)
    /// </summary>
    public List<string> SelectedDocumentCodes { get; set; } = new();

    /// <summary>
    /// Human-readable labels of selected documents
    /// </summary>
    public List<string> SelectedDocumentLabels { get; set; } = new();

    /// <summary>
    /// Free-text additional documents or details
    /// </summary>
    public string? AdditionalDocuments { get; set; }

    public string Purpose { get; set; } = "";                // Why copies are required
    public List<string> Grounds { get; set; } = new();
    public string Prayer { get; set; } = "";
    public string Place { get; set; } = "";
    public string Date { get; set; } = "";
    public string AdvocateName { get; set; } = "";
    public string? AdvocateEnrollmentNo { get; set; }
}

/// <summary>
/// Curative Petition before the Supreme Court of India
/// (filed after dismissal of Review Petition – Rupa Ashok Hurra principle)
/// </summary>
public class CurativePetitionDraft
{
    public string CourtName { get; set; } = "Supreme Court of India";
    public string CaseTitle { get; set; } = "";
    public string CaseNumber { get; set; } = "";              // Original / connected case number
    public string? ReviewPetitionNumber { get; set; }         // Review Petition that was dismissed
    public string? ReviewDismissalDate { get; set; }          // Date of dismissal of Review
    public string PetitionerName { get; set; } = "";
    public string RespondentName { get; set; } = "";
    public string? FirNumber { get; set; }
    public string? PoliceStation { get; set; }
    public string? District { get; set; }
    public List<string> Sections { get; set; } = new();
    public List<string> Grounds { get; set; } = new();        // Curative grounds (natural justice / bias etc.)
    public string Prayer { get; set; } = "";
    public string Place { get; set; } = "";
    public string Date { get; set; } = "";
    public string AdvocateName { get; set; } = "";
    public string? AdvocateEnrollmentNo { get; set; }
    public string? AdvocateOnRecord { get; set; }             // AOR name if different
}

/// <summary>
/// Complaint / Application before Magistrate for non-registration of FIR
/// (typically under Section 156(3) BNSS / CrPC or Section 200 complaint)
/// </summary>
public class ComplaintNonRegistrationFirDraft
{
    public string CourtName { get; set; } = "";              // e.g. Court of Metropolitan Magistrate
    public string CaseTitle { get; set; } = "";
    public string? CaseNumber { get; set; }                  // If already numbered
    public string ComplainantName { get; set; } = "";
    public string ComplainantAddress { get; set; } = "";
    public string AccusedName { get; set; } = "";
    public string? AccusedAddress { get; set; }
    public string PoliceStation { get; set; } = "";          // Thana where FIR was refused
    public string District { get; set; } = "";
    public string? DateOfIncident { get; set; }
    public string? DateOfComplaintToPolice { get; set; }     // When written complaint was given to police
    public string? DateOfRefusal { get; set; }               // When police refused / did not act
    public List<string> Sections { get; set; } = new();      // Alleged offences (BNS sections)
    public List<string> Facts { get; set; } = new();         // Brief facts of the offence
    public List<string> Grounds { get; set; } = new();       // Why direction for FIR is sought
    public string Prayer { get; set; } = "";
    public string Place { get; set; } = "";
    public string Date { get; set; } = "";
    public string AdvocateName { get; set; } = "";
    public string? AdvocateEnrollmentNo { get; set; }
}

/// <summary>
/// Civil plaint (title, possession, injunction, money, partition, etc.)
/// </summary>
public class CivilPlaintDraft
{
    public string CourtName { get; set; } = "";
    public string CaseTitle { get; set; } = "";
    public string? CaseNumber { get; set; }
    public string PlaintNature { get; set; } = "";           // e.g. AncestralLandPossession
    public string SuitTitle { get; set; } = "";
    public string PlaintiffName { get; set; } = "";
    public string PlaintiffAddress { get; set; } = "";
    public string DefendantName { get; set; } = "";
    public string DefendantAddress { get; set; } = "";
    public string? SuitPropertyDescription { get; set; }     // Schedule of property
    public string? SurveyNumber { get; set; }
    public string? Village { get; set; }
    public string? Tehsil { get; set; }
    public string? District { get; set; }
    public string? Area { get; set; }
    public List<string> Facts { get; set; } = new();         // Numbered facts of the case
    public List<string> Grounds { get; set; } = new();       // Legal grounds
    public List<string> ReliefsSought { get; set; } = new(); // Declaration, possession, injunction, etc.
    public string Prayer { get; set; } = "";
    public string? Valuation { get; set; }                   // For court fee
    public string? CauseOfActionDate { get; set; }
    public string? CauseOfAction { get; set; }
    public string? TerritorialJurisdiction { get; set; }
    public string? LimitationPleading { get; set; }
    public string? CourtFeePleading { get; set; }
    public string Place { get; set; } = "";
    public string Date { get; set; } = "";
    public string AdvocateName { get; set; } = "";
    public string? AdvocateEnrollmentNo { get; set; }
}
