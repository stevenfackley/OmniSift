// ============================================================
// OmniSift.Web — Blazor WASM Entry Point
// Configures HttpClient and services for the frontend
// ============================================================

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OmniSift.Web;
using OmniSift.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure the API base address
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

// Register application services
builder.Services.AddScoped<OmniSiftApiClient>();

await builder.Build().RunAsync();
