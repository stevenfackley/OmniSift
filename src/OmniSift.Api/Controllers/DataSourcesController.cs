// ============================================================
// OmniSift.Api — Data Sources Controller
// Manages file uploads, web ingestion, and data source CRUD
// ============================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniSift.Api.Data;
using OmniSift.Api.Middleware;
using OmniSift.Api.Services;
using OmniSift.Shared.DTOs;

namespace OmniSift.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DataSourcesController : ControllerBase
{
    private readonly OmniSiftDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDocumentIngestionService _ingestionService;
    private readonly ILogger<DataSourcesController> _logger;

    /// <summary>
    /// Maximum upload file size (50 MB).
    /// </summary>
    private const long MaxFileSize = 50 * 1024 * 1024;

    /// <summary>
    /// Allowed MIME types for file uploads.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedMimeTypes = new()
    {
        ["application/pdf"] = "pdf",
        ["text/csv"] = "sms",
        ["application/json"] = "sms",
        ["text/html"] = "web"
    };

    public DataSourcesController(
        OmniSiftDbContext dbContext,
        ITenantContext tenantContext,
        IDocumentIngestionService ingestionService,
        ILogger<DataSourcesController> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a file for ingestion into the document pipeline.
    /// Accepts PDF, CSV (SMS), JSON (SMS), and HTML files.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<ActionResult<IngestionResponse>> Upload(
        IFormFile file,
        [FromForm] string? sourceType,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided or file is empty." });
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest(new { error = $"File size exceeds the maximum allowed size of {MaxFileSize / (1024 * 1024)} MB." });
        }

        // Determine source type from MIME type or explicit parameter
        var resolvedSourceType = sourceType?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(resolvedSourceType))
        {
            var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

            if (!AllowedMimeTypes.TryGetValue(contentType, out resolvedSourceType))
            {
                return BadRequest(new
                {
                    error = $"Unsupported file type: '{contentType}'. Allowed: {string.Join(", ", AllowedMimeTypes.Keys)}"
                });
            }
        }

        _logger.LogInformation(
            "Upload request: file={FileName}, size={Size}, type={SourceType}, tenant={TenantId}",
            file.FileName, file.Length, resolvedSourceType, _tenantContext.TenantId);

        try
        {
            using var stream = file.OpenReadStream();
            var dataSource = await _ingestionService.IngestAsync(
                stream, resolvedSourceType, file.FileName, cancellationToken: cancellationToken);

            return Ok(new IngestionResponse
            {
                DataSourceId = dataSource.Id,
                Status = dataSource.Status,
                Message = dataSource.Status == "completed"
                    ? "File uploaded and processed successfully."
                    : $"Ingestion {dataSource.Status}: {dataSource.ErrorMessage}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for file {FileName}", file.FileName);
            return StatusCode(500, new { error = "An error occurred during file processing.", details = ex.Message });
        }
    }

    /// <summary>
    /// Ingest a web page by URL.
    /// </summary>
    [HttpPost("web")]
    public async Task<ActionResult<IngestionResponse>> IngestWeb(
        [FromBody] WebIngestionRequest request,
        [FromServices] IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        _logger.LogInformation(
            "Web ingestion request: url={Url}, tenant={TenantId}",
            request.Url, _tenantContext.TenantId);

        try
        {
            // Fetch the web page
            var httpClient = httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(request.Url, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var dataSource = await _ingestionService.IngestAsync(
                stream, "web", null, request.Url, cancellationToken);

            return Ok(new IngestionResponse
            {
                DataSourceId = dataSource.Id,
                Status = dataSource.Status,
                Message = dataSource.Status == "completed"
                    ? "Web page ingested successfully."
                    : $"Ingestion {dataSource.Status}: {dataSource.ErrorMessage}"
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch URL: {Url}", request.Url);
            return BadRequest(new { error = $"Could not fetch URL: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web ingestion failed for URL: {Url}", request.Url);
            return StatusCode(500, new { error = "An error occurred during web ingestion.", details = ex.Message });
        }
    }

    /// <summary>
    /// List all data sources for the current tenant.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<DataSourceDto>>> List(CancellationToken cancellationToken)
    {
        var sources = await _dbContext.DataSources
            .Where(ds => ds.TenantId == _tenantContext.TenantId)
            .OrderByDescending(ds => ds.CreatedAt)
            .Select(ds => new DataSourceDto
            {
                Id = ds.Id,
                SourceType = ds.SourceType,
                FileName = ds.FileName,
                OriginalUrl = ds.OriginalUrl,
                Status = ds.Status,
                ErrorMessage = ds.ErrorMessage,
                Metadata = ds.Metadata,
                CreatedAt = ds.CreatedAt,
                UpdatedAt = ds.UpdatedAt,
                ChunkCount = ds.Chunks.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(sources);
    }

    /// <summary>
    /// Get a single data source by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DataSourceDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var source = await _dbContext.DataSources
            .Where(ds => ds.TenantId == _tenantContext.TenantId && ds.Id == id)
            .Select(ds => new DataSourceDto
            {
                Id = ds.Id,
                SourceType = ds.SourceType,
                FileName = ds.FileName,
                OriginalUrl = ds.OriginalUrl,
                Status = ds.Status,
                ErrorMessage = ds.ErrorMessage,
                Metadata = ds.Metadata,
                CreatedAt = ds.CreatedAt,
                UpdatedAt = ds.UpdatedAt,
                ChunkCount = ds.Chunks.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
            return NotFound(new { error = $"Data source '{id}' not found." });

        return Ok(source);
    }

    /// <summary>
    /// Delete a data source and all associated document chunks.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var source = await _dbContext.DataSources
            .FirstOrDefaultAsync(ds => ds.TenantId == _tenantContext.TenantId && ds.Id == id, cancellationToken);

        if (source is null)
            return NotFound(new { error = $"Data source '{id}' not found." });

        _dbContext.DataSources.Remove(source);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted DataSource {DataSourceId} for tenant {TenantId}",
            id, _tenantContext.TenantId);

        return NoContent();
    }
}
