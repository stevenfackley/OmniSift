// ============================================================
// OmniSift.Shared — Agent Query DTOs
// Request/Response contracts for the AI research agent
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace OmniSift.Shared.DTOs;

/// <summary>
/// Request DTO for submitting a research query to the agent.
/// </summary>
public sealed record AgentQueryRequest
{
    /// <summary>
    /// The natural language research query.
    /// </summary>
    [Required]
    [MinLength(3, ErrorMessage = "Query must be at least 3 characters.")]
    [MaxLength(5000, ErrorMessage = "Query must not exceed 5000 characters.")]
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Optional conversation history for multi-turn context.
    /// </summary>
    public List<ConversationMessage>? ConversationHistory { get; init; }
}

/// <summary>
/// A single message in the conversation history.
/// </summary>
public sealed record ConversationMessage
{
    [Required]
    public string Role { get; init; } = string.Empty; // "user" or "assistant"

    [Required]
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Response DTO from the AI research agent.
/// </summary>
public sealed record AgentQueryResponse
{
    /// <summary>
    /// The agent's synthesized response text.
    /// </summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>
    /// Sources cited in the response.
    /// </summary>
    public List<SourceCitation> Sources { get; init; } = [];

    /// <summary>
    /// Names of Semantic Kernel plugins invoked during this query.
    /// </summary>
    public List<string> PluginsUsed { get; init; } = [];

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public int DurationMs { get; init; }
}

/// <summary>
/// A citation to a source used in the agent's response.
/// </summary>
public sealed record SourceCitation
{
    public string Type { get; init; } = string.Empty; // "document", "web", "archive"
    public Guid? DataSourceId { get; init; }
    public Guid? ChunkId { get; init; }
    public string? Url { get; init; }
    public string? Title { get; init; }
    public double? RelevanceScore { get; init; }
}
