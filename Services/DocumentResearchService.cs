using LegalDraftAI.Models;

namespace LegalDraftAI.Services;

public class DocumentResearchService : IDocumentResearchService
{
    public IReadOnlyList<PlaintNatureInfo> GetPlaintNatures(string? category = null)
    {
        if (string.IsNullOrWhiteSpace(category))
            return PlaintDocumentCatalog.PlaintNatures;

        return PlaintDocumentCatalog.PlaintNatures
            .Where(n => n.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyList<RequiredDocumentItem> GetDocumentsForNature(string plaintNatureCode) =>
        PlaintDocumentCatalog.GetDocuments(plaintNatureCode);

    public DocumentResearchResult Analyze(CaseIntakeRequest request)
    {
        var natureCode = request.PlaintNature?.Trim();

        if (string.IsNullOrWhiteSpace(natureCode))
            natureCode = PlaintDocumentCatalog.InferNatureFromFacts(request.ClientFacts);

        if (string.IsNullOrWhiteSpace(natureCode))
        {
            return new DocumentResearchResult
            {
                PlaintNature = "",
                Summary = "Could not determine plaint nature. Please select a plaint type or provide clearer facts (e.g. ancestral land, partition, recovery of money).",
                CanFileWithMinimumSet = false,
                NextSteps =
                {
                    "Select a plaint nature from GET /api/intake/plaint-natures",
                    "Or describe the dispute in ClientFacts (ancestral land, partition, injunction, etc.)"
                }
            };
        }

        var natureInfo = PlaintDocumentCatalog.GetNature(natureCode);
        var docs = PlaintDocumentCatalog.GetDocuments(natureCode).ToList();

        if (natureInfo == null || docs.Count == 0)
        {
            return new DocumentResearchResult
            {
                PlaintNature = natureCode,
                Summary = $"Unknown or unsupported plaint nature: {natureCode}",
                CanFileWithMinimumSet = false,
                NextSteps = { "Use a supported code from GET /api/intake/plaint-natures" }
            };
        }

        var have = new HashSet<string>(
            request.AlreadyHaveDocumentCodes ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var missingMandatory = docs
            .Where(d => d.IsMandatoryForFiling && !have.Contains(d.Code))
            .Select(d => d.Code)
            .ToList();

        var missingRecommended = docs
            .Where(d => !d.IsMandatoryForFiling && !have.Contains(d.Code))
            .Select(d => d.Code)
            .ToList();

        var alreadyHave = docs
            .Where(d => have.Contains(d.Code))
            .Select(d => d.Code)
            .ToList();

        var canFile = missingMandatory.Count == 0;

        var nextSteps = new List<string>();
        if (natureInfo.Category.Equals("Criminal", StringComparison.OrdinalIgnoreCase))
        {
            nextSteps.Add(canFile
                ? "The checklist minimum is complete. Confirm the applicable court requirements before filing."
                : "Collect the listed critical supporting records and confirm any additional court requirements.");
            if (missingRecommended.Count > 0)
                nextSteps.Add("Review the recommended records and include those relevant to the case.");
            nextSteps.Add("Verify all case facts, custody details, and applicable bail procedure with counsel.");
        }
        else if (!canFile)
        {
            nextSteps.Add("Collect all Critical / mandatory documents before filing.");
            foreach (var code in missingMandatory)
            {
                var item = docs.First(d => d.Code == code);
                nextSteps.Add($"Obtain: {item.Label} ({item.Purpose})");
            }
        }
        else
        {
            nextSteps.Add("Minimum set is complete. You may draft/file the plaint.");
            if (missingRecommended.Count > 0)
                nextSteps.Add("Strengthen the case by also collecting recommended documents (High/Medium).");
            nextSteps.Add("Upload documents, extract text, then call POST /api/drafts/generate with documentType CivilPlaint (or your plaint template).");
        }

        if (!string.IsNullOrWhiteSpace(request.Jurisdiction))
            nextSteps.Add($"Confirm court fee and limitation rules for jurisdiction: {request.Jurisdiction}.");

        return new DocumentResearchResult
        {
            PlaintNature = natureInfo.Code,
            PlaintNatureLabel = natureInfo.Label,
            SuggestedDocumentType = natureInfo.SuggestedDocumentType,
            Summary = natureInfo.Description,
            RequiredDocuments = docs,
            MissingMandatory = missingMandatory,
            MissingRecommended = missingRecommended,
            AlreadyHave = alreadyHave,
            CanFileWithMinimumSet = canFile,
            NextSteps = nextSteps
        };
    }
}
