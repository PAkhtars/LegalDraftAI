using LegalDraftAI.Models;

namespace LegalDraftAI.Services;

public interface IGeminiDraftService
{
    Task<DraftResult> GenerateStructuredDraftAsync(
        DraftRequest request,
        CancellationToken ct = default);
}
