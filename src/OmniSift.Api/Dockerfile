# ============================================================
# OmniSift.Api — Multi-stage Docker Build
# Stage 1: Build with .NET SDK
# Stage 2: Runtime with ASP.NET runtime
# ============================================================

# ── Build Stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src

# Copy solution and project files first (layer caching)
COPY OmniSift.sln ./
COPY src/OmniSift.Api/OmniSift.Api.csproj src/OmniSift.Api/
COPY src/OmniSift.Shared/OmniSift.Shared.csproj src/OmniSift.Shared/

# Restore dependencies
RUN dotnet restore src/OmniSift.Api/OmniSift.Api.csproj

# Copy all source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/OmniSift.Api/OmniSift.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime Stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY --from=build /app/publish .

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

USER appuser

ENTRYPOINT ["dotnet", "OmniSift.Api.dll"]
