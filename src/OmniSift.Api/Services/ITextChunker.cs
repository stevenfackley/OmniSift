// ============================================================
// OmniSift.Api — Text Chunker Interface & Implementation
// Splits text into overlapping chunks for embedding
// ============================================================

namespace OmniSift.Api.Services;

/// <summary>
/// Represents a single text chunk with metadata.
/// </summary>
public sealed record TextChunk
{
    /// <summary>
    /// The text content of this chunk.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Zero-based index of this chunk in the sequence.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Approximate token count (whitespace-split words).
    /// </summary>
    public int TokenCount { get; init; }
}

/// <summary>
/// Splits text into overlapping chunks suitable for vector embedding.
/// </summary>
public interface ITextChunker
{
    /// <summary>
    /// Splits text into chunks of approximately the specified token size
    /// with the specified overlap between consecutive chunks.
    /// </summary>
    /// <param name="text">The input text to chunk.</param>
    /// <param name="chunkSize">Target tokens per chunk (default: 500).</param>
    /// <param name="overlap">Token overlap between chunks (default: 50).</param>
    /// <returns>Ordered list of text chunks.</returns>
    IReadOnlyList<TextChunk> ChunkText(string text, int chunkSize = 500, int overlap = 50);
}

/// <summary>
/// Default implementation using whitespace tokenization with sliding window.
/// </summary>
public sealed class TextChunker : ITextChunker
{
    /// <inheritdoc />
    public IReadOnlyList<TextChunk> ChunkText(string text, int chunkSize = 500, int overlap = 50)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive.");

        if (overlap < 0)
            throw new ArgumentOutOfRangeException(nameof(overlap), "Overlap must be non-negative.");

        if (overlap >= chunkSize)
            throw new ArgumentOutOfRangeException(nameof(overlap), "Overlap must be less than chunk size.");

        var chunks = new List<TextChunk>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        // Tokenize by whitespace (approximate token count)
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
            return chunks;

        var stride = chunkSize - overlap;
        var index = 0;

        for (var start = 0; start < tokens.Length; start += stride)
        {
            var end = Math.Min(start + chunkSize, tokens.Length);
            var chunkTokens = tokens[start..end];
            var content = string.Join(' ', chunkTokens);

            chunks.Add(new TextChunk
            {
                Content = content,
                Index = index,
                TokenCount = chunkTokens.Length
            });

            index++;

            // If we've reached the end, stop
            if (end >= tokens.Length)
                break;
        }

        return chunks;
    }
}
