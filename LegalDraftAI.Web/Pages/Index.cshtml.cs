using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LegalDraftAI.Web.Pages;

public sealed class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _apiClient = httpClientFactory.CreateClient("DraftsApi");

    public IReadOnlyList<DocumentTypeOption> DocumentTypes { get; } =
    [
        new("BailApplication", "Bail application"),
        new("AdvocateBill", "Advocate bill"),
        new("AbsenceApplication", "Absence / exemption application"),
        new("SummonApplication", "Summons application"),
        new("CertifiedCopyApplication", "Certified copy application"),
        new("CurativePetition", "Curative petition"),
        new("ComplaintNonRegistrationFIR", "Complaint for non-registration of FIR"),
        new("CivilPlaint", "Civil plaint")
    ];

    public IReadOnlyList<LookupOption> AbsenceGrounds { get; private set; } = [];
    public IReadOnlyList<LookupOption> CourtDocuments { get; private set; } = [];
    public string? ApiStatusMessage { get; private set; }
    public string? ApiErrorMessage { get; private set; }
    public string? GeneratedDraftJson { get; private set; }

    [BindProperty]
    public GenerateDraftInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadLookupOptionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadLookupOptionsAsync(cancellationToken);

        if (Input.DocumentType == "AbsenceApplication")
        {
            ValidateAbsenceApplication();
        }
        else if (string.IsNullOrWhiteSpace(Input.ExtractedClientFacts))
        {
            ModelState.AddModelError(
                "Input.ExtractedClientFacts",
                "Enter the client facts to use for the draft.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Dictionary<string, string> extraFields;
        try
        {
            extraFields = string.IsNullOrWhiteSpace(Input.ExtraFieldsJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(Input.ExtraFieldsJson)
                    ?? throw new JsonException("Extra fields must be a JSON object.");
        }
        catch (JsonException)
        {
            ModelState.AddModelError(
                "Input.ExtraFieldsJson",
                "Enter extra fields as a JSON object with string values.");
            return Page();
        }

        if (Input.DocumentType == "AbsenceApplication" && Input.SelectedGroundCodes.Count > 0)
        {
            extraFields["selectedGroundCodes"] = string.Join(",", Input.SelectedGroundCodes);
        }

        if (Input.DocumentType == "CertifiedCopyApplication" && Input.SelectedDocumentCodes.Count > 0)
        {
            extraFields["selectedDocumentCodes"] = string.Join(",", Input.SelectedDocumentCodes);
        }

        var clientFacts = Input.DocumentType == "AbsenceApplication"
            ? BuildAbsenceApplicationFacts()
            : Input.ExtractedClientFacts;

        if (Input.DocumentType == "AbsenceApplication")
        {
            AddAbsenceApplicationFields(extraFields);
        }

        var request = new
        {
            documentType = Input.DocumentType,
            jurisdiction = Input.Jurisdiction,
            extractedClientFacts = clientFacts,
            extraFields = extraFields.Count == 0 ? null : extraFields
        };

        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                "api/drafts/generate",
                request,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Draft generation failed (HTTP {(int)response.StatusCode}).";
                GeneratedDraftJson = FormatJsonOrOriginal(responseBody);
                return Page();
            }

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                GeneratedDraftJson = JsonSerializer.Serialize(
                    document.RootElement,
                    PrettyJsonOptions);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Draft API returned an invalid JSON response.");
                ApiErrorMessage = "The API returned an invalid JSON response.";
                GeneratedDraftJson = responseBody;
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API. Check that it is running and that DraftsApi:BaseUrl is correct.";
        }

        return Page();
    }

    private void ValidateAbsenceApplication()
    {
        RequireAbsenceField(Input.CourtName, nameof(Input.CourtName), "Enter the court name.");
        RequireAbsenceField(Input.CaseTitle, nameof(Input.CaseTitle), "Enter the case title.");
        RequireAbsenceField(Input.CaseNumber, nameof(Input.CaseNumber), "Enter the case number.");
        RequireAbsenceField(Input.ApplicantName, nameof(Input.ApplicantName), "Enter the applicant name.");
        RequireAbsenceField(Input.ApplicantStatus, nameof(Input.ApplicantStatus), "Choose the applicant status.");
        RequireAbsenceField(Input.OppositePartyName, nameof(Input.OppositePartyName), "Enter the opposite party name.");
        RequireAbsenceField(Input.HearingDate, nameof(Input.HearingDate), "Enter the hearing date.");
        RequireAbsenceField(Input.ReasonForAbsence, nameof(Input.ReasonForAbsence), "Enter the reason for absence.");
        RequireAbsenceField(Input.AdvocateName, nameof(Input.AdvocateName), "Enter the advocate name.");
        RequireAbsenceField(Input.Place, nameof(Input.Place), "Enter the place of application.");
        RequireAbsenceField(Input.ApplicationDate, nameof(Input.ApplicationDate), "Enter the date of application.");
    }

    private void RequireAbsenceField(string? value, string propertyName, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ModelState.AddModelError($"Input.{propertyName}", errorMessage);
        }
    }

    private string BuildAbsenceApplicationFacts()
    {
        var facts = new StringBuilder();
        AddFact(facts, "Court name", Input.CourtName);
        AddFact(facts, "Case title", Input.CaseTitle);
        AddFact(facts, "Case number", Input.CaseNumber);
        AddFact(facts, "Applicant name", Input.ApplicantName);
        AddFact(facts, "Applicant status", Input.ApplicantStatus);
        AddFact(facts, "Opposite party name", Input.OppositePartyName);
        AddFact(facts, "Hearing date", Input.HearingDate);
        AddFact(facts, "Reason for absence", Input.ReasonForAbsence);
        AddFact(facts, "FIR number", Input.FirNumber);
        AddFact(facts, "Police station", Input.PoliceStation);
        AddFact(facts, "District", Input.District);
        AddFact(facts, "Sections", Input.Sections);
        AddFact(facts, "Advocate name", Input.AdvocateName);
        AddFact(facts, "Advocate enrollment number", Input.AdvocateEnrollmentNo);
        AddFact(facts, "Place of application", Input.Place);
        AddFact(facts, "Date of application", Input.ApplicationDate);

        if (!string.IsNullOrWhiteSpace(Input.ExtractedClientFacts))
        {
            facts.AppendLine();
            facts.AppendLine("Additional client facts:");
            facts.Append(Input.ExtractedClientFacts.Trim());
        }

        return facts.ToString();
    }

    private void AddAbsenceApplicationFields(Dictionary<string, string> extraFields)
    {
        AddExtraField(extraFields, "courtName", Input.CourtName);
        AddExtraField(extraFields, "caseTitle", Input.CaseTitle);
        AddExtraField(extraFields, "caseNumber", Input.CaseNumber);
        AddExtraField(extraFields, "applicantName", Input.ApplicantName);
        AddExtraField(extraFields, "applicantStatus", Input.ApplicantStatus);
        AddExtraField(extraFields, "oppositePartyName", Input.OppositePartyName);
        AddExtraField(extraFields, "hearingDate", Input.HearingDate);
        AddExtraField(extraFields, "reasonForAbsence", Input.ReasonForAbsence);
        AddExtraField(extraFields, "firNumber", Input.FirNumber);
        AddExtraField(extraFields, "policeStation", Input.PoliceStation);
        AddExtraField(extraFields, "district", Input.District);
        AddExtraField(extraFields, "sections", Input.Sections);
        AddExtraField(extraFields, "advocateName", Input.AdvocateName);
        AddExtraField(extraFields, "advocateEnrollmentNo", Input.AdvocateEnrollmentNo);
        AddExtraField(extraFields, "place", Input.Place);
        AddExtraField(extraFields, "date", Input.ApplicationDate);
    }

    private static void AddFact(StringBuilder facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Append(label).Append(": ").AppendLine(value.Trim());
        }
    }

    private static void AddExtraField(
        Dictionary<string, string> extraFields,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            extraFields[key] = value.Trim();
        }
    }

    private async Task LoadLookupOptionsAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        try
        {
            AbsenceGrounds = await GetLookupOptionsAsync(
                "api/drafts/absence-grounds",
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not load absence grounds from the LegalDraftAI API.");
            errors.Add("absence grounds");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The LegalDraftAI API returned invalid absence grounds.");
            errors.Add("absence grounds");
        }

        try
        {
            CourtDocuments = await GetLookupOptionsAsync(
                "api/drafts/court-documents",
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not load court documents from the LegalDraftAI API.");
            errors.Add("certified-copy document options");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The LegalDraftAI API returned invalid court documents.");
            errors.Add("certified-copy document options");
        }

        if (errors.Count > 0)
        {
            ApiStatusMessage = $"Could not load {string.Join(" and ", errors)} from the API. Other form inputs remain available.";
        }
    }

    private async Task<IReadOnlyList<LookupOption>> GetLookupOptionsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Lookup request to {path} returned HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<List<LookupOption>>(
                   cancellationToken: cancellationToken)
               ?? throw new JsonException($"Lookup request to {path} returned no data.");
    }

    private static string FormatJsonOrOriginal(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
        }
        catch (JsonException)
        {
            return content;
        }
    }
}

public sealed record DocumentTypeOption(string Value, string Label);

public sealed record LookupOption(string Code, string Label);

public sealed class GenerateDraftInput
{
    [Required(ErrorMessage = "Choose a document type.")]
    public string DocumentType { get; set; } = string.Empty;

    [StringLength(200)]
    public string Jurisdiction { get; set; } = "Delhi";

    [StringLength(50000)]
    public string ExtractedClientFacts { get; set; } = string.Empty;

    [StringLength(300)]
    public string CourtName { get; set; } = string.Empty;

    [StringLength(500)]
    public string CaseTitle { get; set; } = string.Empty;

    [StringLength(100)]
    public string CaseNumber { get; set; } = string.Empty;

    [StringLength(200)]
    public string ApplicantName { get; set; } = string.Empty;

    [StringLength(100)]
    public string ApplicantStatus { get; set; } = string.Empty;

    [StringLength(200)]
    public string OppositePartyName { get; set; } = string.Empty;

    public string HearingDate { get; set; } = string.Empty;

    [StringLength(2000)]
    public string ReasonForAbsence { get; set; } = string.Empty;

    [StringLength(100)]
    public string FirNumber { get; set; } = string.Empty;

    [StringLength(200)]
    public string PoliceStation { get; set; } = string.Empty;

    [StringLength(200)]
    public string District { get; set; } = string.Empty;

    [StringLength(500)]
    public string Sections { get; set; } = string.Empty;

    [StringLength(200)]
    public string AdvocateName { get; set; } = string.Empty;

    [StringLength(100)]
    public string AdvocateEnrollmentNo { get; set; } = string.Empty;

    [StringLength(200)]
    public string Place { get; set; } = string.Empty;

    public string ApplicationDate { get; set; } = string.Empty;

    public List<string> SelectedGroundCodes { get; set; } = [];

    public List<string> SelectedDocumentCodes { get; set; } = [];

    [StringLength(10000)]
    public string? ExtraFieldsJson { get; set; }
}
