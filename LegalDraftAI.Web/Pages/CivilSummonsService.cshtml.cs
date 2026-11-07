using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LegalDraftAI.Web.Pages;

[RequestSizeLimit(50 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
public sealed class CivilSummonsServiceModel(
    IHttpClientFactory httpClientFactory,
    ILogger<CivilSummonsServiceModel> logger) : PageModel
{
    private const string DraftDocumentType = "CivilStageBServiceApplication";

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _apiClient = httpClientFactory.CreateClient("DraftsApi");

    public IReadOnlyList<StageBServiceStep> Steps => StageBServiceCatalog.Steps;
    public IReadOnlyList<ExistingCaseOption> CivilSuits { get; private set; } = [];
    public ExistingCaseDetail? SelectedCase { get; private set; }
    public string? ExistingPlaintDraftJson { get; private set; }
    public StageBServiceStep? SelectedStep => StageBServiceCatalog.Get(SelectedStepCode);
    public IReadOnlyList<StageBRequirementViewModel> Requirements { get; private set; } = [];
    public IReadOnlyList<ExistingCaseDocument> DocumentsForSelectedStep =>
        SelectedCase?.Documents
            .Where(document =>
                !string.IsNullOrWhiteSpace(document.ExtractedText) &&
                (document.StageCode is null ||
                    (document.StageCode.Equals("StageB", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(document.ApplicationCode, SelectedStepCode, StringComparison.OrdinalIgnoreCase))))
            .OrderBy(document => document.OriginalFileName, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    public IReadOnlyList<StageBUploadOption> Uploads { get; private set; } = [];
    public string? ApiErrorMessage { get; private set; }
    public string? StatusMessage { get; private set; }
    public string? GeneratedDraftJson { get; private set; }
    public Guid? GeneratedDraftId { get; private set; }
    public IReadOnlyList<StageBOutputFieldOption> GeneratedDraftFields { get; private set; } = [];
    public IReadOnlyList<StageBDocxPreviewLine> GeneratedDraftPreview { get; private set; } = [];

    [BindProperty]
    public Guid? SelectedCaseId { get; set; }

    [BindProperty]
    public string? SelectedStepCode { get; set; }

    [BindProperty]
    public List<Guid> ExcludedDocumentIds { get; set; } = [];

    [BindProperty]
    public List<string> ExcludedDraftFields { get; set; } = [];

    [BindProperty]
    [StringLength(20000)]
    public string? ServiceFacts { get; set; }

    [BindProperty]
    [StringLength(300)]
    public string? DefendantName { get; set; }

    [BindProperty]
    [StringLength(2000)]
    public string? DefendantServiceAddress { get; set; }

    [BindProperty]
    [StringLength(300)]
    public string? CounselName { get; set; }

    [BindProperty]
    [StringLength(100)]
    public string? CounselEnrollmentNo { get; set; }

    [BindProperty]
    public List<StageBUploadInput> UploadInputs { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadCivilSuitsAsync(cancellationToken);
        if (SelectedCaseId is Guid caseId)
            await LoadSelectedCaseAsync(caseId, cancellationToken);
        else
            RefreshRequirements();
    }

    public async Task<IActionResult> OnPostSelectCaseAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        DefendantName = null;
        DefendantServiceAddress = null;
        await LoadCivilSuitsAsync(cancellationToken);
        if (SelectedCaseId is Guid caseId)
            await LoadSelectedCaseAsync(caseId, cancellationToken, useStoredDefendantDetails: true);
        else
            RefreshRequirements();
        return Page();
    }

    public async Task<IActionResult> OnPostUploadDocumentsAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilSuitsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();
        if (SelectedStep is null)
        {
            ApiErrorMessage = "Select one of the Stage B service steps.";
            return Page();
        }

        var selectedUploads = UploadInputs.Where(upload => upload.File is { Length: > 0 }).ToList();
        if (selectedUploads.Count == 0)
        {
            ApiErrorMessage = "Choose at least one file to upload.";
            return Page();
        }

        if (selectedUploads.Sum(upload => upload.File!.Length) > 50L * 1024 * 1024)
        {
            ApiErrorMessage = "Selected files must total no more than 50 MB.";
            return Page();
        }

        var allowedCodes = Requirements
            .Where(requirement => !requirement.AlreadyOnFile)
            .Select(requirement => requirement.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedUploads.Any(upload => !allowedCodes.Contains(upload.DocumentCode)) ||
            selectedUploads.Select(upload => upload.DocumentCode)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != selectedUploads.Count)
        {
            ApiErrorMessage = "Map each file to a document still outstanding in the selected service-step checklist.";
            return Page();
        }

        var uploadedCount = 0;
        var failures = new List<string>();
        foreach (var upload in selectedUploads)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(upload.DocumentCode), "documentCode");
            form.Add(new StringContent("StageB"), "stageCode");
            form.Add(new StringContent(SelectedStep.Code), "applicationCode");
            form.Add(
                new StreamContent(upload.File!.OpenReadStream()),
                "file",
                Path.GetFileName(upload.File.FileName));

            try
            {
                using var response = await _apiClient.PostAsync(
                    $"api/cases/{SelectedCaseId}/documents",
                    form,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    uploadedCount++;
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                failures.Add($"{upload.File.FileName}: {GetApiErrorMessage(body, $"HTTP {(int)response.StatusCode}")}");
            }
            catch (HttpRequestException exception)
            {
                logger.LogError(exception, "Could not upload Stage B document {DocumentCode}.", upload.DocumentCode);
                failures.Add($"{upload.File.FileName}: the LegalDraftAI API could not be reached.");
            }
        }

        if (uploadedCount > 0)
            StatusMessage = $"{uploadedCount} service document(s) uploaded to the selected suit.";
        if (failures.Count > 0)
            ApiErrorMessage = $"Some uploads failed. {string.Join(" ", failures)}";
        await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilSuitsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();

        var document = SelectedCase!.Documents.FirstOrDefault(item => item.Id == documentId);
        if (document is null ||
            !Requirements.Any(requirement =>
                requirement.ExistingDocuments.Any(existing => existing.Id == documentId)))
        {
            ApiErrorMessage = "The selected document is not attached as a supporting record for this service step.";
            return Page();
        }

        try
        {
            using var response = await _apiClient.DeleteAsync(
                $"api/cases/{SelectedCaseId}/documents/{documentId}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                ApiErrorMessage = GetApiErrorMessage(body, $"Could not remove the document (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken);
            StatusMessage = $"Removed {document.OriginalFileName} from this suit.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not remove Stage B document {DocumentId}.", documentId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to remove the document.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        ExcludedDraftFields = [];
        await LoadCivilSuitsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();

        if (SelectedStep is null)
        {
            ApiErrorMessage = "Select one of the Stage B service steps.";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(ServiceFacts) || ServiceFacts.Length > 20000)
        {
            ApiErrorMessage = SelectedStep.Code.Equals("InitialSummons", StringComparison.OrdinalIgnoreCase)
                ? "Confirm that summons have not previously been issued and provide any relevant service instructions (20,000 characters or fewer)."
                : "Describe the service attempts, their results, the correct address or proposed mode, and the directions requested (20,000 characters or fewer).";
            return Page();
        }
        if (SelectedStep.Code.Equals("InitialSummons", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(DefendantName) || string.IsNullOrWhiteSpace(DefendantServiceAddress)))
        {
            ApiErrorMessage = "Enter the defendant's name and complete address for first-time service.";
            return Page();
        }

        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                $"api/cases/{SelectedCaseId}/drafts",
                new
                {
                    documentType = DraftDocumentType,
                    stageCode = "StageB",
                    applicationCode = SelectedStep.Code,
                    extraInstructions = BuildDraftInstructions(SelectedStep, ServiceFacts),
                    excludedDocumentIds = ExcludedDocumentIds,
                    savePdf = false
                },
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = GetApiErrorMessage(body, $"Application generation failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            var draft = JsonSerializer.Deserialize<StageASavedDraftResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (draft is null ||
                !draft.DocumentType.Equals(DraftDocumentType, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(draft.JsonContent))
            {
                logger.LogError("Stage B application API response did not contain saved draft content for case {CaseId}.", SelectedCaseId);
                ApiErrorMessage = "The API response did not contain the saved service application.";
                return Page();
            }

            SetGeneratedDraft(draft.JsonContent);
            GeneratedDraftId = draft.Id;
            StatusMessage = "Service application generated and saved to the selected civil suit.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to generate the Stage B application.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to generate the application.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid Stage B application data.");
            ApiErrorMessage = "The API returned invalid application draft data.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDownloadApplicationDocxAsync(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        if (SelectedCaseId is not Guid caseId || SelectedStep is null)
            return BadRequest("Select the existing civil suit and Stage B service step.");

        try
        {
            using var draftResponse = await _apiClient.GetAsync(
                $"api/cases/{caseId}/drafts/{draftId}",
                cancellationToken);
            if (!draftResponse.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load the saved application (HTTP {(int)draftResponse.StatusCode}).";
                await LoadCivilSuitsAsync(cancellationToken);
                await LoadSelectedCaseAsync(caseId, cancellationToken);
                return Page();
            }

            var savedDraft = await draftResponse.Content.ReadFromJsonAsync<StageASavedDraftResponse>(
                cancellationToken: cancellationToken);
            if (savedDraft is null ||
                !savedDraft.DocumentType.Equals(DraftDocumentType, StringComparison.OrdinalIgnoreCase) ||
                !savedDraft.StageCode.Equals("StageB", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(savedDraft.ApplicationCode, SelectedStep.Code, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(savedDraft.JsonContent))
            {
                ApiErrorMessage = "The selected draft is not a saved application for this Stage B service step.";
                await LoadCivilSuitsAsync(cancellationToken);
                await LoadSelectedCaseAsync(caseId, cancellationToken);
                return Page();
            }

            SetGeneratedDraft(savedDraft.JsonContent);
            GeneratedDraftId = savedDraft.Id;
            var excludedFields = ExcludedDraftFields
                .Where(field => GeneratedDraftFields.Any(option =>
                    option.Name.Equals(field, StringComparison.Ordinal)))
                .ToHashSet(StringComparer.Ordinal);
            var docxText = string.Join(
                Environment.NewLine,
                BuildStageBDocxPreview(ParseDraft(savedDraft.JsonContent), excludedFields)
                    .Select(line => line.Text));
            if (string.IsNullOrWhiteSpace(docxText))
                return BadRequest("The selected fields leave no application content to export.");

            using var exportResponse = await _apiClient.PostAsJsonAsync(
                "api/drafts/export-stage-b-service-docx",
                new { text = docxText },
                cancellationToken);
            if (!exportResponse.IsSuccessStatusCode)
            {
                var body = await exportResponse.Content.ReadAsStringAsync(cancellationToken);
                ApiErrorMessage = GetApiErrorMessage(body, $"Could not create the DOCX (HTTP {(int)exportResponse.StatusCode}).");
                await LoadCivilSuitsAsync(cancellationToken);
                await LoadSelectedCaseAsync(caseId, cancellationToken);
                return Page();
            }

            var docx = await exportResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            var stepCode = string.Concat(SelectedStep.Code.Select(character =>
                char.IsLetterOrDigit(character) ? character : '_'));
            return File(
                docx,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"SummonsService_{stepCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.docx");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to export Stage B draft {DraftId} as DOCX.", draftId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to create the application DOCX.";
            await LoadCivilSuitsAsync(cancellationToken);
            await LoadSelectedCaseAsync(caseId, cancellationToken);
            return Page();
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid Stage B draft data for DOCX export {DraftId}.", draftId);
            ApiErrorMessage = "The saved application could not be read for DOCX export.";
            await LoadCivilSuitsAsync(cancellationToken);
            await LoadSelectedCaseAsync(caseId, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDownloadApplicationPdfAsync(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        if (SelectedCaseId is not Guid caseId || SelectedStep is null)
            return BadRequest("Select the existing civil suit and Stage B service step.");

        try
        {
            using var draftResponse = await _apiClient.GetAsync(
                $"api/cases/{caseId}/drafts/{draftId}",
                cancellationToken);
            if (!draftResponse.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load the saved application (HTTP {(int)draftResponse.StatusCode}).";
                await LoadCivilSuitsAsync(cancellationToken);
                await LoadSelectedCaseAsync(caseId, cancellationToken);
                return Page();
            }

            var savedDraft = await draftResponse.Content.ReadFromJsonAsync<StageASavedDraftResponse>(
                cancellationToken: cancellationToken);
            if (savedDraft is null ||
                !savedDraft.DocumentType.Equals(DraftDocumentType, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(savedDraft.JsonContent))
            {
                ApiErrorMessage = "The selected draft is not a Stage B summons-service application or has no content.";
                await LoadCivilSuitsAsync(cancellationToken);
                await LoadSelectedCaseAsync(caseId, cancellationToken);
                return Page();
            }

            using var exportResponse = await _apiClient.GetAsync(
                $"api/cases/{caseId}/drafts/{draftId}/pdf",
                cancellationToken);
            if (!exportResponse.IsSuccessStatusCode)
            {
                var body = await exportResponse.Content.ReadAsStringAsync(cancellationToken);
                ApiErrorMessage = GetApiErrorMessage(body, $"Could not create the PDF (HTTP {(int)exportResponse.StatusCode}).");
                await LoadCivilSuitsAsync(cancellationToken);
                await LoadSelectedCaseAsync(caseId, cancellationToken);
                return Page();
            }

            var pdf = await exportResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            var stepCode = string.Concat(SelectedStep.Code.Select(character =>
                char.IsLetterOrDigit(character) ? character : '_'));
            return File(pdf, "application/pdf", $"SummonsService_{stepCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to export Stage B draft {DraftId}.", draftId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to create the application PDF.";
            await LoadCivilSuitsAsync(cancellationToken);
            await LoadSelectedCaseAsync(caseId, cancellationToken);
            return Page();
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid Stage B draft data for {DraftId}.", draftId);
            ApiErrorMessage = "The saved application could not be read for PDF export.";
            await LoadCivilSuitsAsync(cancellationToken);
            await LoadSelectedCaseAsync(caseId, cancellationToken);
            return Page();
        }
    }

    private async Task LoadCivilSuitsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync(
                "api/cases?documentType=CivilPlaint",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load saved civil suits (HTTP {(int)response.StatusCode}).";
                CivilSuits = [];
                return;
            }

            CivilSuits = await response.Content.ReadFromJsonAsync<List<ExistingCaseOption>>(
                cancellationToken: cancellationToken) ?? [];
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not load saved civil suits.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to load saved civil suits.";
            CivilSuits = [];
        }
    }

    private async Task<bool> LoadSelectedCaseAsync(
        Guid? caseId,
        CancellationToken cancellationToken,
        bool useStoredDefendantDetails = false)
    {
        SelectedCase = null;
        ExistingPlaintDraftJson = null;
        if (caseId is not Guid selectedCaseId)
        {
            RefreshRequirements();
            return false;
        }

        try
        {
            using var response = await _apiClient.GetAsync($"api/cases/{selectedCaseId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load the selected suit (HTTP {(int)response.StatusCode}).";
                RefreshRequirements();
                return false;
            }

            SelectedCase = await response.Content.ReadFromJsonAsync<ExistingCaseDetail>(
                cancellationToken: cancellationToken);
            if (SelectedCase is null ||
                !SelectedCase.CaseCategory.Equals("Civil", StringComparison.OrdinalIgnoreCase))
            {
                ApiErrorMessage = "Select a saved civil suit.";
                SelectedCase = null;
                RefreshRequirements();
                return false;
            }

            var plaintDraft = SelectedCase.Drafts
                .Where(draft => draft.DocumentType.Equals("CivilPlaint", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(draft => draft.CreatedAtUtc)
                .FirstOrDefault();
            if (plaintDraft is null)
            {
                ApiErrorMessage = "The selected civil suit does not have a saved plaint draft.";
                SelectedCase = null;
                RefreshRequirements();
                return false;
            }

            using var draftResponse = await _apiClient.GetAsync(
                $"api/cases/{selectedCaseId}/drafts/{plaintDraft.Id}",
                cancellationToken);
            if (!draftResponse.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load the filed plaint context (HTTP {(int)draftResponse.StatusCode}).";
                SelectedCase = null;
                RefreshRequirements();
                return false;
            }

            var plaint = await draftResponse.Content.ReadFromJsonAsync<StageASavedDraftResponse>(
                cancellationToken: cancellationToken);
            if (plaint is null || string.IsNullOrWhiteSpace(plaint.JsonContent))
            {
                ApiErrorMessage = "The saved plaint does not contain readable draft content.";
                SelectedCase = null;
                RefreshRequirements();
                return false;
            }

            ExistingPlaintDraftJson = plaint.JsonContent;
            PopulateDefendantDetails(useStoredDefendantDetails);
            PopulateInitialSummonsDefaults(useStoredDefendantDetails);
            RefreshRequirements();
            return true;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not load saved civil suit {CaseId}.", selectedCaseId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to load the selected suit.";
            RefreshRequirements();
            return false;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid civil-suit data for case {CaseId}.", selectedCaseId);
            ApiErrorMessage = "The API returned invalid civil-suit data.";
            SelectedCase = null;
            RefreshRequirements();
            return false;
        }
    }

    private void RefreshRequirements()
    {
        if (SelectedStep is null)
        {
            Requirements = [];
            Uploads = [];
            return;
        }

        Requirements = SelectedStep.Documents.Select(item =>
        {
            var matchingDocuments = SelectedCase?.Documents
                .Where(document =>
                    (document.StageCode is null ||
                        (document.StageCode.Equals("StageB", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(document.ApplicationCode, SelectedStep.Code, StringComparison.OrdinalIgnoreCase))) &&
                    (document.DocumentCode.Equals(item.Code, StringComparison.OrdinalIgnoreCase) ||
                     item.ExistingDocumentCodes.Contains(document.DocumentCode, StringComparer.OrdinalIgnoreCase)))
                .ToList() ?? [];
            return new StageBRequirementViewModel
            {
                Code = item.Code,
                Label = item.Label,
                Guidance = item.Guidance,
                CopiesToPrepare = item.CopiesToPrepare,
                ExistingDocuments = matchingDocuments
            };
        }).ToList();

        Uploads = Requirements
            .Where(requirement => !requirement.AlreadyOnFile && !requirement.CopiesToPrepare)
            .Select(requirement => new StageBUploadOption(requirement.Code, requirement.Label))
            .ToList();
    }

    private string BuildDraftInstructions(StageBServiceStep step, string serviceFacts)
    {
        var instructions = new StringBuilder();
        instructions.AppendLine("Stage B selection:");
        instructions.AppendLine($"Service step: {step.Label}");
        instructions.AppendLine($"Provision generally relevant: {step.Provision}");
        instructions.AppendLine($"Scenario: {step.Scenario}");
        if (step.Code.Equals("InitialSummons", StringComparison.OrdinalIgnoreCase))
        {
            instructions.AppendLine($"Defendant name: {DefendantName?.Trim()}");
            instructions.AppendLine($"Defendant's service address: {DefendantServiceAddress?.Trim()}");
            instructions.AppendLine($"Counsel name: {ValueOrPlaceholder(CounselName)}");
            instructions.AppendLine($"Counsel enrollment number: {ValueOrPlaceholder(CounselEnrollmentNo)}");
        }
        instructions.AppendLine();
        instructions.AppendLine("Applicant's service facts:");
        instructions.AppendLine(serviceFacts.Trim());
        instructions.AppendLine();
        instructions.AppendLine("Saved plaint:");
        instructions.AppendLine(ExistingPlaintDraftJson ?? "No plaint content available.");
        instructions.AppendLine();
        instructions.AppendLine("Service documents status:");
        foreach (var requirement in Requirements)
        {
            var status = requirement.CopiesToPrepare
                ? "Copies must be prepared as directed by the court/registry."
                : requirement.AlreadyOnFile
                    ? $"On file: {string.Join(", ", requirement.ExistingDocuments.Select(document => document.OriginalFileName))}"
                    : "Not supplied.";
            instructions.AppendLine($"- {requirement.Label}: {status} {requirement.Guidance}");
        }

        instructions.AppendLine();
        instructions.AppendLine("Use the plaint and uploaded case documents as context. Do not invent service attempts, results, addresses, returned summons, or court directions. Describe only supplied facts and evidence. Identify unavailable information as a clear placeholder. Refer to plaint/annexure copies as copies to prepare where the checklist says so, not as already attached copies.");
        if (step.Code.Equals("InitialSummons", StringComparison.OrdinalIgnoreCase))
            instructions.AppendLine("For first-time issuance, follow the supplied sample structure: court heading; suit number; IN THE MATTER OF with Plaintiff and Defendant labels; title 'APPLICATION ON BEHALF OF THE PLAINTIFF FOR ISSUANCE OF SUMMONS TO THE DEFENDANT'; 'MOST RESPECTFULLY SHOWETH'; concise numbered paragraphs; the supplied defendant name and complete service address; a PRAYER asking issuance through the prescribed appropriate mode and any other fit order; the traditional concluding prayer line; place/date; Plaintiff signature; and 'Through Counsel' with supplied advocate details only. Use applicationType 'InitialSummons'. Set respondentName and defendantServiceAddress to the supplied values. Do not invent suit number, counsel details, amount, dates, or service method. State only that the suit is pending if supported by the saved plaint/case context. Do not say service failed or request re-issuance.");
        return instructions.ToString();
    }

    private void SetGeneratedDraft(string json)
    {
        using var draft = JsonDocument.Parse(json);
        if (draft.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("The Stage B application JSON must be an object.");

        GeneratedDraftJson = JsonSerializer.Serialize(draft.RootElement, PrettyJsonOptions);
        GeneratedDraftFields = GetOutputFieldOptions(draft.RootElement);
        GeneratedDraftPreview = BuildStageBDocxPreview(draft.RootElement);
    }

    private static JsonElement ParseDraft(string json)
    {
        using var draft = JsonDocument.Parse(json);
        return draft.RootElement.Clone();
    }

    private static IReadOnlyList<StageBOutputFieldOption> GetOutputFieldOptions(JsonElement draft)
    {
        return draft.EnumerateObject()
            .Select(property =>
            {
                var label = GetFieldLabel(property.Name);
                var value = GetDraftFieldValue(property.Value);
                var preview = value.Length > 160 ? $"{value[..157]}..." : value;
                return new StageBOutputFieldOption(property.Name, label, preview);
            })
            .ToList();
    }

    private static IReadOnlyList<StageBDocxPreviewLine> BuildStageBDocxPreview(
        JsonElement draft,
        IReadOnlySet<string>? excludedFields = null)
    {
        if (draft.ValueKind != JsonValueKind.Object)
            throw new JsonException("The Stage B application JSON must be an object.");

        var lines = new List<StageBDocxPreviewLine>();
        var excluded = excludedFields ?? new HashSet<string>(StringComparer.Ordinal);

        void AddLine(string? fieldName, string? text, string cssClass = "")
        {
            if (!string.IsNullOrWhiteSpace(text) &&
                (fieldName is null || !excluded.Contains(fieldName)))
            {
                lines.Add(new StageBDocxPreviewLine(fieldName, text.Trim(), cssClass));
            }
        }

        void AddList(string fieldName, string heading, string itemPrefix)
        {
            if (excluded.Contains(fieldName))
                return;

            var values = GetDraftFieldValues(draft, fieldName);
            if (values.Count == 0)
                return;

            AddLine(null, heading, "stage-b-docx-heading");
            for (var index = 0; index < values.Count; index++)
                AddLine(fieldName, $"{itemPrefix}{index + 1}. {values[index]}", "stage-b-docx-body");
        }

        var courtName = GetDraftString(draft, "courtName");
        var caseNumber = GetDraftString(draft, "caseNumber");
        var caseTitle = GetDraftString(draft, "caseTitle");
        var applicantName = GetDraftString(draft, "applicantName");
        var respondentName = GetDraftString(draft, "respondentName");
        var applicationType = GetDraftString(draft, "applicationType");
        var applicationTitle = GetDraftString(draft, "applicationTitle");

        AddLine("courtName", string.IsNullOrWhiteSpace(courtName) ? null : $"IN THE COURT OF {courtName}", "stage-b-docx-title");
        AddLine("caseNumber", string.IsNullOrWhiteSpace(caseNumber) ? null : $"Civil Suit No. {caseNumber}", "stage-b-docx-center");
        AddLine("caseTitle", caseTitle, "stage-b-docx-center");
        AddLine(null, "IN THE MATTER OF:", "stage-b-docx-heading stage-b-docx-center");
        AddLine("applicantName", string.IsNullOrWhiteSpace(applicantName) ? null : $"{applicantName} ... Plaintiff", "stage-b-docx-center");
        AddLine(null, "VERSUS", "stage-b-docx-center");
        AddLine("respondentName", string.IsNullOrWhiteSpace(respondentName) ? null : $"{respondentName} ... Defendant", "stage-b-docx-center");
        AddLine("applicationTitle", applicationTitle, "stage-b-docx-title");
        AddLine("applicationType", string.IsNullOrWhiteSpace(applicationType) ? null : $"Application type: {applicationType}", "stage-b-docx-meta");
        AddLine("provision", GetDraftString(draft, "provision"), "stage-b-docx-center stage-b-docx-italic");
        AddLine(null, "MOST RESPECTFULLY SHOWETH:", "stage-b-docx-heading");

        var serviceAddress = GetDraftString(draft, "defendantServiceAddress");
        AddLine(
            "defendantServiceAddress",
            string.IsNullOrWhiteSpace(serviceAddress) ? null : $"Defendant's address for service: {serviceAddress}",
            "stage-b-docx-body");

        AddList("factualBackground", "FACTUAL BACKGROUND", string.Empty);
        AddList("grounds", "GROUNDS", string.Empty);
        AddList("documentsReliedOn", "DOCUMENTS RELIED ON", "• ");

        AddLine("supportingAffidavit", string.IsNullOrWhiteSpace(GetDraftString(draft, "supportingAffidavit"))
            ? null
            : "SUPPORTING AFFIDAVIT",
            "stage-b-docx-heading");
        AddLine("supportingAffidavit", GetDraftString(draft, "supportingAffidavit"), "stage-b-docx-body");
        AddLine("prayer", string.IsNullOrWhiteSpace(GetDraftString(draft, "prayer")) ? null : "PRAYER", "stage-b-docx-heading");
        AddLine("prayer", GetDraftString(draft, "prayer"), "stage-b-docx-body");

        if (string.Equals(applicationType, "InitialSummons", StringComparison.OrdinalIgnoreCase))
            AddLine("applicationType", "AND FOR THIS ACT OF KINDNESS, THE PLAINTIFF AS IN DUTY BOUND SHALL EVER PRAY.", "stage-b-docx-heading");

        AddLine("place", string.IsNullOrWhiteSpace(GetDraftString(draft, "place"))
            ? null
            : $"Place: {GetDraftString(draft, "place")}",
            "stage-b-docx-body");
        AddLine("date", string.IsNullOrWhiteSpace(GetDraftString(draft, "date"))
            ? null
            : $"Date: {GetDraftString(draft, "date")}",
            "stage-b-docx-body");
        AddLine(null, "Plaintiff", "stage-b-docx-signature");
        AddLine("advocateName", string.IsNullOrWhiteSpace(GetDraftString(draft, "advocateName"))
            ? null
            : $"Through Counsel: {GetDraftString(draft, "advocateName")}",
            "stage-b-docx-signature");
        AddLine("advocateEnrollmentNo", string.IsNullOrWhiteSpace(GetDraftString(draft, "advocateEnrollmentNo"))
            ? null
            : $"Enrollment No.: {GetDraftString(draft, "advocateEnrollmentNo")}",
            "stage-b-docx-signature");

        var renderedFields = new HashSet<string>(
            [
                "courtName", "caseNumber", "caseTitle", "applicantName", "respondentName",
                "applicationTitle", "applicationType", "provision", "defendantServiceAddress",
                "factualBackground", "grounds", "documentsReliedOn", "supportingAffidavit",
                "prayer", "place", "date", "advocateName", "advocateEnrollmentNo"
            ],
            StringComparer.OrdinalIgnoreCase);
        var additionalFields = draft.EnumerateObject()
            .Where(property => !renderedFields.Contains(property.Name))
            .ToList();
        if (additionalFields.Count > 0)
        {
            AddLine(null, "ADDITIONAL DETAILS", "stage-b-docx-heading");
            foreach (var property in additionalFields)
            {
                AddLine(property.Name, $"{GetFieldLabel(property.Name)}: {GetDraftFieldValue(property.Value)}", "stage-b-docx-body");
            }
        }

        return lines;
    }

    private static string GetFieldLabel(string name)
    {
        var label = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2");
        return label.Length == 0 ? name : char.ToUpperInvariant(label[0]) + label[1..];
    }

    private static string? GetDraftString(JsonElement draft, string propertyName)
    {
        foreach (var property in draft.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : null;
        }

        return null;
    }

    private static IReadOnlyList<string> GetDraftFieldValues(JsonElement draft, string fieldName)
    {
        foreach (var property in draft.EnumerateObject())
        {
            if (!property.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                continue;

            return property.Value.ValueKind switch
            {
                JsonValueKind.Array => property.Value.EnumerateArray()
                    .Select(GetDraftFieldValue)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList(),
                JsonValueKind.String when !string.IsNullOrWhiteSpace(property.Value.GetString()) =>
                    [property.Value.GetString()!],
                _ => []
            };
        }

        return [];
    }

    private static string GetDraftFieldValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join("; ", value.EnumerateArray().Select(GetDraftFieldValue)),
            JsonValueKind.Object => JsonSerializer.Serialize(value),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => value.ToString()
        };

    private void PopulateDefendantDetails(bool overwrite)
    {
        if (SelectedCase is null)
            return;

        if (overwrite || string.IsNullOrWhiteSpace(DefendantName))
            DefendantName = SelectedCase.DefendantName;
        if (overwrite || string.IsNullOrWhiteSpace(DefendantServiceAddress))
            DefendantServiceAddress = SelectedCase.DefendantAddress;
    }

    private void PopulateInitialSummonsDefaults(bool overwrite)
    {
        if (SelectedCase is null ||
            !string.Equals(SelectedStepCode, "InitialSummons", StringComparison.OrdinalIgnoreCase))
            return;

        const string initialSummonsConfirmation =
            "I confirm that summons have not previously been issued to the Defendant in this suit. " +
            "Please issue summons to the Defendant at the address provided above through the prescribed mode of service.";

        if (overwrite || string.IsNullOrWhiteSpace(ServiceFacts))
            ServiceFacts = initialSummonsConfirmation;

        var advocateDetails = ExtractPlaintAdvocateDetails();
        if (overwrite || string.IsNullOrWhiteSpace(CounselName))
            CounselName = advocateDetails.Name;
        if (overwrite || string.IsNullOrWhiteSpace(CounselEnrollmentNo))
            CounselEnrollmentNo = advocateDetails.EnrollmentNo;
    }

    private (string? Name, string? EnrollmentNo) ExtractPlaintAdvocateDetails()
    {
        string? advocateName = null;
        string? enrollmentNo = null;

        try
        {
            using var plaint = JsonDocument.Parse(ExistingPlaintDraftJson ?? "{}");
            advocateName = GetStringProperty(plaint.RootElement, "advocateName");
            enrollmentNo = GetStringProperty(plaint.RootElement, "advocateEnrollmentNo");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Could not read advocate details from saved plaint for case {CaseId}.", SelectedCaseId);
        }

        var vakalatnamaText = SelectedCase?.Documents
            .Where(document =>
                document.DocumentCode.Equals("VAKALATNAMA", StringComparison.OrdinalIgnoreCase) ||
                document.OriginalFileName.Contains("vakalatnama", StringComparison.OrdinalIgnoreCase))
            .Select(document => document.ExtractedText)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        if (!string.IsNullOrWhiteSpace(vakalatnamaText))
        {
            advocateName ??= ExtractLabeledValue(
                vakalatnamaText,
                @"(?:name\s+of\s+)?advocate(?:\s+name)?");
            enrollmentNo ??= ExtractLabeledValue(
                vakalatnamaText,
                @"(?:(?:enrol|enroll)ment|registration)(?:\s+(?:number|no\.?))?");
        }

        return (advocateName, enrollmentNo);
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
                return NullIfWhiteSpace(property.Value.GetString());
        }

        return null;
    }

    private static string? ExtractLabeledValue(string text, string labelPattern)
    {
        var match = Regex.Match(
            text,
            $@"(?im)^\s*{labelPattern}\s*[:\-]\s*(?<value>[^\r\n]+)");
        return match.Success ? NullIfWhiteSpace(match.Groups["value"].Value) : null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ValueOrPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "[To be completed]" : value.Trim();

    private static string GetApiErrorMessage(string responseBody, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(error.GetString()))
                return error.GetString()!;
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}

public sealed class StageBRequirementViewModel
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Guidance { get; set; } = string.Empty;
    public bool CopiesToPrepare { get; set; }
    public List<ExistingCaseDocument> ExistingDocuments { get; set; } = [];
    public bool AlreadyOnFile => ExistingDocuments.Count > 0;
}

public sealed record StageBUploadOption(string Code, string Label);

public sealed class StageBUploadInput
{
    public string DocumentCode { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
}

public sealed record StageBOutputFieldOption(string Name, string Label, string ValuePreview);

public sealed record StageBDocxPreviewLine(string? FieldName, string Text, string CssClass);
