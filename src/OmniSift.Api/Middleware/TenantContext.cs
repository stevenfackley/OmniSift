// ============================================================
// OmniSift.Api — Tenant Context Accessor
// Provides access to the current tenant ID from HttpContext
// ============================================================

namespace OmniSift.Api.Middleware;

/// <summary>
/// Interface for accessing the current tenant context.
/// Registered as scoped to ensure per-request isolation.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID for the request.
    /// </summary>
    Guid TenantId { get; }
}

/// <summary>
/// Resolves the current tenant ID from HttpContext items,
/// which is set by the TenantMiddleware.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid TenantId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("No HTTP context available.");

            if (context.Items.TryGetValue("TenantId", out var tenantIdObj) &&
                tenantIdObj is Guid tenantId)
            {
                return tenantId;
            }

            throw new InvalidOperationException(
                "Tenant ID not found in request context. Ensure TenantMiddleware is registered.");
        }
    }
}
