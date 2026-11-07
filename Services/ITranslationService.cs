namespace LegalDraftAI.Services;
public interface ITranslationService
{
    Task<string> ConvertToHindiAsync(string text);
}