using Microsoft.AspNetCore.Mvc;
using GTranslate.Translators;
using LegalDraftAI.Models;
using System.Collections.Concurrent;
using System.Text;

namespace LegalDraftAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranslationController : ControllerBase
    {
        private readonly GoogleTranslator _googleTranslator;
        private readonly ILogger<TranslationController> _logger;

        public TranslationController(
            GoogleTranslator googleTranslator,
            ILogger<TranslationController> logger)
        {
            _googleTranslator = googleTranslator;
            _logger = logger;
        }

    [HttpPost("to-hindi")]
    [RequestSizeLimit(400_000)]
    public async Task<IActionResult> TranslateDraftToHindi(
        [FromBody] HindiTranslationRequest request)
    {
        if (request.Fields is { Count: > 0 } fields)
        {
            if (fields.Count > 25 ||
                fields.Values.Sum(value => value?.Length ?? 0) > 100_000)
            {
                return BadRequest(new
                {
                    error = "At most 25 fields and 100,000 total characters can be translated at once."
                });
            }

            try
            {
                var translations = await TranslateFieldsAsync(fields);
                return Ok(new { translatedFields = translations });
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(exception, "Case field Hindi translation provider request failed.");
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    error = "Case field Hindi translation failed. Try again."
                });
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogError(exception, "Case field Hindi translation returned an invalid response.");
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    error = "Case field Hindi translation returned an invalid response. Try again."
                });
            }
        }

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Text is required for translation." });
        if (request.Text.Length > 100_000)
            return BadRequest(new { error = "Text must be 100,000 characters or fewer for translation." });

        try
        {
            var translatedText = await TranslateInChunksAsync(request.Text);
            return Ok(new { translatedText });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Civil plaint Hindi translation provider request failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Hindi translation failed. Try again."
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Civil plaint Hindi translation returned an invalid response.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Hindi translation returned an invalid response. Try again."
            });
        }
    }

    [HttpGet("to-hindi")]
    public async Task<IActionResult> TranslateToHindi([FromQuery] string text)
    {
        if (string.IsNullOrEmpty(text))
            return BadRequest("Text parameter is required.");

        // Translate text into Hindi ('hi') from auto-detected source
        var result = await _googleTranslator.TranslateAsync(text, "hi");

        return Ok(new
        {
            OriginalText = text,
            TranslatedText = result.Translation,
            SourceLanguage = result.SourceLanguage.Name,
            Engine = result.Service
        });
    }

    private async Task<IReadOnlyDictionary<string, string>> TranslateFieldsAsync(
        IReadOnlyDictionary<string, string> fields)
    {
        using var concurrencyLimit = new SemaphoreSlim(3);
        var translations = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var translationTasks = fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .Select(async field =>
            {
                await concurrencyLimit.WaitAsync();
                try
                {
                    translations[field.Key] = await TranslateInChunksAsync(field.Value);
                }
                finally
                {
                    concurrencyLimit.Release();
                }
            });

        await Task.WhenAll(translationTasks);
        return translations;
    }

    private async Task<string> TranslateInChunksAsync(string text)
    {
        const int maxChunkLength = 3000;
        var translatedText = new StringBuilder(text.Length);
        var offset = 0;

        while (offset < text.Length)
        {
            var chunkLength = Math.Min(maxChunkLength, text.Length - offset);
            if (offset + chunkLength < text.Length)
            {
                var newline = text.LastIndexOf('\n', offset + chunkLength - 1, chunkLength);
                if (newline > offset + maxChunkLength / 2)
                {
                    chunkLength = newline - offset + 1;
                }
                else
                {
                    var whitespace = text.LastIndexOf(' ', offset + chunkLength - 1, chunkLength);
                    if (whitespace > offset + maxChunkLength / 2)
                        chunkLength = whitespace - offset;
                }
            }

            var chunk = text.Substring(offset, chunkLength);
            var result = await _googleTranslator.TranslateAsync(chunk, "hi");
            translatedText.Append(result.Translation);
            offset += chunkLength;
        }

        return translatedText.ToString();
    }
       
    }
}