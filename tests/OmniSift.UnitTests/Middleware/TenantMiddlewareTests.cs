// ============================================================
// Unit Tests — TenantMiddleware
// Verifies tenant header validation and context setting
// ============================================================

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using OmniSift.Api.Middleware;

namespace OmniSift.UnitTests.Middleware;

public sealed class TenantMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_HealthCheckPath_SkipsTenantResolution()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(ctx => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/api/health");

        // Note: InvokeAsync requires OmniSiftDbContext, but health path skips before using it
        await middleware.InvokeAsync(context, null!);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SwaggerPath_SkipsTenantResolution()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(ctx => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/swagger/index.html");

        await middleware.InvokeAsync(context, null!);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MissingTenantHeader_Returns400()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateContext("/api/datasources");

        await middleware.InvokeAsync(context, null!);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_InvalidGuidHeader_Returns400()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateContext("/api/datasources");
        context.Request.Headers[TenantMiddleware.TenantHeaderName] = "not-a-guid";

        await middleware.InvokeAsync(context, null!);

        context.Response.StatusCode.Should().Be(400);
    }

    private static TenantMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new TenantMiddleware(next, Mock.Of<ILogger<TenantMiddleware>>());
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
