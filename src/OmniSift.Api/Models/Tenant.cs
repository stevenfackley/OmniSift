// ============================================================
// OmniSift.Api — Tenant Entity
// Root multi-tenancy entity
// ============================================================

namespace OmniSift.Api.Models;

/// <summary>
/// Represents an organizational tenant in the multi-tenant system.
/// Each tenant has isolated data via PostgreSQL Row-Level Security.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the tenant/organization.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe unique identifier for the tenant.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Whether the tenant account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<DataSource> DataSources { get; set; } = [];
    public ICollection<DocumentChunk> DocumentChunks { get; set; } = [];
    public ICollection<QueryHistory> QueryHistories { get; set; } = [];
}
