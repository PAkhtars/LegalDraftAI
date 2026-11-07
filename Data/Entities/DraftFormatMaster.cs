namespace LegalDraftAI.Data.Entities;

/// <summary>A reusable named draft format for a civil or criminal matter.</summary>
public sealed class DraftFormatMaster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = "Civil";
    public string FormatName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DraftFormatPath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public long FileSizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
