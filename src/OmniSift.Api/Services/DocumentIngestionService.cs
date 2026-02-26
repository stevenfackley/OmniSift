// ============================================================
// OmniSift.Api — Document Ingestion Service
// Orchestrates the full ingestion pipeline:
// Upload → Validate → Extract Text → Chunk → Embed → Store
// ============================================================

using Microsoft.EntityFrameworkCore;
using OmniSift.Api.Data;
using OmniSift.Api.Middleware;
using OmniSift.Api.Models;

namespace OmniSift.Api.Services;

/// <summary>
/// Orchestrates the data ingestion pipeline for all source types.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Ingests a file stream through the full pipeline.
    /// </summary>
    /// <param name="stream">The file data stream.</param>
    /// <param name="sourceType">Type of source: "pdf", "sms", or "web".</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="originalUrl">Original URL (for web sources).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created DataSource entity.</returns>
    Task<DataSource> IngestAsync(
        Stream stream,
        string sourceType,
        string? fileName = null,
        string? originalUrl = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation that coordinates text extraction, chunking,
/// embedding generation, and database persistence.
/// </summary>
public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly OmniSiftDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IEnumerable<ITextExtractor> _extractors;
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<DocumentIngestionService> _logger;

    /// <summary>
    /// Allowed source types.
    /// </summary>
    private static readonly HashSet<string> AllowedSourceTypes = ["pdf", "sms", "web"];

    /// <summary>
    /// Batch size for embedding generation.
    /// </summary>
    private const int EmbeddingBatchSize = 20;

    public DocumentIngestionService(
        OmniSiftDbContext dbContext,
        ITenantContext tenantContext,
        IEnumerable<ITextExtractor> extractors,
        ITextChunker chunker,
        IEmbeddingService embeddingService,
        ILogger<DocumentIngestionService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _extractors = extractors;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DataSource> IngestAsync(
        Stream stream,
        string sourceType,
        string? fileName = null,
        string? originalUrl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);

        sourceType = sourceType.ToLowerInvariant();

        if (!AllowedSourceTypes.Contains(sourceType))
        {
            throw new ArgumentException(
                $"Unsupported source type: '{sourceType}'. Allowed: {string.Join(", ", AllowedSourceTypes)}",
                nameof(sourceType));
        }

        var tenantId = _tenantContext.TenantId;

        // Use a transaction to ensure atomicity: either all chunks + data source
        // are persisted, or the whole operation rolls back.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Create the data source record
        var dataSource = new DataSource
        {
            TenantId = tenantId,
            SourceType = sourceType,
            FileName = fileName,
            OriginalUrl = originalUrl,
            Status = "processing"
        };

        _dbContext.DataSources.Add(dataSource);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Starting ingestion for DataSource {DataSourceId}, type={SourceType}, tenant={TenantId}",
            dataSource.Id, sourceType, tenantId);

        try
        {
            // Step 1: Extract text
            var extractor = _extractors.FirstOrDefault(e =>
                e.SourceType.Equals(sourceType, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No text extractor registered for source type: {sourceType}");

            var rawText = await extractor.ExtractTextAsync(stream, fileName, cancellationToken);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                dataSource.Status = "failed";
                dataSource.ErrorMessage = "No text could be extracted from the source.";
                dataSource.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return dataSource;
            }

            _logger.LogDebug("Extracted {Length} chars from source {DataSourceId}", rawText.Length, dataSource.Id);

            // Step 2: Chunk text
            var chunks = _chunker.ChunkText(rawText);
            _logger.LogDebug("Created {ChunkCount} chunks from source {DataSourceId}", chunks.Count, dataSource.Id);

            // Step 3: Generate embeddings in batches
            var allEmbeddings = new List<Pgvector.Vector>();

            for (var i = 0; i < chunks.Count; i += EmbeddingBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = chunks.Skip(i).Take(EmbeddingBatchSize).ToList();
                var batchTexts = batch.Select(c => c.Content);
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(batchTexts, cancellationToken);
                allEmbeddings.AddRange(embeddings);

                _logger.LogDebug(
                    "Generated embeddings batch {Batch}/{Total} for source {DataSourceId}",
                    Math.Min(i + EmbeddingBatchSize, chunks.Count), chunks.Count, dataSource.Id);
            }

            // Step 4: Create document chunk entities and persist
            var documentChunks = chunks.Select((chunk, idx) => new DocumentChunk
            {
                TenantId = tenantId,
                DataSourceId = dataSource.Id,
                Content = chunk.Content,
                ChunkIndex = chunk.Index,
                TokenCount = chunk.TokenCount,
                Embedding = allEmbeddings[idx],
                Metadata = new Dictionary<string, object>
                {
                    ["source_type"] = sourceType,
                    ["file_name"] = fileName ?? string.Empty
                }
            }).ToList();

            _dbContext.DocumentChunks.AddRange(documentChunks);

            dataSource.Status = "completed";
            dataSource.UpdatedAt = DateTime.UtcNow;
            dataSource.Metadata = new Dictionary<string, object>
            {
                ["chunk_count"] = documentChunks.Count,
                ["total_tokens"] = documentChunks.Sum(c => c.TokenCount),
                ["text_length"] = rawText.Length
            };

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Ingestion completed for DataSource {DataSourceId}: {ChunkCount} chunks, {TokenCount} total tokens",
                dataSource.Id, documentChunks.Count, documentChunks.Sum(c => c.TokenCount));

            return dataSource;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ingestion failed for DataSource {DataSourceId}", dataSource.Id);

            dataSource.Status = "failed";
            dataSource.ErrorMessage = ex.Message;
            dataSource.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to save error status for DataSource {DataSourceId}", dataSource.Id);
            }

            throw;
        }
    }
}
