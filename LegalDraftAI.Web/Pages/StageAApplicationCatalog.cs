namespace LegalDraftAI.Web.Pages;

public sealed record StageAApplicationDocument(
    string Code,
    string Label,
    string Guidance,
    string Category,
    IReadOnlyList<string> ExistingDocumentCodes);

public sealed record StageAApplication(
    string Code,
    string Label,
    string Provision,
    string WhenNeeded,
    string AffidavitDocumentCode,
    IReadOnlyList<StageAApplicationDocument> Documents);

public static class StageAApplicationCatalog
{
    public static readonly IReadOnlyList<StageAApplication> Applications =
    [
        new(
            "UrgentInterimRelief",
            "Application for urgent interim relief",
            "Order XXXIX Rules 1–2, Code of Civil Procedure, 1908",
            "To restrain a specific act where the facts support interim protection. Ex parte relief has additional safeguards.",
            "INTERIM_AFFIDAVIT",
            [
                Document("INTERIM_AFFIDAVIT", "Supporting affidavit", "Set out the material facts and identify the evidence relied on. For an ex parte request, address the affidavit and notice safeguards in Order XXXIX Rule 3.", "Usually needed", ["INTERIM_AFFIDAVIT"]),
                Document("INTERIM_HARM_EVIDENCE", "Evidence of the threatened or continuing harm", "Examples: dated photographs, correspondence, notices, witness details, or records showing the threatened act. Include only evidence relevant to the pleaded facts.", "If available / relevant", ["THREAT_FACTS", "PHOTOS_VIDEOS"]),
                Document("INTERIM_DRAFT_ORDER", "Proposed order or notice copies", "Some courts or e-filing systems may request a proposed order or extra copies. Check the local registry directions.", "Local practice", [])
            ]),
        new(
            "AttachmentBeforeJudgment",
            "Application for attachment before judgment",
            "Order XXXVIII Rule 5, Code of Civil Procedure, 1908",
            "Only where specific material supports a real risk that the defendant may dispose of or remove property to obstruct or delay execution of a future decree.",
            "ATTACHMENT_AFFIDAVIT",
            [
                Document("ATTACHMENT_AFFIDAVIT", "Affidavit stating the statutory grounds", "Identify the property, the estimated value, the security sought, and specific facts supporting an intention to obstruct or delay execution.", "Core material", ["ATTACHMENT_AFFIDAVIT"]),
                Document("ATTACHMENT_RISK_EVIDENCE", "Evidence of the alleged disposal or removal risk", "Examples: sale advertisements, transfer attempts, correspondence, or other dated evidence. Non-payment by itself is insufficient.", "Evidence supporting the application", []),
                Document("ATTACHMENT_PROPERTY_DETAILS", "Property particulars and valuation", "Provide a precise description and a reasonable estimate for the property sought to be attached; explain the requested security amount.", "Core material", ["PROPERTY_SCHEDULE", "VALUATION", "TITLE_DEED", "REVENUE_RECORD"])
            ]),
        new(
            "AppointmentOfReceiver",
            "Application for appointment of receiver",
            "Order XL Rule 1, Code of Civil Procedure, 1908",
            "In an exceptional case where appointment is shown to be just and convenient to protect or manage property.",
            "RECEIVER_AFFIDAVIT",
            [
                Document("RECEIVER_AFFIDAVIT", "Affidavit explaining why a receiver is necessary", "Describe the property, present control or management, and concrete risk of loss, deterioration, or other prejudice.", "Usually needed", ["RECEIVER_AFFIDAVIT"]),
                Document("RECEIVER_PROPERTY_RECORDS", "Property and title records", "Provide a schedule and records that identify the property and the parties' interests in it.", "If not already on file", ["PROPERTY_SCHEDULE", "TITLE_DEED", "REVENUE_RECORD", "MUTATION"]),
                Document("RECEIVER_CONDITION_EVIDENCE", "Evidence of the need for protection or management", "Examples: condition reports, photographs, accounts, or correspondence showing the management problem or risk.", "If available / relevant", ["PHOTOS_VIDEOS", "POSSESSION_PROOF", "THREAT_FACTS"])
            ]),
        new(
            "AmendPlaint",
            "Application to amend the plaint",
            "Order VI Rule 17, Code of Civil Procedure, 1908",
            "To correct or add pleadings, subject to the court's permission and the applicable restrictions, including the due-diligence test after trial has commenced.",
            "AMENDMENT_AFFIDAVIT",
            [
                Document("AMENDED_PLAINT", "Proposed amended plaint", "Provide a clean proposed version and clearly identify the proposed changes in the format accepted by the court.", "Core material", []),
                Document("AMENDMENT_AFFIDAVIT", "Affidavit explaining the amendment", "Explain why each change is needed, when the relevant facts became known, and (if trial has commenced) the due-diligence basis.", "Usually needed", ["AMENDMENT_AFFIDAVIT"]),
                Document("AMENDMENT_SUPPORT", "Records supporting the proposed changes", "Include only documents that substantiate the new or corrected pleading and are not already on the case file.", "If applicable", ["TITLE_DEED", "REVENUE_RECORD", "MUTATION", "AGREEMENT_TO_SELL", "CONTRACT_OR_PROMISSORY", "NOTICES"])
            ]),
        new(
            "AdditionalDocuments",
            "Application to bring additional documents on record",
            "Order VII Rule 14(3), Code of Civil Procedure, 1908, where applicable",
            "Where a document was not produced with the plaint and the plaintiff seeks the court's permission to produce it later.",
            "ADDITIONAL_DOCUMENT_AFFIDAVIT",
            [
                Document("ADDITIONAL_DOCUMENTS", "The documents sought to be produced", "Upload readable copies of each proposed document. Keep originals available if inspection or proof is required.", "Core material", []),
                Document("ADDITIONAL_DOCUMENT_LIST", "Indexed list of proposed documents", "Identify each document by date, description, author or source (if known), and the pleading paragraph it supports.", "Usually needed", []),
                Document("ADDITIONAL_DOCUMENT_AFFIDAVIT", "Affidavit explaining the omission and relevance", "State why the document was not filed with the plaint, when it became available, and how it is relevant. Explain any delay accurately.", "Usually needed", ["ADDITIONAL_DOCUMENT_AFFIDAVIT"])
            ]),
        new(
            "CourtFeeOrFilingDefect",
            "Application concerning court fees or time to cure filing defects",
            "Applicable court-fee law, court rules, and the registry or court's directions",
            "Where the registry or court identifies a filing defect or a shortfall requiring correction or additional time.",
            "DEFECT_EXTENSION_AFFIDAVIT",
            [
                Document("DEFECT_MEMO", "Registry defect memo or court order", "Include the notice or order that identifies the defect and any deadline already fixed.", "If issued", []),
                Document("CORRECTED_PLAINT_OR_VALUATION", "Corrected pleading, valuation, or court-fee calculation", "Include the corrected page or calculation and explain the correction made.", "As directed", []),
                Document("COURT_FEE_PROOF", "Court-fee payment or adjustment proof", "Attach the receipt or other proof if payment or adjustment has already been made.", "If payment has been made", []),
                Document("DEFECT_EXTENSION_AFFIDAVIT", "Affidavit supporting a request for more time", "State the specific reason for the delay and the time needed; an extension is for the court to decide.", "If more time is sought", ["DEFECT_EXTENSION_AFFIDAVIT"])
            ])
    ];

    public static StageAApplication? Get(string? code) =>
        Applications.FirstOrDefault(application =>
            application.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    private static StageAApplicationDocument Document(
        string code,
        string label,
        string guidance,
        string category,
        IReadOnlyList<string> existingDocumentCodes) =>
        new(code, label, guidance, category, existingDocumentCodes);
}
