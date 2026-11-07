using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LegalDraftAI.Web.Pages;

[RequestSizeLimit(50 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
public sealed class CivilApplicationsModel(
    IHttpClientFactory httpClientFactory,
    ILogger<CivilApplicationsModel> logger) : PageModel
{
    private const string DraftDocumentType = "CivilStageAApplication";
    private const string ProposedNoticeDocumentType = "CivilStageAProposedNotice";

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _apiClient = httpClientFactory.CreateClient("DraftsApi");

    public IReadOnlyList<StageAApplication> Applications => StageAApplicationCatalog.Applications;
    public IReadOnlyList<ExistingCaseOption> CivilPlaints { get; private set; } = [];
    public ExistingCaseDetail? SelectedCase { get; private set; }
    public string? ExistingPlaintDraftJson { get; private set; }
    public StageAApplication? SelectedApplication =>
        StageAApplicationCatalog.Get(SelectedApplicationCode);
    public IReadOnlyList<ExistingCaseDraft> ExistingApplicationDrafts =>
        SelectedCase?.Drafts
            .Where(draft =>
                draft.StageCode?.Equals("StageA", StringComparison.OrdinalIgnoreCase) == true &&
                draft.DocumentType.Equals(DraftDocumentType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    draft.ApplicationCode,
                    SelectedApplicationCode,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(draft => draft.CreatedAtUtc)
            .ToList() ?? [];
    public bool HasAttachedAffidavit =>
        SelectedApplication is not null &&
        SelectedCase?.Documents.Any(document =>
            IsForSelectedApplicationStage(document) &&
            document.DocumentCode.Equals(
                SelectedApplication.AffidavitDocumentCode,
                StringComparison.OrdinalIgnoreCase)) == true;
    public bool HasReadableInterimHarmEvidence =>
        SelectedCase?.Documents.Any(document =>
            IsForSelectedApplicationStage(document) &&
            IsInterimHarmEvidenceDocument(document) &&
            !string.IsNullOrWhiteSpace(document.ExtractedText)) == true;
    public IReadOnlyList<StageARequirementViewModel> Requirements { get; private set; } = [];
    public IReadOnlyList<ExistingCaseDocument> DocumentsForSelectedApplication =>
        SelectedCase?.Documents
            .Where(IsForSelectedApplicationStage)
            .Where(document => !string.IsNullOrWhiteSpace(document.ExtractedText))
            .OrderBy(document => document.OriginalFileName, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    public List<StageAUploadInput> Uploads { get; private set; } = [];
    public string? ApiErrorMessage { get; private set; }
    public string? StatusMessage { get; private set; }
    public string? GeneratedDraftJson { get; private set; }
    public Guid? GeneratedApplicationDraftId { get; private set; }
    public bool IsRetrievedApplicationDraft { get; private set; }
    public string? GeneratedProposedNoticeJson { get; private set; }
    public Guid? GeneratedProposedNoticeDraftId { get; private set; }

    [BindProperty]
    public Guid? SelectedCaseId { get; set; }

    [BindProperty]
    public string? SelectedApplicationCode { get; set; }

    [BindProperty]
    [StringLength(20000)]
    public string? AdditionalFacts { get; set; }

    [BindProperty]
    public List<Guid> ExcludedDocumentIds { get; set; } = [];

    [BindProperty]
    [StringLength(30000)]
    public string? AffidavitText { get; set; }

    [BindProperty]
    public Guid? GeneratedAffidavitDraftId { get; set; }

    [BindProperty]
    public List<StageAUploadInput> UploadInputs { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadCivilPlaintsAsync(cancellationToken);
        if (SelectedCaseId is Guid caseId)
            await LoadSelectedCaseAsync(caseId, cancellationToken);
        else
            RefreshRequirements();
    }

    public async Task<IActionResult> OnPostSelectCaseAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilPlaintsAsync(cancellationToken);
        if (SelectedCaseId is Guid caseId)
            await LoadSelectedCaseAsync(caseId, cancellationToken);
        else
            RefreshRequirements();

        return Page();
    }

    public async Task<IActionResult> OnPostUploadDocumentsAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilPlaintsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();
        if (SelectedApplication is null)
        {
            ApiErrorMessage = "Select one of the Stage A applications.";
            return Page();
        }

        var selectedUploads = UploadInputs
            .Where(upload => upload.File is { Length: > 0 })
            .ToList();
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
            .Where(requirement =>
                !requirement.AlreadyOnFile &&
                !requirement.Code.Equals(
                    SelectedApplication?.AffidavitDocumentCode,
                    StringComparison.OrdinalIgnoreCase))
            .Select(requirement => requirement.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedUploads.Any(upload => !allowedCodes.Contains(upload.DocumentCode)) ||
            selectedUploads.Select(upload => upload.DocumentCode)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != selectedUploads.Count)
        {
            ApiErrorMessage = "Each uploaded file must be mapped to one outstanding document in the selected application's checklist.";
            return Page();
        }

        var uploadedCount = 0;
        var failures = new List<string>();
        foreach (var upload in selectedUploads)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(upload.DocumentCode), "documentCode");
            form.Add(new StringContent("StageA"), "stageCode");
            form.Add(new StringContent(SelectedApplication.Code), "applicationCode");
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
                var errorMessage = GetApiErrorMessage(body, $"HTTP {(int)response.StatusCode}");
                failures.Add($"{upload.File.FileName}: {errorMessage}");
            }
            catch (HttpRequestException exception)
            {
                logger.LogError(
                    exception,
                    "Could not upload Stage A application document {DocumentCode} for case {CaseId}.",
                    upload.DocumentCode,
                    SelectedCaseId);
                failures.Add($"{upload.File.FileName}: the LegalDraftAI API could not be reached.");
            }
        }

        if (uploadedCount > 0)
            StatusMessage = $"{uploadedCount} supporting document(s) uploaded to the selected case.";
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
        await LoadCivilPlaintsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();

        var document = SelectedCase!.Documents.FirstOrDefault(item => item.Id == documentId);
        if (document is null ||
            SelectedApplication is null ||
            !Requirements.Any(requirement => requirement.ExistingDocuments.Any(existing => existing.Id == documentId)))
        {
            ApiErrorMessage = "The selected document is not attached as a supporting document for this Stage A application.";
            return Page();
        }

        try
        {
            using var response = await _apiClient.DeleteAsync(
                $"api/cases/{SelectedCaseId}/documents/{documentId}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Could not remove the attached document (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken);
            StatusMessage = $"Removed {document.OriginalFileName} from this case.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "Could not reach the API to remove supporting document {DocumentId} from case {CaseId}.",
                documentId,
                SelectedCaseId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to remove the attached document.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAffidavitAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilPlaintsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();

        if (SelectedApplication is null)
        {
            ApiErrorMessage = "Select one of the Stage A applications.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(AdditionalFacts) || AdditionalFacts.Length > 20000)
        {
            ApiErrorMessage = "Enter the application-specific facts (20,000 characters or fewer). The plaint alone may not establish the grounds for a procedural application.";
            return Page();
        }

        var instructions = BuildAffidavitInstructions(SelectedApplication, AdditionalFacts);
        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                $"api/cases/{SelectedCaseId}/drafts",
                new
                {
                    DocumentType = "CivilSupportingAffidavit",
                    StageCode = "StageA",
                    ApplicationCode = SelectedApplication.Code,
                    ExtraInstructions = instructions,
                    ExcludedDocumentIds = ExcludedDocumentIds,
                    SavePdf = false
                },
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = GetApiErrorMessage(responseBody,
                    $"Supporting affidavit generation failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            var affidavitDraft = JsonSerializer.Deserialize<StageASavedDraftResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (affidavitDraft is null || string.IsNullOrWhiteSpace(affidavitDraft.JsonContent))
            {
                logger.LogError(
                    "Supporting affidavit API response did not contain saved draft content for case {CaseId}.",
                    SelectedCaseId);
                ApiErrorMessage = "The API response did not contain the saved supporting affidavit.";
                return Page();
            }

            using var affidavitJson = JsonDocument.Parse(affidavitDraft.JsonContent);
            if (!affidavitJson.RootElement.TryGetProperty("affidavitText", out var affidavitText) ||
                affidavitText.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(affidavitText.GetString()))
            {
                logger.LogError(
                    "Generated supporting affidavit did not contain affidavitText for case {CaseId}.",
                    SelectedCaseId);
                ApiErrorMessage = "The generated supporting affidavit did not contain affidavit text.";
                return Page();
            }

            AffidavitText = affidavitText.GetString();
            GeneratedAffidavitDraftId = affidavitDraft.Id;
            await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken);
            StatusMessage = "Supporting affidavit generated. Review or edit it, then attach it to this case before generating the application.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to generate a Stage A supporting affidavit.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API. Check that it is running and that DraftsApi:BaseUrl is correct.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid JSON for a Stage A supporting affidavit.");
            ApiErrorMessage = "The API returned invalid supporting affidavit data.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAttachAffidavitAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilPlaintsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();

        if (SelectedApplication is null || GeneratedAffidavitDraftId is not Guid affidavitDraftId)
        {
            ApiErrorMessage = "Generate the supporting affidavit for the selected application before attaching it.";
            return Page();
        }

        if (HasAttachedAffidavit)
        {
            ApiErrorMessage = "A supporting affidavit is already attached for this application.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(AffidavitText) || AffidavitText.Length > 30000)
        {
            ApiErrorMessage = "The affidavit text must be non-empty and no more than 30,000 characters.";
            return Page();
        }

        var savedAffidavit = SelectedCase!.Drafts.FirstOrDefault(draft =>
            draft.Id == affidavitDraftId &&
            draft.DocumentType.Equals("CivilSupportingAffidavit", StringComparison.OrdinalIgnoreCase));
        if (savedAffidavit is null)
        {
            ApiErrorMessage = "The supporting affidavit draft is not saved on the selected case. Generate it again before attaching.";
            return Page();
        }

        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(SelectedApplication.AffidavitDocumentCode), "documentCode");
            form.Add(new StringContent("StageA"), "stageCode");
            form.Add(new StringContent(SelectedApplication.Code), "applicationCode");
            form.Add(
                new ByteArrayContent(Encoding.UTF8.GetBytes(AffidavitText)),
                "file",
                $"SupportingAffidavit_{SelectedApplication.Code}.txt");

            using var response = await _apiClient.PostAsync(
                $"api/cases/{SelectedCaseId}/documents",
                form,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Could not attach the supporting affidavit (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken);
            StatusMessage = "Supporting affidavit attached to the selected case. You can now generate the application.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to attach the Stage A supporting affidavit.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to attach the supporting affidavit.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostDownloadAffidavitPdfAsync(CancellationToken cancellationToken)
    {
        if (SelectedApplication is null ||
            SelectedCaseId is not Guid caseId ||
            string.IsNullOrWhiteSpace(AffidavitText) ||
            AffidavitText.Length > 30000)
        {
            return BadRequest("Select an application and provide affidavit text of no more than 30,000 characters.");
        }

        return await ExportAffidavitPdfAsync(
            AffidavitText,
            $"SupportingAffidavit_{SelectedApplication.Code}.pdf",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostDownloadAttachedAffidavitPdfAsync(CancellationToken cancellationToken)
    {
        await LoadCivilPlaintsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken) ||
            SelectedApplication is null)
        {
            return Page();
        }

        var document = SelectedCase!.Documents.FirstOrDefault(item =>
            IsForSelectedApplicationStage(item) &&
            item.DocumentCode.Equals(
                SelectedApplication.AffidavitDocumentCode,
                StringComparison.OrdinalIgnoreCase));
        if (document is null || string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            ApiErrorMessage = "The attached affidavit has no readable text to export.";
            return Page();
        }

        return await ExportAffidavitPdfAsync(
            document.ExtractedText,
            $"SupportingAffidavit_{SelectedApplication.Code}.pdf",
            cancellationToken);
    }

    private async Task<IActionResult> ExportAffidavitPdfAsync(
        string affidavitText,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                "api/drafts/export-affidavit-pdf",
                new { text = affidavitText },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Could not create the affidavit PDF (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var responseFileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? fileName;
            return File(pdfBytes, "application/pdf", responseFileName.Trim('"'));
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to export the supporting affidavit PDF.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to create the affidavit PDF.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilPlaintsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();

        if (SelectedApplication is null)
        {
            ApiErrorMessage = "Select one of the Stage A applications.";
            return Page();
        }

        if (!HasAttachedAffidavit)
        {
            ApiErrorMessage = "Generate and attach the supporting affidavit for this application before proceeding.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(AdditionalFacts) || AdditionalFacts.Length > 20000)
        {
            ApiErrorMessage = "Enter the application-specific facts (20,000 characters or fewer). The plaint alone may not establish the grounds for a procedural application.";
            return Page();
        }

        var extraInstructions = BuildApplicationInstructions(SelectedApplication, AdditionalFacts);
        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                $"api/cases/{SelectedCaseId}/drafts",
                new
                {
                    DocumentType = DraftDocumentType,
                    StageCode = "StageA",
                    ApplicationCode = SelectedApplication.Code,
                    ExtraInstructions = extraInstructions,
                    ExcludedDocumentIds = ExcludedDocumentIds,
                    SavePdf = false
                },
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Application draft generation failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            var draft = JsonSerializer.Deserialize<StageASavedDraftResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (draft is null || string.IsNullOrWhiteSpace(draft.JsonContent))
            {
                logger.LogError(
                    "Stage A application API response did not contain saved draft content for case {CaseId}.",
                    SelectedCaseId);
                ApiErrorMessage = "The API response did not contain the saved application draft.";
                return Page();
            }

            using var draftJson = JsonDocument.Parse(draft.JsonContent);
            GeneratedDraftJson = JsonSerializer.Serialize(draftJson.RootElement, PrettyJsonOptions);
            GeneratedApplicationDraftId = draft.Id;
            IsRetrievedApplicationDraft = false;
            StatusMessage = "The application draft was generated and saved to the selected civil case.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to generate a Stage A application.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API. Check that it is running and that DraftsApi:BaseUrl is correct.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid JSON for a Stage A application draft.");
            ApiErrorMessage = "The API returned invalid application draft data.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDownloadApplicationPdfAsync(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        if (SelectedCaseId is not Guid selectedCaseId ||
            SelectedApplication is null)
        {
            return BadRequest("Select the case and application draft to download.");
        }

        return await DownloadApplicationPdfCoreAsync(draftId, selectedCaseId, cancellationToken);
    }

    public async Task<IActionResult> OnPostRetrieveApplicationDraftAsync(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilPlaintsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();

        if (SelectedApplication is null)
        {
            ApiErrorMessage = "Select a Stage A application before retrieving a saved draft.";
            return Page();
        }

        var existingDraft = ExistingApplicationDrafts.FirstOrDefault(draft => draft.Id == draftId);
        if (existingDraft is null)
        {
            ApiErrorMessage = "The selected draft is not saved for this case and Stage A application.";
            return Page();
        }

        try
        {
            using var response = await _apiClient.GetAsync(
                $"api/cases/{SelectedCaseId}/drafts/{draftId}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not retrieve the saved application draft (HTTP {(int)response.StatusCode}).";
                return Page();
            }

            var draft = await response.Content.ReadFromJsonAsync<StageASavedDraftResponse>(
                cancellationToken: cancellationToken);
            if (draft is null ||
                draft.Id != draftId ||
                !draft.DocumentType.Equals(DraftDocumentType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    draft.ApplicationCode,
                    SelectedApplication.Code,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(draft.StageCode, "StageA", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(draft.JsonContent))
            {
                ApiErrorMessage = "The saved draft response does not match the selected Stage A application.";
                return Page();
            }

            using var draftJson = JsonDocument.Parse(draft.JsonContent);
            GeneratedDraftJson = JsonSerializer.Serialize(draftJson.RootElement, PrettyJsonOptions);
            GeneratedApplicationDraftId = draft.Id;
            IsRetrievedApplicationDraft = true;
            StatusMessage = $"Retrieved saved application draft version {existingDraft.Version}.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "Could not retrieve Stage A application draft {DraftId} for case {CaseId}.",
                draftId,
                SelectedCaseId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to retrieve the saved application draft.";
        }
        catch (JsonException exception)
        {
            logger.LogError(
                exception,
                "The API returned invalid data for Stage A application draft {DraftId}.",
                draftId);
            ApiErrorMessage = "The saved application draft could not be read.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateProposedNoticeAsync(CancellationToken cancellationToken)
        {
            ModelState.Clear();
            await LoadCivilPlaintsAsync(cancellationToken);
            if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
                return Page();

            if (!string.Equals(SelectedApplicationCode, "UrgentInterimRelief", StringComparison.OrdinalIgnoreCase))
            {
                ApiErrorMessage = "A proposed notice is available only for the urgent interim-relief application.";
                return Page();
            }

            var selectedApplication = SelectedApplication;
            if (selectedApplication is null)
            {
                ApiErrorMessage = "Select one of the Stage A applications.";
                return Page();
            }

            var evidenceDocuments = SelectedCase!.Documents
                .Where(IsForSelectedApplicationStage)
                .Where(IsInterimHarmEvidenceDocument)
                .Where(document => !string.IsNullOrWhiteSpace(document.ExtractedText))
                .ToList();
            if (evidenceDocuments.Count == 0)
            {
                ApiErrorMessage = "Attach readable evidence of the threatened or continuing harm before generating the proposed notice.";
                return Page();
            }

        try
            {
                using var response = await _apiClient.PostAsJsonAsync(
                    $"api/cases/{SelectedCaseId}/drafts",
                    new
                    {
                        documentType = ProposedNoticeDocumentType,
                        stageCode = "StageA",
                        applicationCode = selectedApplication.Code,
                        extraInstructions = BuildProposedNoticeInstructions(evidenceDocuments),
                        savePdf = false
                    },
                    cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    ApiErrorMessage = GetApiErrorMessage(
                        responseBody,
                        $"Proposed notice generation failed (HTTP {(int)response.StatusCode}).");
                    return Page();
                }

                var noticeDraft = JsonSerializer.Deserialize<StageASavedDraftResponse>(
                    responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (noticeDraft is null ||
                    !noticeDraft.DocumentType.Equals(ProposedNoticeDocumentType, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(noticeDraft.JsonContent))
                {
                    logger.LogError(
                        "Proposed notice generation response did not contain saved draft content for case {CaseId}.",
                        SelectedCaseId);
                    ApiErrorMessage = "The API response did not contain the saved proposed notice.";
                    return Page();
                }

                using var noticeJson = JsonDocument.Parse(noticeDraft.JsonContent);
                GeneratedProposedNoticeJson = JsonSerializer.Serialize(noticeJson.RootElement, PrettyJsonOptions);
                GeneratedProposedNoticeDraftId = noticeDraft.Id;
                StatusMessage = "Proposed notice generated from the uploaded harm evidence and saved to the selected case.";
            }
            catch (HttpRequestException exception)
            {
                logger.LogError(exception, "Could not reach the API to generate a Stage A proposed notice.");
                ApiErrorMessage = "Could not reach the LegalDraftAI API to generate the proposed notice.";
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "The API returned invalid Stage A proposed notice data.");
                ApiErrorMessage = "The API returned invalid proposed notice data.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDownloadProposedNoticePdfAsync(
            Guid draftId,
            CancellationToken cancellationToken)
        {
            if (SelectedCaseId is not Guid selectedCaseId ||
                !string.Equals(SelectedApplicationCode, "UrgentInterimRelief", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Select the civil case and urgent interim-relief application.");

            try
            {
                using var draftResponse = await _apiClient.GetAsync(
                    $"api/cases/{selectedCaseId}/drafts/{draftId}",
                    cancellationToken);
                if (!draftResponse.IsSuccessStatusCode)
                {
                    ApiErrorMessage = $"Could not load the saved proposed notice (HTTP {(int)draftResponse.StatusCode}).";
                    await LoadCivilPlaintsAsync(cancellationToken);
                    await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
                    return Page();
                }

                var savedDraft = await draftResponse.Content.ReadFromJsonAsync<StageASavedDraftResponse>(
                    cancellationToken: cancellationToken);
                if (savedDraft is null ||
                    !savedDraft.DocumentType.Equals(ProposedNoticeDocumentType, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(savedDraft.JsonContent))
                {
                    ApiErrorMessage = "The selected saved draft is not a Stage A proposed notice or has no content.";
                    await LoadCivilPlaintsAsync(cancellationToken);
                    await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
                    return Page();
                }

                using var exportResponse = await _apiClient.GetAsync(
                    $"api/cases/{selectedCaseId}/drafts/{draftId}/pdf",
                    cancellationToken);
                if (!exportResponse.IsSuccessStatusCode)
                {
                    var responseBody = await exportResponse.Content.ReadAsStringAsync(cancellationToken);
                    ApiErrorMessage = GetApiErrorMessage(
                        responseBody,
                        $"Could not create the proposed notice PDF (HTTP {(int)exportResponse.StatusCode}).");
                    await LoadCivilPlaintsAsync(cancellationToken);
                    await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
                    return Page();
                }

                var pdfBytes = await exportResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                return File(
                    pdfBytes,
                    "application/pdf",
                    $"ProposedNotice_UrgentInterimRelief_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
            }
            catch (HttpRequestException exception)
            {
                logger.LogError(exception, "Could not reach the API to export proposed notice draft {DraftId}.", draftId);
                ApiErrorMessage = "Could not reach the LegalDraftAI API to create the proposed notice PDF.";
                await LoadCivilPlaintsAsync(cancellationToken);
                await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
                return Page();
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "The API returned invalid data for proposed notice draft {DraftId}.", draftId);
                ApiErrorMessage = "The saved proposed notice could not be read for PDF export.";
                await LoadCivilPlaintsAsync(cancellationToken);
                await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
                return Page();
            }
        }

    private async Task<IActionResult> DownloadApplicationPdfCoreAsync(
        Guid draftId,
        Guid selectedCaseId,
        CancellationToken cancellationToken)
    {
        var application = SelectedApplication;
        if (application is null)
            return BadRequest("Select the Stage A application draft to download.");

        try
        {
            using var draftResponse = await _apiClient.GetAsync(
                $"api/cases/{selectedCaseId}/drafts/{draftId}",
                cancellationToken);
            if (!draftResponse.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load the saved application draft (HTTP {(int)draftResponse.StatusCode}).";
                await LoadCivilPlaintsAsync(cancellationToken);
                await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
                return Page();
            }

            var savedDraft = await draftResponse.Content.ReadFromJsonAsync<StageASavedDraftResponse>(
                cancellationToken: cancellationToken);
            if (savedDraft is null ||
                !savedDraft.DocumentType.Equals(DraftDocumentType, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(savedDraft.JsonContent))
            {
                ApiErrorMessage = "The selected saved draft is not a Stage A application or has no content to export.";
                await LoadCivilPlaintsAsync(cancellationToken);
                await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
                return Page();
            }

            using var exportResponse = await _apiClient.GetAsync(
            $"api/cases/{selectedCaseId}/drafts/{draftId}/pdf",
            cancellationToken);
            if (!exportResponse.IsSuccessStatusCode)
            {
                var responseBody = await exportResponse.Content.ReadAsStringAsync(cancellationToken);
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Could not create the application PDF (HTTP {(int)exportResponse.StatusCode}).");
                await LoadCivilPlaintsAsync(cancellationToken);
                await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
                return Page();
            }

            var pdfBytes = await exportResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            var applicationCode = string.Concat(
                application.Code.Select(character =>
                    char.IsLetterOrDigit(character) ? character : '_'));
            return File(
                pdfBytes,
                "application/pdf",
                $"CivilApplication_{applicationCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "Could not reach the API to export Stage A application draft {DraftId}.",
                draftId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to create the application PDF.";
            await LoadCivilPlaintsAsync(cancellationToken);
            await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
            return Page();
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid data for application draft {DraftId}.", draftId);
            ApiErrorMessage = "The saved application draft could not be read for PDF export.";
            await LoadCivilPlaintsAsync(cancellationToken);
            await LoadSelectedCaseAsync(selectedCaseId, cancellationToken);
            return Page();
        }
    }

    private async Task LoadCivilPlaintsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync(
                "api/cases?documentType=CivilPlaint",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Could not load existing civil plaints: HTTP {StatusCode}.",
                    (int)response.StatusCode);
                ApiErrorMessage = $"Could not load saved civil plaints (HTTP {(int)response.StatusCode}).";
                CivilPlaints = [];
                return;
            }

            CivilPlaints = await response.Content.ReadFromJsonAsync<List<ExistingCaseOption>>(
                cancellationToken: cancellationToken) ?? [];
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to load existing civil plaints.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to load saved civil plaints.";
            CivilPlaints = [];
        }
    }

    private async Task<bool> LoadSelectedCaseAsync(Guid? caseId, CancellationToken cancellationToken)
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
            using var response = await _apiClient.GetAsync(
                $"api/cases/{selectedCaseId}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load the selected case (HTTP {(int)response.StatusCode}).";
                RefreshRequirements();
                return false;
            }

            SelectedCase = await response.Content.ReadFromJsonAsync<ExistingCaseDetail>(
                cancellationToken: cancellationToken);
            if (SelectedCase is null)
            {
                ApiErrorMessage = "The selected case could not be read from the API response.";
                RefreshRequirements();
                return false;
            }

            if (!SelectedCase.CaseCategory.Equals("Civil", StringComparison.OrdinalIgnoreCase))
            {
                ApiErrorMessage = "The selected saved plaint is not a civil case.";
                SelectedCase = null;
                RefreshRequirements();
                return false;
            }

            var plaintDraft = SelectedCase.Drafts
                .Where(draft => draft.DocumentType.Equals(
                    "CivilPlaint",
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(draft => draft.CreatedAtUtc)
                .FirstOrDefault();
            if (plaintDraft is null)
            {
                ApiErrorMessage = "Select a case that has a saved civil plaint draft.";
                SelectedCase = null;
                RefreshRequirements();
                return false;
            }

            using var draftResponse = await _apiClient.GetAsync(
                $"api/cases/{selectedCaseId}/drafts/{plaintDraft.Id}",
                cancellationToken);
            if (!draftResponse.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load the saved civil plaint draft (HTTP {(int)draftResponse.StatusCode}).";
                SelectedCase = null;
                RefreshRequirements();
                return false;
            }

            var plaintDetail = await draftResponse.Content.ReadFromJsonAsync<StageASavedDraftResponse>(
                cancellationToken: cancellationToken);
            if (plaintDetail is null || string.IsNullOrWhiteSpace(plaintDetail.JsonContent))
            {
                ApiErrorMessage = "The saved civil plaint did not contain draft content.";
                SelectedCase = null;
                RefreshRequirements();
                return false;
            }

            ExistingPlaintDraftJson = plaintDetail.JsonContent;
            RefreshRequirements();
            return true;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not load selected civil plaint case {CaseId}.", selectedCaseId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to load the selected case.";
            RefreshRequirements();
            return false;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid saved civil plaint data for case {CaseId}.", selectedCaseId);
            ApiErrorMessage = "The API returned invalid saved civil plaint data.";
            SelectedCase = null;
            RefreshRequirements();
            return false;
        }
    }

    private void RefreshRequirements()
    {
        var application = SelectedApplication;
        if (application is null)
        {
            Requirements = [];
            Uploads = [];
            return;
        }

        Requirements = application.Documents.Select(document =>
        {
            var matchingDocuments = SelectedCase?.Documents
                .Where(existing =>
                    (existing.StageCode is null ||
                        (existing.StageCode.Equals("StageA", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(existing.ApplicationCode, application.Code, StringComparison.OrdinalIgnoreCase))) &&
                    (existing.DocumentCode.Equals(document.Code, StringComparison.OrdinalIgnoreCase) ||
                     document.ExistingDocumentCodes.Contains(
                         existing.DocumentCode,
                         StringComparer.OrdinalIgnoreCase)))
                .ToList() ?? [];
            return new StageARequirementViewModel
            {
                Code = document.Code,
                Label = document.Label,
                Guidance = document.Guidance,
                Category = document.Category,
                ExistingDocuments = matchingDocuments
            };
        }).ToList();

        Uploads = Requirements
            .Where(requirement =>
                !requirement.AlreadyOnFile &&
                !requirement.Code.Equals(
                    application.AffidavitDocumentCode,
                    StringComparison.OrdinalIgnoreCase))
            .Select(requirement => new StageAUploadInput
            {
                DocumentCode = requirement.Code,
                Label = requirement.Label
            })
            .ToList();
    }

    private string BuildApplicationInstructions(StageAApplication application, string additionalFacts)
    {
        var instructions = new StringBuilder();
        instructions.AppendLine("Stage A selection:");
        instructions.AppendLine($"Application: {application.Label}");
        instructions.AppendLine($"Provision: {application.Provision}");
        instructions.AppendLine($"When it may be needed: {application.WhenNeeded}");
        instructions.AppendLine();
        instructions.AppendLine("Applicant's additional facts for this application:");
        instructions.AppendLine(additionalFacts.Trim());
        instructions.AppendLine();
        instructions.AppendLine("Existing filed CivilPlaint draft (use this as the primary pleading context; do not alter it):");
        instructions.AppendLine(ExistingPlaintDraftJson ?? "No plaint content is available.");
        instructions.AppendLine();
        instructions.AppendLine("Attachment checklist status (uploaded documents are also included separately in the case record):");
        foreach (var requirement in Requirements)
        {
            var status = requirement.AlreadyOnFile
                ? $"Already on file: {string.Join(", ", requirement.ExistingDocuments.Select(document => document.OriginalFileName))}"
                : "Not supplied";
            instructions.AppendLine($"- {requirement.Label}: {status}. {requirement.Guidance}");
        }

        instructions.AppendLine();
        instructions.AppendLine("The saved CivilPlaint draft already belongs to this case. Do not repeat or request it as a new attachment. Use the case facts and uploaded documents, distinguish unsupplied material from evidence on file, and leave unknown details for advocate review. Do not describe a checklist item as mandatory unless the cited provision or court direction makes it so.");
        instructions.AppendLine($"The supporting affidavit ({application.AffidavitDocumentCode}) is attached to the case. Treat its text as a filed supporting document and do not draft the affidavit again inside the application.");
        return instructions.ToString();
    }

    private string BuildAffidavitInstructions(StageAApplication application, string additionalFacts)
    {
        var instructions = new StringBuilder();
        instructions.AppendLine("Prepare a separate supporting affidavit for this Stage A application.");
        instructions.AppendLine($"Selected application: {application.Label}");
        instructions.AppendLine($"Provision supplied for the application: {application.Provision}");
        instructions.AppendLine("Applicant's additional facts for this application:");
        instructions.AppendLine(additionalFacts.Trim());
        instructions.AppendLine();
        instructions.AppendLine("Existing filed CivilPlaint draft:");
        instructions.AppendLine(ExistingPlaintDraftJson ?? "No plaint content is available.");
        instructions.AppendLine();
        instructions.AppendLine("""
            Return the complete document in the affidavitText string with this structure:
            IN THE COURT OF [court name]
            [case number, if known]

            IN THE MATTER OF:
            [plaintiff/applicant] ... Plaintiff/Applicant
            VERSUS
            [defendant/respondent] ... Defendant/Respondent

            AFFIDAVIT
            I, [deponent name and parentage, age and address where known; otherwise clear bracketed placeholders], do hereby solemnly affirm and state on oath as under:
            1. Numbered paragraphs identifying the deponent's role, knowledge and competence to swear, based only on available case data.
            2. Numbered, concise factual paragraphs supporting this specific application, using only facts from the plaint, case documents and additional facts supplied. Do not copy unrelated sample facts.
            3. Where material information such as parentage, age, service, dates, amounts, property details or evidence is unknown, insert an explicit placeholder such as [Insert age] or omit the assertion; never invent it.
            Include an appropriate truth/knowledge statement and close with DEPONENT.

            VERIFICATION:
            State place and date as [Insert place] and [Insert date] when unknown. Use a verification limited to facts stated from personal knowledge and clearly distinguish information or belief where applicable. Close with DEPONENT.

            Do not include citation tokens, fabricated citations, unsupported claims, or statements that an event or evidence is true unless supported by the case record or supplied facts. Return affidavit text only in the affidavitText field, without markdown or commentary.
            """);
        return instructions.ToString();
    }

    private string BuildProposedNoticeInstructions(
        IReadOnlyList<ExistingCaseDocument> evidenceDocuments)
    {
        var instructions = new StringBuilder();
        instructions.AppendLine("Stage A selection: urgent interim relief under Order XXXIX Rules 1–2 CPC.");
        instructions.AppendLine("Existing filed CivilPlaint draft:");
        instructions.AppendLine(ExistingPlaintDraftJson ?? "No plaint content is available.");
        instructions.AppendLine();
        instructions.AppendLine("Extracted text from uploaded evidence of threatened or continuing harm:");
        const int maxEvidenceCharacters = 40000;
        var remainingCharacters = maxEvidenceCharacters;
        foreach (var document in evidenceDocuments)
        {
            if (remainingCharacters <= 0)
            {
                instructions.AppendLine("[Further evidence text omitted because the context limit was reached.]");
                break;
            }

            var extractedText = document.ExtractedText!.Trim();
            var includedText = extractedText.Length > remainingCharacters
                ? extractedText[..remainingCharacters]
                : extractedText;
            instructions.AppendLine($"Evidence document: {document.OriginalFileName}");
            instructions.AppendLine(includedText);
            instructions.AppendLine();
            remainingCharacters -= includedText.Length;
        }

        var attachedAffidavit = SelectedCase?.Documents
            .Where(document => IsForSelectedApplicationStage(document))
            .Where(document => document.DocumentCode.Equals(
                SelectedApplication?.AffidavitDocumentCode,
                StringComparison.OrdinalIgnoreCase))
            .Select(document => document.ExtractedText)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
        if (!string.IsNullOrWhiteSpace(attachedAffidavit))
        {
            instructions.AppendLine("Attached supporting affidavit text:");
            instructions.AppendLine(attachedAffidavit);
            instructions.AppendLine();
        }

        instructions.AppendLine("Use the evidence text to identify the specific alleged threatened or continuing harm and the supporting dated facts. Do not add facts that are absent from the plaint, affidavit, or extracted evidence. Identify the evidence by its actual filename. If an extraction is unclear or incomplete, do not infer the missing content.");
        return instructions.ToString();
    }

    private static bool IsInterimHarmEvidenceDocument(ExistingCaseDocument document) =>
        document.DocumentCode.Equals("INTERIM_HARM_EVIDENCE", StringComparison.OrdinalIgnoreCase) ||
        document.DocumentCode.Equals("THREAT_FACTS", StringComparison.OrdinalIgnoreCase) ||
        document.DocumentCode.Equals("PHOTOS_VIDEOS", StringComparison.OrdinalIgnoreCase);

    private bool IsForSelectedApplicationStage(ExistingCaseDocument document) =>
        document.StageCode is null ||
        (SelectedApplication is not null &&
         document.StageCode.Equals("StageA", StringComparison.OrdinalIgnoreCase) &&
         string.Equals(
             document.ApplicationCode,
             SelectedApplication.Code,
             StringComparison.OrdinalIgnoreCase));

    private static string GetApiErrorMessage(string responseBody, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(error.GetString()))
            {
                return error.GetString()!;
            }
        }
        catch (JsonException)
        {
            // The API body is not a structured error response; return the explicit fallback.
        }

        return fallback;
    }
}

public sealed class StageARequirementViewModel
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Guidance { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<ExistingCaseDocument> ExistingDocuments { get; set; } = [];
    public bool AlreadyOnFile => ExistingDocuments.Count > 0;
}

public sealed class StageAUploadInput
{
    public string DocumentCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
}

public sealed class StageASavedDraftResponse
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string StageCode { get; set; } = string.Empty;
    public string? ApplicationCode { get; set; }
    public string JsonContent { get; set; } = string.Empty;
}
