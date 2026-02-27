// ============================================================
// OmniSift.Api — CORS Configuration Options
// ============================================================

namespace OmniSift.Api.Options;

/// <summary>
/// Strongly-typed options for CORS policy configuration.
/// Bound from the "Cors" configuration section.
/// </summary>
public sealed class CorsOptions
{
    public const string Section = "Cors";

    /// <summary>
    /// Allowed origins for the default CORS policy.
    /// </summary>
    public string[] AllowedOrigins { get; set; } =
    [
        "http://localhost:5080",
        "http://omnisift-web:80"
    ];
}
