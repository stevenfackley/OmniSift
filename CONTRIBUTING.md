# Contributing to OmniSift

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- An editor with C# support (Visual Studio, Rider, or VS Code with C# Dev Kit)

### Local Setup

```bash
git clone https://github.com/stevenfackley/OmniSift.git
cd OmniSift
cp .env.example .env   # fill in your API keys
docker compose up -d db
dotnet restore OmniSift.sln
dotnet build OmniSift.sln
```

### Running Tests

```bash
dotnet test OmniSift.sln
```

Tests use an in-memory database — no running PostgreSQL instance is required.

## Branch & PR Workflow

- Branch from `main` using the pattern `feat/`, `fix/`, or `chore/` followed by a short description
- Keep PRs focused on a single concern
- All CI checks must pass before merging

## Code Style

- Follow standard C# conventions (PascalCase types/methods, camelCase locals)
- Async methods must be suffixed with `Async`
- Do not commit secrets, build artifacts, or `.env` files — the `.gitignore` covers these

## Project Structure

```
src/OmniSift.Api/        # ASP.NET Core backend
src/OmniSift.Web/        # Blazor WebAssembly frontend
src/OmniSift.Shared/     # Shared DTOs and contracts
tests/OmniSift.UnitTests/
tests/OmniSift.IntegrationTests/
infra/db/                # PostgreSQL schema and seed scripts
docs/                    # Design documents
```

## Reporting Issues

Open an issue describing the problem, steps to reproduce, and expected vs. actual behaviour.
