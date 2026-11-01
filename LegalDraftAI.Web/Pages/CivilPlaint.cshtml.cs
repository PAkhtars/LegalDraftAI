using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LegalDraftAI.Web.Pages;

[RequestSizeLimit(50 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
public sealed class CivilPlaintModel(
    IHttpClientFactory httpClientFactory,
    ILogger<CivilPlaintModel> logger) : PageModel
{
    private const string UploadedDocumentsSessionKey = "CivilPlaint.UploadedDocuments";
    private const string UploadedNatureSessionKey = "CivilPlaint.UploadedNature";
    private const string CaseIdSessionKey = "CivilPlaint.CaseId";

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _apiClient = httpClientFactory.CreateClient("DraftsApi");

    public IReadOnlyList<PlaintNatureOption> PlaintNatures { get; private set; } = [];
    public IReadOnlyList<ExistingCaseOption> ExistingCases { get; private set; } = [];
    public ExistingCaseDetail? SelectedCase { get; private set; }
    public DocumentResearchResultViewModel? ResearchResult { get; private set; }
    public IReadOnlyList<UploadedDocumentRecord> UploadedDocuments { get; private set; } = [];
    public string? ApiStatusMessage { get; private set; }
    public string? ApiErrorMessage { get; private set; }
    public string? UploadStatusMessage { get; private set; }
    public string? GeneratedDraftPreviewText { get; private set; }
    public IReadOnlyList<DraftFieldOption> GeneratedDraftFields { get; private set; } = [];
    public IReadOnlyList<DraftFormatMasterOption> CivilDraftFormatMasters { get; private set; } = [];
    public string? DraftFormatMasterLoadError { get; private set; }

    [BindProperty]
    public CivilPlaintInput Input { get; set; } = new();

    [BindProperty]
    public Guid? SelectedCaseId { get; set; }

    [BindProperty]
    public List<Guid> ExcludedDocumentIds { get; set; } = [];

    [BindProperty]
    [StringLength(100000)]
    public string? EditedDraftText { get; set; }

    [BindProperty]
    public List<string> ExcludedDraftFields { get; set; } = [];

    [BindProperty]
    public Guid? SelectedDraftFormatMasterId { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPlaintNaturesAsync(cancellationToken);
        await LoadExistingCasesAsync(cancellationToken);
        var caseId = await GetCurrentCaseIdAsync();
        if (caseId is Guid selectedCaseId &&
            await LoadSelectedCaseAsync(selectedCaseId, populateInput: true, cancellationToken))
        {
            SelectedCaseId = selectedCaseId;
        }
        else
        {
            await LoadUploadedDocumentsAsync();
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Input.GeneratedDraftJson = null;
        await LoadPlaintNaturesAsync(cancellationToken);
        await LoadExistingCasesAsync(cancellationToken);
        var selectedCaseId = await GetCurrentCaseIdAsync();
        if (selectedCaseId is Guid currentCaseId)
            await LoadSelectedCaseAsync(currentCaseId, populateInput: false, cancellationToken);
        else
            await LoadUploadedDocumentsAsync();

        if (!await RunResearchAsync(cancellationToken))
        {
            return Page();
        }

        if (ResearchResult?.CanFileWithMinimumSet != true)
        {
            ApiErrorMessage = "Complete all Critical documents in the checklist before generating the plaint.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.Facts))
        {
            ModelState.AddModelError("Input.Facts", "Enter the material facts before generating the plaint.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new
        {
            documentType = "CivilPlaint",
            jurisdiction = Input.Jurisdiction,
            extractedClientFacts = BuildClientFacts(ExcludedDocumentIds.ToHashSet()),
            caseId = await GetCurrentCaseIdAsync(),
            civilPlaintDetails = Input.SaveDetailsToDatabase
                ? BuildSavedPlaintDetails(Input)
                : null
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
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Draft generation failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                if (!document.RootElement.TryGetProperty("data", out var draft))
                {
                    logger.LogError("Civil plaint API response did not contain draft data.");
                    ApiErrorMessage = "The API response did not contain the generated draft.";
                    return Page();
                }

                Input.GeneratedDraftJson = JsonSerializer.Serialize(draft, PrettyJsonOptions);
                GeneratedDraftPreviewText = BuildCivilPlaintPreview(draft);
                GeneratedDraftFields = GetDraftFieldOptions(draft);
                await LoadCivilDraftFormatMastersAsync(cancellationToken);
                ModelState.Remove("Input.GeneratedDraftJson");
                if (document.RootElement.TryGetProperty("caseId", out var caseIdElement) &&
                    caseIdElement.TryGetGuid(out var generatedCaseId))
                {
                    await SaveUploadedDocumentsAsync(generatedCaseId);
                    SelectedCaseId = generatedCaseId;
                    await LoadSelectedCaseAsync(generatedCaseId, populateInput: false, cancellationToken);
                }
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Civil plaint API returned an invalid JSON response.");
                ApiErrorMessage = "The API returned an invalid JSON response.";
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API for civil plaint generation.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API. Check that it is running and that DraftsApi:BaseUrl is correct.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostResearchAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadPlaintNaturesAsync(cancellationToken);
        await LoadExistingCasesAsync(cancellationToken);
        var selectedCaseId = await GetCurrentCaseIdAsync();
        if (selectedCaseId is Guid currentCaseId)
            await LoadSelectedCaseAsync(currentCaseId, populateInput: false, cancellationToken);
        else
            await LoadUploadedDocumentsAsync();
        await RunResearchAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSelectCaseAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadPlaintNaturesAsync(cancellationToken);
        await LoadExistingCasesAsync(cancellationToken);

        if (SelectedCaseId is not Guid caseId)
        {
            await HttpContext.Session.LoadAsync(cancellationToken);
            HttpContext.Session.Remove(CaseIdSessionKey);
            HttpContext.Session.Remove(UploadedDocumentsSessionKey);
            HttpContext.Session.Remove(UploadedNatureSessionKey);
            await HttpContext.Session.CommitAsync(cancellationToken);
            Input = new CivilPlaintInput();
            UploadedDocuments = [];
            SelectedCase = null;
            ResearchResult = null;
            SelectedCaseId = null;
            return Page();
        }

        if (!await LoadSelectedCaseAsync(caseId, populateInput: true, cancellationToken))
            return Page();

        await SaveUploadedDocumentsAsync(caseId);
        await RunResearchAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostTranslateToHindiAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await LoadPlaintNaturesAsync(cancellationToken);
        await LoadExistingCasesAsync(cancellationToken);

        var caseId = SelectedCaseId ?? await GetCurrentCaseIdAsync();
        if (caseId is not Guid selectedCaseId ||
            !await LoadSelectedCaseAsync(selectedCaseId, populateInput: false, cancellationToken))
        {
            ApiErrorMessage ??= "Select a saved case before translating document text.";
            return Page();
        }

        SelectedCaseId = selectedCaseId;
        var document = SelectedCase?.Documents.FirstOrDefault(item => item.Id == documentId);
        if (document is null)
        {
            ApiErrorMessage = "The selected supporting document was not found in this case.";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            ApiErrorMessage = "This document has no extracted text to translate.";
            return Page();
        }

        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                "api/drafts/translate-to-hindi",
                new HindiTranslationRequestViewModel { Text = document.ExtractedText },
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Hindi translation failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            using var result = JsonDocument.Parse(responseBody);
            if (!result.RootElement.TryGetProperty("translatedText", out var translatedText) ||
                translatedText.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(translatedText.GetString()))
            {
                logger.LogError("Hindi translation API response did not contain translated text.");
                ApiErrorMessage = "The API returned an empty Hindi translation.";
                return Page();
            }

            document.TranslatedText = translatedText.GetString();
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API for Hindi translation.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to translate the document.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Hindi translation API returned invalid JSON.");
            ApiErrorMessage = "The API returned invalid Hindi translation data.";
        }

        return Page();
    }

    public async Task<IActionResult> OnGetDocumentAsync(
        Guid caseId,
        Guid documentId,
        bool download,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync(
                $"api/cases/{caseId}/documents/{documentId}/content?download={download}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Could not retrieve supporting document {DocumentId} for case {CaseId}: HTTP {StatusCode}.",
                    documentId,
                    caseId,
                    (int)response.StatusCode);
                return StatusCode((int)response.StatusCode);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType
                ?? "application/octet-stream";
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (download)
            {
                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName
                    ?? "supporting-document";
                return File(bytes, contentType, fileName.Trim('"'));
            }

            Response.Headers.ContentDisposition = "inline";
            return File(bytes, contentType);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not retrieve a saved supporting document.");
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    public async Task<IActionResult> OnPostUploadAsync(
        List<IFormFile> files,
        List<string> documentCodes,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await LoadPlaintNaturesAsync(cancellationToken);
        await LoadExistingCasesAsync(cancellationToken);
        var selectedCaseId = await GetCurrentCaseIdAsync();
        if (selectedCaseId is Guid currentCaseId)
            await LoadSelectedCaseAsync(currentCaseId, populateInput: false, cancellationToken);
        else
            await LoadUploadedDocumentsAsync();

        if (ResearchResult is null && !await RunResearchAsync(cancellationToken))
        {
            return Page();
        }

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

        var availableCodes = ResearchResult!.RequiredDocuments
            .Select(document => document.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (documentCodes.Any(code => !availableCodes.Contains(code)))
        {
            ApiErrorMessage = "Map each file to a document in the current plaint checklist.";
            return Page();
        }

        using var form = new MultipartFormDataContent();
        AddFormField(form, "caseId", (await GetCurrentCaseIdAsync())?.ToString());
        AddFormField(form, "caseTitle", Input.CaseTitle);
        AddFormField(form, "plaintNature", Input.PlaintNature);
        AddFormField(form, "jurisdiction", Input.Jurisdiction);
        AddFormField(form, "courtName", Input.CourtName);
        AddFormField(form, "caseNumber", Input.CaseNumber);
        AddFormField(form, "plaintiffName", Input.PlaintiffName);
        AddFormField(form, "plaintiffAddress", Input.PlaintiffAddress);
        AddFormField(form, "defendantName", Input.DefendantName);
        AddFormField(form, "defendantAddress", Input.DefendantAddress);
        AddFormField(form, "clientFacts", BuildClientFacts());
        for (var index = 0; index < files.Count; index++)
        {
            var fileContent = new StreamContent(files[index].OpenReadStream());
            form.Add(fileContent, "files", Path.GetFileName(files[index].FileName));
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
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Document extraction failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            var extraction = JsonSerializer.Deserialize<MappedDocumentExtractionResult>(
                responseBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (extraction?.Documents is null || extraction.Documents.Count != files.Count)
            {
                logger.LogError("Mapped document extraction returned an invalid response.");
                ApiErrorMessage = "The API returned an invalid document extraction response.";
                return Page();
            }

            if (extraction.CaseId is null)
            {
                logger.LogError("Mapped document extraction response did not contain a case ID.");
                ApiErrorMessage = "The uploaded documents were not linked to a saved case.";
                return Page();
            }

            UploadedDocuments = UploadedDocuments
                .Concat(extraction.Documents)
                .ToList();
            await SaveUploadedDocumentsAsync(extraction.CaseId);
            SelectedCaseId = extraction.CaseId;
            await LoadExistingCasesAsync(cancellationToken);
            await LoadSelectedCaseAsync(extraction.CaseId.Value, populateInput: false, cancellationToken);
            var extractedCount = extraction.Documents.Count(document =>
                !string.IsNullOrWhiteSpace(document.Text));
            UploadStatusMessage =
                $"Saved {extraction.Documents.Count} supporting document(s) to the case and extracted text from {extractedCount}. Re-run research to update the checklist.";
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API for document extraction.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to extract documents. Check that it is running.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Mapped document extraction returned invalid JSON.");
            ApiErrorMessage = "The API returned invalid document extraction data.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        await LoadPlaintNaturesAsync(cancellationToken);
        await LoadExistingCasesAsync(cancellationToken);
        var selectedCaseId = await GetCurrentCaseIdAsync();
        if (selectedCaseId is Guid currentCaseId)
            await LoadSelectedCaseAsync(currentCaseId, populateInput: false, cancellationToken);
        else
            await LoadUploadedDocumentsAsync();

        if (string.IsNullOrWhiteSpace(Input.GeneratedDraftJson))
        {
            ApiErrorMessage = "Generate a draft before downloading the PDF.";
            return Page();
        }

        try
        {
            using var document = JsonDocument.Parse(Input.GeneratedDraftJson);
            using var response = await _apiClient.PostAsJsonAsync(
                "api/drafts/export-pdf",
                new
                {
                    documentType = "CivilPlaint",
                    json = Input.GeneratedDraftJson
                },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"PDF export failed (HTTP {(int)response.StatusCode}).");
                return Page();
            }

            var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return File(pdfBytes, "application/pdf", "CivilPlaint.pdf");
        }
        catch (JsonException)
        {
            ApiErrorMessage = "The generated draft is not valid JSON. Generate it again before downloading.";
            return Page();
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API for civil plaint PDF export.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to export the PDF. Check that it is running.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDownloadDocxAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(EditedDraftText))
            return BadRequest("The editable plaint preview is empty.");
        if (EditedDraftText.Length > 100000)
            return BadRequest("The editable plaint preview must be 100,000 characters or fewer.");

        string docxText;
        try
        {
            docxText = RemoveExcludedDraftFields(
                EditedDraftText,
                Input.GeneratedDraftJson,
                ExcludedDraftFields);
        }
        catch (JsonException)
        {
            return BadRequest("The generated draft JSON is invalid. Generate the plaint again before downloading.");
        }

        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                "api/drafts/export-civil-plaint-docx",
                new
                {
                    text = docxText,
                    draftFormatMasterId = SelectedDraftFormatMasterId,
                    templateValues = BuildTemplateValues(
                        Input.GeneratedDraftJson,
                        ExcludedDraftFields,
                        UploadedDocuments)
                },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return BadRequest(GetApiErrorMessage(
                    responseBody,
                    $"DOCX export failed (HTTP {(int)response.StatusCode})."));
            }

            var docxBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return File(
                docxBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "CivilPlaint.docx");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API for civil plaint DOCX export.");
            return StatusCode(StatusCodes.Status502BadGateway, "Could not reach the API to export the DOCX.");
        }
    }

    private async Task LoadCivilDraftFormatMastersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync(
                "api/draft-format-masters?category=Civil",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                DraftFormatMasterLoadError = GetApiErrorMessage(
                    responseBody,
                    $"Could not load Civil DOCX formats (HTTP {(int)response.StatusCode}).");
                return;
            }

            CivilDraftFormatMasters = await response.Content.ReadFromJsonAsync<List<DraftFormatMasterOption>>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken) ?? [];
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not load Civil draft format masters.");
            DraftFormatMasterLoadError = "Could not reach the API to load saved Civil DOCX formats.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Civil draft format master API returned invalid JSON.");
            DraftFormatMasterLoadError = "The API returned an invalid list of saved Civil DOCX formats.";
        }
    }

    private static Dictionary<string, string> BuildTemplateValues(
        string? draftJson,
        IReadOnlyCollection<string> excludedFields,
        IReadOnlyList<UploadedDocumentRecord> uploadedDocuments)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(draftJson))
            return values;

        using var document = JsonDocument.Parse(draftJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return values;

        var excluded = excludedFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            values[property.Name] = excluded.Contains(property.Name)
                ? string.Empty
                : string.Join("; ", GetDraftStringValues(property.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        for (var index = 0; index < 4; index++)
        {
            var placeholderName = $"document{index + 1}";
            if (excluded.Contains(placeholderName))
            {
                values[placeholderName] = string.Empty;
                continue;
            }

            values[placeholderName] = index < uploadedDocuments.Count
                ? uploadedDocuments[index].FileName
                : string.Empty;
        }

        return values;
    }

    private static IReadOnlyList<DraftFieldOption> GetDraftFieldOptions(JsonElement draft)
    {
        if (draft.ValueKind != JsonValueKind.Object)
            return [];

        return draft.EnumerateObject()
            .Select(property => new DraftFieldOption(
                property.Name,
                System.Text.RegularExpressions.Regex.Replace(
                    property.Name,
                    "([a-z0-9])([A-Z])",
                    "$1 $2")))
            .ToList();
    }

        private static string RemoveExcludedDraftFields(
            string text,
            string? draftJson,
            IReadOnlyCollection<string> excludedFields)
        {
            if (excludedFields.Count == 0 || string.IsNullOrWhiteSpace(draftJson))
            {
                return text;
            }

            using var document = JsonDocument.Parse(draftJson);
            return RemoveExcludedDraftFields(text, document.RootElement, excludedFields);
        }

        private static string RemoveExcludedDraftFields(
            string text,
            JsonElement draft,
            IReadOnlyCollection<string> excludedFields)
        {
            if (draft.ValueKind != JsonValueKind.Object)
                return text;

            var excluded = excludedFields
                .Where(field => draft.TryGetProperty(field, out _))
                .ToHashSet(StringComparer.Ordinal);
            if (excluded.Count == 0)
                return text;

            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .ToList();

            RemoveSectionForExcludedField(lines, excluded, "facts", "2. FACTS OF THE CASE", "LEGAL GROUNDS");
            RemoveSectionForExcludedField(lines, excluded, "grounds", "LEGAL GROUNDS", "SCHEDULE OF SUIT PROPERTY", "3. CAUSE OF ACTION");
            RemoveSectionForExcludedField(lines, excluded, "limitationPleading", "5. LIMITATION", "6. COURT FEE");
            RemoveSectionForExcludedField(lines, excluded, "territorialJurisdiction", "4. JURISDICTION", "5. LIMITATION");
            RemoveSectionForExcludedField(lines, excluded, "courtFeePleading", "6. COURT FEE", "7. RELIEFS SOUGHT");
            RemoveSectionForExcludedField(lines, excluded, "reliefsSought", "7. RELIEFS SOUGHT", "PRAYER");
            RemoveSectionForExcludedField(lines, excluded, "prayer", "PRAYER", "And for this act of kindness, the Plaintiff as in duty bound shall ever pray.");

            if (excluded.Contains("causeOfAction") && excluded.Contains("causeOfActionDate"))
            {
                RemoveSection(lines, "3. CAUSE OF ACTION", "4. JURISDICTION");
            }

            var fieldValues = excluded
                .SelectMany(field => GetDraftStringValues(draft.GetProperty(field)))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var index = lines.Count - 1; index >= 0; index--)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var segments = line.Split(" | ", StringSplitOptions.None);
                if (segments.Length > 1)
                {
                    var filteredSegments = segments
                        .Where(segment => !fieldValues.Any(value =>
                            segment.Contains(value, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();
                    if (filteredSegments.Length != segments.Length)
                    {
                        if (filteredSegments.Length == 0)
                            lines.RemoveAt(index);
                        else
                            lines[index] = string.Join(" | ", filteredSegments);
                        continue;
                    }
                }

                var matchingValues = fieldValues
                    .Where(value => line.Contains(value, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matchingValues.Count == 0)
                    continue;

                if (matchingValues.Any(value => IsValueOnlyLine(line, value)))
                {
                    lines.RemoveAt(index);
                    continue;
                }

                foreach (var value in matchingValues)
                    line = line.Replace(value, string.Empty, StringComparison.OrdinalIgnoreCase);
                lines[index] = line.TrimEnd();
            }

            if (excluded.Contains("causeOfAction"))
            {
                lines.RemoveAll(line => line.Contains(
                    "The cause of action arose as stated in the facts above.",
                    StringComparison.OrdinalIgnoreCase));
            }
            if (excluded.Contains("caseNumber") &&
                draft.TryGetProperty("caseNumber", out var caseNumber) &&
                (caseNumber.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                 caseNumber.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(caseNumber.GetString())))
            {
                lines.RemoveAll(line => line.StartsWith("Civil Suit No.", StringComparison.OrdinalIgnoreCase));
            }
            if (excluded.Contains("suitTitle") &&
                excluded.Contains("plaintNature") &&
                string.IsNullOrWhiteSpace(GetDraftString(draft, "suitTitle")) &&
                string.IsNullOrWhiteSpace(GetDraftString(draft, "plaintNature")))
            {
                lines.RemoveAll(line => line.Equals("SUIT FOR ____________________", StringComparison.OrdinalIgnoreCase));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static bool IsValueOnlyLine(string line, string value)
        {
            var trimmedLine = line.Trim();
            if (string.Equals(trimmedLine, value, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!trimmedLine.EndsWith(value, StringComparison.OrdinalIgnoreCase))
                return false;

            var prefix = trimmedLine[..^value.Length].TrimEnd();
            return prefix.Equals("IN THE COURT OF", StringComparison.OrdinalIgnoreCase) ||
                   prefix.Equals("R/o", StringComparison.OrdinalIgnoreCase) ||
                   prefix.Equals("Civil Suit No.", StringComparison.OrdinalIgnoreCase) ||
                   prefix.Equals("Place:", StringComparison.OrdinalIgnoreCase) ||
                   prefix.Equals("Date:", StringComparison.OrdinalIgnoreCase);
        }

        private static void RemoveSectionForExcludedField(
            List<string> lines,
            HashSet<string> excludedFields,
            string field,
            params string[] headings)
        {
            if (excludedFields.Contains(field))
                RemoveSection(lines, headings[0], headings[1..]);
        }

        private static void RemoveSection(List<string> lines, string heading, params string[] nextHeadings)
        {
            var startIndex = lines.FindIndex(line =>
                string.Equals(line.Trim(), heading, StringComparison.OrdinalIgnoreCase));
            if (startIndex < 0)
                return;

            var endIndexes = nextHeadings
                .Select(nextHeading => lines.FindIndex(startIndex + 1, line =>
                    string.Equals(line.Trim(), nextHeading, StringComparison.OrdinalIgnoreCase)))
                .Where(index => index >= 0)
                .ToList();
            var endIndex = endIndexes.Count > 0 ? endIndexes.Min() : lines.Count;

            lines.RemoveRange(startIndex, endIndex - startIndex);
        }

        private static IEnumerable<string> GetDraftStringValues(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    if (!string.IsNullOrWhiteSpace(element.GetString()))
                        yield return element.GetString()!;
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        foreach (var value in GetDraftStringValues(item))
                            yield return value;
                    }
                    break;
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        foreach (var value in GetDraftStringValues(property.Value))
                            yield return value;
                    }
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    yield return element.ToString();
                    break;
            }
        }

        public sealed record DraftFieldOption(string Name, string Label);
        public sealed record DraftFormatMasterOption(
            Guid Id,
            string Category,
            string FormatName,
            string? Description,
            string OriginalFileName,
            string DraftFormatPath,
            string ContentType,
            long FileSizeBytes);

        private static string BuildCivilPlaintPreview(JsonElement draft)
        {
            var text = new StringBuilder();
            var courtName = GetDraftString(draft, "courtName");
            var caseTitle = GetDraftString(draft, "caseTitle");
            var caseNumber = GetDraftString(draft, "caseNumber");
            var plaintiffName = GetDraftString(draft, "plaintiffName");
            var plaintiffAddress = GetDraftString(draft, "plaintiffAddress");
            var defendantName = GetDraftString(draft, "defendantName");
            var defendantAddress = GetDraftString(draft, "defendantAddress");

            AppendPreviewLine(text, $"IN THE COURT OF {courtName}");
            AppendPreviewLine(text, string.IsNullOrWhiteSpace(caseNumber)
                ? "Civil Suit No. _______ of 20__"
                : $"Civil Suit No. {caseNumber}");
            AppendPreviewLine(text, string.Empty);
            AppendPreviewLine(text, "IN THE MATTER OF:");
            AppendPreviewLine(text, plaintiffName);
            AppendPreviewLine(text, string.IsNullOrWhiteSpace(plaintiffAddress) ? null : $"R/o {plaintiffAddress}");
            AppendPreviewLine(text, "… Plaintiff");
            AppendPreviewLine(text, "VERSUS");
            AppendPreviewLine(text, defendantName);
            AppendPreviewLine(text, defendantAddress);
            AppendPreviewLine(text, "… Defendant");
            AppendPreviewLine(text, string.Empty);
            AppendPreviewLine(text, GetDraftString(draft, "suitTitle") ??
                GetDraftString(draft, "plaintNature") ?? "SUIT FOR ____________________");
            AppendPreviewLine(text, string.IsNullOrWhiteSpace(caseTitle) ? null : caseTitle);
            AppendPreviewLine(text, string.Empty);
            AppendPreviewLine(text, "The Plaintiff above named most respectfully showeth:");
            AppendPreviewLine(text, string.Empty);
            AppendPreviewLine(text, "1. PARTIES");
            AppendPreviewLine(text, "1.1 The Plaintiff is as described above.");
            AppendPreviewLine(text, "1.2 The Defendant is as described above.");
            AppendPreviewList(text, "2. FACTS OF THE CASE", GetDraftStringList(draft, "facts"), "2.");
            AppendPreviewList(text, "LEGAL GROUNDS", GetDraftStringList(draft, "grounds"), null);

            var propertyDescription = GetDraftString(draft, "suitPropertyDescription");
            var surveyNumber = GetDraftString(draft, "surveyNumber");
            var area = GetDraftString(draft, "area");
            var village = GetDraftString(draft, "village");
            var tehsil = GetDraftString(draft, "tehsil");
            var district = GetDraftString(draft, "district");
            if (new[] { propertyDescription, surveyNumber, area, village, tehsil, district }
                .Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                AppendPreviewLine(text, "SCHEDULE OF SUIT PROPERTY");
                AppendPreviewLine(text, propertyDescription);
                AppendPreviewLine(text, JoinPreviewDetails(
                    ("Survey / Plot No.", surveyNumber),
                    ("Area", area),
                    ("Village", village),
                    ("Tehsil", tehsil),
                    ("District", district)));
            }

            AppendPreviewLine(text, "3. CAUSE OF ACTION");
            AppendPreviewLine(text, GetDraftString(draft, "causeOfAction") ??
                "The cause of action arose as stated in the facts above.");
            AppendPreviewLine(text, GetDraftString(draft, "causeOfActionDate"));
            AppendPreviewLine(text, "4. JURISDICTION");
            AppendPreviewLine(text, GetDraftString(draft, "territorialJurisdiction"));
            AppendPreviewLine(text, "5. LIMITATION");
            AppendPreviewLine(text, GetDraftString(draft, "limitationPleading"));
            AppendPreviewLine(text, "6. COURT FEE");
            AppendPreviewLine(text, GetDraftString(draft, "courtFeePleading"));
            AppendPreviewList(text, "7. RELIEFS SOUGHT", GetDraftStringList(draft, "reliefsSought"), null);
            AppendPreviewLine(text, "PRAYER");
            AppendPreviewLine(text, GetDraftString(draft, "prayer"));
            AppendPreviewLine(text, string.Empty);
            AppendPreviewLine(text, "And for this act of kindness, the Plaintiff as in duty bound shall ever pray.");
            AppendPreviewLine(text, string.Empty);
            AppendPreviewLine(text, $"Place: {GetDraftString(draft, "place")}");
            AppendPreviewLine(text, $"Date: {GetDraftString(draft, "date")}");
            AppendPreviewLine(text, string.Empty);
            AppendPreviewLine(text, "Plaintiff");
            AppendPreviewLine(text, "Through Advocate");
            AppendPreviewLine(text, GetDraftString(draft, "advocateName"));
            AppendPreviewLine(text, GetDraftString(draft, "advocateEnrollmentNo"));
            AppendPreviewLine(text, string.Empty);
            AppendPreviewLine(text, "VERIFICATION");
            AppendPreviewLine(text, "Verified at the place and on the date stated above.");
            AppendPreviewLine(text, plaintiffName);
            return text.ToString().TrimEnd();
        }

        private static string? GetDraftString(JsonElement draft, string propertyName)
        {
            if (draft.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in draft.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
            }

            return null;
        }

        private static IReadOnlyList<string> GetDraftStringList(JsonElement draft, string propertyName)
        {
            if (draft.ValueKind != JsonValueKind.Object)
                return [];

            foreach (var property in draft.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    return property.Value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Cast<string>()
                        .ToList();
                }
            }

            return [];
        }

        private static void AppendPreviewList(
            StringBuilder text,
            string heading,
            IReadOnlyList<string> items,
            string? numberPrefix)
        {
            if (items.Count == 0)
                return;
            AppendPreviewLine(text, heading);
            for (var index = 0; index < items.Count; index++)
            {
                var prefix = numberPrefix is null ? $"{index + 1}. " : $"{numberPrefix}{index + 1} ";
                AppendPreviewLine(text, $"{prefix}{items[index]}");
            }
        }

        private static string? JoinPreviewDetails(params (string Label, string? Value)[] details)
        {
            var values = details
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => $"{item.Label}: {item.Value}")
                .ToList();
            return values.Count == 0 ? null : string.Join(" | ", values);
        }

        private static void AppendPreviewLine(StringBuilder text, string? line)
        {
            if (line is not null)
                text.AppendLine(line.Trim());
        }

    private string BuildClientFacts(IReadOnlySet<Guid>? excludedDocumentIds = null)
    {
        var facts = new StringBuilder();
        AddFact(facts, "Court name", Input.CourtName);
        AddFact(facts, "Case title", Input.CaseTitle);
        AddFact(facts, "Case number", Input.CaseNumber);
        AddFact(facts, "Nature of plaint", PlaintNatures
            .FirstOrDefault(nature => nature.Code == Input.PlaintNature)?.Label ?? Input.PlaintNature);
        AddFact(facts, "Plaintiff name", Input.PlaintiffName);
        AddFact(facts, "Plaintiff address", Input.PlaintiffAddress);
        AddFact(facts, "Defendant name", Input.DefendantName);
        AddFact(facts, "Defendant address", Input.DefendantAddress);
        AddFact(facts, "Suit property description", Input.SuitPropertyDescription);
        AddFact(facts, "Survey number", Input.SurveyNumber);
        AddFact(facts, "Village", Input.Village);
        AddFact(facts, "Tehsil", Input.Tehsil);
        AddFact(facts, "District", Input.District);
        AddFact(facts, "Area", Input.Area);
        AddFact(facts, "Facts of the case", Input.Facts);
        AddFact(facts, "Legal grounds", Input.Grounds);
        AddFact(facts, "Reliefs sought", Input.ReliefsSought);
        AddFact(facts, "Prayer", Input.Prayer);
        AddFact(facts, "Suit valuation", Input.Valuation);
        AddFact(facts, "Cause of action date", Input.CauseOfActionDate);
        AddFact(facts, "Place", Input.Place);
        AddFact(facts, "Date", Input.Date);
        AddFact(facts, "Advocate name", Input.AdvocateName);
        AddFact(facts, "Advocate enrollment number", Input.AdvocateEnrollmentNo);

        var includedUploads = UploadedDocuments
            .Where(upload => excludedDocumentIds?.Contains(upload.Id) != true)
            .ToList();
        if (includedUploads.Count > 0)
        {
            facts.AppendLine("Uploaded supporting documents:");
            foreach (var upload in includedUploads)
            {
                var label = ResearchResult?.RequiredDocuments
                    .FirstOrDefault(document => document.Code.Equals(
                        upload.DocumentCode,
                        StringComparison.OrdinalIgnoreCase))?.Label ?? upload.DocumentCode;
                facts.Append("Document code: ").AppendLine(upload.DocumentCode);
                facts.Append("Document type: ").AppendLine(label);
                facts.Append("File: ").AppendLine(upload.FileName);
                facts.AppendLine(upload.Text).AppendLine();
            }
        }

        return facts.ToString();
    }

    private static CivilPlaintDetailsViewModel BuildSavedPlaintDetails(CivilPlaintInput input) => new()
    {
        PlaintNature = input.PlaintNature,
        Jurisdiction = input.Jurisdiction,
        CourtAndSuit = new CivilPlaintCourtAndSuitViewModel
        {
            CourtName = input.CourtName,
            CaseTitle = input.CaseTitle,
            CaseNumber = input.CaseNumber,
            CauseOfActionDate = input.CauseOfActionDate,
            PlaintiffName = input.PlaintiffName,
            PlaintiffAddress = input.PlaintiffAddress,
            DefendantName = input.DefendantName,
            DefendantAddress = input.DefendantAddress
        },
        SuitProperty = new CivilPlaintSuitPropertyViewModel
        {
            PropertyDescription = input.SuitPropertyDescription,
            SurveyPlotNumber = input.SurveyNumber,
            Village = input.Village,
            Tehsil = input.Tehsil,
            District = input.District,
            Area = input.Area,
            SuitValuationCourtFee = input.Valuation
        },
        FactsAndRelief = new CivilPlaintFactsAndReliefViewModel
        {
            Facts = input.Facts,
            LegalGrounds = input.Grounds,
            ReliefsSought = input.ReliefsSought,
            Prayer = input.Prayer
        },
        FilingDetails = new CivilPlaintFilingDetailsViewModel
        {
            Place = input.Place,
            Date = input.Date,
            AdvocateName = input.AdvocateName,
            EnrollmentNumber = input.AdvocateEnrollmentNo
        }
    };

    private async Task<bool> RunResearchAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Input.PlaintNature) &&
            string.IsNullOrWhiteSpace(Input.Facts))
        {
            ApiErrorMessage = "Select a plaint nature or enter case facts before running research.";
            ResearchResult = null;
            return false;
        }

        var request = new
        {
            jurisdiction = Input.Jurisdiction,
            caseCategory = "Civil",
            plaintNature = Input.PlaintNature,
            clientFacts = Input.Facts,
            alreadyHaveDocumentCodes = (Input.AlreadyHaveDocumentCodes ?? [])
                .Concat(UploadedDocuments.Select(document => document.DocumentCode))
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
                ApiErrorMessage = GetApiErrorMessage(
                    responseBody,
                    $"Intake research failed (HTTP {(int)response.StatusCode}).");
                ResearchResult = null;
                return false;
            }

            ResearchResult = JsonSerializer.Deserialize<DocumentResearchResultViewModel>(
                responseBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (ResearchResult is null)
            {
                logger.LogError("Intake research returned an empty result.");
                ApiErrorMessage = "The API returned an empty research result.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Input.PlaintNature) &&
                !string.IsNullOrWhiteSpace(ResearchResult.PlaintNature))
            {
                Input.PlaintNature = ResearchResult.PlaintNature;
            }

            ApiErrorMessage = null;
            return true;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the LegalDraftAI API for intake research.");
            ApiErrorMessage = "Could not reach the LegalDraftAI API to research the checklist. Check that it is running.";
            ResearchResult = null;
            return false;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Intake research returned invalid JSON.");
            ApiErrorMessage = "The API returned invalid intake research data.";
            ResearchResult = null;
            return false;
        }
    }

    private async Task LoadUploadedDocumentsAsync()
    {
        await HttpContext.Session.LoadAsync();
        var serialized = HttpContext.Session.GetString(UploadedDocumentsSessionKey);
        var storedNature = HttpContext.Session.GetString(UploadedNatureSessionKey);
        if (!string.Equals(storedNature, Input.PlaintNature, StringComparison.OrdinalIgnoreCase))
        {
            HttpContext.Session.Remove(UploadedDocumentsSessionKey);
            HttpContext.Session.Remove(UploadedNatureSessionKey);
            HttpContext.Session.Remove(CaseIdSessionKey);
            UploadedDocuments = [];
            return;
        }

        if (string.IsNullOrWhiteSpace(serialized))
        {
            UploadedDocuments = [];
            return;
        }

        try
        {
            var uploads = JsonSerializer.Deserialize<List<UploadedDocumentRecord>>(serialized) ?? [];
            var assignedMissingIds = false;
            UploadedDocuments = uploads.Select(upload =>
            {
                if (upload.Id != Guid.Empty)
                    return upload;
                assignedMissingIds = true;
                return upload with { Id = Guid.NewGuid() };
            }).ToList();
            if (assignedMissingIds)
                await SaveUploadedDocumentsAsync();
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Stored civil plaint uploads could not be read from the session.");
            HttpContext.Session.Remove(UploadedDocumentsSessionKey);
            HttpContext.Session.Remove(UploadedNatureSessionKey);
            UploadedDocuments = [];
            ApiErrorMessage = "Previously uploaded document data could not be loaded. Please upload the documents again.";
        }
    }

    private async Task SaveUploadedDocumentsAsync(Guid? caseId = null)
    {
        HttpContext.Session.SetString(
            UploadedDocumentsSessionKey,
            JsonSerializer.Serialize(UploadedDocuments));
        HttpContext.Session.SetString(UploadedNatureSessionKey, Input.PlaintNature ?? string.Empty);
        if (caseId is Guid savedCaseId)
            HttpContext.Session.SetString(CaseIdSessionKey, savedCaseId.ToString());
        await HttpContext.Session.CommitAsync();
    }

    private async Task LoadExistingCasesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync("api/cases", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Existing case lookup returned HTTP {StatusCode}.", (int)response.StatusCode);
                ApiStatusMessage = $"Could not load existing cases from the API (HTTP {(int)response.StatusCode}).";
                ExistingCases = [];
                return;
            }

            var cases = await response.Content.ReadFromJsonAsync<List<ExistingCaseOption>>(
                cancellationToken: cancellationToken);
            ExistingCases = cases?
                .Where(item => string.Equals(item.CaseCategory, "Civil", StringComparison.OrdinalIgnoreCase))
                .ToList() ?? [];
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not load existing cases from the LegalDraftAI API.");
            ApiStatusMessage = "Could not load existing cases. Check that the LegalDraftAI API is running.";
            ExistingCases = [];
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The LegalDraftAI API returned invalid existing case data.");
            ApiStatusMessage = "The API returned invalid existing case data.";
            ExistingCases = [];
        }
    }

    private async Task<bool> LoadSelectedCaseAsync(
        Guid caseId,
        bool populateInput,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync($"api/cases/{caseId}", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ApiErrorMessage = "The selected case could not be found.";
                return false;
            }
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Existing case {CaseId} lookup returned HTTP {StatusCode}.",
                    caseId,
                    (int)response.StatusCode);
                ApiErrorMessage = $"Could not load the selected case (HTTP {(int)response.StatusCode}).";
                return false;
            }

            var selectedCase = await response.Content.ReadFromJsonAsync<ExistingCaseDetail>(
                cancellationToken: cancellationToken);
            if (selectedCase is null)
            {
                logger.LogError("Existing case {CaseId} lookup returned an empty result.", caseId);
                ApiErrorMessage = "The API returned an empty case record.";
                return false;
            }

            SelectedCase = selectedCase;
            SelectedCaseId = caseId;
            UploadedDocuments = selectedCase.Documents
                .Where(document => string.IsNullOrWhiteSpace(document.StageCode) &&
                    document.CaseDraftId is null)
                .Select(document => new UploadedDocumentRecord(
                    document.DocumentCode,
                    document.OriginalFileName,
                    document.ExtractedText ?? string.Empty,
                    document.Id))
                .ToList();

            if (populateInput)
            {
                Input.PlaintNature = selectedCase.PlaintNature ?? string.Empty;
                Input.Jurisdiction = selectedCase.Jurisdiction;
                Input.CourtName = selectedCase.CourtName ?? string.Empty;
                Input.CaseTitle = selectedCase.Title;
                Input.CaseNumber = selectedCase.CaseNumber;
                Input.PlaintiffName = selectedCase.PlaintiffName ?? string.Empty;
                Input.PlaintiffAddress = selectedCase.PlaintiffAddress ?? string.Empty;
                Input.DefendantName = selectedCase.DefendantName ?? string.Empty;
                Input.DefendantAddress = selectedCase.DefendantAddress ?? string.Empty;
                Input.Facts = selectedCase.ClientFacts ?? string.Empty;
                if (selectedCase.CivilPlaintDetails is { } savedDetails)
                {
                    Input.PlaintNature = savedDetails.PlaintNature ?? Input.PlaintNature;
                    Input.Jurisdiction = savedDetails.Jurisdiction ?? Input.Jurisdiction;
                    Input.CourtName = savedDetails.CourtAndSuit.CourtName ?? Input.CourtName;
                    Input.CaseTitle = savedDetails.CourtAndSuit.CaseTitle ?? Input.CaseTitle;
                    Input.CaseNumber = savedDetails.CourtAndSuit.CaseNumber;
                    Input.CauseOfActionDate = savedDetails.CourtAndSuit.CauseOfActionDate;
                    Input.PlaintiffName = savedDetails.CourtAndSuit.PlaintiffName ?? Input.PlaintiffName;
                    Input.PlaintiffAddress = savedDetails.CourtAndSuit.PlaintiffAddress ?? Input.PlaintiffAddress;
                    Input.DefendantName = savedDetails.CourtAndSuit.DefendantName ?? Input.DefendantName;
                    Input.DefendantAddress = savedDetails.CourtAndSuit.DefendantAddress ?? Input.DefendantAddress;
                    Input.SuitPropertyDescription = savedDetails.SuitProperty.PropertyDescription;
                    Input.SurveyNumber = savedDetails.SuitProperty.SurveyPlotNumber;
                    Input.Village = savedDetails.SuitProperty.Village;
                    Input.Tehsil = savedDetails.SuitProperty.Tehsil;
                    Input.District = savedDetails.SuitProperty.District;
                    Input.Area = savedDetails.SuitProperty.Area;
                    Input.Valuation = savedDetails.SuitProperty.SuitValuationCourtFee;
                    Input.Facts = savedDetails.FactsAndRelief.Facts ?? Input.Facts;
                    Input.Grounds = savedDetails.FactsAndRelief.LegalGrounds;
                    Input.ReliefsSought = savedDetails.FactsAndRelief.ReliefsSought;
                    Input.Prayer = savedDetails.FactsAndRelief.Prayer;
                    Input.Place = savedDetails.FilingDetails.Place;
                    Input.Date = savedDetails.FilingDetails.Date;
                    Input.AdvocateName = savedDetails.FilingDetails.AdvocateName;
                    Input.AdvocateEnrollmentNo = savedDetails.FilingDetails.EnrollmentNumber;
                    Input.SaveDetailsToDatabase = true;
                }
            }

            return true;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not load case {CaseId} from the LegalDraftAI API.", caseId);
            ApiErrorMessage = "Could not load the selected case. Check that the LegalDraftAI API is running.";
            return false;
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The LegalDraftAI API returned invalid details for case {CaseId}.", caseId);
            ApiErrorMessage = "The API returned invalid case details.";
            return false;
        }
    }

    private async Task<Guid?> GetCurrentCaseIdAsync()
    {
        await HttpContext.Session.LoadAsync();
        return Guid.TryParse(HttpContext.Session.GetString(CaseIdSessionKey), out var caseId)
            ? caseId
            : null;
    }

    private static void AddFormField(MultipartFormDataContent form, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            form.Add(new StringContent(value), name);
    }

    private async Task LoadPlaintNaturesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync(
                "api/intake/plaint-natures?category=Civil",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Civil plaint nature lookup returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                ApiStatusMessage = $"Could not load civil plaint types from the API (HTTP {(int)response.StatusCode}).";
                return;
            }

            PlaintNatures = await response.Content.ReadFromJsonAsync<List<PlaintNatureOption>>(
                                cancellationToken: cancellationToken)
                            ?? throw new JsonException("The civil plaint type lookup returned no data.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not load civil plaint types from the LegalDraftAI API.");
            ApiStatusMessage = "Could not load civil plaint types. Check that the LegalDraftAI API is running.";
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "The LegalDraftAI API returned invalid civil plaint types.");
            ApiStatusMessage = "The API returned invalid civil plaint type data.";
        }
    }

    private string GetApiErrorMessage(string responseBody, string fallbackMessage)
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
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The LegalDraftAI API returned a non-JSON error response.");
        }

        return fallbackMessage;
    }

    private static void AddFact(StringBuilder facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Append(label).AppendLine(":").AppendLine(value.Trim()).AppendLine();
        }
    }
}

public sealed record PlaintNatureOption(string Code, string Label, string Description);

public sealed record ExistingCaseOption(
    Guid Id,
    string Title,
    string CaseCategory,
    string? PlaintiffName,
    string? DefendantName);

public sealed class ExistingCaseDetail
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CaseCategory { get; set; } = string.Empty;
    public string? PlaintNature { get; set; }
    public string Jurisdiction { get; set; } = string.Empty;
    public string? CourtName { get; set; }
    public string? CaseNumber { get; set; }
    public string? PlaintiffName { get; set; }
    public string? PlaintiffAddress { get; set; }
    public string? DefendantName { get; set; }
    public string? DefendantAddress { get; set; }
    public string? ClientFacts { get; set; }
    public CivilPlaintDetailsViewModel? CivilPlaintDetails { get; set; }
    public List<ExistingCaseDocument> Documents { get; set; } = [];
    public List<ExistingCaseDraft> Drafts { get; set; } = [];
}

public sealed class ExistingCaseDocument
{
    public Guid Id { get; set; }
    public Guid? CaseDraftId { get; set; }
    public Guid? StageApplicationId { get; set; }
    public string? StageCode { get; set; }
    public string? ApplicationCode { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public string? ExtractedText { get; set; }
    public string? TranslatedText { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public sealed class CivilPlaintDetailsViewModel
{
    public string? PlaintNature { get; set; }
    public string? Jurisdiction { get; set; }
    public CivilPlaintCourtAndSuitViewModel CourtAndSuit { get; set; } = new();
    public CivilPlaintSuitPropertyViewModel SuitProperty { get; set; } = new();
    public CivilPlaintFactsAndReliefViewModel FactsAndRelief { get; set; } = new();
    public CivilPlaintFilingDetailsViewModel FilingDetails { get; set; } = new();
}

public sealed class CivilPlaintCourtAndSuitViewModel
{
    public string? CourtName { get; set; }
    public string? CaseTitle { get; set; }
    public string? CaseNumber { get; set; }
    public string? CauseOfActionDate { get; set; }
    public string? PlaintiffName { get; set; }
    public string? DefendantName { get; set; }
    public string? PlaintiffAddress { get; set; }
    public string? DefendantAddress { get; set; }
}

public sealed class CivilPlaintSuitPropertyViewModel
{
    public string? PropertyDescription { get; set; }
    public string? SurveyPlotNumber { get; set; }
    public string? Area { get; set; }
    public string? Village { get; set; }
    public string? Tehsil { get; set; }
    public string? District { get; set; }
    public string? SuitValuationCourtFee { get; set; }
}

public sealed class CivilPlaintFactsAndReliefViewModel
{
    public string? Facts { get; set; }
    public string? LegalGrounds { get; set; }
    public string? ReliefsSought { get; set; }
    public string? Prayer { get; set; }
}

public sealed class CivilPlaintFilingDetailsViewModel
{
    public string? Place { get; set; }
    public string? Date { get; set; }
    public string? AdvocateName { get; set; }
    public string? EnrollmentNumber { get; set; }
}

public sealed class ExistingCaseDraft
{
    public Guid Id { get; set; }
    public Guid? StageApplicationId { get; set; }
    public string? StageCode { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? ApplicationCode { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class HindiTranslationRequestViewModel
{
    public string Text { get; set; } = string.Empty;
}

public sealed class DocumentResearchResultViewModel
{
    public string PlaintNature { get; set; } = string.Empty;
    public string PlaintNatureLabel { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<RequiredDocumentViewModel> RequiredDocuments { get; set; } = [];
    public List<string> MissingMandatory { get; set; } = [];
    public List<string> MissingRecommended { get; set; } = [];
    public List<string> AlreadyHave { get; set; } = [];
    public bool CanFileWithMinimumSet { get; set; }
    public List<string> NextSteps { get; set; } = [];
}

public sealed class RequiredDocumentViewModel
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public bool IsMandatoryForFiling { get; set; }
}

public sealed record UploadedDocumentRecord(
    string DocumentCode,
    string FileName,
    string Text,
    Guid Id = default);

public sealed class MappedDocumentExtractionResult
{
    public Guid? CaseId { get; set; }
    public List<UploadedDocumentRecord> Documents { get; set; } = [];
}

public sealed class CivilPlaintInput
{
    public bool SaveDetailsToDatabase { get; set; } = true;

    [Required(ErrorMessage = "Enter the court name.")]
    [StringLength(300)]
    public string CourtName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the case title.")]
    [StringLength(500)]
    public string CaseTitle { get; set; } = string.Empty;

    [StringLength(100)]
    public string? CaseNumber { get; set; }

    [StringLength(100)]
    public string? PlaintNature { get; set; }

    [Required(ErrorMessage = "Enter the plaintiff's name.")]
    [StringLength(300)]
    public string PlaintiffName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the plaintiff's address.")]
    [StringLength(2000)]
    public string PlaintiffAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the defendant's name.")]
    [StringLength(300)]
    public string DefendantName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the defendant's address.")]
    [StringLength(2000)]
    public string DefendantAddress { get; set; } = string.Empty;

    [StringLength(5000)]
    public string? SuitPropertyDescription { get; set; }

    [StringLength(200)]
    public string? SurveyNumber { get; set; }

    [StringLength(200)]
    public string? Village { get; set; }

    [StringLength(200)]
    public string? Tehsil { get; set; }

    [StringLength(200)]
    public string? District { get; set; }

    [StringLength(200)]
    public string? Area { get; set; }

    [StringLength(20000)]
    public string? Facts { get; set; }

    [StringLength(10000)]
    public string? Grounds { get; set; }

    [StringLength(10000)]
    public string? ReliefsSought { get; set; }

    [StringLength(5000)]
    public string? Prayer { get; set; }

    [StringLength(500)]
    public string? Valuation { get; set; }

    [DataType(DataType.Date)]
    public string? CauseOfActionDate { get; set; }

    [StringLength(200)]
    public string? Place { get; set; }

    [DataType(DataType.Date)]
    public string? Date { get; set; }

    [StringLength(300)]
    public string? AdvocateName { get; set; }

    [StringLength(100)]
    public string? AdvocateEnrollmentNo { get; set; }

    [StringLength(500000)]
    public string? GeneratedDraftJson { get; set; }

    [StringLength(200)]
    public string Jurisdiction { get; set; } = "India";

    public List<string> AlreadyHaveDocumentCodes { get; set; } = [];
}
