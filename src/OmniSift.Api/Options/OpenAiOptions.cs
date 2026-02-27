// ============================================================
// OmniSift.Api — OpenAI Configuration Options
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace OmniSift.Api.Options;

/// <summary>
/// Strongly-typed options for the OpenAI API.
/// Bound from the "OpenAI" configuration section.
/// </summary>
public sealed class OpenAiOptions
{
    public const string Section = "OpenAI";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingModel { get; set; } = "text-embedding-3-large";

    public int EmbeddingDimensions { get; set; } = 3072;
}
