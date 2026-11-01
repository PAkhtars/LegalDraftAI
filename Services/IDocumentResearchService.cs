using LegalDraftAI.Models;

namespace LegalDraftAI.Services;

public interface IDocumentResearchService
{
    IReadOnlyList<PlaintNatureInfo> GetPlaintNatures(string? category = null);
    IReadOnlyList<RequiredDocumentItem> GetDocumentsForNature(string plaintNatureCode);
    DocumentResearchResult Analyze(CaseIntakeRequest request);
}
