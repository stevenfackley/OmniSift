// ============================================================
// OmniSift.Api — Health Check Controller
// Simple health check endpoint (exempt from tenant middleware)
// ============================================================

using Microsoft.AspNetCore.Mvc;
using OmniSift.Api.Data;

namespace OmniSift.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(
    OmniSiftDbContext dbContext,
    ILogger<HealthController> logger) : ControllerBase
{
    /// <summary>
    /// Health check endpoint. Returns service status and database connectivity.
    /// Exempt from tenant middleware — no X-Tenant-Id header required.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var dbHealthy = false;

        try
        {
            dbHealthy = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database health check failed");
        }

        var status = dbHealthy ? "healthy" : "degraded";

        return Ok(new
        {
            status,
            timestamp = DateTime.UtcNow,
            services = new
            {
                database = dbHealthy ? "connected" : "unavailable",
                api = "running"
            }
        });
    }
}
