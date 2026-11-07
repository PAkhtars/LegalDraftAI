using LegalDraftAI.Models;

namespace LegalDraftAI.Services;

public interface IGroqDraftService
{
    Task<DraftResult> GenerateStructuredDraftAsync(DraftRequest request, CancellationToken ct = default);
    Task<string> TranslateToHindiAsync(string text, CancellationToken ct = default);
    Task<string> RefineCivilPlaintTextAsync(string field, string text, CancellationToken ct = default);
}
