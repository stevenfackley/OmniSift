namespace OmniSift.Api.Options;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "OmniSift";
    public string Audience { get; set; } = "OmniSift";
    public int ExpiryMinutes { get; set; } = 60;
}
