using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LegalDraftAI.Web.Pages;

[RequestSizeLimit(50 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
public sealed class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    private const string AccountNameSessionKey = "Account.DisplayName";
    private const string PendingCivilPlaintFactsSessionKey = "CivilPlaint.PendingFacts";
    private const string PendingCivilPlaintJurisdictionSessionKey = "CivilPlaint.PendingJurisdiction";
    private const string BailResearchSessionKey = "BailApplication.ResearchResult";
    private const string BailResearchFactsSessionKey = "BailApplication.ResearchFacts";
    private const string BailUploadsSessionKey = "BailApplication.UploadedDocuments";
    private const string BailCaseIdSessionKey = "BailApplication.CaseId";

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _apiClient = httpClientFactory.CreateClient("DraftsApi");
    private readonly HttpClient _accountApiClient = httpClientFactory.CreateClient("EmployeeAuth");

    public bool IsAuthenticated { get; private set; }
    public string? AccountDisplayName { get; private set; }
    public string AuthFormMode { get; private set; } = "login";
    public string? AuthSuccessMessage { get; private set; }
    public string? AuthErrorMessage { get; private set; }

    [BindProperty]
    public AccountLoginInput Login { get; set; } = new();

    [BindProperty]
    public AccountRegistrationInput Registration { get; set; } = new();

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
    public DocumentResearchResultViewModel? BailResearchResult { get; private set; }
    public IReadOnlyList<UploadedDocumentRecord> BailUploadedDocuments { get; private set; } = [];
    public string? ApiStatusMessage { get; private set; }
    public string? ApiErrorMessage { get; private set; }
    public string? GeneratedDraftJson { get; private set; }
    public string? BailUploadStatusMessage { get; private set; }

    [BindProperty]
    public GenerateDraftInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await HttpContext.Session.LoadAsync(cancellationToken);
        AccountDisplayName = HttpContext.Session.GetString(AccountNameSessionKey);
        IsAuthenticated = !string.IsNullOrWhiteSpace(AccountDisplayName);
        if (!IsAuthenticated)
            return;

        await LoadLookupOptionsAsync(cancellationToken);
        await LoadBailStateAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostLoginAsync(CancellationToken cancellationToken)
    {
        AuthFormMode = "login";
        ModelState.Clear();
        if (!TryValidateModel(Login, nameof(Login)))
            return Page();

        try
        {
            using var response = await _accountApiClient.PostAsJsonAsync(
                "api/Users/login",
                new
                {
                    emailOrUsername = Login.EmailOrUsername.Trim(),
                    password = Login.Password
                },
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                AuthErrorMessage = ReadAccountApiMessage(
                    responseBody,
                    $"Sign in failed (HTTP {(int)response.StatusCode}).");
                Login.Password = string.Empty;
                ModelState.Clear();
                return Page();
            }

            await HttpContext.Session.LoadAsync(cancellationToken);
            AccountDisplayName = GetAccountDisplayName(responseBody) ?? Login.EmailOrUsername.Trim();
            HttpContext.Session.SetString(AccountNameSessionKey, AccountDisplayName);
            await HttpContext.Session.CommitAsync(cancellationToken);
            return RedirectToPage();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The employee authentication API could not be reached for login.");
            AuthErrorMessage = "Could not reach the sign-in service. Please try again later.";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AuthErrorMessage = "The sign-in request timed out. Please try again.";
        }

        Login.Password = string.Empty;
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostRegisterAsync(CancellationToken cancellationToken)
    {
        AuthFormMode = "register";
        ModelState.Clear();
        if (!TryValidateModel(Registration, nameof(Registration)))
            return Page();

        try
        {
            using var response = await _accountApiClient.PostAsJsonAsync(
                "api/Users/register",
                new
                {
                    username = Registration.Username.Trim(),
                    email = Registration.Email.Trim(),
                    password = Registration.Password,
                    fullName = Registration.FullName.Trim()
                },
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                AuthErrorMessage = ReadAccountApiMessage(
                    responseBody,
                    $"Account creation failed (HTTP {(int)response.StatusCode}).");
                Registration.Password = string.Empty;
                ModelState.Clear();
                return Page();
            }

            AuthSuccessMessage = ReadAccountApiMessage(
                responseBody,
                "Your account is ready. Sign in to continue.");
            if (string.IsNullOrWhiteSpace(AuthSuccessMessage) ||
                AuthSuccessMessage.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase))
            {
                AuthSuccessMessage = "Your account is ready. Sign in to continue.";
            }

            Login.EmailOrUsername = Registration.Email.Trim();
            Registration.Password = string.Empty;
            ModelState.Clear();
            AuthFormMode = "login";
            return Page();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The employee authentication API could not be reached for registration.");
            AuthErrorMessage = "Could not reach the account service. Please try again later.";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AuthErrorMessage = "The registration request timed out. Please try again.";
        }

        Registration.Password = string.Empty;
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync(CancellationToken cancellationToken)
    {
        await HttpContext.Session.LoadAsync(cancellationToken);
        HttpContext.Session.Remove(AccountNameSessionKey);
        await HttpContext.Session.CommitAsync(cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostContinueCivilPlaintAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(Input.DocumentType, "CivilPlaint", StringComparison.OrdinalIgnoreCase))
            return RedirectToPage();

        await HttpContext.Session.LoadAsync(cancellationToken);
        HttpContext.Session.SetString(PendingCivilPlaintFactsSessionKey, Input.ExtractedClientFacts ?? string.Empty);
        HttpContext.Session.SetString(PendingCivilPlaintJurisdictionSessionKey, Input.Jurisdiction ?? string.Empty);
        await HttpContext.Session.CommitAsync(cancellationToken);

        return RedirectToPage("/CivilPlaint");
    }

    public async Task<IActionResult> OnPostResearchBailAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAuthenticatedAccountAsync(cancellationToken))
            return RedirectToPage();

        ModelState.Clear();
        await LoadLookupOptionsAsync(cancellationToken);
        await LoadBailStateAsync(cancellationToken);
        await RunBailResearchAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostUploadBailAsync(
        List<IFormFile> files,
        List<string> documentCodes,
        CancellationToken cancellationToken)
    {
        if (!await LoadAuthenticatedAccountAsync(cancellationToken))
            return RedirectToPage();

        ModelState.Clear();
        await LoadLookupOptionsAsync(cancellationToken);
        await LoadBailStateAsync(cancellationToken);
        if (!await RunBailResearchAsync(cancellationToken))
            return Page();

        if (files.Count == 0 || files.Count != documentCodes.Count)
        {
            ApiErrorMessage = "Select at least one file and map every file to a checklist document.";
            return Page();
        }

        if (files.Any(file => file.Length == 0) ||
            files.Sum(file => file.Length) > 50L * 1024 * 1024)
        {
            ApiErrorMessage = "Uploaded files must be non-empty and total no more than 50 MB.";
            return Page();
        }

        var availableCodes = BailResearchResult!.RequiredDocuments
            .Select(document => document.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (documentCodes.Any(code => !availableCodes.Contains(code)))
        {
            ApiErrorMessage = "Map each file to a document in the current bail checklist.";
            return Page();
        }

        await HttpContext.Session.LoadAsync(cancellationToken);
        using var form = new MultipartFormDataContent();
        AddFormField(form, "caseId", HttpContext.Session.GetString(BailCaseIdSessionKey));
        AddFormField(form, "caseTitle", Input.CaseTitle);
        AddFormField(form, "caseCategory", "Criminal");
        AddFormField(form, "plaintNature", "BailApplication");
        AddFormField(form, "jurisdiction", Input.Jurisdiction);
        AddFormField(form, "caseNumber", Input.CaseNumber);
        AddFormField(form, "plaintiffName", Input.ApplicantName);
        AddFormField(form, "defendantName", Input.OppositePartyName);
        AddFormField(form, "clientFacts", Input.ExtractedClientFacts);
        for (var index = 0; index < files.Count; index++)
        {
            form.Add(new StreamContent(files[index].OpenReadStream()), "files", Path.GetFileName(files[index].FileName));
            form.Add(new StringContent(documentCodes[index]), "documentCodes");
        }

        try
        {
            using var response = await _apiClient.PostAsync(
                "api/drafts/extract-mapped-documents",
                form,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = ReadApiErrorMessage(
                    responseBody,
                    $"Document extraction failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            var extraction = JsonSerializer.Deserialize<MappedDocumentExtractionResult>(
                responseBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (extraction?.CaseId is not Guid caseId || extraction.Documents.Count != files.Count)
            {
                ApiErrorMessage = "The API returned an invalid mapped-document extraction response.";
                return Page();
            }

            BailUploadedDocuments = BailUploadedDocuments.Concat(extraction.Documents).ToList();
            HttpContext.Session.SetString(BailCaseIdSessionKey, caseId.ToString());
            HttpContext.Session.SetString(BailUploadsSessionKey, JsonSerializer.Serialize(BailUploadedDocuments));
            await HttpContext.Session.CommitAsync(cancellationToken);
            BailUploadStatusMessage = $"Uploaded and extracted {extraction.Documents.Count} supporting document(s).";
            await RunBailResearchAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API for Bail Application document extraction.");
            ApiErrorMessage = "Could not reach the API to extract supporting documents.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Bail Application document extraction returned invalid JSON.");
            ApiErrorMessage = "The API returned invalid document extraction data.";
        }

        return Page();
    }

    private static string? GetAccountDisplayName(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return FindStringProperty(document.RootElement, "fullName") ??
                FindStringProperty(document.RootElement, "username") ??
                FindStringProperty(document.RootElement, "name");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    return property.Value.GetString();
                }

                var nested = FindStringProperty(property.Value, propertyName);
                if (nested is not null)
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindStringProperty(item, propertyName);
                if (nested is not null)
                    return nested;
            }
        }

        return null;
    }

    private static string ReadAccountApiMessage(string responseBody, string fallback)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return fallback;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var message = ReadMessage(document.RootElement);
            return string.IsNullOrWhiteSpace(message) ? fallback : message;
        }
        catch (JsonException)
        {
            var plainText = responseBody.Trim();
            return plainText.Length <= 500 ? plainText : fallback;
        }
    }

    private static string? ReadMessage(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString();
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var candidate in new[] { "message", "error", "detail", "title" })
        {
            var property = element.EnumerateObject().FirstOrDefault(item =>
                item.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (property.Value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAuthenticatedAccountAsync(cancellationToken))
            return RedirectToPage();

        await LoadLookupOptionsAsync(cancellationToken);
        if (string.Equals(Input.DocumentType, "BailApplication", StringComparison.OrdinalIgnoreCase))
            await LoadBailStateAsync(cancellationToken);

        if (string.Equals(Input.DocumentType, "BailApplication", StringComparison.OrdinalIgnoreCase))
        {
            await HttpContext.Session.LoadAsync(cancellationToken);
            var researchedFacts = HttpContext.Session.GetString(BailResearchFactsSessionKey);
            if (BailResearchResult is null ||
                !string.Equals(researchedFacts, Input.ExtractedClientFacts, StringComparison.Ordinal))
            {
                ApiErrorMessage = "Research the Bail Application supporting documents for the current facts before generating the draft.";
                return Page();
            }
        }

        if (Input.DocumentType == "AbsenceApplication")
        {
            ValidateAbsenceApplication();
        }
        else if (string.Equals(Input.DocumentType, "BailApplication", StringComparison.OrdinalIgnoreCase))
        {
            ValidateBailApplication();
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

        var clientFacts = Input.DocumentType switch
        {
            "AbsenceApplication" => BuildAbsenceApplicationFacts(),
            "BailApplication" => BuildBailApplicationFacts(),
            _ => Input.ExtractedClientFacts
        };

        if (Input.DocumentType == "AbsenceApplication")
        {
            AddAbsenceApplicationFields(extraFields);
        }
        else if (string.Equals(Input.DocumentType, "BailApplication", StringComparison.OrdinalIgnoreCase))
        {
            AddBailApplicationFields(extraFields);
        }

        var request = new
        {
            documentType = Input.DocumentType,
            jurisdiction = Input.Jurisdiction,
            extractedClientFacts = clientFacts,
            caseId = string.Equals(Input.DocumentType, "BailApplication", StringComparison.OrdinalIgnoreCase)
                ? await GetBailCaseIdAsync(cancellationToken)
                : null,
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

    private void ValidateBailApplication()
    {
        RequireBailField(Input.ExtractedClientFacts, nameof(Input.ExtractedClientFacts), "Enter the client facts before preparing the bail application.");
        RequireBailField(Input.CourtName, nameof(Input.CourtName), "Enter the court name.");
        RequireBailField(Input.CaseTitle, nameof(Input.CaseTitle), "Enter the case title.");
        RequireBailField(Input.ApplicantName, nameof(Input.ApplicantName), "Enter the applicant or accused name.");
        RequireBailField(Input.OppositePartyName, nameof(Input.OppositePartyName), "Enter the respondent or State name.");
        RequireBailField(Input.BailType, nameof(Input.BailType), "Select the bail application type.");
        RequireBailField(Input.BailAllegations, nameof(Input.BailAllegations), "Enter the allegations and factual chronology.");
        RequireBailField(Input.BailCustodyStatus, nameof(Input.BailCustodyStatus), "Enter the arrest or custody status.");
        RequireBailField(Input.BailGrounds, nameof(Input.BailGrounds), "Enter the grounds for bail.");
        RequireBailField(Input.BailPrayer, nameof(Input.BailPrayer), "Enter the relief requested.");
        RequireBailField(Input.Place, nameof(Input.Place), "Enter the place of filing.");
        RequireBailField(Input.ApplicationDate, nameof(Input.ApplicationDate), "Enter the filing date.");
        RequireBailField(Input.AdvocateName, nameof(Input.AdvocateName), "Enter the advocate name.");
    }

    private void RequireBailField(string? value, string propertyName, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            ModelState.AddModelError($"Input.{propertyName}", errorMessage);
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
        AddExtraField(extraFields, "applicantAddress", Input.BailApplicantAddress);
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

    private void AddBailApplicationFields(Dictionary<string, string> extraFields)
    {
        AddExtraField(extraFields, "courtName", Input.CourtName);
        AddExtraField(extraFields, "caseTitle", Input.CaseTitle);
        AddExtraField(extraFields, "caseNumber", Input.CaseNumber);
        AddExtraField(extraFields, "applicantName", Input.ApplicantName);
        AddExtraField(extraFields, "respondentName", Input.OppositePartyName);
        AddExtraField(extraFields, "firNumber", Input.FirNumber);
        AddExtraField(extraFields, "policeStation", Input.PoliceStation);
        AddExtraField(extraFields, "district", Input.District);
        AddExtraField(extraFields, "sections", Input.Sections);
        AddExtraField(extraFields, "bailType", Input.BailType);
        AddExtraField(extraFields, "allegationsAndChronology", Input.BailAllegations);
        AddExtraField(extraFields, "arrestOrCustodyStatus", Input.BailCustodyStatus);
        AddExtraField(extraFields, "grounds", Input.BailGrounds);
        AddExtraField(extraFields, "prayer", Input.BailPrayer);
        AddExtraField(extraFields, "place", Input.Place);
        AddExtraField(extraFields, "date", Input.ApplicationDate);
        AddExtraField(extraFields, "advocateName", Input.AdvocateName);
        AddExtraField(extraFields, "advocateEnrollmentNo", Input.AdvocateEnrollmentNo);
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

    private async Task<bool> RunBailResearchAsync(CancellationToken cancellationToken)
    {
        await HttpContext.Session.LoadAsync(cancellationToken);
        HttpContext.Session.Remove(BailResearchSessionKey);
        HttpContext.Session.Remove(BailResearchFactsSessionKey);
        await HttpContext.Session.CommitAsync(cancellationToken);

        if (!string.Equals(Input.DocumentType, "BailApplication", StringComparison.OrdinalIgnoreCase))
        {
            ApiErrorMessage = "Select Bail application before researching supporting documents.";
            BailResearchResult = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(Input.ExtractedClientFacts))
        {
            ApiErrorMessage = "Enter the client facts before researching Bail Application documents.";
            BailResearchResult = null;
            return false;
        }

        var request = new
        {
            jurisdiction = Input.Jurisdiction,
            caseCategory = "Criminal",
            plaintNature = "BailApplication",
            clientFacts = Input.ExtractedClientFacts,
            alreadyHaveDocumentCodes = (Input.AlreadyHaveDocumentCodes ?? [])
                .Concat(BailUploadedDocuments.Select(document => document.DocumentCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                "api/intake/research",
                request,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = ReadApiErrorMessage(
                    responseBody,
                    $"Document research failed (HTTP {(int)response.StatusCode}).");
                BailResearchResult = null;
                return false;
            }

            BailResearchResult = JsonSerializer.Deserialize<DocumentResearchResultViewModel>(
                responseBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (BailResearchResult is null)
            {
                ApiErrorMessage = "The API returned an empty Bail Application research result.";
                return false;
            }

            ApiErrorMessage = null;
            await HttpContext.Session.LoadAsync(cancellationToken);
            HttpContext.Session.SetString(BailResearchSessionKey, JsonSerializer.Serialize(BailResearchResult));
            HttpContext.Session.SetString(BailResearchFactsSessionKey, Input.ExtractedClientFacts);
            await HttpContext.Session.CommitAsync(cancellationToken);
            return true;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API for Bail Application document research.");
            ApiErrorMessage = "Could not reach the API to research Bail Application documents.";
            BailResearchResult = null;
            return false;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Bail Application document research returned invalid JSON.");
            ApiErrorMessage = "The API returned invalid Bail Application research data.";
            BailResearchResult = null;
            return false;
        }
    }

    private async Task LoadBailStateAsync(CancellationToken cancellationToken)
    {
        await HttpContext.Session.LoadAsync(cancellationToken);
        var serializedUploads = HttpContext.Session.GetString(BailUploadsSessionKey);
        var serializedResearch = HttpContext.Session.GetString(BailResearchSessionKey);

        try
        {
            BailUploadedDocuments = string.IsNullOrWhiteSpace(serializedUploads)
                ? []
                : JsonSerializer.Deserialize<List<UploadedDocumentRecord>>(serializedUploads) ?? [];
            BailResearchResult = string.IsNullOrWhiteSpace(serializedResearch)
                ? null
                : JsonSerializer.Deserialize<DocumentResearchResultViewModel>(
                    serializedResearch,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var researchedFacts = HttpContext.Session.GetString(BailResearchFactsSessionKey);
            if (!string.Equals(researchedFacts, Input.ExtractedClientFacts, StringComparison.Ordinal))
                BailResearchResult = null;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Stored Bail Application research or upload data could not be loaded.");
            HttpContext.Session.Remove(BailUploadsSessionKey);
            HttpContext.Session.Remove(BailResearchSessionKey);
            HttpContext.Session.Remove(BailResearchFactsSessionKey);
            HttpContext.Session.Remove(BailCaseIdSessionKey);
            BailUploadedDocuments = [];
            BailResearchResult = null;
        }
    }

    private async Task<bool> LoadAuthenticatedAccountAsync(CancellationToken cancellationToken)
    {
        await HttpContext.Session.LoadAsync(cancellationToken);
        AccountDisplayName = HttpContext.Session.GetString(AccountNameSessionKey);
        IsAuthenticated = !string.IsNullOrWhiteSpace(AccountDisplayName);
        return IsAuthenticated;
    }

    private async Task<Guid?> GetBailCaseIdAsync(CancellationToken cancellationToken)
    {
        await HttpContext.Session.LoadAsync(cancellationToken);
        return Guid.TryParse(HttpContext.Session.GetString(BailCaseIdSessionKey), out var caseId)
            ? caseId
            : null;
    }

    private string BuildBailApplicationFacts()
    {
        var facts = new StringBuilder(Input.ExtractedClientFacts.Trim());
        AddFact(facts, "Court name", Input.CourtName);
        AddFact(facts, "Case title", Input.CaseTitle);
        AddFact(facts, "Case number", Input.CaseNumber);
        AddFact(facts, "Bail application type", Input.BailType);
        AddFact(facts, "Applicant / accused", Input.ApplicantName);
        AddFact(facts, "Applicant address", Input.BailApplicantAddress);
        AddFact(facts, "Respondent / State", Input.OppositePartyName);
        AddFact(facts, "FIR / complaint number", Input.FirNumber);
        AddFact(facts, "Police station", Input.PoliceStation);
        AddFact(facts, "District", Input.District);
        AddFact(facts, "Applicable sections", Input.Sections);
        AddFact(facts, "Allegations and factual chronology", Input.BailAllegations);
        AddFact(facts, "Arrest / custody status", Input.BailCustodyStatus);
        AddFact(facts, "Grounds for bail", Input.BailGrounds);
        AddFact(facts, "Prayer / relief sought", Input.BailPrayer);
        AddFact(facts, "Place", Input.Place);
        AddFact(facts, "Filing date", Input.ApplicationDate);
        AddFact(facts, "Advocate name", Input.AdvocateName);
        AddFact(facts, "Advocate enrollment number", Input.AdvocateEnrollmentNo);
        if (BailUploadedDocuments.Count == 0)
            return facts.ToString();

        facts.AppendLine().AppendLine("Uploaded Bail Application supporting documents:");
        foreach (var upload in BailUploadedDocuments)
        {
            var label = BailResearchResult?.RequiredDocuments
                .FirstOrDefault(document => document.Code.Equals(
                    upload.DocumentCode,
                    StringComparison.OrdinalIgnoreCase))?.Label ?? upload.DocumentCode;
            facts.Append("Document type: ").AppendLine(label);
            facts.Append("File: ").AppendLine(upload.FileName);
            facts.AppendLine(upload.Text).AppendLine();
        }

        return facts.ToString();
    }

    private static void AddFormField(MultipartFormDataContent form, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            form.Add(new StringContent(value), name);
    }

    private static string ReadApiErrorMessage(string responseBody, string fallback)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return fallback;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return ReadMessage(document.RootElement) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
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

public sealed class AccountLoginInput
{
    [Required(ErrorMessage = "Enter your email or username.")]
    [StringLength(254)]
    public string EmailOrUsername { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class AccountRegistrationInput
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a username.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Create a password.")]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

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
    public string BailType { get; set; } = string.Empty;

    [StringLength(2000)]
    public string BailApplicantAddress { get; set; } = string.Empty;

    [StringLength(10000)]
    public string BailAllegations { get; set; } = string.Empty;

    [StringLength(5000)]
    public string BailCustodyStatus { get; set; } = string.Empty;

    [StringLength(10000)]
    public string BailGrounds { get; set; } = string.Empty;

    [StringLength(5000)]
    public string BailPrayer { get; set; } = string.Empty;

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

    public List<string> AlreadyHaveDocumentCodes { get; set; } = [];

    [StringLength(10000)]
    public string? ExtraFieldsJson { get; set; }
}
