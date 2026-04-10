// ============================================================
// OmniSift.Api — API Key Authentication Middleware
// Validates X-API-Key header against tenant API keys and
// a fallback global key (OMNISIFT_API_KEY env var).
// Must run BEFORE TenantMiddleware in the pipeline.
// ============================================================

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OmniSift.Api.Data;

namespace OmniSift.Api.Middleware;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
{
    public const string ApiKeyHeaderName = "X-API-Key";

    public async Task InvokeAsync(HttpContext context, OmniSiftDbContext dbContext)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.StartsWith("/health") || path.StartsWith("/api/health") || path.StartsWith("/swagger"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeader) ||
            string.IsNullOrWhiteSpace(apiKeyHeader.FirstOrDefault()))
        {
            logger.LogWarning("Missing {Header} from {RemoteIp}",
                ApiKeyHeaderName, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = $"Missing '{ApiKeyHeaderName}' header."
            });
            return;
        }

        var apiKey = apiKeyHeader.FirstOrDefault()!;
        var apiKeyHash = HashApiKey(apiKey);

        // Global fallback key (env var) — useful for admin/ops
        var globalKey = Environment.GetEnvironmentVariable("OMNISIFT_API_KEY");
        var isGlobalKey = !string.IsNullOrWhiteSpace(globalKey) &&
                          CryptographicOperations.FixedTimeEquals(
                              Encoding.UTF8.GetBytes(apiKey),
                              Encoding.UTF8.GetBytes(globalKey));

        // Tenant-scoped validation
        if (context.Request.Headers.TryGetValue(TenantMiddleware.TenantHeaderName, out var tenantHeader) &&
            Guid.TryParse(tenantHeader.FirstOrDefault(), out var tenantId))
        {
            if (!isGlobalKey)
            {
                var tenant = await dbContext.Tenants
                    .AsNoTracking()
                    .Where(t => t.Id == tenantId && t.IsActive)
                    .Select(t => new { t.ApiKeyHash })
                    .FirstOrDefaultAsync(context.RequestAborted);

                if (tenant is null)
                {
                    logger.LogWarning("Tenant {TenantId} not found or inactive", tenantId);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Tenant not found or inactive."
                    });
                    return;
                }

                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(apiKeyHash),
                        Encoding.UTF8.GetBytes(tenant.ApiKeyHash)))
                {
                    logger.LogWarning("API key mismatch for tenant {TenantId} from {RemoteIp}",
                        tenantId, context.Connection.RemoteIpAddress);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "API key is not authorized for this tenant."
                    });
                    return;
                }
            }
        }
        else if (!isGlobalKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid API key."
            });
            return;
        }

        await next(context);
    }

    internal static string HashApiKey(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(bytes);
    }
}

public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}
