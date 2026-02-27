// ============================================================
// OmniSift.Api — Anthropic Configuration Options
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace OmniSift.Api.Options;

/// <summary>
/// Strongly-typed options for the Anthropic API.
/// Bound from the "Anthropic" configuration section.
/// </summary>
public sealed class AnthropicOptions
{
    public const string Section = "Anthropic";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = "claude-sonnet-4-20250514";
}
