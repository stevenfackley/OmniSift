// ============================================================
// Integration Tests — Health Endpoint
// Verifies health check returns OK without tenant header
// ============================================================

using System.Net;
using FluentAssertions;

namespace OmniSift.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_WithoutTenantHeader_Returns200()
    {
        var response = await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ReturnsJsonWithStatus()
    {
        var response = await _client.GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();

        content.Should().Contain("status");
        content.Should().Contain("services");
    }
}
