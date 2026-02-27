// ============================================================
// OmniSift.Api — Entity Framework Core DbContext
// Configured for PostgreSQL + pgvector with multi-tenancy
// ============================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OmniSift.Api.Models;
using Pgvector;

namespace OmniSift.Api.Data;

/// <summary>
/// Primary database context for OmniSift.
/// Configures entity mappings for PostgreSQL with pgvector support.
/// Tenant isolation is enforced at the database level via RLS policies,
/// with the session variable set by TenantMiddleware.
/// </summary>
public class OmniSiftDbContext : DbContext
{
    public OmniSiftDbContext(DbContextOptions<OmniSiftDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<QueryHistory> QueryHistories => Set<QueryHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("vector");

        // ── Tenant ──────────────────────────────────────
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
            entity.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(128).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // ── DataSource ──────────────────────────────────
        modelBuilder.Entity<DataSource>(entity =>
        {
            entity.ToTable("data_sources");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(e => e.SourceType).HasColumnName("source_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.FileName).HasColumnName("file_name").HasMaxLength(512);
            entity.Property(e => e.OriginalUrl).HasColumnName("original_url").HasMaxLength(2048);

            // IngestionStatus stored as lowercase string for DB compatibility
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(50)
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<IngestionStatus>(v, ignoreCase: true))
                .HasDefaultValue(IngestionStatus.Pending);

            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb")
                .HasConversion(JsonDictionaryConverter);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.DataSources)
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Status });
        });

        // ── DocumentChunk ───────────────────────────────
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(e => e.DataSourceId).HasColumnName("data_source_id").IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index").IsRequired();
            entity.Property(e => e.TokenCount).HasColumnName("token_count").HasDefaultValue(0);
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(3072)");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb")
                .HasConversion(JsonDictionaryConverter);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.DocumentChunks)
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DataSource)
                  .WithMany(ds => ds.Chunks)
                  .HasForeignKey(e => e.DataSourceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.DataSourceId);
        });

        // ── QueryHistory ────────────────────────────────
        modelBuilder.Entity<QueryHistory>(entity =>
        {
            entity.ToTable("query_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(e => e.QueryText).HasColumnName("query_text").IsRequired();
            entity.Property(e => e.ResponseText).HasColumnName("response_text");
            entity.Property(e => e.PluginsUsed).HasColumnName("plugins_used").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());
            entity.Property(e => e.Sources).HasColumnName("sources").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<List<object>>(v, JsonSerializerOptions.Default) ?? new List<object>());
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.QueryHistories)
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
        });
    }

    /// <summary>
    /// Shared value converter for Dictionary&lt;string, object&gt; JSONB columns.
    /// Ensures compatibility with both Npgsql and InMemory providers.
    /// </summary>
    private static readonly ValueConverter<Dictionary<string, object>, string> JsonDictionaryConverter = new(
        v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
        v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonSerializerOptions.Default) ?? new Dictionary<string, object>());
}
