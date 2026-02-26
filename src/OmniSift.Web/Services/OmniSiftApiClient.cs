// ============================================================
// OmniSift.Web — API Client Service
// Typed HTTP client for communicating with the OmniSift API
// ============================================================

using System.Net.Http.Headers;
using System.Net.Http.Json;
using OmniSift.Shared.DTOs;

namespace OmniSift.Web.Services;

/// <summary>
/// Strongly-typed HTTP client for the OmniSift API.
/// Automatically includes the X-Tenant-Id header on all requests.
/// </summary>
public sealed class OmniSiftApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// The current tenant ID. In a real app this would come from
    /// authentication; for now we use the dev tenant.
    /// </summary>
    public Guid TenantId { get; set; } = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public OmniSiftApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Tenant-Id", TenantId.ToString());
        return request;
    }

    // ── Data Sources ────────────────────────────────────────

    /// <summary>
    /// List all data sources for the current tenant.
    /// </summary>
    public async Task<List<DataSourceDto>> GetDataSourcesAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "api/datasources");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DataSourceDto>>() ?? [];
    }

    /// <summary>
    /// Get a single data source by ID.
    /// </summary>
    public async Task<DataSourceDto?> GetDataSourceAsync(Guid id)
    {
        var request = CreateRequest(HttpMethod.Get, $"api/datasources/{id}");
        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DataSourceDto>();
    }

    /// <summary>
    /// Upload a file for ingestion.
    /// </summary>
    public async Task<IngestionResponse> UploadFileAsync(Stream fileStream, string fileName, string? sourceType = null)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        if (!string.IsNullOrWhiteSpace(sourceType))
        {
            content.Add(new StringContent(sourceType), "sourceType");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "api/datasources/upload")
        {
            Content = content
        };
        request.Headers.Add("X-Tenant-Id", TenantId.ToString());

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestionResponse>()
            ?? new IngestionResponse { Status = "error", Message = "Failed to parse response." };
    }

    /// <summary>
    /// Ingest a web page by URL.
    /// </summary>
    public async Task<IngestionResponse> IngestWebAsync(string url)
    {
        var request = CreateRequest(HttpMethod.Post, "api/datasources/web");
        request.Content = JsonContent.Create(new WebIngestionRequest { Url = url });
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestionResponse>()
            ?? new IngestionResponse { Status = "error", Message = "Failed to parse response." };
    }

    /// <summary>
    /// Delete a data source.
    /// </summary>
    public async Task DeleteDataSourceAsync(Guid id)
    {
        var request = CreateRequest(HttpMethod.Delete, $"api/datasources/{id}");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // ── Agent ───────────────────────────────────────────────

    /// <summary>
    /// Submit a research query to the AI agent.
    /// </summary>
    public async Task<AgentQueryResponse> QueryAgentAsync(AgentQueryRequest queryRequest)
    {
        var request = CreateRequest(HttpMethod.Post, "api/agent/query");
        request.Content = JsonContent.Create(queryRequest);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentQueryResponse>()
            ?? new AgentQueryResponse { Response = "Failed to parse response." };
    }

    // ── Health ──────────────────────────────────────────────

    /// <summary>
    /// Check API health status.
    /// </summary>
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
