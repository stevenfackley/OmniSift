// ============================================================
// OmniSift.Api — DataSource Entity
// Represents an uploaded/ingested data source
// ============================================================

namespace OmniSift.Api.Models;

/// <summary>
/// Tracks every uploaded or ingested data source for a tenant.
/// Supports PDF, SMS exports, and web page captures.
/// </summary>
public sealed class DataSource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the owning tenant. Required for RLS.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Type of data source: "pdf", "sms", or "web".
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Original file name for uploaded files.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Original URL for web-captured sources.
    /// </summary>
    public string? OriginalUrl { get; set; }

    /// <summary>
    /// Processing status: "pending", "processing", "completed", "failed".
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Flexible metadata stored as JSONB.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}
