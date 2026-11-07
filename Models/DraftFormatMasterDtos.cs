using System.ComponentModel.DataAnnotations;

namespace LegalDraftAI.Models;

/// <summary>Request payload to create a reusable draft format master.</summary>
public sealed record CreateDraftFormatMasterRequest
{
    /// <summary>Legal matter category; must be Civil or Criminal.</summary>
    [Required, RegularExpression("^(Civil|Criminal)$")]
    public required string Category { get; init; }

    /// <summary>Display name of the reusable draft format.</summary>
    [Required, StringLength(200)]
    public required string FormatName { get; init; }

    /// <summary>Optional description of the format.</summary>
    [StringLength(1000)]
    public string? Description { get; init; }

}

/// <summary>Form fields used to update a reusable draft format master.</summary>
public sealed record UpdateDraftFormatMasterRequest
{
    /// <summary>Legal matter category; must be Civil or Criminal.</summary>
    [Required, RegularExpression("^(Civil|Criminal)$")]
    public required string Category { get; init; }

    /// <summary>Display name of the reusable draft format.</summary>
    [Required, StringLength(200)]
    public required string FormatName { get; init; }

    /// <summary>Optional description of the format.</summary>
    [StringLength(1000)]
    public string? Description { get; init; }
}

/// <summary>A reusable named draft format stored in the master catalog.</summary>
public sealed record DraftFormatMasterResponse(
    Guid Id,
    string Category,
    string FormatName,
    string? Description,
    string OriginalFileName,
    string DraftFormatPath,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
