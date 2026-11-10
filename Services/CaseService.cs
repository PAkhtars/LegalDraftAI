using System.Text;
using System.Text.Json;
using LegalDraftAI.Data;
using LegalDraftAI.Data.Entities;
using LegalDraftAI.Models;
using Microsoft.EntityFrameworkCore;

namespace LegalDraftAI.Services;

public class CaseService : ICaseService
{
    private const string GeneratedDraftPdfDocumentCode = "GENERATED_DRAFT_PDF";

    private readonly AppDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IDocumentExtractionService _extract;
    private readonly IDocumentResearchService _research;
    private readonly IGroqDraftService _draftAi;
    private readonly IPdfExportService _pdf;

    public CaseService(
        AppDbContext db,
        IFileStorageService files,
        IDocumentExtractionService extract,
        IDocumentResearchService research,
        IGroqDraftService draftAi,
        IPdfExportService pdf)
    {
        _db = db;
        _files = files;
        _extract = extract;
        _research = research;
        _draftAi = draftAi;
        _pdf = pdf;
    }

    public async Task<CaseSummaryDto> CreateAsync(CreateCaseRequest request, CancellationToken ct = default)
    {
        var entity = new CaseRecord
        {
            Title = string.IsNullOrWhiteSpace(request.Title)
                ? $"{request.PlaintiffName ?? "Client"} vs {request.DefendantName ?? "Opposite Party"}"
                : request.Title,
            CaseCategory = request.CaseCategory,
            PlaintNature = request.PlaintNature,
            Jurisdiction = request.Jurisdiction,
            CourtName = request.CourtName,
            CaseNumber = request.CaseNumber,
            PlaintiffName = request.PlaintiffName,
            PlaintiffAddress = request.PlaintiffAddress,
            DefendantName = request.DefendantName,
            DefendantAddress = request.DefendantAddress,
            ClientFacts = request.ClientFacts,
            Notes = request.Notes
        };

        _db.Cases.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToSummary(entity, 0, 0);
    }

    public async Task<CaseDetailDto?> GetAsync(Guid caseId, CancellationToken ct = default)
    {
        return await _db.Cases
            .AsNoTracking()
            .Where(c => c.Id == caseId)
            .Select(c => new CaseDetailDto
            {
                Id = c.Id,
                Title = c.Title,
                CaseCategory = c.CaseCategory,
                PlaintNature = c.PlaintNature,
                Jurisdiction = c.Jurisdiction,
                CourtName = c.CourtName,
                CaseNumber = c.CaseNumber,
                Status = c.Status,
                PlaintiffName = c.PlaintiffName,
                PlaintiffAddress = c.PlaintiffAddress,
                DefendantName = c.DefendantName,
                DefendantAddress = c.DefendantAddress,
                ClientFacts = c.ClientFacts,
                Notes = c.Notes,
                CivilPlaintDetails = c.CivilPlaintCourtAndSuit == null &&
                    c.CivilPlaintSuitProperty == null &&
                    c.CivilPlaintFactsAndRelief == null &&
                    c.CivilPlaintFilingDetails == null
                    ? null
                    : new CivilPlaintDetailsDto
                    {
                        PlaintNature = c.PlaintNature,
                        Jurisdiction = c.Jurisdiction,
                        CourtAndSuit = c.CivilPlaintCourtAndSuit == null
                            ? new CivilPlaintCourtAndSuitDto()
                            : new CivilPlaintCourtAndSuitDto
                            {
                                CourtName = c.CivilPlaintCourtAndSuit.CourtName,
                                CaseTitle = c.CivilPlaintCourtAndSuit.CaseTitle,
                                CaseNumber = c.CivilPlaintCourtAndSuit.CaseNumber,
                                CauseOfActionDate = c.CivilPlaintCourtAndSuit.CauseOfActionDate,
                                PlaintiffName = c.CivilPlaintCourtAndSuit.PlaintiffName,
                                DefendantName = c.CivilPlaintCourtAndSuit.DefendantName,
                                PlaintiffAddress = c.CivilPlaintCourtAndSuit.PlaintiffAddress,
                                DefendantAddress = c.CivilPlaintCourtAndSuit.DefendantAddress
                            },
                        SuitProperty = c.CivilPlaintSuitProperty == null
                            ? new CivilPlaintSuitPropertyDto()
                            : new CivilPlaintSuitPropertyDto
                            {
                                PropertyDescription = c.CivilPlaintSuitProperty.PropertyDescription,
                                SurveyPlotNumber = c.CivilPlaintSuitProperty.SurveyPlotNumber,
                                Area = c.CivilPlaintSuitProperty.Area,
                                Village = c.CivilPlaintSuitProperty.Village,
                                Tehsil = c.CivilPlaintSuitProperty.Tehsil,
                                District = c.CivilPlaintSuitProperty.District,
                                SuitValuationCourtFee = c.CivilPlaintSuitProperty.SuitValuationCourtFee
                            },
                        FactsAndRelief = c.CivilPlaintFactsAndRelief == null
                            ? new CivilPlaintFactsAndReliefDto()
                            : new CivilPlaintFactsAndReliefDto
                            {
                                Facts = c.CivilPlaintFactsAndRelief.Facts,
                                LegalGrounds = c.CivilPlaintFactsAndRelief.LegalGrounds,
                                ReliefsSought = c.CivilPlaintFactsAndRelief.ReliefsSought,
                                Prayer = c.CivilPlaintFactsAndRelief.Prayer
                            },
                        FilingDetails = c.CivilPlaintFilingDetails == null
                            ? new CivilPlaintFilingDetailsDto()
                            : new CivilPlaintFilingDetailsDto
                            {
                                Place = c.CivilPlaintFilingDetails.Place,
                                Date = c.CivilPlaintFilingDetails.Date,
                                AdvocateName = c.CivilPlaintFilingDetails.AdvocateName,
                                EnrollmentNumber = c.CivilPlaintFilingDetails.EnrollmentNumber
                            }
                    },
                DocumentCount = c.Documents.Count,
                DraftCount = c.Drafts.Count,
                CreatedAtUtc = c.CreatedAtUtc,
                StageApplications = c.StageApplications
                    .OrderBy(application => application.StageCode)
                    .ThenBy(application => application.ApplicationCode)
                    .Select(application => new StageApplicationDto
                    {
                        Id = application.Id,
                        StageCode = application.StageCode,
                        ApplicationCode = application.ApplicationCode,
                        DraftContent = application.DraftContent,
                        CreatedAtUtc = application.CreatedAtUtc,
                        UpdatedAtUtc = application.UpdatedAtUtc
                    })
                    .ToList(),
                Documents = c.Documents
                    .OrderByDescending(document => document.UploadedAtUtc)
                    .Select(document => new CaseDocumentDto
                    {
                        Id = document.Id,
                        StageApplicationId = document.StageApplicationId,
                        CaseDraftId = document.CaseDraftId,
                        StageCode = document.StageApplication == null
                            ? null
                            : document.StageApplication.StageCode,
                        ApplicationCode = document.StageApplication == null
                            ? null
                            : document.StageApplication.ApplicationCode,
                        DocumentCode = document.DocumentCode,
                        OriginalFileName = document.OriginalFileName,
                        ContentType = document.ContentType,
                        StoragePath = document.StoragePath,
                        FileSizeBytes = document.FileSizeBytes,
                        HasExtractedText = document.ExtractedText != null &&
                            document.ExtractedText.Trim() != "",
                        ExtractedText = document.ExtractedText,
                        UploadedAtUtc = document.UploadedAtUtc
                    })
                    .ToList(),
                Drafts = c.Drafts
                    .OrderByDescending(draft => draft.CreatedAtUtc)
                    .Select(draft => new CaseDraftDto
                    {
                        Id = draft.Id,
                        StageApplicationId = draft.StageApplicationId,
                        StageCode = draft.StageApplication == null
                            ? null
                            : draft.StageApplication.StageCode,
                        DocumentType = draft.DocumentType,
                        ApplicationCode = draft.ApplicationCode,
                        Version = draft.Version,
                        Status = draft.Status,
                        PdfStoragePath = draft.PdfStoragePath,
                        HasPdf = draft.PdfContent != null ||
                            (draft.PdfStoragePath != null && draft.PdfStoragePath != "") ||
                            (draft.StageApplicationId != null &&
                             _db.CaseDocuments.Any(document =>
                                 document.CaseDraftId == draft.Id &&
                                 document.DocumentCode == GeneratedDraftPdfDocumentCode)),
                        CreatedAtUtc = draft.CreatedAtUtc
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CaseSummaryDto>> ListAsync(
        string? status = null,
        string? documentType = null,
        CancellationToken ct = default)
    {
        var q = _db.Cases.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(documentType))
            q = q.Where(x => x.Drafts.Any(draft => draft.DocumentType == documentType));
        if (string.Equals(documentType, "CivilPlaint", StringComparison.OrdinalIgnoreCase))
            q = q.Where(x => x.CaseCategory == "Civil");

        var list = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CaseSummaryDto
            {
                Id = x.Id,
                Title = x.Title,
                CaseCategory = x.CaseCategory,
                PlaintNature = x.PlaintNature,
                Jurisdiction = x.Jurisdiction,
                CourtName = x.CourtName,
                CaseNumber = x.CaseNumber,
                Status = x.Status,
                PlaintiffName = x.PlaintiffName,
                DefendantName = x.DefendantName,
                DocumentCount = x.Documents.Count,
                DraftCount = x.Drafts.Count,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .Take(100)
            .ToListAsync(ct);

        return list;
    }

    public async Task<CaseSummaryDto?> UpdateAsync(Guid caseId, UpdateCaseRequest request, CancellationToken ct = default)
    {
        var c = await _db.Cases.FirstOrDefaultAsync(x => x.Id == caseId, ct);
        if (c == null) return null;

        if (request.Title != null) c.Title = request.Title;
        if (request.CourtName != null) c.CourtName = request.CourtName;
        if (request.CaseNumber != null) c.CaseNumber = request.CaseNumber;
        if (request.Status != null) c.Status = request.Status;
        if (request.PlaintiffName != null) c.PlaintiffName = request.PlaintiffName;
        if (request.PlaintiffAddress != null) c.PlaintiffAddress = request.PlaintiffAddress;
        if (request.DefendantName != null) c.DefendantName = request.DefendantName;
        if (request.DefendantAddress != null) c.DefendantAddress = request.DefendantAddress;
        if (request.ClientFacts != null) c.ClientFacts = request.ClientFacts;
        if (request.Notes != null) c.Notes = request.Notes;
        if (request.PlaintNature != null) c.PlaintNature = request.PlaintNature;
        c.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        var docCount = await _db.CaseDocuments.CountAsync(x => x.CaseId == caseId, ct);
        var draftCount = await _db.CaseDrafts.CountAsync(x => x.CaseId == caseId, ct);
        return ToSummary(c, docCount, draftCount);
    }

    public async Task<CaseDetailDto?> SaveCivilPlaintDetailsAsync(
        Guid caseId,
        CivilPlaintDetailsDto details,
        CancellationToken ct = default)
    {
        var caseRecord = await _db.Cases
            .Include(item => item.CivilPlaintCourtAndSuit)
            .Include(item => item.CivilPlaintSuitProperty)
            .Include(item => item.CivilPlaintFactsAndRelief)
            .Include(item => item.CivilPlaintFilingDetails)
            .FirstOrDefaultAsync(item => item.Id == caseId, ct);
        if (caseRecord is null)
            return null;

        CivilPlaintDetailsMapper.Apply(caseRecord, details);
        if (details.PlaintNature is not null)
            caseRecord.PlaintNature = details.PlaintNature;
        if (details.Jurisdiction is not null)
            caseRecord.Jurisdiction = details.Jurisdiction;
        caseRecord.Title = details.CourtAndSuit.CaseTitle ?? caseRecord.Title;
        caseRecord.CourtName = details.CourtAndSuit.CourtName ?? caseRecord.CourtName;
        caseRecord.CaseNumber = details.CourtAndSuit.CaseNumber ?? caseRecord.CaseNumber;
        caseRecord.PlaintiffName = details.CourtAndSuit.PlaintiffName ?? caseRecord.PlaintiffName;
        caseRecord.PlaintiffAddress = details.CourtAndSuit.PlaintiffAddress ?? caseRecord.PlaintiffAddress;
        caseRecord.DefendantName = details.CourtAndSuit.DefendantName ?? caseRecord.DefendantName;
        caseRecord.DefendantAddress = details.CourtAndSuit.DefendantAddress ?? caseRecord.DefendantAddress;
        caseRecord.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(caseId, ct);
    }

    public async Task<CaseDetailDto?> SaveCivilPlaintSectionAsync(
        Guid caseId,
        string section,
        CivilPlaintDetailsDto details,
        CancellationToken ct = default)
    {
        var caseRecord = await _db.Cases
            .Include(item => item.CivilPlaintCourtAndSuit)
            .Include(item => item.CivilPlaintSuitProperty)
            .Include(item => item.CivilPlaintFactsAndRelief)
            .Include(item => item.CivilPlaintFilingDetails)
            .FirstOrDefaultAsync(item => item.Id == caseId, ct);
        if (caseRecord is null)
            return null;

        switch (section.ToLowerInvariant())
        {
            case "court-and-suit":
                var courtAndSuit = caseRecord.CivilPlaintCourtAndSuit ??= new();
                courtAndSuit.CourtName = details.CourtAndSuit.CourtName;
                courtAndSuit.CaseTitle = details.CourtAndSuit.CaseTitle;
                courtAndSuit.CaseNumber = details.CourtAndSuit.CaseNumber;
                courtAndSuit.CauseOfActionDate = details.CourtAndSuit.CauseOfActionDate;
                courtAndSuit.PlaintiffName = details.CourtAndSuit.PlaintiffName;
                courtAndSuit.DefendantName = details.CourtAndSuit.DefendantName;
                courtAndSuit.PlaintiffAddress = details.CourtAndSuit.PlaintiffAddress;
                courtAndSuit.DefendantAddress = details.CourtAndSuit.DefendantAddress;
                caseRecord.Title = details.CourtAndSuit.CaseTitle ?? caseRecord.Title;
                caseRecord.CourtName = details.CourtAndSuit.CourtName ?? caseRecord.CourtName;
                caseRecord.CaseNumber = details.CourtAndSuit.CaseNumber ?? caseRecord.CaseNumber;
                caseRecord.PlaintiffName = details.CourtAndSuit.PlaintiffName ?? caseRecord.PlaintiffName;
                caseRecord.PlaintiffAddress = details.CourtAndSuit.PlaintiffAddress ?? caseRecord.PlaintiffAddress;
                caseRecord.DefendantName = details.CourtAndSuit.DefendantName ?? caseRecord.DefendantName;
                caseRecord.DefendantAddress = details.CourtAndSuit.DefendantAddress ?? caseRecord.DefendantAddress;
                break;
            case "suit-property":
                var suitProperty = caseRecord.CivilPlaintSuitProperty ??= new();
                suitProperty.PropertyDescription = details.SuitProperty.PropertyDescription;
                suitProperty.SurveyPlotNumber = details.SuitProperty.SurveyPlotNumber;
                suitProperty.Area = details.SuitProperty.Area;
                suitProperty.Village = details.SuitProperty.Village;
                suitProperty.Tehsil = details.SuitProperty.Tehsil;
                suitProperty.District = details.SuitProperty.District;
                suitProperty.SuitValuationCourtFee = details.SuitProperty.SuitValuationCourtFee;
                break;
            case "facts-and-relief":
                var factsAndRelief = caseRecord.CivilPlaintFactsAndRelief ??= new();
                factsAndRelief.Facts = details.FactsAndRelief.Facts;
                factsAndRelief.LegalGrounds = details.FactsAndRelief.LegalGrounds;
                factsAndRelief.ReliefsSought = details.FactsAndRelief.ReliefsSought;
                factsAndRelief.Prayer = details.FactsAndRelief.Prayer;
                caseRecord.ClientFacts = details.FactsAndRelief.Facts ?? caseRecord.ClientFacts;
                break;
            case "filing-details":
                var filingDetails = caseRecord.CivilPlaintFilingDetails ??= new();
                filingDetails.Place = details.FilingDetails.Place;
                filingDetails.Date = details.FilingDetails.Date;
                filingDetails.AdvocateName = details.FilingDetails.AdvocateName;
                filingDetails.EnrollmentNumber = details.FilingDetails.EnrollmentNumber;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, "Unsupported Civil Plaint section.");
        }

        if (details.PlaintNature is not null)
            caseRecord.PlaintNature = details.PlaintNature;
        if (details.Jurisdiction is not null)
            caseRecord.Jurisdiction = details.Jurisdiction;
        caseRecord.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(caseId, ct);
    }

    public async Task<CaseDocumentDto> AddDocumentAsync(
        Guid caseId,
        string documentCode,
        IFormFile file,
        CancellationToken ct = default,
        string? extractedText = null,
        string? stageCode = null,
        string? applicationCode = null)
    {
        var exists = await _db.Cases.AnyAsync(x => x.Id == caseId, ct);
        if (!exists) throw new InvalidOperationException("Case not found");
        var stageApplication = await GetOrCreateStageApplicationAsync(
            caseId,
            stageCode,
            applicationCode,
            ct);

        if (extractedText is null)
        {
            await using var extractionStream = file.OpenReadStream();
            extractedText = await _extract.ExtractTextFromStreamAsync(
                extractionStream,
                file.FileName,
                ct);
        }

        await using var contentStream = new MemoryStream();
        await file.CopyToAsync(contentStream, ct);

        var doc = new CaseDocument
        {
            CaseId = caseId,
            StageApplication = stageApplication,
            DocumentCode = string.IsNullOrWhiteSpace(documentCode) ? "OTHER" : documentCode.Trim().ToUpperInvariant(),
            OriginalFileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
            FileSizeBytes = file.Length,
            StoragePath = "",
            FileContent = contentStream.ToArray(),
            ExtractedText = Truncate(extractedText, 50000)
        };

        _db.CaseDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        return new CaseDocumentDto
        {
            Id = doc.Id,
            StageApplicationId = doc.StageApplicationId,
            StageCode = stageApplication?.StageCode,
            ApplicationCode = stageApplication?.ApplicationCode,
            DocumentCode = doc.DocumentCode,
            OriginalFileName = doc.OriginalFileName,
            ContentType = doc.ContentType,
            StoragePath = doc.StoragePath,
            FileSizeBytes = doc.FileSizeBytes,
            HasExtractedText = !string.IsNullOrWhiteSpace(doc.ExtractedText),
            ExtractedText = doc.ExtractedText,
            UploadedAtUtc = doc.UploadedAtUtc
        };
    }

    public async Task<StoredCaseDocumentFile?> GetDocumentFileAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken ct = default)
    {
        var document = await _db.CaseDocuments
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.Id == documentId)
            .Select(item => new
            {
                item.OriginalFileName,
                item.ContentType,
                item.StoragePath,
                item.FileContent
            })
            .FirstOrDefaultAsync(ct);
        if (document is null)
            return null;

        var content = document.FileContent ??
            (string.IsNullOrWhiteSpace(document.StoragePath)
                ? null
                : await _files.ReadAsync(document.StoragePath, ct));
        if (content is null)
            return null;

        return new StoredCaseDocumentFile
        {
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            Content = content
        };
    }

    public async Task<bool> DeleteDocumentAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken ct = default)
    {
        var storagePath = await _db.CaseDocuments
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.Id == documentId)
            .Select(item => item.StoragePath)
            .FirstOrDefaultAsync(ct);
        if (storagePath is null)
            return false;

        if (!string.IsNullOrWhiteSpace(storagePath))
            await _files.DeleteCaseDocumentAsync(caseId, storagePath, ct);
        return await _db.CaseDocuments
            .Where(item => item.CaseId == caseId && item.Id == documentId)
            .ExecuteDeleteAsync(ct) > 0;
    }

    public async Task<DocumentResearchResult> ResearchAsync(Guid caseId, CancellationToken ct = default)
    {
        var caseData = await _db.Cases
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new
            {
                x.Jurisdiction,
                x.CaseCategory,
                x.PlaintNature,
                x.ClientFacts,
                DocumentCodes = x.Documents.Select(document => document.DocumentCode).Distinct().ToList()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Case not found");

        return _research.Analyze(new CaseIntakeRequest
        {
            Jurisdiction = caseData.Jurisdiction,
            CaseCategory = caseData.CaseCategory,
            PlaintNature = caseData.PlaintNature,
            ClientFacts = caseData.ClientFacts,
            AlreadyHaveDocumentCodes = caseData.DocumentCodes
        });
    }

    public async Task<CaseDraftDetailDto> GenerateDraftAsync(Guid caseId, GenerateCaseDraftRequest request, CancellationToken ct = default)
    {
        var c = await _db.Cases
            .FirstOrDefaultAsync(x => x.Id == caseId, ct)
            ?? throw new InvalidOperationException("Case not found");
        var stageApplication = await GetOrCreateStageApplicationAsync(
            caseId,
            request.StageCode,
            request.ApplicationCode,
            ct);
        if (request.ApplicationCode?.Length > 100)
            throw new ArgumentException("ApplicationCode must be 100 characters or fewer.");

        var documents = await _db.CaseDocuments
            .AsNoTracking()
            .Where(document => document.CaseId == caseId &&
                (stageApplication == null ||
                    document.StageApplicationId == null ||
                    document.StageApplicationId == stageApplication.Id) &&
                !request.ExcludedDocumentIds.Contains(document.Id))
            .Select(document => new
            {
                document.DocumentCode,
                document.OriginalFileName,
                document.ExtractedText
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(c.ClientFacts))
            sb.AppendLine(c.ClientFacts);
        sb.AppendLine($"Plaintiff: {c.PlaintiffName}; Address: {c.PlaintiffAddress}");
        sb.AppendLine($"Defendant: {c.DefendantName}; Address: {c.DefendantAddress}");
        if (!string.IsNullOrWhiteSpace(c.CourtName)) sb.AppendLine($"Court: {c.CourtName}");
        if (!string.IsNullOrWhiteSpace(c.CaseNumber)) sb.AppendLine($"Case No.: {c.CaseNumber}");
        if (!string.IsNullOrWhiteSpace(request.ExtraInstructions))
            sb.AppendLine(request.ExtraInstructions);

        foreach (var doc in documents.Where(d => !string.IsNullOrWhiteSpace(d.ExtractedText)))
        {
            sb.AppendLine($"--- Document [{doc.DocumentCode}] {doc.OriginalFileName} ---");
            sb.AppendLine(Truncate(doc.ExtractedText, 8000));
        }

        var extra = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(c.PlaintNature))
            extra["plaintNature"] = c.PlaintNature!;

        var result = await _draftAi.GenerateStructuredDraftAsync(new DraftRequest
        {
            DocumentType = request.DocumentType,
            Jurisdiction = c.Jurisdiction,
            ExtractedClientFacts = sb.ToString(),
            ExtraFields = extra
        }, ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.RawJson))
            throw new InvalidOperationException(result.Error ?? "Draft generation failed");

        var json = result.RawJson;

        var nextVersion = (await _db.CaseDrafts
            .Where(draft => draft.CaseId == caseId &&
                draft.DocumentType == request.DocumentType &&
                (stageApplication == null ||
                    draft.StageApplicationId == stageApplication.Id))
            .Select(draft => (int?)draft.Version)
            .MaxAsync(ct) ?? 0) + 1;

        byte[]? pdfContent = null;
        byte[]? stagePdfContent = null;
        string? stagePdfPath = null;
        if (stageApplication is not null)
        {
            stagePdfContent = GenerateStageDraftPdf(request.DocumentType, json);
        }
        else if (request.SavePdf)
        {
            try
            {
                pdfContent = _pdf.GenerateFromJson(request.DocumentType, json);
            }
            catch
            {
                // PDF remains optional for non-stage drafts.
            }
        }

        var draftId = Guid.NewGuid();
        var pdfFileName = stageApplication is null
            ? null
            : CreateStageDraftPdfFileName(stageApplication, nextVersion, draftId);
        if (stageApplication is not null && stagePdfContent is not null && pdfFileName is not null)
        {
            stagePdfPath = await _files.SaveCaseDraftPdfAsync(
                caseId,
                pdfFileName,
                stagePdfContent,
                ct);
        }

        var draft = new CaseDraft
        {
            Id = draftId,
            CaseId = caseId,
            StageApplication = stageApplication,
            DocumentType = request.DocumentType,
            ApplicationCode = stageApplication?.ApplicationCode ?? request.ApplicationCode?.Trim(),
            Version = nextVersion,
            Status = "Draft",
            JsonContent = json,
            PdfContent = pdfContent,
            PdfStoragePath = stagePdfPath
        };

        if (stageApplication?.StageCode == "StageA" &&
            request.DocumentType.Equals("CivilStageAApplication", StringComparison.OrdinalIgnoreCase))
        {
            stageApplication.DraftContent = json;
            stageApplication.UpdatedAtUtc = DateTime.UtcNow;
        }

        _db.CaseDrafts.Add(draft);
        if (stageApplication is not null && stagePdfContent is not null)
        {
            _db.CaseDocuments.Add(new CaseDocument
            {
                CaseId = caseId,
                StageApplication = stageApplication,
                CaseDraftId = draft.Id,
                DocumentCode = GeneratedDraftPdfDocumentCode,
                OriginalFileName = pdfFileName!,
                ContentType = "application/pdf",
                FileSizeBytes = stagePdfContent.LongLength,
                StoragePath = stagePdfPath!,
                FileContent = stagePdfContent
            });
        }
        c.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new CaseDraftDetailDto
        {
            Id = draft.Id,
            StageApplicationId = draft.StageApplicationId,
            StageCode = stageApplication?.StageCode,
            DocumentType = draft.DocumentType,
            Version = draft.Version,
            Status = draft.Status,
            ApplicationCode = draft.ApplicationCode,
            PdfStoragePath = draft.PdfStoragePath,
            HasPdf = draft.PdfContent is not null ||
                !string.IsNullOrWhiteSpace(draft.PdfStoragePath) ||
                stagePdfContent is not null,
            CreatedAtUtc = draft.CreatedAtUtc,
            JsonContent = draft.JsonContent
        };
    }

    public async Task<CaseDraftDetailDto?> GetDraftAsync(Guid caseId, Guid draftId, CancellationToken ct = default)
    {
        return await _db.CaseDrafts
            .AsNoTracking()
            .Where(draft => draft.CaseId == caseId && draft.Id == draftId)
            .Select(draft => new CaseDraftDetailDto
            {
                Id = draft.Id,
                StageApplicationId = draft.StageApplicationId,
                StageCode = draft.StageApplication == null
                    ? null
                    : draft.StageApplication.StageCode,
                DocumentType = draft.DocumentType,
                Version = draft.Version,
                Status = draft.Status,
                ApplicationCode = draft.ApplicationCode,
                PdfStoragePath = draft.PdfStoragePath,
                HasPdf = draft.PdfContent != null ||
                    (draft.PdfStoragePath != null && draft.PdfStoragePath != "") ||
                    (draft.StageApplicationId != null &&
                     _db.CaseDocuments.Any(document =>
                         document.CaseDraftId == draft.Id &&
                         document.DocumentCode == GeneratedDraftPdfDocumentCode)),
                CreatedAtUtc = draft.CreatedAtUtc,
                JsonContent = draft.JsonContent
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<byte[]?> GetDraftPdfAsync(Guid caseId, Guid draftId, CancellationToken ct = default)
    {
        var draft = await _db.CaseDrafts
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.Id == draftId)
            .Select(item => new
            {
                item.PdfContent,
                item.PdfStoragePath,
                item.StageApplicationId,
                item.Version,
                item.DocumentType,
                item.JsonContent
            })
            .FirstOrDefaultAsync(ct);
        if (draft is null)
            return null;
        if (draft.PdfContent is not null)
            return draft.PdfContent;
        if (!string.IsNullOrWhiteSpace(draft.PdfStoragePath))
        {
            var legacyPdf = await _files.ReadAsync(draft.PdfStoragePath, ct);
            if (legacyPdf is not null || draft.StageApplicationId is null)
                return legacyPdf;
        }

        if (draft.StageApplicationId is not Guid stageApplicationId)
            return null;

        var storedPdf = await _db.CaseDocuments
            .AsNoTracking()
            .Where(document =>
                document.CaseDraftId == draftId &&
                document.DocumentCode == GeneratedDraftPdfDocumentCode)
            .Select(document => document.FileContent)
            .FirstOrDefaultAsync(ct);
        if (storedPdf is not null)
        {
            var applicationWithDraft = await _db.CaseDrafts
                .AsNoTracking()
                .Where(item => item.Id == draftId)
                .Join(
                    _db.StageApplications.AsNoTracking(),
                    item => item.StageApplicationId,
                    application => (Guid?)application.Id,
                    (item, application) => new { item, application })
                .FirstAsync(ct);
            var storedFileName = CreateStageDraftPdfFileName(
                applicationWithDraft.application,
                applicationWithDraft.item.Version,
                draftId);
            var storedPath = await _files.SaveCaseDraftPdfAsync(
                caseId,
                storedFileName,
                storedPdf,
                ct);

            await _db.CaseDrafts
                .Where(item => item.Id == draftId &&
                    (item.PdfStoragePath == null || item.PdfStoragePath == ""))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.PdfStoragePath, storedPath),
                    ct);
            await _db.CaseDocuments
                .Where(document =>
                    document.CaseDraftId == draftId &&
                    document.DocumentCode == GeneratedDraftPdfDocumentCode)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(document => document.StoragePath, storedPath),
                    ct);
            return storedPdf;
        }

        var pdfContent = GenerateStageDraftPdf(draft.DocumentType, draft.JsonContent);
        var application = await _db.StageApplications
            .AsNoTracking()
            .FirstAsync(item => item.Id == stageApplicationId, ct);
        var fileName = CreateStageDraftPdfFileName(application, draft.Version, draftId);
        var storagePath = await _files.SaveCaseDraftPdfAsync(caseId, fileName, pdfContent, ct);
        var documentEntity = new CaseDocument
        {
            CaseId = caseId,
            StageApplicationId = stageApplicationId,
            CaseDraftId = draftId,
            DocumentCode = GeneratedDraftPdfDocumentCode,
            OriginalFileName = fileName,
            ContentType = "application/pdf",
            FileSizeBytes = pdfContent.LongLength,
            StoragePath = storagePath,
            FileContent = pdfContent
        };
        _db.CaseDocuments.Add(documentEntity);
        await _db.CaseDrafts
            .Where(item => item.Id == draftId &&
                (item.PdfStoragePath == null || item.PdfStoragePath == ""))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.PdfStoragePath, storagePath),
                ct);
        await _db.SaveChangesAsync(ct);
        return pdfContent;
    }

    private static string CreateStageDraftPdfFileName(
        StageApplication application,
        int version,
        Guid draftId)
    {
        var safeApplicationCode = string.Concat(application.ApplicationCode.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_'));
        return $"{application.StageCode}_{safeApplicationCode}_v{version}_{draftId:N}.pdf";
    }

    private byte[] GenerateStageDraftPdf(string documentType, string json)
    {
        if (documentType.Equals("CivilSupportingAffidavit", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("affidavitText", out var affidavitText) ||
                affidavitText.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(affidavitText.GetString()))
            {
                throw new InvalidOperationException(
                    "The generated supporting affidavit does not contain readable affidavitText.");
            }

            return _pdf.GenerateAffidavitPdf(affidavitText.GetString()!);
        }

        return _pdf.GenerateFromJson(documentType, json);
    }

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);

    private async Task<StageApplication?> GetOrCreateStageApplicationAsync(
        Guid caseId,
        string? stageCode,
        string? applicationCode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stageCode))
            return null;
        if (string.IsNullOrWhiteSpace(applicationCode))
            throw new ArgumentException("ApplicationCode is required when StageCode is provided.");

        var normalizedStageCode = stageCode.Trim();
        if (normalizedStageCode.Equals("StageA", StringComparison.OrdinalIgnoreCase))
            normalizedStageCode = "StageA";
        else if (normalizedStageCode.Equals("StageB", StringComparison.OrdinalIgnoreCase))
            normalizedStageCode = "StageB";
        else if (normalizedStageCode.Equals("StageC", StringComparison.OrdinalIgnoreCase))
            normalizedStageCode = "StageC";
        else
            throw new ArgumentException("StageCode must be StageA, StageB, or StageC.");

        var normalizedApplicationCode = applicationCode.Trim();
        if (normalizedApplicationCode.Length > 100)
            throw new ArgumentException("ApplicationCode must be 100 characters or fewer.");

        var existing = await _db.StageApplications.FirstOrDefaultAsync(
            application => application.CaseId == caseId &&
                application.StageCode == normalizedStageCode &&
                application.ApplicationCode == normalizedApplicationCode,
            ct);
        if (existing is not null)
        {
            existing.UpdatedAtUtc = DateTime.UtcNow;
            return existing;
        }

        var stageApplication = new StageApplication
        {
            CaseId = caseId,
            StageCode = normalizedStageCode,
            ApplicationCode = normalizedApplicationCode
        };
        _db.StageApplications.Add(stageApplication);
        return stageApplication;
    }

    private static CaseSummaryDto ToSummary(CaseRecord c, int docs, int drafts) => new()
    {
        Id = c.Id,
        Title = c.Title,
        CaseCategory = c.CaseCategory,
        PlaintNature = c.PlaintNature,
        Jurisdiction = c.Jurisdiction,
        CourtName = c.CourtName,
        CaseNumber = c.CaseNumber,
        Status = c.Status,
        PlaintiffName = c.PlaintiffName,
        DefendantName = c.DefendantName,
        DocumentCount = docs,
        DraftCount = drafts,
        CreatedAtUtc = c.CreatedAtUtc
    };

}
