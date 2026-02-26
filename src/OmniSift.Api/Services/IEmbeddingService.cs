// ============================================================
// OmniSift.Api — Embedding Service Interface & Implementation
// Generates vector embeddings via OpenAI API
// ============================================================

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Pgvector;

namespace OmniSift.Api.Services;

/// <summary>
/// Service for generating vector embeddings from text.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a vector embedding for the given text.
    /// </summary>
    /// <param name="text">Input text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Vector embedding (3072 dimensions for text-embedding-3-large).</returns>
    Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates vector embeddings for multiple texts in a batch.
    /// </summary>
    /// <param name="texts">Input texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of vector embeddings in the same order as inputs.</returns>
    Task<IReadOnlyList<Vector>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}

/// <summary>
/// OpenAI embedding service implementation using text-embedding-3-large.
/// </summary>
public sealed class OpenAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIEmbeddingService> _logger;
    private const string Model = "text-embedding-3-large";
    private const int Dimensions = 3072;

    public OpenAIEmbeddingService(HttpClient httpClient, ILogger<OpenAIEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await GenerateEmbeddingsAsync([text], cancellationToken);
        return embeddings[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Vector>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();

        if (textList.Count == 0)
            return [];

        _logger.LogDebug("Generating embeddings for {Count} text(s) using {Model}", textList.Count, Model);

        var request = new OpenAIEmbeddingRequest
        {
            Input = textList,
            Model = Model,
            Dimensions = Dimensions
        };

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/embeddings",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(
            cancellationToken: cancellationToken);

        if (result?.Data is null || result.Data.Count != textList.Count)
        {
            throw new InvalidOperationException(
                $"Expected {textList.Count} embeddings but received {result?.Data?.Count ?? 0}.");
        }

        return result.Data
            .OrderBy(d => d.Index)
            .Select(d => new Vector(d.Embedding))
            .ToList();
    }

    // ── OpenAI API DTOs ─────────────────────────────

    private sealed class OpenAIEmbeddingRequest
    {
        [JsonPropertyName("input")]
        public List<string> Input { get; init; } = [];

        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("dimensions")]
        public int Dimensions { get; init; }
    }

    private sealed class OpenAIEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; init; } = [];
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; init; } = [];
    }
}
