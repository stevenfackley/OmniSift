// ============================================================
// OmniSift.Api — Application Entry Point
// Configures services, middleware, and Semantic Kernel
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using OmniSift.Api.Data;
using OmniSift.Api.Middleware;
using OmniSift.Api.Plugins;
using OmniSift.Api.Services;

var builder = WebApplication.CreateBuilder(args);

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

// OpenAI embedding client
builder.Services.AddHttpClient<IEmbeddingService, OpenAIEmbeddingService>(client =>
{
    client.DefaultRequestHeaders.Add("Authorization",
        $"Bearer {builder.Configuration["OpenAI:ApiKey"]}");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ── Data Ingestion Services ─────────────────────────────────
builder.Services.AddSingleton<ITextChunker, TextChunker>();
builder.Services.AddScoped<ITextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<ITextExtractor, SmsTextExtractor>();
builder.Services.AddScoped<ITextExtractor, WebTextExtractor>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

// ── Semantic Kernel ─────────────────────────────────────────
// Named HttpClient for the Anthropic-compatible endpoint (avoids leaked HttpClient)
builder.Services.AddHttpClient("AnthropicChat", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/v1/");
});

// Build a base Kernel once and store it as a singleton Func factory.
// We use Func<Kernel> instead of Kernel directly so the scoped registration
// can resolve the base builder without a circular DI conflict.
builder.Services.AddSingleton<Func<Kernel>>(sp =>
{
    var anthropicApiKey = sp.GetRequiredService<IConfiguration>()["Anthropic:ApiKey"] ?? string.Empty;
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

    return () =>
    {
        var kernelBuilder = Kernel.CreateBuilder();

        if (!string.IsNullOrWhiteSpace(anthropicApiKey))
        {
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: "claude-sonnet-4-20250514",
                apiKey: anthropicApiKey,
                httpClient: httpClientFactory.CreateClient("AnthropicChat"));
        }

        return kernelBuilder.Build();
    };
});

// Register SK plugins — these are resolved per-request via DI
builder.Services.AddScoped<VectorSearchPlugin>();
builder.Services.AddScoped<WebScraperPlugin>();
builder.Services.AddScoped<WaybackMachinePlugin>();

// Register a scoped Kernel that includes plugins for each request.
// Resolves the base kernel via Func<Kernel> to avoid circular DI.
builder.Services.AddScoped(sp =>
{
    var kernelFactory = sp.GetRequiredService<Func<Kernel>>();
    var kernel = kernelFactory();

    // Import plugins from DI
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

// ── CORS (allow Blazor frontend) ────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5080",
                "http://omnisift-web:80")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// ── Middleware Pipeline ──────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Tenant resolution middleware (sets RLS session variable)
app.UseTenantMiddleware();

app.MapControllers();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
