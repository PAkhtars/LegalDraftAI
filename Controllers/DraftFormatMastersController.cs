using LegalDraftAI.Models;
using LegalDraftAI.Services;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Mvc;

namespace LegalDraftAI.Controllers;

[ApiController]
[Route("api/draft-format-masters")]
public sealed class DraftFormatMastersController(IDraftFormatMasterService service) : ControllerBase
{
    /// <summary>List reusable draft formats, optionally filtered by category.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DraftFormatMasterResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DraftFormatMasterResponse>>> List(
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(category) &&
            !IsSupportedCategory(category))
        {
            return BadRequest(new { error = "Category must be Civil or Criminal." });
        }

        return Ok(await service.ListAsync(category?.Trim(), cancellationToken));
    }

    /// <summary>Get one reusable draft format by its ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<DraftFormatMasterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DraftFormatMasterResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await service.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Create a reusable draft format master.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
    [ProducesResponseType<DraftFormatMasterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DraftFormatMasterResponse>> Create(
        [FromForm] CreateDraftFormatMasterRequest request,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedCategory(request.Category))
            return BadRequest(new { error = "Category must be Civil or Criminal." });
        if (!TryValidateDocx(file, out var fileError))
            return BadRequest(new { error = fileError });

        if (await service.NameExistsAsync(
                request.Category.Trim(),
                request.FormatName.Trim(),
                null,
                cancellationToken))
        {
            return Conflict(new { error = "A format with this name already exists in the selected category." });
        }

        var created = await service.CreateAsync(request, file!, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replace a reusable draft format master.</summary>
    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
    [ProducesResponseType<DraftFormatMasterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DraftFormatMasterResponse>> Update(
        Guid id,
        [FromForm] UpdateDraftFormatMasterRequest request,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedCategory(request.Category))
            return BadRequest(new { error = "Category must be Civil or Criminal." });
        if (file is not null && !TryValidateDocx(file, out var fileError))
            return BadRequest(new { error = fileError });

        if (await service.NameExistsAsync(
                request.Category.Trim(),
                request.FormatName.Trim(),
                id,
                cancellationToken))
        {
            return Conflict(new { error = "A format with this name already exists in the selected category." });
        }

        if (file is null)
        {
            var existing = await service.GetAsync(id, cancellationToken);
            if (existing is null)
                return NotFound();
            if (string.IsNullOrWhiteSpace(existing.DraftFormatPath))
                return BadRequest(new { error = "Upload a DOCX file for this draft format before saving changes." });
        }

        var updated = await service.UpdateAsync(id, request, file, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Download the uploaded DOCX format file.</summary>
    [HttpGet("{id:guid}/file")]
    [Produces("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken cancellationToken)
    {
        var file = await service.GetFileAsync(id, cancellationToken);
        return file is null
            ? NotFound(new { error = "The draft format or its DOCX file was not found." })
            : File(file.Content, file.ContentType, file.OriginalFileName);
    }

    /// <summary>Delete a reusable draft format master.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    private static bool IsSupportedCategory(string category) =>
        category.Equals("Civil", StringComparison.OrdinalIgnoreCase) ||
        category.Equals("Criminal", StringComparison.OrdinalIgnoreCase);

    private static bool TryValidateDocx(IFormFile? file, out string error)
    {
        if (file is null || file.Length == 0)
        {
            error = "Upload a non-empty DOCX draft format.";
            return false;
        }
        if (file.Length > 20 * 1024 * 1024)
        {
            error = "DOCX files must be 20 MB or smaller.";
            return false;
        }
        if (!Path.GetExtension(file.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only .docx files are supported.";
            return false;
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var document = WordprocessingDocument.Open(stream, false);
            if (document.MainDocumentPart is null)
            {
                error = "The uploaded file is not a valid Word document.";
                return false;
            }
        }
        catch (Exception exception) when (
            exception is OpenXmlPackageException or IOException or ArgumentException or FormatException)
        {
            error = "The uploaded file is not a valid DOCX document.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
