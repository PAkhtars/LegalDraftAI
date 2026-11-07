using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LegalDraftAI.Web.Pages;

[RequestSizeLimit(50 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
public sealed class CreatePdfFromImagesModel(
    IHttpClientFactory httpClientFactory,
    ILogger<CreatePdfFromImagesModel> logger) : PageModel
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly HttpClient _apiClient = httpClientFactory.CreateClient("DraftsApi");

    public string? ErrorMessage { get; private set; }
    public string? StatusMessage { get; private set; }
    public bool HasExtractedText { get; private set; }

    [BindProperty]
    [StringLength(100)]
    [Required(ErrorMessage = "Enter a name for the downloaded DOCX file.")]
    public string OutputFileName { get; set; } = "ScannedDocuments";

    [BindProperty]
    [StringLength(100000)]
    public string? ExtractedText { get; set; }

    public async Task<IActionResult> OnPostExtractAsync(
        List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        OutputFileName = NormalizeFileName(OutputFileName);
        if (files.Count == 0)
            ModelState.AddModelError(string.Empty, "Choose at least one image.");
        if (files.Count > 20)
            ModelState.AddModelError(string.Empty, "Upload no more than 20 images at a time.");
        if (files.Any(file => file.Length == 0) ||
            files.Sum(file => file.Length) > 50L * 1024 * 1024)
        {
            ModelState.AddModelError(string.Empty, "Images must be non-empty and total no more than 50 MB.");
        }

        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".gif", ".webp"
        };
        if (files.Any(file => !supportedExtensions.Contains(Path.GetExtension(file.FileName))))
            ModelState.AddModelError(string.Empty, "Use PNG, JPG, TIFF, BMP, GIF, or WebP images.");

        if (!ModelState.IsValid)
        {
            HasExtractedText = !string.IsNullOrWhiteSpace(ExtractedText);
            return Page();
        }

        using var content = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var fileContent = new StreamContent(file.OpenReadStream());
            if (MediaTypeHeaderValue.TryParse(file.ContentType, out var contentType))
                fileContent.Headers.ContentType = contentType;
            content.Add(fileContent, "files", Path.GetFileName(file.FileName));
        }

        try
        {
            using var response = await _apiClient.PostAsync(
                "api/drafts/extract-image-text",
                content,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = await ReadApiErrorAsync(response, cancellationToken);
                HasExtractedText = !string.IsNullOrWhiteSpace(ExtractedText);
                return Page();
            }

            var results = await response.Content.ReadFromJsonAsync<List<ImageOcrResult>>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);
            if (results is null || results.Count != files.Count)
            {
                logger.LogError("Image OCR API returned an unexpected result count.");
                ErrorMessage = "The API returned an incomplete OCR result. Please try again.";
                return Page();
            }

            var text = new StringBuilder();
            foreach (var result in results)
            {
                if (text.Length > 0)
                    text.AppendLine().AppendLine();
                text.Append("IMAGE: ").AppendLine(result.FileName);
                text.AppendLine(result.Text);
            }

            ExtractedText = text.ToString().Trim();
            HasExtractedText = true;
            StatusMessage = $"OCR completed for {results.Count} image(s). Review and edit the text before downloading.";
            return Page();
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API for image OCR.");
            ErrorMessage = "Could not reach the API for OCR. Check that the API is running.";
            return Page();
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Image OCR API returned invalid JSON.");
            ErrorMessage = "The API returned an invalid OCR response.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDownloadDocxAsync(CancellationToken cancellationToken)
    {
        OutputFileName = NormalizeFileName(OutputFileName);
        if (string.IsNullOrWhiteSpace(ExtractedText))
            ModelState.AddModelError(nameof(ExtractedText), "Run OCR or enter text before downloading.");
        if (ExtractedText?.Length > 100000)
            ModelState.AddModelError(nameof(ExtractedText), "The document text must be 100,000 characters or fewer.");

        if (!ModelState.IsValid)
        {
            HasExtractedText = !string.IsNullOrWhiteSpace(ExtractedText);
            return Page();
        }

        try
        {
            using var response = await _apiClient.PostAsJsonAsync(
                "api/drafts/export-image-ocr-docx",
                new ImageOcrDocxExportRequest(ExtractedText!),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = await ReadApiErrorAsync(response, cancellationToken);
                HasExtractedText = true;
                return Page();
            }

            return File(
                await response.Content.ReadAsByteArrayAsync(cancellationToken),
                DocxContentType,
                $"{OutputFileName}.docx");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to export OCR text as DOCX.");
            return StatusCode(StatusCodes.Status502BadGateway, "Could not reach the API to export the DOCX.");
        }
    }

    private static async Task<string> ReadApiErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                return error.GetString() ?? $"API request failed (HTTP {(int)response.StatusCode}).";
            }
        }
        catch (JsonException)
        {
            return $"API request failed (HTTP {(int)response.StatusCode}).";
        }

        return $"API request failed (HTTP {(int)response.StatusCode}).";
    }

    private static string NormalizeFileName(string? fileName)
    {
        var normalized = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            normalized = normalized.Replace(invalidCharacter, '_');
        return normalized.Trim();
    }

    public sealed record ImageOcrResult(string FileName, string Text);

    public sealed record ImageOcrDocxExportRequest(string Text);
}
