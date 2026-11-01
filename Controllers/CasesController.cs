using LegalDraftAI.Models;
using LegalDraftAI.Services;
using Microsoft.AspNetCore.Mvc;

namespace LegalDraftAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CasesController : ControllerBase
{
    private readonly ICaseService _cases;

    public CasesController(ICaseService cases) => _cases = cases;

    /// <summary>Create a new case file (metadata only).</summary>
    [HttpPost]
    public async Task<ActionResult<CaseSummaryDto>> Create([FromBody] CreateCaseRequest request, CancellationToken ct)
    {
        var created = await _cases.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { caseId = created.Id }, created);
    }

    /// <summary>List cases (optional status and saved-draft type filters).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CaseSummaryDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? documentType,
        CancellationToken ct)
        => Ok(await _cases.ListAsync(status, documentType, ct));

    /// <summary>Get full case with documents and drafts.</summary>
    [HttpGet("{caseId:guid}")]
    public async Task<ActionResult<CaseDetailDto>> Get(Guid caseId, CancellationToken ct)
    {
        var c = await _cases.GetAsync(caseId, ct);
        return c == null ? NotFound() : Ok(c);
    }

    /// <summary>Update case metadata.</summary>
    [HttpPut("{caseId:guid}")]
    public async Task<ActionResult<CaseSummaryDto>> Update(Guid caseId, [FromBody] UpdateCaseRequest request, CancellationToken ct)
    {
        var u = await _cases.UpdateAsync(caseId, request, ct);
        return u == null ? NotFound() : Ok(u);
    }

    /// <summary>Save or replace the four structured Civil Plaint sections for a case.</summary>
    [HttpPut("{caseId:guid}/civil-plaint-details")]
    public async Task<ActionResult<CaseDetailDto>> SaveCivilPlaintDetails(
        Guid caseId,
        [FromBody] CivilPlaintDetailsDto details,
        CancellationToken ct)
    {
        if (details.CourtAndSuit is null ||
            details.SuitProperty is null ||
            details.FactsAndRelief is null ||
            details.FilingDetails is null)
        {
            return BadRequest(new { error = "All four Civil Plaint detail sections are required." });
        }

        var saved = await _cases.SaveCivilPlaintDetailsAsync(caseId, details, ct);
        return saved is null ? NotFound() : Ok(saved);
    }

    /// <summary>Upload a supporting document and extract text.</summary>
    [HttpPost("{caseId:guid}/documents")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<ActionResult<CaseDocumentDto>> UploadDocument(
        Guid caseId,
        [FromForm] string documentCode,
        [FromForm] string? stageCode,
        [FromForm] string? applicationCode,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required" });
        try
        {
            var doc = await _cases.AddDocumentAsync(
                caseId,
                documentCode ?? "OTHER",
                file,
                ct,
                stageCode: stageCode,
                applicationCode: applicationCode);
            return Ok(doc);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>View or download a stored supporting document.</summary>
    [HttpGet("{caseId:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(
        Guid caseId,
        Guid documentId,
        [FromQuery] bool download = false,
        CancellationToken ct = default)
    {
        var document = await _cases.GetDocumentFileAsync(caseId, documentId, ct);
        if (document is null)
            return NotFound(new { error = "Supporting document not found." });

        if (download)
            return File(document.Content, document.ContentType, Path.GetFileName(document.OriginalFileName));

        Response.Headers.ContentDisposition = "inline";
        return File(document.Content, document.ContentType);
    }

    /// <summary>Remove an attached supporting document and any legacy stored file.</summary>
    [HttpDelete("{caseId:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(
        Guid caseId,
        Guid documentId,
        CancellationToken ct)
    {
        try
        {
            return await _cases.DeleteDocumentAsync(caseId, documentId, ct)
                ? NoContent()
                : NotFound(new { error = "Supporting document not found for this case." });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (IOException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Could not remove the supporting document from storage."
            });
        }
    }

    /// <summary>Document checklist + gap analysis for this case.</summary>
    [HttpPost("{caseId:guid}/research")]
    public async Task<ActionResult<DocumentResearchResult>> Research(Guid caseId, CancellationToken ct)
    {
        try
        {
            return Ok(await _cases.ResearchAsync(caseId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Generate a draft from stored facts + documents and save to the case.</summary>
    [HttpPost("{caseId:guid}/drafts")]
    public async Task<ActionResult<CaseDraftDetailDto>> GenerateDraft(
        Guid caseId,
        [FromBody] GenerateCaseDraftRequest request,
        CancellationToken ct)
    {
        try
        {
            var draft = await _cases.GenerateDraftAsync(caseId, request, ct);
            return Ok(draft);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get a saved draft (JSON).</summary>
    [HttpGet("{caseId:guid}/drafts/{draftId:guid}")]
    public async Task<ActionResult<CaseDraftDetailDto>> GetDraft(Guid caseId, Guid draftId, CancellationToken ct)
    {
        var d = await _cases.GetDraftAsync(caseId, draftId, ct);
        return d == null ? NotFound() : Ok(d);
    }

    /// <summary>Download stored PDF for a draft.</summary>
    [HttpGet("{caseId:guid}/drafts/{draftId:guid}/pdf")]
    public async Task<IActionResult> GetDraftPdf(Guid caseId, Guid draftId, CancellationToken ct)
    {
        var bytes = await _cases.GetDraftPdfAsync(caseId, draftId, ct);
        if (bytes == null) return NotFound(new { error = "PDF not found for this draft" });
        return File(bytes, "application/pdf", $"draft-{draftId}.pdf");
    }
}
