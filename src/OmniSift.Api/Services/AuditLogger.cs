using OmniSift.Api.Data;
using OmniSift.Api.Middleware;
using OmniSift.Api.Models;

namespace OmniSift.Api.Services;

public sealed class AuditLogger(
    OmniSiftDbContext dbContext,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor) : IAuditLogger
{
    public async Task LogAsync(string action, string resourceType, Guid? resourceId = null, CancellationToken ct = default)
    {
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        var entry = new AuditLog
        {
            TenantId = tenantContext.TenantId,
            UserId = null,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            IpAddress = ipAddress
        };

        dbContext.AuditLogs.Add(entry);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
