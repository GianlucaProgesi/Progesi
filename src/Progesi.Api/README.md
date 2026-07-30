# Progesi.Api

ASP.NET Core Web API for the Progesi web tier (ADR-016). EF-backed CRUD over Variables, Metadata, and Clusters.

## Prerequisites

- .NET 8 SDK
- Built solution (`dotnet build -c Release` from repo root)

## Configuration

Per-project databases are resolved via the **`X-Project-Id`** request header (falls back to `Progesi:DefaultProjectId`, default `"default"`).

| Key | Purpose |
|---|---|
| `Progesi:DbProvider` | `Sqlite` (dev/CI default) or `SqlServer` (prod-ready seam) |
| `Progesi:DefaultProjectId` | Project used when `X-Project-Id` is absent |
| `Progesi:ProjectsDirectory` | Folder for SQLite project DBs + `projects.json` registry |
| `ConnectionStrings:SqlServerProjectTemplate` | SqlServer template with `{ProjectId}` placeholder (deploy only) |

Optional test/bootstrap flag:

```json
"Progesi": {
  "ResetSchemaOnStartup": false
}
```

When `true`, deletes and re-migrates the **default** project database on startup (integration tests only).

## Run locally

```powershell
cd src/Progesi.Api
dotnet run
```

Open Swagger UI: [https://localhost:7xxx/swagger](https://localhost:5001/swagger) (port from `Properties/launchSettings.json`).

## Endpoints

| Resource | Routes |
|---|---|
| Projects | `GET/POST /api/projects`, `GET /api/projects/{id}` (writer only) |
| Variables | `GET/POST /api/variables`, `GET/PUT/DELETE /api/variables/{id}` |
| Metadata | `GET/POST /api/metadata`, `GET/PUT/DELETE /api/metadata/{id}` |
| Clusters | `GET/POST /api/clusters`, `GET/PUT/DELETE /api/clusters/{id}` |
| Summary | `GET /api/summary`, `GET /api/summary/value-types` (reader; project-scoped) |

Pass **`X-Project-Id`** on CRUD and summary requests to target a specific project database.

All responses use API DTOs only (no Core types on the wire).

## Multi-project provisioning (A3.3)

- **Template clone = Migrate()** on a fresh empty database (EF migrations are the schema template).
- `POST /api/projects` (writer) provisions a new per-project DB and registers it in `projects.json`.
- Dev/CI uses one SQLite file per project; production swaps `Progesi:DbProvider` to `SqlServer` and supplies `SqlServerProjectTemplate` at deploy time.
- **Deploy-time (not CI):** Azure SQL provisioning and running provider-specific migrations against live cloud DBs.

## Dashboard summary (A4.1)

Read-only, project-scoped statistics for dashboards (Power BI or other consumers):

- `GET /api/summary` — counts, metadata coverage, cluster membership stats, value-type breakdown
- `GET /api/summary/value-types` — value-type breakdown only

Both require the **reader** policy and honour **`X-Project-Id`**.

## Authentication (ADR-018)

The API uses **Entra External ID** (Azure AD) JWT bearer tokens via `Microsoft.Identity.Web`.

Configuration section: `AzureAd` in `appsettings.json` (placeholders only — inject real values at deploy via environment variables or user-secrets):

| Key | Purpose |
|---|---|
| `Instance` | Entra authority URL (e.g. `https://login.microsoftonline.com/` or CIAM tenant URL) |
| `TenantId` | Directory / tenant ID |
| `ClientId` | App registration client ID |
| `Audience` | API audience (app ID URI) |

**App roles** (assign in Entra app registration):

| Role | Access |
|---|---|
| `reader` | `GET` on Variables, Metadata, Clusters |
| `writer` | `POST` / `PUT` / `DELETE` (writers also satisfy reader policy) |

Swagger UI exposes a **Bearer** token field for interactive testing once a token is available.

**Deploy-time step (not CI):** provision the Entra External ID tenant, register the API app, define `reader`/`writer` app roles, and configure the deployed `AzureAd` settings. CI validates auth behaviour with a test scheme — no live tenant required.

## Architecture notes

- `ProgesiDbContext` and EF repositories are **scoped per request per project** (see `Progesi.Infrastructure.EF/README.md`).
- Schema is applied via `Database.Migrate()` when a project is provisioned.
- Auth↔project membership mapping and Rhino sync are out of scope for this MVP.
