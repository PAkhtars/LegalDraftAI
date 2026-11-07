using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LegalDraftAI.Web.Pages;

[RequestSizeLimit(50 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
public sealed class CivilDefaultProcedureModel(
    IHttpClientFactory httpClientFactory,
    ILogger<CivilDefaultProcedureModel> logger) : PageModel
{
    private const string DraftDocumentType = "CivilStageCDefaultProcedureApplication";

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _apiClient = httpClientFactory.CreateClient("DraftsApi");

    public IReadOnlyList<StageCDefaultProcedure> Procedures => StageCDefaultProcedureCatalog.Procedures;
    public IReadOnlyList<ExistingCaseOption> CivilSuits { get; private set; } = [];
    public ExistingCaseDetail? SelectedCase { get; private set; }
    public string? ExistingPlaintDraftJson { get; private set; }
    public StageCDefaultProcedure? SelectedProcedure => StageCDefaultProcedureCatalog.Get(SelectedProcedureCode);
    public IReadOnlyList<StageCRequirementViewModel> Requirements { get; private set; } = [];
    public IReadOnlyList<ExistingCaseDocument> DocumentsForSelectedProcedure =>
        SelectedCase?.Documents
            .Where(IsForSelectedProcedure)
            .Where(document => !string.IsNullOrWhiteSpace(document.ExtractedText))
            .OrderBy(document => document.OriginalFileName, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    public IReadOnlyList<StageCUploadOption> Uploads { get; private set; } = [];
    public string? ApiErrorMessage { get; private set; }
    public string? StatusMessage { get; private set; }
    public string? GeneratedDraftJson { get; private set; }
    public Guid? GeneratedDraftId { get; private set; }

    [BindProperty]
    public Guid? SelectedCaseId { get; set; }

    [BindProperty]
    public string? SelectedProcedureCode { get; set; }

    [BindProperty]
    [StringLength(20000)]
    public string? ProcedureFacts { get; set; }

    [BindProperty]
    public List<Guid> ExcludedDocumentIds { get; set; } = [];

    [BindProperty]
    public List<StageCUploadInput> UploadInputs { get; set; } = [];

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
        await LoadCivilSuitsAsync(cancellationToken);
        if (SelectedCaseId is Guid caseId)
            await LoadSelectedCaseAsync(caseId, cancellationToken);
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
        if (SelectedProcedure is null)
        {
            ApiErrorMessage = "Select one of the Stage C applications.";
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
            ApiErrorMessage = "Map each file to an outstanding document in the selected Stage C checklist.";
            return Page();
        }

        var uploadedCount = 0;
        var failures = new List<string>();
        foreach (var upload in selectedUploads)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(upload.DocumentCode), "documentCode");
            form.Add(new StringContent("StageC"), "stageCode");
            form.Add(new StringContent(SelectedProcedure.Code), "applicationCode");
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
                logger.LogError(exception, "Could not upload Stage C document {DocumentCode}.", upload.DocumentCode);
                failures.Add($"{upload.File.FileName}: the LegalDraftAI API could not be reached.");
            }
        }

        if (uploadedCount > 0)
            StatusMessage = $"{uploadedCount} Stage C document(s) uploaded to the selected suit.";
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
            ApiErrorMessage = "The selected document is not attached as a supporting record for this Stage C procedure.";
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
            logger.LogError(exception, "Could not remove Stage C document {DocumentId}.", documentId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to remove the document.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadCivilSuitsAsync(cancellationToken);
        if (!await LoadSelectedCaseAsync(SelectedCaseId, cancellationToken))
            return Page();
        if (SelectedProcedure is null)
        {
            ApiErrorMessage = "Select one of the Stage C applications.";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(ProcedureFacts) || ProcedureFacts.Length > 20000)
        {
            ApiErrorMessage = "Enter the relevant procedural facts, dates, and explanation (20,000 characters or fewer).";
            return Page();
        }

        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                $"api/cases/{SelectedCaseId}/drafts",
                new
                {
                    documentType = DraftDocumentType,
                    stageCode = "StageC",
                    applicationCode = SelectedProcedure.Code,
                    extraInstructions = BuildDraftInstructions(SelectedProcedure, ProcedureFacts),
                    excludedDocumentIds = ExcludedDocumentIds,
                    savePdf = false
                },
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = GetApiErrorMessage(body, $"Stage C application generation failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            var draft = JsonSerializer.Deserialize<StageASavedDraftResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (draft is null ||
                !draft.DocumentType.Equals(DraftDocumentType, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(draft.JsonContent))
            {
                logger.LogError("Stage C API response did not contain saved draft content for case {CaseId}.", SelectedCaseId);
                ApiErrorMessage = "The API response did not contain the saved Stage C application.";
                return Page();
            }

            using var draftJson = JsonDocument.Parse(draft.JsonContent);
            GeneratedDraftJson = JsonSerializer.Serialize(draftJson.RootElement, PrettyJsonOptions);
            GeneratedDraftId = draft.Id;
            StatusMessage = "Stage C application generated and saved to the selected civil suit.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to generate the Stage C application.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to generate the Stage C application.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid Stage C application data.");
            ApiErrorMessage = "The API returned invalid Stage C application data.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDownloadApplicationPdfAsync(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        if (SelectedCaseId is not Guid caseId || SelectedProcedure is null)
            return BadRequest("Select the existing civil suit and Stage C procedure.");

        try
        {
            using var draftResponse = await _apiClient.GetAsync(
                $"api/cases/{caseId}/drafts/{draftId}",
                cancellationToken);
            if (!draftResponse.IsSuccessStatusCode)
            {
                ApiErrorMessage = $"Could not load the saved Stage C application (HTTP {(int)draftResponse.StatusCode}).";
                await ReloadPageAsync(caseId, cancellationToken);
                return Page();
            }

            var savedDraft = await draftResponse.Content.ReadFromJsonAsync<StageASavedDraftResponse>(
                cancellationToken: cancellationToken);
            if (savedDraft is null ||
                !savedDraft.DocumentType.Equals(DraftDocumentType, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(savedDraft.JsonContent))
            {
                ApiErrorMessage = "The selected draft is not a Stage C procedural application or has no content.";
                await ReloadPageAsync(caseId, cancellationToken);
                return Page();
            }

            using var exportResponse = await _apiClient.GetAsync(
                $"api/cases/{caseId}/drafts/{draftId}/pdf",
                cancellationToken);
            if (!exportResponse.IsSuccessStatusCode)
            {
                var body = await exportResponse.Content.ReadAsStringAsync(cancellationToken);
                ApiErrorMessage = GetApiErrorMessage(body, $"Could not create the PDF (HTTP {(int)exportResponse.StatusCode}).");
                await ReloadPageAsync(caseId, cancellationToken);
                return Page();
            }

            var pdf = await exportResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            var safeCode = string.Concat(SelectedProcedure.Code.Select(character =>
                char.IsLetterOrDigit(character) ? character : '_'));
            return File(pdf, "application/pdf", $"StageC_{safeCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not export Stage C draft {DraftId}.", draftId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to create the Stage C application PDF.";
            await ReloadPageAsync(caseId, cancellationToken);
            return Page();
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid Stage C draft data for {DraftId}.", draftId);
            ApiErrorMessage = "The saved Stage C application could not be read for PDF export.";
            await ReloadPageAsync(caseId, cancellationToken);
            return Page();
        }
    }

    private async Task ReloadPageAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await LoadCivilSuitsAsync(cancellationToken);
        await LoadSelectedCaseAsync(caseId, cancellationToken);
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
            logger.LogError(exception, "Could not load saved civil suits for Stage C.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to load saved civil suits.";
            CivilSuits = [];
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
            RefreshRequirements();
            return true;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not load saved civil suit {CaseId} for Stage C.", selectedCaseId);
            ApiErrorMessage = "Could not reach the LegalDraftAI API to load the selected civil suit.";
            RefreshRequirements();
            return false;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The API returned invalid civil-suit data for Stage C case {CaseId}.", selectedCaseId);
            ApiErrorMessage = "The API returned invalid civil-suit data.";
            SelectedCase = null;
            RefreshRequirements();
            return false;
        }
    }

    private void RefreshRequirements()
    {
        var procedure = SelectedProcedure;
        if (procedure is null)
        {
            Requirements = [];
            Uploads = [];
            return;
        }

        Requirements = procedure.Documents.Select(item =>
        {
            var matchingDocuments = SelectedCase?.Documents
                .Where(IsForSelectedProcedure)
                .Where(document =>
                    document.DocumentCode.Equals(item.Code, StringComparison.OrdinalIgnoreCase) ||
                    item.ExistingDocumentCodes.Contains(document.DocumentCode, StringComparer.OrdinalIgnoreCase))
                .ToList() ?? [];
            return new StageCRequirementViewModel
            {
                Code = item.Code,
                Label = item.Label,
                Guidance = item.Guidance,
                ExistingDocuments = matchingDocuments
            };
        }).ToList();

        Uploads = Requirements
            .Where(requirement => !requirement.AlreadyOnFile)
            .Select(requirement => new StageCUploadOption(requirement.Code, requirement.Label))
            .ToList();
    }

    private bool IsForSelectedProcedure(ExistingCaseDocument document) =>
        document.StageCode is null ||
        (SelectedProcedure is not null &&
         document.StageCode.Equals("StageC", StringComparison.OrdinalIgnoreCase) &&
         string.Equals(document.ApplicationCode, SelectedProcedure.Code, StringComparison.OrdinalIgnoreCase));

    private string BuildDraftInstructions(StageCDefaultProcedure procedure, string procedureFacts)
    {
        var instructions = new StringBuilder();
        instructions.AppendLine("Stage C selection:");
        instructions.AppendLine($"Application or step: {procedure.Label}");
        instructions.AppendLine($"Provision supplied: {procedure.Provision}");
        instructions.AppendLine($"Scenario: {procedure.Scenario}");
        instructions.AppendLine();
        instructions.AppendLine("Applicant's procedural facts:");
        instructions.AppendLine(procedureFacts.Trim());
        instructions.AppendLine();
        instructions.AppendLine("Saved plaint (case background only):");
        instructions.AppendLine(ExistingPlaintDraftJson ?? "No saved plaint content is available.");
        instructions.AppendLine();
        instructions.AppendLine("Stage C document checklist:");
        foreach (var requirement in Requirements)
        {
            var status = requirement.AlreadyOnFile
                ? $"On file: {string.Join(", ", requirement.ExistingDocuments.Select(document => document.OriginalFileName))}"
                : "Not supplied.";
            instructions.AppendLine($"- {requirement.Label}: {status} {requirement.Guidance}");
        }

        instructions.AppendLine();
        instructions.AppendLine("Use only supplied facts and case records. Do not invent service, attendance, dates, knowledge of an order, reasons for absence, or court findings. Clearly mark unknown particulars for advocate completion. A request to proceed ex parte does not make an ex parte decree automatic; due service and the court's satisfaction about the applicable procedure are required. Verify limitation, maintainability, local amendments, and the exact procedural posture before filing.");
        return instructions.ToString();
    }

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

public sealed class StageCRequirementViewModel
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Guidance { get; set; } = string.Empty;
    public List<ExistingCaseDocument> ExistingDocuments { get; set; } = [];
    public bool AlreadyOnFile => ExistingDocuments.Count > 0;
}

public sealed record StageCUploadOption(string Code, string Label);

public sealed class StageCUploadInput
{
    public string DocumentCode { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
}
