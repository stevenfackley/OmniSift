# OmniSift

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
git clone https://github.com/your-org/OmniSift.git
cd OmniSift
```

Create a `.env` file in the project root:

```env
# Database
POSTGRES_USER=omnisift
POSTGRES_PASSWORD=your_secure_password
POSTGRES_DB=omnisift

# API Keys (Replace with your actual keys)
ANTHROPIC_API_KEY=sk-ant-...
OPENAI_API_KEY=sk-...
TAVILY_API_KEY=tvly-...

# JWT
JWT_SECRET=your_jwt_secret_at_least_32_chars_long
JWT_ISSUER=https://omnisift.local
JWT_AUDIENCE=omnisift-api
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
├── README.md                          # This file
├── PDD.md                             # Product Design Document
├── SDD.md                             # Software Design Document
├── docker-compose.yml                 # Container orchestration
├── .env                               # Environment variables (create manually)
├── db/
│   └── init.sql                       # Database schema + RLS policies
├── src/
│   ├── OmniSift.Shared/              # Shared DTOs and contracts
│   ├── OmniSift.Api/                 # ASP.NET Core Web API
│   │   ├── Controllers/              # API endpoints
│   │   ├── Data/                     # EF Core DbContext
│   │   ├── Middleware/               # Tenant resolution middleware
│   │   ├── Models/                   # Domain entities
│   │   ├── Plugins/                  # Semantic Kernel plugins
│   │   ├── Services/                 # Business logic & data ingestion
│   │   └── Dockerfile
│   └── OmniSift.Web/                # Blazor WebAssembly frontend
│       ├── Pages/                    # Routable pages
│       ├── Components/               # Reusable UI components
│       ├── Services/                 # HTTP client services
│       ├── Layout/                   # App shell layout
│       └── Dockerfile
├── tests/
│   ├── OmniSift.UnitTests/          # xUnit unit tests
│   └── OmniSift.IntegrationTests/   # xUnit integration tests
└── OmniSift.sln                      # Solution file
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

- [Product Design Document (PDD)](./PDD.md)
- [Software Design Document (SDD)](./SDD.md)

## 📜 License

Proprietary — All rights reserved.
