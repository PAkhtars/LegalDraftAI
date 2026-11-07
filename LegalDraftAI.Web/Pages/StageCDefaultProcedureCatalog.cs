namespace LegalDraftAI.Web.Pages;

public sealed record StageCDefaultProcedureDocument(
    string Code,
    string Label,
    string Guidance,
    IReadOnlyList<string> ExistingDocumentCodes);

public sealed record StageCDefaultProcedure(
    string Code,
    string Label,
    string Provision,
    string Scenario,
    IReadOnlyList<StageCDefaultProcedureDocument> Documents);

public static class StageCDefaultProcedureCatalog
{
    public static readonly IReadOnlyList<StageCDefaultProcedure> Procedures =
    [
        new(
            "ProceedExParte",
            "Request that the court proceed ex parte",
            "Order IX Rule 6, Code of Civil Procedure, 1908",
            "The defendant does not appear when the suit is called for hearing, and the court is satisfied that summons was duly served or that another condition in Rule 6 applies.",
            [
                Document("STAGE_C_SERVICE_PROOF", "Proof of due service of summons", "Provide the served summons, process-server return, postal acknowledgment, or other reliable record showing due service and its date.", ["SERVICE_RETURN", "RETURNED_SUMMONS", "POSTAL_RECORD", "PROCESS_SERVER_REPORT"]),
                Document("STAGE_C_HEARING_RECORD", "Order sheet or hearing record", "Provide the order sheet showing the hearing date and the defendant's non-appearance, if available.", ["ORDER_SHEET", "COURT_ORDER"]),
                Document("STAGE_C_PLAINT_EVIDENCE", "Plaintiff's evidence and supporting records", "Identify the evidence the plaintiff proposes to lead. Do not assert that a decree follows automatically from non-appearance.", ["EVIDENCE", "SUPPORTING_DOCUMENTS"])
            ]),
        new(
            "DefendantAppearsExParte",
            "Request permission for defendant to participate in an ex parte proceeding",
            "Order IX Rule 7, Code of Civil Procedure, 1908",
            "The defendant appears at or before a subsequent hearing after the suit has been directed to proceed ex parte and seeks leave to participate, subject to the applicable conditions.",
            [
                Document("STAGE_C_EXPARTE_ORDER", "Order directing the matter to proceed ex parte", "Provide the relevant order and subsequent order sheets to establish the procedural stage.", ["COURT_ORDER", "ORDER_SHEET"]),
                Document("STAGE_C_DEFENDANT_ABSENCE_CAUSE", "Evidence explaining the earlier non-appearance", "Provide records supporting the defendant's explanation, such as medical or travel documents, only where applicable and available.", ["SUPPORTING_DOCUMENTS", "MEDICAL_RECORD"]),
                Document("STAGE_C_NEXT_HEARING", "Next hearing date or current case status", "State the current hearing date and procedural status as reflected in the court record.", ["ORDER_SHEET", "COURT_ORDER"])
            ]),
        new(
            "SetAsideExParteDecree",
            "Application to set aside an ex parte decree",
            "Order IX Rule 13, Code of Civil Procedure, 1908",
            "An ex parte decree has been passed and the defendant seeks to set it aside on a legally available ground. Limitation and service/knowledge dates require careful verification.",
            [
                Document("STAGE_C_EXPARTE_DECREE", "Copy of the ex parte decree", "Provide the decree and judgment/order date. Do not state a decree date unless supported by the record.", ["DECREE", "COURT_ORDER"]),
                Document("STAGE_C_SUMMONS_SERVICE_RECORD", "Summons and service record", "Provide the summons, service return, and related orders relevant to due service or any failure of service.", ["SERVICE_RETURN", "RETURNED_SUMMONS", "POSTAL_RECORD", "PROCESS_SERVER_REPORT"]),
                Document("STAGE_C_KNOWLEDGE_DATE_EVIDENCE", "Evidence of date of knowledge, if relied on", "If relying on a later date of knowledge, provide the facts and documents establishing when and how the decree became known.", ["SUPPORTING_DOCUMENTS"]),
                Document("STAGE_C_ABSENCE_CAUSE", "Evidence supporting sufficient cause for non-appearance", "Provide evidence for the specific reason the defendant could not appear or was prevented from appearing, where that ground is relied on.", ["SUPPORTING_DOCUMENTS", "MEDICAL_RECORD"])
            ]),
        new(
            "RestoreDismissedSuit",
            "Application to restore a suit dismissed for default",
            "Order IX Rule 9, Code of Civil Procedure, 1908",
            "The plaintiff's suit has been dismissed for default and the plaintiff seeks restoration by showing sufficient cause for non-appearance, subject to the applicable procedure.",
            [
                Document("STAGE_C_DISMISSAL_ORDER", "Order dismissing the suit for default", "Provide the dismissal order and order sheet, including the date and recorded reason for dismissal.", ["COURT_ORDER", "ORDER_SHEET"]),
                Document("STAGE_C_PLAINTIFF_ABSENCE_CAUSE", "Evidence supporting the plaintiff's reason for absence", "Provide documents supporting the specific reason the plaintiff or authorized representative could not appear.", ["SUPPORTING_DOCUMENTS", "MEDICAL_RECORD"]),
                Document("STAGE_C_RESTORATION_CHRONOLOGY", "Chronology from dismissal to restoration request", "State when the dismissal became known, steps taken afterward, and relevant dates supported by records.", ["SUPPORTING_DOCUMENTS"])
            ]),
        new(
            "CondoneDelayRestoration",
            "Application to condone delay in seeking restoration, where maintainable",
            "Section 5, Limitation Act, 1963, where applicable",
            "A restoration application may be delayed. Explain the complete period of delay and why the delay may legally be condoned; applicability and maintainability must be verified.",
            [
                Document("STAGE_C_DISMISSAL_ORDER", "Order dismissing the suit for default", "Provide the dismissal order and date to support the limitation calculation.", ["COURT_ORDER", "ORDER_SHEET"]),
                Document("STAGE_C_DELAY_CHRONOLOGY", "Date-wise explanation for the delay", "Set out the complete chronology, including when the dismissal became known and each material step taken. Explain gaps without inventing dates.", ["SUPPORTING_DOCUMENTS"]),
                Document("STAGE_C_DELAY_EVIDENCE", "Documents supporting the delay explanation", "Provide records supporting the circumstances relied on to explain the delay.", ["SUPPORTING_DOCUMENTS", "MEDICAL_RECORD"]),
                Document("STAGE_C_RESTORATION_APPLICATION", "Proposed restoration application", "Prepare the underlying restoration request as well, if it is not already filed. Verify whether a separate condonation request is maintainable.", ["RESTORATION_APPLICATION"])
            ])
    ];

    public static StageCDefaultProcedure? Get(string? code) =>
        Procedures.FirstOrDefault(procedure =>
            procedure.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    private static StageCDefaultProcedureDocument Document(
        string code,
        string label,
        string guidance,
        IReadOnlyList<string> existingDocumentCodes) =>
        new(code, label, guidance, existingDocumentCodes);
}
