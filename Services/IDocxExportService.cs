namespace LegalDraftAI.Services;

public interface IDocxExportService
{
    byte[] GenerateDocx(string text);

    byte[] GenerateCivilPlaintDocx(
        string text,
        byte[]? templateDocx = null,
        IReadOnlyDictionary<string, string>? templateValues = null);
}
