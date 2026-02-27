# OmniSift

![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)


![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)


[![CI](https://github.com/stevenfackley/OmniSift/actions/workflows/ci.yml/badge.svg)](https://github.com/stevenfackley/OmniSift/actions/workflows/ci.yml)

**AI-Powered Multi-Tenant Research SaaS for Personal & Family History**

OmniSift is an agentic research application that aggregates, parses, and synthesizes personal and family history data. It searches internal user-provided documents and actively scrapes public web sources to build comprehensive personal profiles and timelines.

---

## 🏗️ Architecture

| Layer | Technology |
|---|---|
| **Frontend** | Blazor WebAssembly |
| **Backend API** | ASP.NET Core 8 (C# 12) |
| **AI Orchestration** | Microsoft Semantic Kernel |
| **Database** | PostgreSQL + pgvector |
| **Containerization** | Docker & Docker Compose |
| **LLM** | Claude 4.6 Sonnet (Logic/Agents) |
| **Embeddings** | OpenAI `text-embedding-3-large` |

## 🚀 Quick Start

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) & [Docker Compose](https://docs.docker.com/compose/install/)
- (Optional) [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for local development

### 1. Clone & Configure

```bash
git clone https://github.com/stevenfackley/OmniSift.git
cd OmniSift
```

Fill in your API keys in `.env.dev` (committed with safe defaults — only the keys need updating):

```bash
# Edit .env.dev and set your personal dev keys
ANTHROPIC_API_KEY=sk-ant-...
OPENAI_API_KEY=sk-...
TAVILY_API_KEY=tvly-...
```

### 2. Build & Run

```bash
docker compose up --build
```

### 3. Access

| Service | URL |
|---|---|
| **Frontend (Blazor)** | [http://localhost:5080](http://localhost:5080) |
| **Backend API** | [http://localhost:5000](http://localhost:5000) |
| **API Docs (Swagger)** | [http://localhost:5000/swagger](http://localhost:5000/swagger) |
| **PostgreSQL** | `localhost:5432` |

---

## 📂 Project Structure

```
OmniSift/
├── OmniSift.sln                       # Solution file
├── docker-compose.yml                 # Dev orchestration (uses .env.dev)
├── docker-compose.prod.yml            # Prod overlay — adds Docker secrets
├── .env.example                       # Environment variable template
├── .env.dev                           # Development defaults (committed)
├── .env.test                          # Test/CI defaults (committed)
├── .env.prod                          # Production values (gitignored)
├── .github/
│   └── workflows/
│       └── ci.yml                     # Build & test pipeline
├── docs/
│   ├── PDD.md                         # Product Design Document
│   └── SDD.md                         # Software Design Document
├── infra/
│   └── db/
│       └── init.sql                   # PostgreSQL schema + RLS policies
├── secrets/
│   └── README.md                      # How to populate Docker secret files
├── src/
│   ├── OmniSift.Shared/               # Shared DTOs and contracts
│   ├── OmniSift.Api/                  # ASP.NET Core Web API
│   │   ├── Controllers/               # API endpoints
│   │   ├── Data/                      # EF Core DbContext
│   │   ├── Middleware/                # Tenant resolution middleware
│   │   ├── Models/                    # Domain entities
│   │   ├── Plugins/                   # Semantic Kernel plugins
│   │   ├── Services/                  # Business logic & data ingestion
│   │   └── Dockerfile
│   └── OmniSift.Web/                  # Blazor WebAssembly frontend
│       ├── Pages/                     # Routable pages
│       ├── Layout/                    # App shell layout
│       ├── Services/                  # HTTP client services
│       └── Dockerfile
└── tests/
    ├── OmniSift.UnitTests/            # xUnit unit tests
    └── OmniSift.IntegrationTests/     # xUnit integration tests
```

---

## 🧠 AI Agent Capabilities

OmniSift uses **Microsoft Semantic Kernel** to orchestrate an intelligent research agent with the following plugins:

| Plugin | Description |
|---|---|
| **VectorSearchPlugin** | Queries the internal pgvector database for uploaded & embedded records |
| **WebScraperPlugin** | Executes live public web searches via configurable search API |
| **WaybackMachinePlugin** | Retrieves archived snapshots of dead links via the Internet Archive API |

## 📄 Data Ingestion

Supported upload formats:

- **PDF Documents** — Extracted via PdfPig, chunked, and embedded
- **Text Message Exports** — CSV/JSON parsing with timestamp normalization
- **Web Pages** — HTML stripped and article text isolated via HtmlAgilityPack

All ingested data is chunked (500 tokens, 50-token overlap), embedded via OpenAI, and stored in pgvector for semantic search.

## 🔐 Multi-Tenancy

- Every database table includes a `tenant_id` column
- PostgreSQL Row-Level Security (RLS) enforced at the database level
- Tenant resolution via `X-Tenant-Id` header on every API request
- Middleware validates tenant context before any data access

## 🧪 Testing

```bash
# Run all tests
dotnet test OmniSift.sln

# Run unit tests only
dotnet test tests/OmniSift.UnitTests/

# Run integration tests only
dotnet test tests/OmniSift.IntegrationTests/
```

## 📝 Documentation

- [Product Design Document (PDD)](./docs/PDD.md)
- [Software Design Document (SDD)](./docs/SDD.md)

## 📜 License

Source-available — forks permitted for personal and non-commercial use. Commercial use requires written permission. See [LICENSE](./LICENSE) for full terms.
