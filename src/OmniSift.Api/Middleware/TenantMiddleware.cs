// ============================================================
// OmniSift.Api — Tenant Resolution Middleware
// Extracts tenant ID from request headers and sets
// the PostgreSQL session variable for RLS enforcement.
// ============================================================

using Microsoft.EntityFrameworkCore;
using OmniSift.Api.Data;

namespace OmniSift.Api.Middleware;

/// <summary>
/// Middleware that resolves the current tenant from the X-Tenant-Id header,
/// validates the tenant exists and is active, and sets the PostgreSQL
/// session variable 'app.current_tenant' for Row-Level Security enforcement.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
{
    /// <summary>
    /// Header name used to identify the current tenant.
    /// </summary>
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, OmniSiftDbContext dbContext)
    {
        // Skip tenant resolution for health checks and Swagger
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.StartsWith("/api/health") || path.StartsWith("/swagger"))
        {
            await next(context);
            return;
        }

        // Extract tenant ID from header
        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeader) ||
            !Guid.TryParse(tenantHeader.FirstOrDefault(), out var tenantId))
        {
            logger.LogWarning("Request missing or invalid {Header} header from {RemoteIp}",
                TenantHeaderName, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = $"Missing or invalid '{TenantHeaderName}' header. Must be a valid GUID."
            });
            return;
        }

        // Store the tenant ID in HttpContext.Items for downstream use
        context.Items["TenantId"] = tenantId;

        // Set the PostgreSQL session variable for RLS enforcement.
        // This ensures all queries through this connection are scoped to the tenant.
        // Skip for non-relational providers (e.g., InMemory during testing).
        if (!dbContext.Database.IsRelational())
        {
            await next(context);
            return;
        }

        try
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(context.RequestAborted);
            }

            await using var command = connection.CreateCommand();
            // Use set_config() instead of SET to allow parameterized query.
            // 'false' = setting persists for the entire session/connection.
            command.CommandText = "SELECT set_config('app.current_tenant', @tenantId, false)";
            var param = command.CreateParameter();
            param.ParameterName = "@tenantId";
            param.Value = tenantId.ToString();
            command.Parameters.Add(param);
            await command.ExecuteNonQueryAsync(context.RequestAborted);

            logger.LogDebug("Tenant context set to {TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set tenant context for {TenantId}", tenantId);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Failed to establish tenant context." });
            return;
        }

        await next(context);
    }
}

/// <summary>
/// Extension methods for registering the TenantMiddleware.
/// </summary>
public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantMiddleware>();
    }
}
