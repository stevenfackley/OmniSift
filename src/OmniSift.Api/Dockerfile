# ============================================================
# OmniSift.Api — Multi-stage Docker Build
# Stage 1 (model-fetch): Download the bge-small-en-v1.5 ONNX model
# Stage 2 (build):       Build + publish the .NET app
# Stage 3 (runtime):     Minimal ASP.NET runtime image
# ============================================================

# ── Model-Fetch Stage ─────────────────────────────────────────
# Isolated so the ~130 MB download is cached independently from
# both the SDK and the source build layers.
FROM debian:bookworm-slim AS model-fetch

# curl is the only dependency; install and clean up in one layer.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /models/bge-small-en-v1.5

# ONNX graph — Xenova mirror (fp32, emits last_hidden_state)
RUN curl -fSL \
    "https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main/onnx/model.onnx" \
    -o model.onnx

# WordPiece vocabulary consumed by Microsoft.ML.Tokenizers BertTokenizer
RUN curl -fSL \
    "https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/vocab.txt" \
    -o vocab.txt

# ── Build Stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
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

# Embed the pre-fetched model at /app/models/bge-small-en-v1.5/ — this is
# ContentRootPath-relative, matching the EmbeddingOptions defaults:
#   Embedding:ModelPath     = models/bge-small-en-v1.5/model.onnx
#   Embedding:TokenizerPath = models/bge-small-en-v1.5/vocab.txt
# ContentRootPath for the published app is /app, so the resolved absolute
# paths are /app/models/bge-small-en-v1.5/model.onnx and …/vocab.txt.
COPY --from=model-fetch /models /app/models

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
# Enable ONNX provider by default in the container image.
# Override with Embedding__Provider=OpenAi to revert to the cloud path.
ENV Embedding__Provider=Onnx
ENV Embedding__ModelPath=models/bge-small-en-v1.5/model.onnx
ENV Embedding__TokenizerPath=models/bge-small-en-v1.5/vocab.txt

EXPOSE 8080

USER appuser

ENTRYPOINT ["dotnet", "OmniSift.Api.dll"]
