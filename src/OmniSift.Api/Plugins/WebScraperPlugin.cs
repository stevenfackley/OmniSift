// ============================================================
// OmniSift.Api — Web Scraper Plugin for Semantic Kernel
// Searches the web via Tavily API for real-time information
// ============================================================

using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OmniSift.Api.Options;

namespace OmniSift.Api.Plugins;

/// <summary>
/// Semantic Kernel plugin that searches the web using Tavily API.
/// The agent invokes this to find current information not available
/// in the tenant's uploaded documents.
/// </summary>
public sealed class WebScraperPlugin(
    HttpClient httpClient,
    IOptions<TavilyOptions> tavilyOptions,
    ILogger<WebScraperPlugin> logger)
{
    private readonly string _apiKey = tavilyOptions.Value.ApiKey;

    [KernelFunction("SearchWeb")]
    [Description("Searches the web for current information related to the query. " +
                 "Returns titles, URLs, and content snippets from web pages. " +
                 "Use this when the user's question requires information beyond what's in their uploaded documents.")]
    public async Task<string> SearchWebAsync(
        [Description("The search query to find information on the web")] string query,
        [Description("Maximum number of results to return (default: 5)")] int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "No query provided for web search.";

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("Tavily API key not configured; web search unavailable");
            return "Web search is not available — API key not configured.";
        }

        maxResults = Math.Clamp(maxResults, 1, 10);

        logger.LogDebug("WebSearch: query='{Query}', maxResults={MaxResults}", query, maxResults);

        try
        {
            var request = new TavilySearchRequest
            {
                ApiKey = _apiKey,
                Query = query,
                MaxResults = maxResults,
                SearchDepth = "basic",
                IncludeAnswer = true
            };

            var response = await httpClient.PostAsJsonAsync(
                "https://api.tavily.com/search",
                request);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TavilySearchResponse>();

            if (result?.Results is null || result.Results.Count == 0)
            {
                return "No web results found for this query.";
            }

            var formattedResults = new
            {
                answer = result.Answer,
                results = result.Results.Select((r, i) => new
                {
                    rank = i + 1,
                    title = r.Title,
                    url = r.Url,
                    snippet = r.Content?.Length > 500 ? r.Content[..500] + "..." : r.Content,
                    score = r.Score
                })
            };

            logger.LogInformation("WebSearch returned {Count} results for query", result.Results.Count);

            return JsonSerializer.Serialize(formattedResults, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebSearch failed for query: {Query}", query);
            return $"Error performing web search: {ex.Message}";
        }
    }

    // ── Tavily API DTOs ─────────────────────────────

    private sealed class TavilySearchRequest
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; init; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; init; } = string.Empty;

        [JsonPropertyName("max_results")]
        public int MaxResults { get; init; } = 5;

        [JsonPropertyName("search_depth")]
        public string SearchDepth { get; init; } = "basic";

        [JsonPropertyName("include_answer")]
        public bool IncludeAnswer { get; init; } = true;
    }

    private sealed class TavilySearchResponse
    {
        [JsonPropertyName("answer")]
        public string? Answer { get; init; }

        [JsonPropertyName("results")]
        public List<TavilyResult> Results { get; init; } = [];
    }

    private sealed class TavilyResult
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("score")]
        public double Score { get; init; }
    }
}
