# ============================================================
# OmniSift.Web — Multi-stage Docker Build
# Stage 1: Build Blazor WASM with .NET SDK
# Stage 2: Serve with nginx
# ============================================================

# ── Build Stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files first (layer caching)
COPY OmniSift.sln ./
COPY src/OmniSift.Web/OmniSift.Web.csproj src/OmniSift.Web/
COPY src/OmniSift.Shared/OmniSift.Shared.csproj src/OmniSift.Shared/

# Restore dependencies
RUN dotnet restore src/OmniSift.Web/OmniSift.Web.csproj

# Copy all source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/OmniSift.Web/OmniSift.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime Stage (nginx) ───────────────────────────────────
FROM nginx:alpine AS runtime

# Copy the published Blazor WASM output to nginx
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Copy custom nginx config for SPA routing
COPY src/OmniSift.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
