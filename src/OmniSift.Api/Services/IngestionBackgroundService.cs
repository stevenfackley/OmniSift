// ============================================================
// OmniSift.Api — Ingestion Background Service
// Drains the IngestionChannel and runs the full ingestion
// pipeline outside the HTTP request lifecycle.
//
// TENANT / RLS SAFETY:
//   The work item carries the tenantId captured at enqueue time.
//   For each item this service:
//     1. Creates a fresh DI scope (own DbContext connection).
//     2. Opens the Npgsql connection explicitly.
//     3. Calls set_config('app.current_tenant', tenantId, false)
//        — identical to what TenantMiddleware does per HTTP request.
//     4. Only then runs the ingestion pipeline.
//   This ensures RLS is active for every DB write made by the worker.
// ============================================================

using Microsoft.EntityFrameworkCore;
using OmniSift.Api.Data;
using OmniSift.Api.Models;

namespace OmniSift.Api.Services;

/// <summary>
/// Long-running hosted service that drains the <see cref="IngestionChannel"/>
/// and executes the extract→chunk→embed pipeline in a dedicated DI scope.
/// </summary>
public sealed class IngestionBackgroundService(
    IngestionChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<IngestionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Ingestion background service started.");

        await foreach (var item in channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            await ProcessItemAsync(item, stoppingToken).ConfigureAwait(false);
        }

        logger.LogInformation("Ingestion background service stopped.");
    }

    private async Task ProcessItemAsync(IngestionWorkItem item, CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Background ingestion starting: DataSource={DataSourceId}, Tenant={TenantId}, Type={SourceType}",
            item.DataSourceId, item.TenantId, item.SourceType);

        // Each item gets its own scope so its DbContext gets its own connection.
#pragma warning disable CA2007 // await using DisposeAsync — no SyncContext in hosted service
        await using var scope = scopeFactory.CreateAsyncScope();
#pragma warning restore CA2007
        var dbContext = scope.ServiceProvider.GetRequiredService<OmniSiftDbContext>();

        // ── Set RLS tenant context ───────────────────────────────
        // Must happen BEFORE any EF queries, on the same connection
        // that EF will use for this scope.
        if (dbContext.Database.IsRelational())
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(stoppingToken).ConfigureAwait(false);
            }

#pragma warning disable CA2007 // await using DisposeAsync — block-form restructure not worth it
            await using var cmd = connection.CreateCommand();
#pragma warning restore CA2007
            cmd.CommandText = "SELECT set_config('app.current_tenant', @tid, false)";
            var param = cmd.CreateParameter();
            param.ParameterName = "@tid";
            param.Value = item.TenantId.ToString();
            cmd.Parameters.Add(param);
            await cmd.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);

            logger.LogDebug(
                "RLS tenant context set to {TenantId} for background job on DataSource {DataSourceId}",
                item.TenantId, item.DataSourceId);
        }

        // ── Load the DataSource record ───────────────────────────
        var dataSource = await dbContext.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == item.DataSourceId, stoppingToken)
            .ConfigureAwait(false);

        if (dataSource is null)
        {
            logger.LogError(
                "DataSource {DataSourceId} not found during background ingestion. Skipping.",
                item.DataSourceId);
            return;
        }

        // Mark as processing
        dataSource.Status = IngestionStatus.Processing;
        dataSource.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

        // ── Run pipeline ─────────────────────────────────────────
        // Resolve the background-scoped ingestion service so it uses
        // the same DbContext (and therefore the same connection with RLS set).
        var ingestionService = scope.ServiceProvider
            .GetRequiredService<IBackgroundIngestionService>();

        try
        {
            using var contentStream = new MemoryStream(item.Content);
            await ingestionService.RunPipelineAsync(
                dataSource, contentStream, item.SourceType, item.FileName, stoppingToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Background ingestion completed for DataSource {DataSourceId}",
                item.DataSourceId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Background ingestion failed for DataSource {DataSourceId}",
                item.DataSourceId);
        }
    }
}
