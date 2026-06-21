// ============================================================
// OmniSift.Api — Research Report Builder
// Pure function: (conversation, citations) → Markdown string.
// No I/O, no DI dependencies — fully unit-testable.
// ============================================================

using System.Text;
using OmniSift.Shared.DTOs;

namespace OmniSift.Api.Services;

/// <summary>
/// A single turn in the conversation to export.
/// </summary>
public sealed record ReportMessage
{
    /// <summary>"user" or "assistant"</summary>
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public IReadOnlyList<SourceCitation> Citations { get; init; } = [];
}

/// <summary>
/// Input model for <see cref="ResearchReportBuilder.Build"/>.
/// </summary>
public sealed record ReportRequest
{
    public string Title { get; init; } = "Research Report";
    public IReadOnlyList<ReportMessage> Messages { get; init; } = [];
    /// <summary>
    /// ISO 8601 timestamp string.  If null the builder uses UTC now.
    /// </summary>
    public string? Timestamp { get; init; }
}

/// <summary>
/// Stateless Markdown report builder.
/// </summary>
public static class ResearchReportBuilder
{
    /// <summary>
    /// Builds a Markdown research report from the supplied conversation.
    /// </summary>
    /// <param name="request">Conversation + metadata.</param>
    /// <param name="utcNow">
    /// UTC timestamp to embed in the report.  Passed explicitly so tests
    /// can pin the value and make assertions deterministic.
    /// </param>
    public static string Build(ReportRequest request, DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var timestamp = request.Timestamp
            ?? (utcNow ?? DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var sb = new StringBuilder(2048);

        // ── Title block ────────────────────────────────────────
        sb.AppendLine($"# {EscapeMarkdown(request.Title)}");
        sb.AppendLine();
        sb.AppendLine($"*Generated: {timestamp}*");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── Conversation turns ─────────────────────────────────
        var assistantTurns = request.Messages
            .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (assistantTurns.Count == 0)
        {
            sb.AppendLine("*No assistant responses to report.*");
        }
        else
        {
            // Collect all citations across turns for the global bibliography
            var allCitations = new List<(int Index, SourceCitation Citation)>();
            // Map citation dedup key → footnote number (1-based)
            var footnoteLookup = new Dictionary<string, int>(StringComparer.Ordinal);

            // First pass: assign footnote numbers globally
            foreach (var msg in request.Messages)
            {
                foreach (var c in msg.Citations)
                {
                    var key = CitationKey(c);
                    if (!footnoteLookup.ContainsKey(key))
                    {
                        var idx = footnoteLookup.Count + 1;
                        footnoteLookup[key] = idx;
                        allCitations.Add((idx, c));
                    }
                }
            }

            // Second pass: render turns
            var turnNumber = 0;
            foreach (var msg in request.Messages)
            {
                if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    turnNumber++;
                    sb.AppendLine($"## Query {turnNumber}");
                    sb.AppendLine();
                    sb.AppendLine($"> {EscapeBlockquote(msg.Content)}");
                    sb.AppendLine();
                }
                else if (string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("### Answer");
                    sb.AppendLine();
                    sb.AppendLine(msg.Content);
                    sb.AppendLine();

                    // Inline citation footnote list for this turn
                    if (msg.Citations.Count > 0)
                    {
                        sb.AppendLine("**Sources for this answer:**");
                        sb.AppendLine();
                        foreach (var c in msg.Citations)
                        {
                            var fn = footnoteLookup[CitationKey(c)];
                            sb.AppendLine(FormatInlineCitation(fn, c));
                        }
                        sb.AppendLine();
                    }

                    sb.AppendLine("---");
                    sb.AppendLine();
                }
            }

            // ── Global bibliography ────────────────────────────
            if (allCitations.Count > 0)
            {
                sb.AppendLine("## Sources Bibliography");
                sb.AppendLine();
                foreach (var (idx, citation) in allCitations.OrderBy(x => x.Index))
                {
                    sb.AppendLine(FormatBibliographyEntry(idx, citation));
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    // ── private helpers ────────────────────────────────────────

    private static string CitationKey(SourceCitation c) =>
        c.ChunkId.HasValue ? $"chunk:{c.ChunkId}"
        : c.Url is not null ? $"url:{c.Url}"
        : $"title:{c.Title}";

    private static string FormatInlineCitation(int fn, SourceCitation c)
    {
        var label = c.Title ?? c.Url ?? "(unknown)";
        var scoreStr = c.RelevanceScore.HasValue
            ? $" — score: {c.RelevanceScore:F3}"
            : string.Empty;
        var linkPart = c.Url is not null
            ? $" [link]({c.Url})"
            : string.Empty;
        return $"- [{fn}] **{EscapeMarkdown(label)}**{scoreStr}{linkPart}";
    }

    private static string FormatBibliographyEntry(int fn, SourceCitation c)
    {
        var parts = new List<string> { $"[{fn}]" };

        parts.Add($"**Type:** {c.Type}");

        if (c.Title is not null)
            parts.Add($"**Title:** {EscapeMarkdown(c.Title)}");

        if (c.Url is not null)
            parts.Add($"**URL:** <{c.Url}>");

        if (c.DataSourceId.HasValue)
            parts.Add($"**DataSourceId:** {c.DataSourceId}");

        if (c.RelevanceScore.HasValue)
            parts.Add($"**Relevance:** {c.RelevanceScore:F4}");

        if (c.Snippet is not null)
            parts.Add($"*{EscapeMarkdown(c.Snippet.Length > 200 ? c.Snippet[..200] + "…" : c.Snippet)}*");

        return string.Join(" | ", parts);
    }

    private static string EscapeMarkdown(string text)
    {
        // Escape characters that break Markdown formatting in headings/inline text
        return text
            .Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("|", "\\|");
    }

    private static string EscapeBlockquote(string text)
    {
        // Multi-line blockquotes need each line prefixed
        return string.Join("\n> ", text.Split('\n'));
    }
}
