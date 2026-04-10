// ============================================================
// OmniSift.Api — Application Entry Point
// Configures services, middleware, and Semantic Kernel
// ============================================================

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OmniSift.Api.Data;
using OmniSift.Api.Infrastructure;
using OmniSift.Api.Middleware;
using OmniSift.Api.Options;
using OmniSift.Api.Plugins;
using OmniSift.Api.Services;
using OmniSift.Shared;
using Serilog;
using Serilog.Events;

// ── Serilog Bootstrap Logger ─────────────────────────────────
// Used only during host startup (before DI is ready). Replaced
// by the full logger configured in UseSerilog() below.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{

var builder = WebApplication.CreateBuilder(args);

// ── Serilog Full Configuration ───────────────────────────────
// Replaces the bootstrap logger. JSON console output for
// structured ingestion; CorrelationId enriched from LogContext.
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationIdHeader(CorrelationIdMiddleware.HeaderName)
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// ── Docker Secrets ───────────────────────────────────────────
// In production, docker-compose.prod.yml mounts secrets as files under /run/secrets/.
// AddKeyPerFile reads each file as a config key, converting __ to : (e.g.
// Anthropic__ApiKey → Anthropic:ApiKey). Added after env vars so secrets win.
builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true);

// ── Strongly-Typed Options ───────────────────────────────────
builder.Services.Configure<AnthropicOptions>(
    builder.Configuration.GetSection(AnthropicOptions.Section));

builder.Services.Configure<OpenAiOptions>(
    builder.Configuration.GetSection(OpenAiOptions.Section));

builder.Services.Configure<TavilyOptions>(
    builder.Configuration.GetSection(TavilyOptions.Section));

builder.Services.Configure<OmniSift.Api.Options.CorsOptions>(
    builder.Configuration.GetSection(OmniSift.Api.Options.CorsOptions.Section));

// ── Global Exception Handler + ProblemDetails ────────────────
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ── Database ────────────────────────────────────────────────
builder.Services.AddDbContext<OmniSiftDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.UseVector();
            npgsql.EnableRetryOnFailure(3);
        });
});

// ── HTTP Context & Tenant ───────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ── HTTP Clients ────────────────────────────────────────────
builder.Services.AddHttpClient();

// OpenAI embedding client — with resilience pipeline
builder.Services.AddHttpClient<IEmbeddingService, OpenAIEmbeddingService>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opts.ApiKey}");
    client.Timeout = TimeSpan.FromSeconds(60);
})
.AddStandardResilienceHandler();

// ── Data Ingestion Services ─────────────────────────────────
builder.Services.AddSingleton<ITextChunker, TextChunker>();

// Keyed DI: each extractor registered by its source-type key.
// DocumentIngestionService resolves via [FromKeyedServices] — no runtime scan.
builder.Services.AddKeyedScoped<ITextExtractor, PdfTextExtractor>("pdf");
builder.Services.AddKeyedScoped<ITextExtractor, SmsTextExtractor>("sms");
builder.Services.AddKeyedScoped<ITextExtractor, WebTextExtractor>("web");

builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

// ── Semantic Kernel ─────────────────────────────────────────
// Named HttpClient for the Anthropic-compatible endpoint (avoids leaked HttpClient)
builder.Services.AddHttpClient("AnthropicChat", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/v1/");
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
});

// Build a base Kernel once and store it as a singleton Func factory.
builder.Services.AddSingleton<Func<Kernel>>(sp =>
{
    var anthropicOpts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

    return () =>
    {
        var kernelBuilder = Kernel.CreateBuilder();

        if (!string.IsNullOrWhiteSpace(anthropicOpts.ApiKey))
        {
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: anthropicOpts.ModelId,
                apiKey: anthropicOpts.ApiKey,
                httpClient: httpClientFactory.CreateClient("AnthropicChat"));
        }

        return kernelBuilder.Build();
    };
});

// Plugins — WebScraper and WaybackMachine use typed HttpClients with resilience
builder.Services.AddHttpClient<WebScraperPlugin>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<WaybackMachinePlugin>()
    .AddStandardResilienceHandler();

builder.Services.AddScoped<VectorSearchPlugin>();

// Register a scoped Kernel that includes plugins for each request.
builder.Services.AddScoped(sp =>
{
    var kernelFactory = sp.GetRequiredService<Func<Kernel>>();
    var kernel = kernelFactory();

    kernel.ImportPluginFromObject(sp.GetRequiredService<VectorSearchPlugin>(), "VectorSearch");
    kernel.ImportPluginFromObject(sp.GetRequiredService<WebScraperPlugin>(), "WebScraper");
    kernel.ImportPluginFromObject(sp.GetRequiredService<WaybackMachinePlugin>(), "WaybackMachine");

    return kernel;
});

// ── API Configuration ───────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "OmniSift API",
        Version = "v1",
        Description = "Multi-tenant AI research agent API with document ingestion and semantic search."
    });
});

// ── JSON Source Generation ───────────────────────────────────
// Registers compile-time serialization for all shared DTOs. Eliminates
// runtime reflection and enables Native AOT compatibility.
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, OmniSiftJsonContext.Default));

// ── CORS (allow Blazor frontend) ────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Resolve origins from strongly-typed options at registration time
        var corsOpts = builder.Configuration
            .GetSection(OmniSift.Api.Options.CorsOptions.Section)
            .Get<OmniSift.Api.Options.CorsOptions>()
            ?? new OmniSift.Api.Options.CorsOptions();

        policy.WithOrigins(corsOpts.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ── Rate Limiting (per-tenant token bucket) ─────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("per-tenant", ctx =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: ctx.Request.Headers["X-Tenant-Id"].ToString(),
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 20,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// ── Middleware Pipeline ──────────────────────────────────────
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Correlation ID must be first so every subsequent log line
// and response carries the ID.
app.UseCorrelationId();

// Serilog request logging replaces the default ASP.NET request
// log; emits one structured line per request with duration,
// status code, and CorrelationId from LogContext.
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("RequestHost", http.Request.Host.Value);
        diag.Set("RequestScheme", http.Request.Scheme);
        if (http.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var cid))
            diag.Set("CorrelationId", cid);
    };
});

app.UseCors();
app.UseRateLimiter();

// API key authentication (must run before tenant resolution)
app.UseApiKeyAuth();

// Tenant resolution middleware (sets RLS session variable)
app.UseTenantMiddleware();

app.MapControllers();

app.Run();

} // end try
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "OmniSift host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for integration tests
public partial class Program { }
