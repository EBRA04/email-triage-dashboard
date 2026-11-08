# Email Triage Dashboard

An AI-powered email triage and routing system: a single "contact-us" Gmail
inbox gets read, classified by department (HR / IT / Marketing / Sales) with
an LLM, and routed into a department-scoped dashboard — with a full audit
trail and a "view original email" option so staff can double-check the AI's
routing decision.

See `architecture-diagram.html` for a visual walkthrough of the pipeline.

## Stack

- **Backend:** ASP.NET Core 10 Web API (controllers) + EF Core + PostgreSQL
- **Frontend:** React 19 + Vite
- **Email ingestion:** Gmail API (OAuth2, installed-app flow)
- **Classification:** Groq API (`openai/gpt-oss-120b`)
- **Auth:** JWT bearer tokens, BCrypt password hashes, department-scoped claims

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- Docker (for local Postgres)
- A Gmail account + OAuth client credentials ([console.cloud.google.com](https://console.cloud.google.com) → enable Gmail API → OAuth client ID, type "Desktop app")
- A free Groq API key ([console.groq.com](https://console.groq.com))

## Setup

1. **Start Postgres:**
   ```bash
   docker compose up -d
   ```

2. **Configure the API:**
   ```bash
   cp api/appsettings.Development.json.example api/appsettings.Development.json
   ```
   Fill in `Gmail:ClientId`, `Gmail:ClientSecret`, `Groq:ApiKey`, and a random
   `Jwt:Secret`. The `ConnectionStrings:Default` default already matches the
   docker-compose Postgres above.

3. **Apply migrations and seed test data:**
   ```bash
   cd api
   dotnet tool install --global dotnet-ef   # first time only
   dotnet ef database update
   ```
   This also seeds 4 departments and 5 test users (see below) on first run.

4. **Run the API:**
   ```bash
   dotnet run --launch-profile http
   ```
   Listens on `http://localhost:5074`. First call to any `/api/ingestion/*`
   endpoint opens a browser for one-time Gmail OAuth consent; the token is
   then cached in `api/.gmail-credentials/` (git-ignored) for silent refresh.

5. **Run the frontend:**
   ```bash
   cd web
   npm install
   npm run dev
   ```
   Open `http://localhost:5173`.

## Test accounts

All seeded with password `password123`:

| Email                     | Role       | Department        |
|---------------------------|------------|--------------------|
| admin@company.test        | SuperAdmin | all                |
| hr@company.test           | DeptAdmin  | Human Resources    |
| it@company.test           | DeptAdmin  | IT Support         |
| marketing@company.test    | DeptAdmin  | Marketing          |
| sales@company.test        | DeptAdmin  | Sales              |

## Pulling and classifying email

```bash
# Pull recent inbox messages + classify any unprocessed ones in one call
curl -X POST "http://localhost:5074/api/ingestion/run?max=10"

# Or run the two steps separately
curl -X POST "http://localhost:5074/api/ingestion/gmail/run?max=10"
curl -X POST "http://localhost:5074/api/ingestion/classify/run"
```

Emails classified with confidence below 60%, or that don't match a known
department, land in the **Needs Review** queue instead of being auto-routed.

## Data model

- `RawEmail` — immutable copy of the original inbound email; never edited
  after insert. This is the "view original" source of truth.
- `Ticket` — the routed work item: department, status, AI classification
  output (department guess, confidence, summary, reasoning).
- `AuditLog` — append-only log of every status change, reassignment, and
  the original AI classification, tied to its ticket.
- `Department` / `User` — department scoping and login accounts.

## Security notes for this learning project

- Real secrets (Gmail OAuth client, Groq API key, JWT signing secret) live
  only in `api/appsettings.Development.json`, which is git-ignored. Only
  `appsettings.Development.json.example` (no real values) is committed.
- The cached Gmail OAuth token (`api/.gmail-credentials/`) is also
  git-ignored.
- CORS is currently locked to `http://localhost:5173` for local dev only.
- This has not been hardened for production deployment (no HTTPS
  enforcement in dev, no rate limiting, no automated background polling —
  ingestion is manual-trigger only). Treat it as a learning/demo project.
