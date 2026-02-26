# OmniSift — Product Design Document (PDD)

**Version:** 1.0
**Date:** 2026-02-25
**Status:** Active Development

---

## 1. Product Vision

OmniSift is an AI-powered research platform that empowers individuals, genealogists, journalists, and investigators to aggregate, search, and synthesize personal and family history data from multiple sources — both private documents and public web records — into a unified, intelligent knowledge base.

The platform acts as a tireless research assistant that can independently search across uploaded documents, live web sources, and archived internet snapshots to answer complex, multi-step research questions about people and their histories.

## 2. Problem Statement

Researching personal and family histories is a fragmented, labor-intensive process:

- **Data is scattered** across physical documents, digital files, text messages, web pages, and archived sources
- **Manual searching** through hundreds of documents for relevant details is time-consuming and error-prone
- **Web sources disappear** — critical obituaries, news articles, and public records go offline with no way to recover them
- **Connecting the dots** between disparate pieces of information requires significant cognitive effort
- **No unified tool** exists that combines document ingestion, web scraping, archive retrieval, and AI-powered synthesis

## 3. Target Audience

### Primary Users
| Persona | Description | Key Need |
|---|---|---|
| **Genealogists** | Amateur and professional family history researchers | Aggregate family records and public data into timelines |
| **Journalists** | Investigative reporters researching individuals | Cross-reference public records with private source documents |
| **Legal Professionals** | Paralegals and attorneys building case profiles | Search and synthesize large document collections efficiently |
| **Private Investigators** | Licensed PIs conducting background research | Combine web scraping with document analysis |

### Secondary Users
| Persona | Description | Key Need |
|---|---|---|
| **Individuals** | People researching their own family history | Upload and search personal documents with AI assistance |
| **Archivists** | Digital preservation professionals | Bulk ingest and semantically index document collections |

## 4. Core Features

### 4.1 Document Ingestion & Processing
- **PDF Upload & Extraction** — Upload PDF documents; text is automatically extracted, chunked, and semantically indexed
- **Text Message Import** — Import text message exports (CSV/JSON) with conversation threading and timestamp normalization
- **Web Page Capture** — Provide URLs; article text is extracted, cleaned, and indexed for semantic search
- **Intelligent Chunking** — All content is split into overlapping 500-token chunks for optimal retrieval accuracy

### 4.2 AI-Powered Research Agent
- **Natural Language Queries** — Ask complex research questions in plain English
- **Autonomous Tool Selection** — The agent independently decides which tools to use based on the query:
  - Search uploaded documents (vector similarity)
  - Execute live web searches
  - Check the Wayback Machine for archived pages
- **Source Attribution** — Every answer includes citations linking back to the original source documents or URLs
- **Conversational Context** — Multi-turn conversations maintain context for follow-up questions

### 4.3 Multi-Tenant Workspace
- **Isolated Workspaces** — Each tenant's data is completely isolated at the database level
- **Team Collaboration** — Multiple users can belong to the same tenant organization
- **Data Sovereignty** — Tenant data never leaks across organizational boundaries

### 4.4 Knowledge Management
- **Data Source Registry** — Track all ingested sources with metadata (type, upload date, status)
- **Chunk Explorer** — Browse and inspect individual document chunks and their embeddings
- **Search History** — Review past queries and agent responses

## 5. User Journeys

### Journey 1: Document Upload & Research
```
1. User logs in → lands on Dashboard
2. User clicks "Upload" → selects PDF files
3. System ingests, chunks, and embeds documents (progress shown)
4. User navigates to "Research" chat interface
5. User asks: "What addresses did John Smith live at between 1990 and 2005?"
6. Agent searches vector DB → finds relevant chunks → synthesizes answer
7. Answer displayed with source citations (clickable links to original chunks)
```

### Journey 2: Web-Augmented Research
```
1. User asks: "Find recent obituaries for Mary Johnson in Ohio"
2. Agent determines web search is needed → calls WebScraperPlugin
3. Agent finds relevant URLs → extracts article text
4. If any URLs are dead → Agent calls WaybackMachinePlugin for archived versions
5. Agent synthesizes findings with any matching uploaded documents
6. Comprehensive answer displayed with both web and document sources
```

## 6. Non-Functional Requirements

| Requirement | Target |
|---|---|
| **Response Time** | Agent responses within 15 seconds for typical queries |
| **Upload Processing** | PDFs processed at ~10 pages/second |
| **Concurrent Users** | Support 100+ concurrent users per deployment |
| **Data Retention** | All uploaded data retained until explicitly deleted by tenant |
| **Availability** | 99.5% uptime target for cloud deployments |
| **Security** | All data encrypted at rest and in transit; strict tenant isolation |

## 7. Success Metrics

| Metric | Definition | Target |
|---|---|---|
| **Query Relevance** | % of agent responses rated "helpful" by users | > 80% |
| **Ingestion Success** | % of uploaded documents successfully processed | > 95% |
| **Source Recovery** | % of dead links successfully retrieved via Wayback Machine | > 60% |
| **User Retention** | Monthly active users returning after first month | > 70% |

## 8. Future Roadmap

| Phase | Features |
|---|---|
| **v1.1** | Image/OCR ingestion (scanned documents), email import (EML/MBOX) |
| **v1.2** | Timeline visualization, relationship graph builder |
| **v1.3** | Collaborative annotations, shared research workspaces |
| **v2.0** | Multi-language support, real-time web monitoring alerts |
