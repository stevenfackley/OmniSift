// ============================================================
// OmniSift.Api — Embedding Service Interface & Implementation
// Generates vector embeddings via OpenAI API
// ============================================================

using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OmniSift.Api.Options;
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
    Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates vector embeddings for multiple texts in a batch.
    /// </summary>
    Task<IReadOnlyList<Vector>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}

/// <summary>
/// OpenAI embedding service implementation using text-embedding-3-large.
/// </summary>
public sealed class OpenAIEmbeddingService(
    HttpClient httpClient,
    IOptions<OpenAiOptions> openAiOptions,
    ILogger<OpenAIEmbeddingService> logger) : IEmbeddingService
{
    private readonly string _model = openAiOptions.Value.EmbeddingModel;
    private readonly int _dimensions = openAiOptions.Value.EmbeddingDimensions;

    /// <inheritdoc />
    public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await GenerateEmbeddingsAsync([text], cancellationToken).ConfigureAwait(false);
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

        logger.LogDebug("Generating embeddings for {Count} text(s) using {Model}", textList.Count, _model);

        var request = new OpenAIEmbeddingRequest
        {
            Input = textList,
            Model = _model,
            Dimensions = _dimensions
        };

        var response = await httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/embeddings",
            request,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);

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
