using LegalDraftAI.Models;
using LegalDraftAI.Services;
using Microsoft.AspNetCore.Mvc;

namespace LegalDraftAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IntakeController : ControllerBase
{
    private readonly IDocumentResearchService _research;

    public IntakeController(IDocumentResearchService research)
    {
        _research = research;
    }

    /// <summary>
    /// List supported plaint natures (optionally filter by category: Civil / Criminal).
    /// </summary>
    [HttpGet("plaint-natures")]
    public IActionResult GetPlaintNatures([FromQuery] string? category = null)
    {
        var list = _research.GetPlaintNatures(category);
        return Ok(list.Select(n => new
        {
            n.Code,
            n.Label,
            n.Category,
            n.PlaintGroup,
            n.Description,
            n.SuggestedDocumentType
        }));
    }

    /// <summary>
    /// Full document checklist for a given plaint nature code.
    /// </summary>
    [HttpGet("documents/{plaintNature}")]
    public IActionResult GetDocuments(string plaintNature)
    {
        var docs = _research.GetDocumentsForNature(plaintNature);
        if (docs.Count == 0)
            return NotFound(new { error = $"No checklist for plaint nature '{plaintNature}'" });

        return Ok(docs);
    }

    /// <summary>
    /// Research step: given plaint nature and/or client facts and documents already held,
    /// returns required checklist, gaps, and whether minimum set is enough to file.
    /// </summary>
    [HttpPost("research")]
    public IActionResult Research([FromBody] CaseIntakeRequest request)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        var result = _research.Analyze(request);
        return Ok(result);
    }
}
