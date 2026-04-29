// ============================================================
// OmniSift.Api — Wayback Machine Plugin for Semantic Kernel
// Retrieves archived web page snapshots from the Internet Archive
// ============================================================

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace OmniSift.Api.Plugins;

/// <summary>
/// Semantic Kernel plugin that queries the Wayback Machine API
/// to find archived snapshots of web pages. Useful for historical
/// research and recovering removed or changed web content.
/// </summary>
public sealed class WaybackMachinePlugin(
    HttpClient httpClient,
    ILogger<WaybackMachinePlugin> logger)
{
    [KernelFunction("GetArchivedPage")]
    [Description("Checks the Internet Archive's Wayback Machine for an archived snapshot of a web page. " +
                 "Returns the closest archived snapshot URL and timestamp if available. " +
                 "Use this when the user needs to find historical versions of web pages or recover removed content.")]
    public async Task<string> GetArchivedPageAsync(
        [Description("The URL of the web page to look up in the Wayback Machine")] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "No URL provided for Wayback Machine lookup.";

        logger.LogDebug("WaybackMachine: looking up {Url}", url);

        try
        {
            var encodedUrl = Uri.EscapeDataString(url);
            var apiUrl = $"http://archive.org/wayback/available?url={encodedUrl}";

            var response = await httpClient.GetFromJsonAsync<WaybackResponse>(apiUrl).ConfigureAwait(false);

            if (response?.ArchivedSnapshots?.Closest is null)
            {
                logger.LogInformation("No Wayback Machine snapshot found for {Url}", url);
                return JsonSerializer.Serialize(new
                {
                    found = false,
                    originalUrl = url,
                    message = "No archived snapshot found for this URL in the Wayback Machine."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var snapshot = response.ArchivedSnapshots.Closest;

            logger.LogInformation(
                "Wayback Machine snapshot found for {Url}: {SnapshotUrl} ({Timestamp})",
                url, snapshot.Url, snapshot.Timestamp);

            return JsonSerializer.Serialize(new
            {
                found = true,
                originalUrl = url,
                archiveUrl = snapshot.Url,
                timestamp = snapshot.Timestamp,
                available = snapshot.Available,
                status = snapshot.Status
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WaybackMachine lookup failed for {Url}", url);
            return $"Error querying Wayback Machine: {ex.Message}";
        }
    }

    // ── Wayback Machine API DTOs ─────────────────────

    private sealed class WaybackResponse
    {
        [JsonPropertyName("archived_snapshots")]
        public ArchivedSnapshots? ArchivedSnapshots { get; init; }
    }

    private sealed class ArchivedSnapshots
    {
        [JsonPropertyName("closest")]
        public WaybackSnapshot? Closest { get; init; }
    }

    private sealed class WaybackSnapshot
    {
        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; init; } = string.Empty;

        [JsonPropertyName("available")]
        public bool Available { get; init; }

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;
    }
}
