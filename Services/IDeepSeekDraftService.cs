using LegalDraftAI.Models;

namespace LegalDraftAI.Services;

public interface IDeepSeekDraftService
{
    Task<DraftResult> GenerateStructuredDraftAsync(DraftRequest request, CancellationToken ct = default);
}
