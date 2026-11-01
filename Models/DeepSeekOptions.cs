namespace LegalDraftAI.Models;

public class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "deepseek-chat";
}
