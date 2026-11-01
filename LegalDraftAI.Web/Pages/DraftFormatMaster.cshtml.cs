using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LegalDraftAI.Web.Pages;

[RequestSizeLimit(25 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
public sealed class DraftFormatMasterModel(
    IHttpClientFactory httpClientFactory,
    ILogger<DraftFormatMasterModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _apiClient = httpClientFactory.CreateClient("DraftsApi");

    public IReadOnlyList<DraftFormatMasterItem> DraftFormats { get; private set; } = [];
    public string? EditingDraftFileName { get; private set; }
    public string? StatusMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    [BindProperty]
    public DraftFormatInput Input { get; set; } = new();

    [BindProperty]
    public string CategoryFilter { get; set; } = "Civil";

    [BindProperty]
    public Guid? EditingId { get; set; }

    public async Task OnGetAsync(string? category, CancellationToken cancellationToken)
    {
        CategoryFilter = NormalizeCategory(category) ?? "Civil";
        Input.Category = CategoryFilter;
        await LoadDraftFormatsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostFilterAsync(CancellationToken cancellationToken)
    {
        CategoryFilter = NormalizeCategory(CategoryFilter) ?? "Civil";
        Input.Category = CategoryFilter;
        await LoadDraftFormatsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        CategoryFilter = NormalizeCategory(CategoryFilter) ?? "Civil";
        if (!IsSupportedCategory(Input.Category))
            ModelState.AddModelError("Input.Category", "Select Civil or Criminal.");
        if (EditingId is null && (Input.File is null || Input.File.Length == 0))
            ModelState.AddModelError("Input.File", "Choose a DOCX draft format file.");
        if (Input.File is not null &&
            (!Path.GetExtension(Input.File.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
             Input.File.Length > 20 * 1024 * 1024))
        {
            ModelState.AddModelError("Input.File", "Upload a .docx file that is no larger than 20 MB.");
        }

        if (!ModelState.IsValid)
        {
            await LoadDraftFormatsAsync(cancellationToken);
            return Page();
        }

        try
        {
            using var content = CreateMultipartContent(Input);
            using var response = EditingId is Guid id
                ? await _apiClient.PutAsync($"api/draft-format-masters/{id}", content, cancellationToken)
                : await _apiClient.PostAsync("api/draft-format-masters", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = await ReadApiErrorAsync(response, cancellationToken);
                await LoadDraftFormatsAsync(cancellationToken);
                return Page();
            }

            StatusMessage = EditingId is null
                ? "Draft format master created."
                : "Draft format master updated.";
            CategoryFilter = Input.Category;
            ResetInput(CategoryFilter);
            await LoadDraftFormatsAsync(cancellationToken);
            return Page();
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to save a draft format master.");
            ErrorMessage = "Could not reach the API to save the draft format master.";
            await LoadDraftFormatsAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostEditAsync(Guid id, CancellationToken cancellationToken)
    {
        CategoryFilter = NormalizeCategory(CategoryFilter) ?? "Civil";
        try
        {
            using var response = await _apiClient.GetAsync(
                $"api/draft-format-masters/{id}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "The selected draft format no longer exists."
                    : await ReadApiErrorAsync(response, cancellationToken);
                await LoadDraftFormatsAsync(cancellationToken);
                return Page();
            }

            var item = await response.Content.ReadFromJsonAsync<DraftFormatMasterItem>(
                JsonOptions,
                cancellationToken);
            if (item is null)
            {
                ErrorMessage = "The API returned an empty draft format.";
                await LoadDraftFormatsAsync(cancellationToken);
                return Page();
            }

            Input = new DraftFormatInput
            {
                Category = item.Category,
                FormatName = item.FormatName,
                Description = item.Description
            };
            EditingId = item.Id;
            EditingDraftFileName = item.OriginalFileName;
            CategoryFilter = item.Category;
            await LoadDraftFormatsAsync(cancellationToken);
            return Page();
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to load a draft format master.");
            ErrorMessage = "Could not reach the API to load the selected draft format.";
            await LoadDraftFormatsAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        CategoryFilter = NormalizeCategory(CategoryFilter) ?? "Civil";
        try
        {
            using var response = await _apiClient.DeleteAsync(
                $"api/draft-format-masters/{id}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "The selected draft format no longer exists."
                    : await ReadApiErrorAsync(response, cancellationToken);
            }
            else
            {
                StatusMessage = "Draft format master deleted.";
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to delete a draft format master.");
            ErrorMessage = "Could not reach the API to delete the draft format master.";
        }

        await LoadDraftFormatsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelEditAsync(CancellationToken cancellationToken)
    {
        CategoryFilter = NormalizeCategory(CategoryFilter) ?? "Civil";
        ResetInput(CategoryFilter);
        await LoadDraftFormatsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnGetDownloadAsync(
        Guid id,
        string? category,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync(
                $"api/draft-format-masters/{id}/file",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = response.StatusCode == HttpStatusCode.NotFound
                    ? "The DOCX file for this draft format was not found."
                    : await ReadApiErrorAsync(response, cancellationToken);
                CategoryFilter = NormalizeCategory(category) ?? "Civil";
                await LoadDraftFormatsAsync(cancellationToken);
                return Page();
            }

            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ??
                           response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ??
                           "DraftFormat.docx";
            var contentType = response.Content.Headers.ContentType?.MediaType ??
                              "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            return File(
                await response.Content.ReadAsByteArrayAsync(cancellationToken),
                contentType,
                Path.GetFileName(fileName));
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to download a draft format DOCX.");
            return StatusCode(StatusCodes.Status502BadGateway, "Could not reach the API to download the DOCX file.");
        }
    }

    private async Task LoadDraftFormatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var url = $"api/draft-format-masters?category={Uri.EscapeDataString(CategoryFilter)}";
            using var response = await _apiClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage ??= await ReadApiErrorAsync(response, cancellationToken);
                DraftFormats = [];
                return;
            }

            DraftFormats = await response.Content.ReadFromJsonAsync<List<DraftFormatMasterItem>>(
                JsonOptions,
                cancellationToken) ?? [];
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not reach the API to list draft format masters.");
            ErrorMessage ??= "Could not reach the API to load draft format masters.";
            DraftFormats = [];
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
            // Use the HTTP status message when the API response is not JSON.
        }

        return $"API request failed (HTTP {(int)response.StatusCode}).";
    }

    private static string? NormalizeCategory(string? category) =>
        category?.Trim().Equals("Criminal", StringComparison.OrdinalIgnoreCase) == true
            ? "Criminal"
            : category?.Trim().Equals("Civil", StringComparison.OrdinalIgnoreCase) == true
                ? "Civil"
                : null;

    private static bool IsSupportedCategory(string? category) =>
        NormalizeCategory(category) is not null;

    private void ResetInput(string category)
    {
        Input = new DraftFormatInput { Category = category };
        EditingId = null;
        EditingDraftFileName = null;
        ModelState.Clear();
    }

    private static MultipartFormDataContent CreateMultipartContent(DraftFormatInput input)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(input.Category), nameof(input.Category));
        content.Add(new StringContent(input.FormatName), nameof(input.FormatName));
        if (!string.IsNullOrWhiteSpace(input.Description))
            content.Add(new StringContent(input.Description), nameof(input.Description));

        if (input.File is not null)
        {
            var fileContent = new StreamContent(input.File.OpenReadStream());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            content.Add(fileContent, "file", Path.GetFileName(input.File.FileName));
        }

        return content;
    }

    public sealed class DraftFormatInput
    {
        [Required]
        public string Category { get; set; } = "Civil";

        [Required, StringLength(200)]
        public string FormatName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public IFormFile? File { get; set; }
    }

    public sealed record DraftFormatMasterItem(
        Guid Id,
        string Category,
        string FormatName,
        string? Description,
        string OriginalFileName,
        string DraftFormatPath,
        string ContentType,
        long FileSizeBytes,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);
}
