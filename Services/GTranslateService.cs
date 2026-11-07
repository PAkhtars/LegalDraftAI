using GTranslate.Translators;
using LegalDraftAI.Services;

namespace LegalDraftAI.Services;
public class GTranslateService : ITranslationService
{
    private readonly GoogleTranslator _translator;

    public GTranslateService(GoogleTranslator translator)
    {
        _translator = translator;
    }

    public async Task<string> ConvertToHindiAsync(string text)
    {
        var result = await _translator.TranslateAsync(text, "hi");
        return result.Translation;
    }
}