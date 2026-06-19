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

# curl + coreutils (sha256sum) — install and clean up in one layer.
RUN apt-get update && apt-get install -y --no-install-recommends curl coreutils \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /models/bge-small-en-v1.5

# ── Model + vocabulary download (single layer so they travel together) ──
#
# URLs are pinned to specific HuggingFace commit SHAs to guard against
# silent upstream changes that would break tokenization or change output
# dimensions without any signal. To update: fetch the new commit SHA from
# the HF repo's commit history, download the files, recompute sha256sum,
# and update all three values below.
#
# Xenova/bge-small-en-v1.5  ONNX graph pin (resolve/<sha>/...):
#   TODO: replace 'main' below with a specific commit SHA from
#   https://huggingface.co/Xenova/bge-small-en-v1.5/commits/main
#   e.g. resolve/abc1234.../onnx/model.onnx
#
# BAAI/bge-small-en-v1.5  vocab.txt pin:
#   TODO: replace 'main' below with a specific commit SHA from
#   https://huggingface.co/BAAI/bge-small-en-v1.5/commits/main
#   e.g. resolve/def5678.../vocab.txt
#
# sha256 checksums — regenerate with:
#   sha256sum model.onnx vocab.txt
# after downloading from the pinned SHAs. The placeholder values below
# must be replaced before this Dockerfile is used in a secure build.
# Set MODEL_SHA256 and VOCAB_SHA256 to the real values once pinned.
ARG MODEL_SHA256="REPLACE_WITH_REAL_SHA256_AFTER_PINNING_COMMIT"
ARG VOCAB_SHA256="REPLACE_WITH_REAL_SHA256_AFTER_PINNING_COMMIT"

RUN curl -fSL \
        "https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main/onnx/model.onnx" \
        -o model.onnx \
    && curl -fSL \
        "https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/vocab.txt" \
        -o vocab.txt \
    && echo "${MODEL_SHA256}  model.onnx" | sha256sum -c - \
    && echo "${VOCAB_SHA256}  vocab.txt"  | sha256sum -c -

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
#
# WARNING: do NOT override with Embedding__Provider=OpenAi without also
# migrating the pgvector column from vector(384) to vector(3072). The
# column is currently vector(384) matching bge-small-en-v1.5. OpenAI
# text-embedding-3-large emits 3072-dimensional vectors; inserting them
# into a 384-column fails with a dimension mismatch error. A new EF
# migration is required before switching providers.
ENV Embedding__Provider=Onnx
ENV Embedding__ModelPath=models/bge-small-en-v1.5/model.onnx
ENV Embedding__TokenizerPath=models/bge-small-en-v1.5/vocab.txt

EXPOSE 8080

USER appuser

ENTRYPOINT ["dotnet", "OmniSift.Api.dll"]
