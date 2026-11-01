namespace LegalDraftAI.Web.Pages;

public sealed record StageBServiceDocument(
    string Code,
    string Label,
    string Guidance,
    IReadOnlyList<string> ExistingDocumentCodes,
    bool CopiesToPrepare = false);

public sealed record StageBServiceStep(
    string Code,
    string Label,
    string Provision,
    string Scenario,
    IReadOnlyList<StageBServiceDocument> Documents);

public static class StageBServiceCatalog
{
    public static readonly IReadOnlyList<StageBServiceStep> Steps =
    [
        new(
            "InitialSummons",
            "Application for first-time issuance of summons",
            "Order V, Code of Civil Procedure, 1908; applicable rules on issue and service",
            "The suit has been instituted and summons have not yet been issued to the defendant.",
            [
                Document("SERVICE_PLAINT_COPIES", "Copies of the plaint and annexures for service", "Prepare the number and format of sets directed by the court/registry. An existing plaint draft does not itself establish that service copies have been prepared.", [], true),
                Document("DEFENDANT_SERVICE_ADDRESS", "Complete address for first service", "Provide the defendant's complete address, including house/flat number, street, locality, district, state, and PIN code. Verify the address before filing.", ["OPP_PARTY_DETAILS"])
            ]),
        new(
            "FreshSummons",
            "Application for fresh summons",
            "Order V, Code of Civil Procedure, 1908; applicable rules on issue and service",
            "Summons was not served, or a correct address becomes available.",
            [
                Document("SERVICE_PLAINT_COPIES", "Copies of the plaint and annexures for service", "Prepare the number and format of sets directed by the court/registry. An existing plaint draft does not itself establish that service copies have been prepared.", [], true),
                Document("DEFENDANT_SERVICE_ADDRESS", "Correct and complete address for service", "Include house/flat number, street, locality, PIN code, district, and any known identifying details. State how the address was verified.", ["OPP_PARTY_DETAILS"]),
                Document("SERVICE_RETURNED_SUMMONS", "Returned summons or proof of prior service attempt", "Include the process server's report, postal tracking/returned cover, or other record showing the previous attempt and its result.", ["SERVICE_RETURN", "RETURNED_SUMMONS", "POSTAL_RECORD", "PROCESS_SERVER_REPORT"])
            ]),
        new(
            "SubstitutedService",
            "Application for substituted service",
            "Order V Rule 20, Code of Civil Procedure, 1908",
            "Ordinary service has failed and the court is satisfied that substituted service is justified. The court must direct the method; do not assume publication or affixation is automatically permitted.",
            [
                Document("SERVICE_PLAINT_COPIES", "Copies of the plaint and annexures for service", "Prepare the number and format of sets directed by the court/registry.", [], true),
                Document("DEFENDANT_SERVICE_ADDRESS", "Last known and attempted addresses", "List each address attempted, the source/date it was verified, and any reason the defendant may be avoiding service or cannot be found.", ["OPP_PARTY_DETAILS"]),
                Document("SERVICE_FAILURE_EVIDENCE", "Proof of failed ordinary service", "Attach returned summons, process-server reports, postal tracking/returned covers, and records of each attempt. Explain the outcome of every attempt.", ["SERVICE_RETURN", "RETURNED_SUMMONS", "POSTAL_RECORD", "PROCESS_SERVER_REPORT"]),
                Document("SUBSTITUTED_SERVICE_AFFIDAVIT", "Affidavit supporting substituted service", "Set out the steps taken to serve ordinarily and facts supporting the request under Order V Rule 20. Do not claim evasion without evidence.", ["SUBSTITUTED_SERVICE_AFFIDAVIT"])
            ]),
        new(
            "AlternativeServiceMode",
            "Application for permission for an alternative mode of service",
            "Applicable CPC rules and court directions",
            "A legally permitted alternative service method is appropriate. Identify the proposed method and ask the court for directions; do not proceed without permission where required.",
            [
                Document("SERVICE_PLAINT_COPIES", "Copies of the plaint and annexures for service", "Prepare the number and format of sets directed by the court/registry.", [], true),
                Document("DEFENDANT_SERVICE_ADDRESS", "Verified address and contact details", "Provide the defendant's known address and any proposed service channel's identifying details, with their source.", ["OPP_PARTY_DETAILS"]),
                Document("ALTERNATIVE_SERVICE_BASIS", "Evidence supporting the proposed service mode", "Explain why ordinary service is unsuitable and provide reliable evidence that the proposed channel is linked to the defendant.", ["SERVICE_RETURN", "RETURNED_SUMMONS", "POSTAL_RECORD", "PROCESS_SERVER_REPORT"]),
                Document("ALTERNATIVE_SERVICE_DIRECTIONS", "Relevant court order or local service directions", "Include any order, applicable rule, or registry direction that permits or requires the proposed method, if already issued.", [])
            ]),
        new(
            "ServiceDirections",
            "Application for appropriate directions regarding service",
            "Relevant procedural provisions and court directions",
            "Repeated service failures or practical difficulties in serving a defendant.",
            [
                Document("SERVICE_PLAINT_COPIES", "Copies of the plaint and annexures for service", "Prepare the number and format of sets directed by the court/registry.", [], true),
                Document("DEFENDANT_SERVICE_ADDRESS", "Addresses checked and sources", "Provide current and last-known addresses and identify how each was checked.", ["OPP_PARTY_DETAILS"]),
                Document("SERVICE_ATTEMPT_RECORDS", "Complete record of service attempts", "Include process-server reports, postal tracking/returned covers, returned summons, dates, and outcomes for every attempt.", ["SERVICE_RETURN", "RETURNED_SUMMONS", "POSTAL_RECORD", "PROCESS_SERVER_REPORT"]),
                Document("SERVICE_DIFFICULTY_AFFIDAVIT", "Affidavit or statement explaining service difficulty", "Give a concise chronology of failed attempts and explain the specific directions sought, supported by the available record.", ["SERVICE_DIFFICULTY_AFFIDAVIT"])
            ])
    ];

    public static StageBServiceStep? Get(string? code) =>
        Steps.FirstOrDefault(step =>
            step.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    private static StageBServiceDocument Document(
        string code,
        string label,
        string guidance,
        IReadOnlyList<string> existingDocumentCodes,
        bool copiesToPrepare = false) =>
        new(code, label, guidance, existingDocumentCodes, copiesToPrepare);
}
