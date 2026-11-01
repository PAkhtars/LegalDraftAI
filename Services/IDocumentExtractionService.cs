namespace LegalDraftAI.Services;

public interface IDocumentExtractionService
{
    Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct = default);
    Task<string> ExtractTextFromMultipleAsync(IEnumerable<IFormFile> files, CancellationToken ct = default);
    Task<string> ExtractTextFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default);
}
