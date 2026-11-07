using LegalDraftAI.Models;
using LegalDraftAI.Services;
using LegalDraftAI.Data;
using LegalDraftAI.Data.Entities;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LegalDraftAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DraftsController : ControllerBase
{
    private readonly IDocumentExtractionService _extractor;
    private readonly IGroqDraftService _draftService;
    private readonly IGeminiDraftService _geminiDraftService;
    private readonly IPdfExportService _pdfService;
    private readonly ILogger<DraftsController> _logger;
    private readonly AppDbContext _db;
    private readonly ICaseService _cases;
    private readonly IDocxExportService _docxExport;
    private readonly IDraftFormatMasterService _draftFormatMasters;

    public DraftsController(
        IDocumentExtractionService extractor,
        IGroqDraftService draftService,
        IGeminiDraftService geminiDraftService,
        IPdfExportService pdfService,
        ILogger<DraftsController> logger,
        AppDbContext db,
        ICaseService cases,
        IDocxExportService docxExport,
        IDraftFormatMasterService draftFormatMasters)
    {
        _extractor = extractor;
        _draftService = draftService;
        _geminiDraftService = geminiDraftService;
        _pdfService = pdfService;
        _logger = logger;
        _db = db;
        _cases = cases;
        _docxExport = docxExport;
        _draftFormatMasters = draftFormatMasters;
    }

    /// <summary>
    /// Returns the list of predefined grounds for Absence / Exemption application.
    /// Use this to populate a dropdown in the UI.
    /// </summary>
    [HttpGet("absence-grounds")]
    public IActionResult GetAbsenceGrounds()
    {
        return Ok(AbsenceGrounds.Options.Select(o => new
        {
            code = o.Code,
            label = o.Label
        }));
    }

    /// <summary>
    /// Returns the list of common court documents for Certified Copy application.
    /// Use this to populate a multi-select dropdown in the UI.
    /// </summary>
    [HttpGet("court-documents")]
    public IActionResult GetCourtDocuments()
    {
        return Ok(CourtDocumentTypes.Options.Select(o => new
        {
            code = o.Code,
            label = o.Label
        }));
    }

    /// <summary>Translate extracted document text into Hindi without modifying the stored original.</summary>
    [HttpPost("translate-to-hindi")]
    public async Task<IActionResult> TranslateToHindi(
        [FromBody] HindiTranslationRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Text is required for translation." });
        if (request.Text.Length > 30000)
            return BadRequest(new { error = "Text must be 30,000 characters or fewer for translation." });

        try
        {
            var translatedText = await _draftService.TranslateToHindiAsync(request.Text, ct);
            return Ok(new { translatedText });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Hindi translation provider request failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Hindi translation failed. Check the configured Groq service and try again."
            });
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Hindi translation provider returned invalid JSON.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Hindi translation returned an invalid response. Try again."
            });
        }
    }

    /// <summary>Refine one civil plaint field without adding or changing its substance.</summary>
    [HttpPost("refine-civil-plaint-text")]
    public async Task<IActionResult> RefineCivilPlaintText(
        [FromBody] CivilPlaintTextRefinementRequest request,
        CancellationToken ct)
    {
        var supportedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "facts",
            "propertyDescription",
            "grounds",
            "reliefsSought",
            "prayer"
        };
        if (string.IsNullOrWhiteSpace(request.Field) ||
            !supportedFields.Contains(request.Field))
            return BadRequest(new { error = "Select a supported civil plaint field to refine." });
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Enter text before requesting AI refinement." });
        if (request.Text.Length > 20000)
            return BadRequest(new { error = "Field text must be 20,000 characters or fewer." });

        try
        {
            var refinedText = await _draftService.RefineCivilPlaintTextAsync(
                request.Field,
                request.Text,
                ct);
            return Ok(new { refinedText });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Civil plaint AI text refinement provider request failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "AI refinement failed. Check the configured Groq service and try again."
            });
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Civil plaint AI text refinement returned invalid JSON.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "AI refinement returned an invalid response. Try again."
            });
        }
    }

    /// <summary>
    /// Generate a structured legal draft from plain text facts (no file upload).
    /// For AbsenceApplication you can pass selectedGroundCodes via ExtraFields.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] DraftRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentType))
            return BadRequest(new { error = "DocumentType is required (e.g. BailApplication, AdvocateBill, AbsenceApplication)" });

        if (string.IsNullOrWhiteSpace(request.ExtractedClientFacts))
            return BadRequest(new { error = "ExtractedClientFacts is required" });
        var isCivilPlaint = string.Equals(
            request.DocumentType,
            "CivilPlaint",
            StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(request.AiProvider, "Groq", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.AiProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "AiProvider must be Groq or Gemini." });
        }
        if (!isCivilPlaint &&
            string.Equals(request.AiProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Gemini provider selection is currently available only for CivilPlaint drafts." });
        }
        if (!string.Equals(request.OutputLanguage, "English", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.OutputLanguage, "Hindi", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "OutputLanguage must be English or Hindi." });
        }
        if (request.CivilPlaintDetails is not null &&
            !string.Equals(request.DocumentType, "CivilPlaint", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Civil plaint details can only be saved with a CivilPlaint draft." });
        }
        if (request.CivilPlaintDetails is { } details &&
            (details.CourtAndSuit is null ||
             details.SuitProperty is null ||
             details.FactsAndRelief is null ||
             details.FilingDetails is null))
        {
            return BadRequest(new { error = "All four Civil Plaint detail sections are required." });
        }

        // If user selected grounds from dropdown, enrich ExtraFields with labels
        if (request.DocumentType == "AbsenceApplication" &&
            request.ExtraFields != null &&
            request.ExtraFields.TryGetValue("selectedGroundCodes", out var codes) &&
            !string.IsNullOrWhiteSpace(codes))
        {
            var codeList = codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var labels = codeList.Select(AbsenceGrounds.GetLabel).ToList();
            request.ExtraFields["selectedGroundLabels"] = string.Join(" | ", labels);
        }

        // If user selected documents for Certified Copy application
        if (request.DocumentType == "CertifiedCopyApplication" &&
            request.ExtraFields != null &&
            request.ExtraFields.TryGetValue("selectedDocumentCodes", out var docCodes) &&
            !string.IsNullOrWhiteSpace(docCodes))
        {
            var codeList = docCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var labels = codeList.Select(CourtDocumentTypes.GetLabel).ToList();
            request.ExtraFields["selectedDocumentLabels"] = string.Join(" | ", labels);
        }

        var useGemini = isCivilPlaint &&
                        string.Equals(request.AiProvider, "Gemini", StringComparison.OrdinalIgnoreCase);
        DraftResult result;
        try
        {
            result = useGemini
                ? await _geminiDraftService.GenerateStructuredDraftAsync(request, ct)
                : await _draftService.GenerateStructuredDraftAsync(request, ct);
        }
        catch (HttpRequestException exception) when (useGemini)
        {
            _logger.LogError(exception, "Gemini civil plaint generation request failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Gemini draft generation failed. Check the Gemini service configuration and try again."
            });
        }

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        var saved = await SaveGeneratedDraftAsync(request, result, ct);

        return Ok(new
        {
            data = result.StructuredData,
            rawJson = result.RawJson,
            caseId = saved.CaseId,
            draftId = saved.DraftId,
            usage = new { result.PromptTokens, result.CompletionTokens }
        });
    }

    /// <summary>
    /// Upload one or more documents (PDF / DOCX / TXT), extract text, then generate a structured draft.
    /// </summary>
    [HttpPost("generate-from-files")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> GenerateFromFiles(
        [FromForm] string documentType,
        [FromForm] string jurisdiction,
        List<IFormFile> files,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            return BadRequest(new { error = "documentType is required" });

        if (files == null || files.Count == 0)
            return BadRequest(new { error = "At least one file is required" });

        string extractedFacts;
        try
        {
            extractedFacts = await _extractor.ExtractTextFromMultipleAsync(files, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document extraction failed");
            return BadRequest(new { error = $"Extraction failed: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(extractedFacts))
            return BadRequest(new { error = "No readable text found in the uploaded files" });

        var request = new DraftRequest
        {
            DocumentType = documentType,
            Jurisdiction = string.IsNullOrWhiteSpace(jurisdiction) ? "Delhi" : jurisdiction,
            ExtractedClientFacts = extractedFacts
        };

        var result = await _draftService.GenerateStructuredDraftAsync(request, ct);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        var saved = await SaveGeneratedDraftAsync(request, result, ct);

        return Ok(new
        {
            extractedPreview = extractedFacts.Length > 1000
                ? extractedFacts[..1000] + "..."
                : extractedFacts,
            draft = result.StructuredData,
            rawJson = result.RawJson,
            caseId = saved.CaseId,
            draftId = saved.DraftId,
            usage = new { result.PromptTokens, result.CompletionTokens }
        });
    }

    /// <summary>Extract uploaded files and return their text alongside each checklist code.</summary>
    [HttpPost("extract-mapped-documents")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<IActionResult> ExtractMappedDocuments(
        List<IFormFile> files,
        [FromForm] List<string> documentCodes,
        [FromForm] Guid? caseId,
        [FromForm] string? caseTitle,
        [FromForm] string? plaintNature,
        [FromForm] string? jurisdiction,
        [FromForm] string? courtName,
        [FromForm] string? caseNumber,
        [FromForm] string? plaintiffName,
        [FromForm] string? plaintiffAddress,
        [FromForm] string? defendantName,
        [FromForm] string? defendantAddress,
        [FromForm] string? clientFacts,
        CancellationToken ct)
    {
        if (files.Count == 0 || files.Count != documentCodes.Count)
            return BadRequest(new { error = "Select at least one file and map every file to a checklist document." });

        if (files.Any(file => file.Length == 0) ||
            files.Sum(file => file.Length) > 50L * 1024 * 1024)
        {
            return BadRequest(new { error = "Uploaded files must be non-empty and total no more than 50 MB." });
        }

        try
        {
            var extractedTexts = new List<string>(files.Count);
            foreach (var file in files)
                extractedTexts.Add(await _extractor.ExtractTextAsync(file, ct));

            if (caseId is null)
            {
                var caseTitleValue = string.IsNullOrWhiteSpace(caseTitle)
                    ? $"{plaintiffName ?? "Client"} vs {defendantName ?? "Opposite Party"}"
                    : caseTitle;
                var createdCase = await _cases.CreateAsync(new CreateCaseRequest
                {
                    Title = caseTitleValue,
                    CaseCategory = "Civil",
                    PlaintNature = plaintNature,
                    Jurisdiction = string.IsNullOrWhiteSpace(jurisdiction) ? "India" : jurisdiction,
                    CourtName = courtName,
                    CaseNumber = caseNumber,
                    PlaintiffName = plaintiffName,
                    PlaintiffAddress = plaintiffAddress,
                    DefendantName = defendantName,
                    DefendantAddress = defendantAddress,
                    ClientFacts = clientFacts
                }, ct);
                caseId = createdCase.Id;
            }

            var documents = new List<object>(files.Count);
            for (var index = 0; index < files.Count; index++)
            {
                var saved = await _cases.AddDocumentAsync(
                    caseId.Value,
                    documentCodes[index],
                    files[index],
                    ct,
                    extractedTexts[index]);
                documents.Add(new
                {
                    documentCode = saved.DocumentCode,
                    fileName = saved.OriginalFileName,
                    text = saved.ExtractedText ?? string.Empty,
                    saved.Id,
                    saved.ContentType,
                    saved.FileSizeBytes,
                    saved.UploadedAtUtc
                });
            }

            return Ok(new { caseId, documents });
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidDataException exception)
        {
            _logger.LogWarning(exception, "Uploaded image could not be decoded.");
            return BadRequest(new { error = exception.Message });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Mapped document extraction failed");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Document extraction or database storage failed. Check the case document list before retrying."
            });
        }
    }

    /// <summary>
    /// Convert the final (possibly edited) JSON into a court-ready PDF.
    /// </summary>
    [HttpPost("export-pdf")]
    public IActionResult ExportPdf([FromBody] ExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentType) || string.IsNullOrWhiteSpace(request.Json))
            return BadRequest(new { error = "DocumentType and Json are required" });
        if (!string.Equals(request.OutputLanguage, "English", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.OutputLanguage, "Hindi", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "OutputLanguage must be English or Hindi." });
        }

        try
        {
            var pdfBytes = _pdfService.GenerateFromJson(
                request.DocumentType,
                request.Json,
                request.OutputLanguage);
            var fileName = $"{request.DocumentType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF generation failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Export the edited Civil Plaint preview as an editable Word document.</summary>
    [HttpPost("export-civil-plaint-docx")]
    [Produces("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task<IActionResult> ExportCivilPlaintDocx(
        [FromBody] CivilPlaintDocxExportRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Civil plaint text is required." });
        if (request.Text.Length > 100000)
            return BadRequest(new { error = "Civil plaint text must be 100,000 characters or fewer." });

        try
        {
            byte[]? templateDocx = null;
            if (request.DraftFormatMasterId is Guid masterId)
            {
                var master = await _draftFormatMasters.GetAsync(masterId, cancellationToken);
                if (master is null ||
                    !master.Category.Equals("Civil", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new { error = "The selected Civil draft format was not found." });
                }

                var storedTemplate = await _draftFormatMasters.GetFileAsync(masterId, cancellationToken);
                if (storedTemplate is null)
                {
                    return NotFound(new { error = "The selected Civil draft format or its DOCX file was not found." });
                }

                templateDocx = storedTemplate.Content;
            }

            var docxBytes = _docxExport.GenerateCivilPlaintDocx(
                request.Text,
                templateDocx,
                new Dictionary<string, string>(
                    request.TemplateValues ?? [],
                    StringComparer.OrdinalIgnoreCase));
            return File(
                docxBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "CivilPlaint.docx");
        }
        catch (OpenXmlPackageException exception)
        {
            _logger.LogError(exception, "Civil plaint DOCX generation failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Could not create the Civil Plaint Word document."
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "The selected Civil plaint DOCX template could not be updated.");
            return BadRequest(new { error = "The selected DOCX format could not be used." });
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "The selected Civil plaint DOCX template could not be read.");
            return BadRequest(new { error = "The selected DOCX format could not be read." });
        }
    }

    /// <summary>Extract English and Hindi OCR text from uploaded images.</summary>
    [HttpPost("extract-image-text")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [ProducesResponseType<IReadOnlyList<ImageOcrResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExtractImageText(
        List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return BadRequest(new { error = "Choose at least one image." });
        if (files.Count > 20)
            return BadRequest(new { error = "Upload no more than 20 images at a time." });
        if (files.Any(file => file.Length == 0) ||
            files.Sum(file => file.Length) > 50L * 1024 * 1024)
        {
            return BadRequest(new { error = "Images must be non-empty and total no more than 50 MB." });
        }

        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".gif", ".webp"
        };
        if (files.Any(file => !supportedExtensions.Contains(Path.GetExtension(file.FileName))))
            return BadRequest(new { error = "Upload PNG, JPG, TIFF, BMP, GIF, or WebP images only." });

        try
        {
            var results = new List<ImageOcrResult>(files.Count);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = await _extractor.ExtractTextAsync(file, cancellationToken);
                results.Add(new ImageOcrResult(Path.GetFileName(file.FileName), text));
            }

            return Ok(results);
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidDataException exception)
        {
            _logger.LogWarning(exception, "Image OCR could not decode an uploaded image.");
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Image OCR is not configured correctly.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "English/Hindi OCR is unavailable. Check that both Tesseract language data files are deployed."
            });
        }
    }

    /// <summary>Export reviewed OCR text as an editable Word document.</summary>
    [HttpPost("export-image-ocr-docx")]
    [Produces("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public IActionResult ExportImageOcrDocx([FromBody] ImageOcrDocxExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "OCR text is required." });
        if (request.Text.Length > 100000)
            return BadRequest(new { error = "OCR text must be 100,000 characters or fewer." });

        try
        {
            var docxBytes = _docxExport.GenerateDocx(request.Text);
            return File(
                docxBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "OcrDocument.docx");
        }
        catch (OpenXmlPackageException exception)
        {
            _logger.LogError(exception, "OCR text DOCX generation failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Could not create the OCR Word document."
            });
        }
    }

    /// <summary>Export the reviewed Stage B service application as an editable Word document.</summary>
    [HttpPost("export-stage-b-service-docx")]
    [Produces("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public IActionResult ExportStageBServiceDocx([FromBody] StageBServiceDocxExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Stage B service application text is required." });
        if (request.Text.Length > 100000)
            return BadRequest(new { error = "Stage B service application text must be 100,000 characters or fewer." });

        try
        {
            var docxBytes = _docxExport.GenerateDocx(request.Text);
            return File(
                docxBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "StageBServiceApplication.docx");
        }
        catch (OpenXmlPackageException exception)
        {
            _logger.LogError(exception, "Stage B service application DOCX generation failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Could not create the Stage B service application Word document."
            });
        }
    }

    /// <summary>Export reviewed affidavit text as a paginated PDF.</summary>
    [HttpPost("export-affidavit-pdf")]
    public IActionResult ExportAffidavitPdf([FromBody] AffidavitPdfRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Affidavit text is required." });
        if (request.Text.Length > 30000)
            return BadRequest(new { error = "Affidavit text must be 30,000 characters or fewer." });

        try
        {
            var pdfBytes = _pdfService.GenerateAffidavitPdf(request.Text);
            return File(
                pdfBytes,
                "application/pdf",
                $"SupportingAffidavit_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Supporting affidavit PDF generation failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Could not create the supporting affidavit PDF."
            });
        }
    }

    private async Task<(Guid CaseId, Guid DraftId)> SaveGeneratedDraftAsync(
        DraftRequest request,
        DraftResult result,
        CancellationToken ct)
    {
        var json = result.RawJson
            ?? JsonSerializer.Serialize(result.StructuredData);
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement;

        var plaintiffName = GetString(data, "plaintiffName", "applicantName", "petitionerName", "complainantName", "clientName");
        var defendantName = GetString(data, "defendantName", "respondentName", "oppositePartyName", "accusedName");
        var caseTitle = GetString(data, "caseTitle", "title");
        var title = !string.IsNullOrWhiteSpace(caseTitle)
            ? caseTitle
            : $"{plaintiffName ?? "Client"} vs {defendantName ?? "Opposite Party"}";
        var extraNotes = request.ExtraFields is { Count: > 0 }
            ? JsonSerializer.Serialize(request.ExtraFields)
            : null;

        CaseRecord caseRecord;
        if (request.CaseId is Guid existingCaseId)
        {
            caseRecord = await _db.Cases
                .Include(item => item.CivilPlaintCourtAndSuit)
                .Include(item => item.CivilPlaintSuitProperty)
                .Include(item => item.CivilPlaintFactsAndRelief)
                .Include(item => item.CivilPlaintFilingDetails)
                .FirstOrDefaultAsync(item => item.Id == existingCaseId, ct)
                ?? throw new InvalidOperationException("The supplied case ID does not exist.");
        }
        else
        {
            caseRecord = new CaseRecord
            {
                Title = title.Length <= 500 ? title : title[..500],
                CaseCategory = IsCriminalDocumentType(request.DocumentType) ? "Criminal" : "Civil",
                PlaintNature = GetString(data, "plaintNature") ??
                    request.ExtraFields?.GetValueOrDefault("plaintNature"),
                Jurisdiction = string.IsNullOrWhiteSpace(request.Jurisdiction) ? "India" : request.Jurisdiction,
                CourtName = GetString(data, "courtName"),
                CaseNumber = GetString(data, "caseNumber", "firNumber"),
                PlaintiffName = plaintiffName,
                PlaintiffAddress = GetString(data, "plaintiffAddress", "applicantAddress", "petitionerAddress", "complainantAddress"),
                DefendantName = defendantName,
                DefendantAddress = GetString(data, "defendantAddress", "respondentAddress", "oppositePartyAddress", "accusedAddress"),
                ClientFacts = request.ExtractedClientFacts,
                Notes = extraNotes
            };
        }
        if (request.CaseId is not null)
        {
            caseRecord.Title = title.Length <= 500 ? title : title[..500];
            caseRecord.PlaintNature ??= GetString(data, "plaintNature") ??
                request.ExtraFields?.GetValueOrDefault("plaintNature");
            caseRecord.CourtName ??= GetString(data, "courtName");
            caseRecord.CaseNumber ??= GetString(data, "caseNumber", "firNumber");
            caseRecord.PlaintiffName ??= plaintiffName;
            caseRecord.PlaintiffAddress ??= GetString(data, "plaintiffAddress", "applicantAddress", "petitionerAddress", "complainantAddress");
            caseRecord.DefendantName ??= defendantName;
            caseRecord.DefendantAddress ??= GetString(data, "defendantAddress", "respondentAddress", "oppositePartyAddress", "accusedAddress");
            caseRecord.ClientFacts = request.ExtractedClientFacts;
            if (extraNotes is not null)
                caseRecord.Notes = extraNotes;
            caseRecord.UpdatedAtUtc = DateTime.UtcNow;
        }
        if (request.CivilPlaintDetails is not null)
        {
            CivilPlaintDetailsMapper.Apply(caseRecord, request.CivilPlaintDetails);
            caseRecord.PlaintNature = request.CivilPlaintDetails.PlaintNature ?? caseRecord.PlaintNature;
            caseRecord.Jurisdiction = request.CivilPlaintDetails.Jurisdiction ?? caseRecord.Jurisdiction;
            caseRecord.CourtName = request.CivilPlaintDetails.CourtAndSuit.CourtName ?? caseRecord.CourtName;
            caseRecord.CaseNumber = request.CivilPlaintDetails.CourtAndSuit.CaseNumber ?? caseRecord.CaseNumber;
            caseRecord.PlaintiffName = request.CivilPlaintDetails.CourtAndSuit.PlaintiffName ?? caseRecord.PlaintiffName;
            caseRecord.PlaintiffAddress = request.CivilPlaintDetails.CourtAndSuit.PlaintiffAddress ?? caseRecord.PlaintiffAddress;
            caseRecord.DefendantName = request.CivilPlaintDetails.CourtAndSuit.DefendantName ?? caseRecord.DefendantName;
            caseRecord.DefendantAddress = request.CivilPlaintDetails.CourtAndSuit.DefendantAddress ?? caseRecord.DefendantAddress;
            caseRecord.Title = request.CivilPlaintDetails.CourtAndSuit.CaseTitle ?? caseRecord.Title;
        }
        var nextVersion = await _db.CaseDrafts
            .Where(draft => draft.CaseId == caseRecord.Id &&
                            draft.DocumentType == request.DocumentType)
            .Select(draft => (int?)draft.Version)
            .MaxAsync(ct) ?? 0;
        nextVersion++;
        var caseDraft = new CaseDraft
        {
            Case = caseRecord,
            Version = nextVersion,
            DocumentType = request.DocumentType,
            JsonContent = json
        };

        if (request.CaseId is null)
            _db.Cases.Add(caseRecord);
        _db.CaseDrafts.Add(caseDraft);
        await _db.SaveChangesAsync(ct);

        return (caseRecord.Id, caseDraft.Id);
    }

    private static string? GetString(JsonElement data, params string[] propertyNames)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propertyName in propertyNames)
        {
            if (data.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return null;
    }

    private static bool IsCriminalDocumentType(string documentType) =>
        documentType is "BailApplication" or "AbsenceApplication" or "ComplaintNonRegistrationFIR";
}
