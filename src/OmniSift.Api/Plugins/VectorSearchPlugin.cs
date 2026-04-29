// ============================================================
// OmniSift.Api — Vector Search Plugin for Semantic Kernel
// Searches tenant document chunks by semantic similarity
// ============================================================

using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using OmniSift.Api.Data;
using OmniSift.Api.Middleware;
using OmniSift.Api.Services;
using Pgvector.EntityFrameworkCore;

namespace OmniSift.Api.Plugins;

/// <summary>
/// Semantic Kernel plugin that performs vector similarity search
/// against the tenant's document chunks. The agent invokes this
/// to retrieve relevant information from uploaded documents.
/// </summary>
public sealed class VectorSearchPlugin(
    OmniSiftDbContext dbContext,
    ITenantContext tenantContext,
    IEmbeddingService embeddingService,
    ILogger<VectorSearchPlugin> logger)
{
    [KernelFunction("SearchDocuments")]
    [Description("Searches the tenant's uploaded documents for information relevant to the query. " +
                 "Returns the most semantically similar text chunks with metadata including source file names and relevance scores.")]
    public async Task<string> SearchDocumentsAsync(
        [Description("The search query describing the information to find")] string query,
        [Description("Maximum number of results to return (default: 5)")] int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "No query provided for document search.";

        topK = Math.Clamp(topK, 1, 20);

        logger.LogDebug("VectorSearch: query='{Query}', topK={TopK}, tenant={TenantId}",
            query, topK, tenantContext.TenantId);

        try
        {
            // Generate query embedding
            var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query).ConfigureAwait(false);

            // Perform cosine similarity search
            var results = await dbContext.DocumentChunks
                .Where(c => c.TenantId == tenantContext.TenantId)
                .Where(c => c.Embedding != null)
                .OrderBy(c => c.Embedding!.CosineDistance(queryEmbedding))
                .Take(topK)
                .Select(c => new
                {
                    c.Id,
                    c.Content,
                    c.ChunkIndex,
                    c.DataSourceId,
                    DataSourceFileName = c.DataSource.FileName,
                    DataSourceType = c.DataSource.SourceType,
                    Distance = c.Embedding!.CosineDistance(queryEmbedding)
                })
                .ToListAsync().ConfigureAwait(false);

            if (results.Count == 0)
            {
                return "No relevant documents found for this query. The tenant may not have uploaded any documents yet.";
            }

            var formattedResults = results.Select((r, i) => new
            {
                rank = i + 1,
                chunkId = r.Id,
                dataSourceId = r.DataSourceId,
                fileName = r.DataSourceFileName ?? "unknown",
                sourceType = r.DataSourceType,
                chunkIndex = r.ChunkIndex,
                relevanceScore = Math.Round(1.0 - r.Distance, 4),
                content = r.Content
            });

            logger.LogInformation(
                "VectorSearch returned {Count} results for query in tenant {TenantId}",
                results.Count, tenantContext.TenantId);

            return JsonSerializer.Serialize(formattedResults, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "VectorSearch failed for tenant {TenantId}", tenantContext.TenantId);
            return $"Error searching documents: {ex.Message}";
        }
    }
}
