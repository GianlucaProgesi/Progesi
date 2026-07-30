# Progesi.Api

ASP.NET Core Web API for the Progesi web tier (ADR-016). EF-backed CRUD over Variables, Metadata, and Clusters.

## Prerequisites

- .NET 8 SDK
- Built solution (`dotnet build -c Release` from repo root)

## Configuration

Connection string key: `ConnectionStrings:ProgesiDb`.

Default (local dev SQLite file):

```json
"ConnectionStrings": {
  "ProgesiDb": "Data Source=progesi-api.sqlite"
}
```

The provider is selected via EF configuration; swap the connection string later for Azure SQL or Postgres without changing controllers.

Optional test/bootstrap flag:

```json
"Progesi": {
  "ResetSchemaOnStartup": false
}
```

When `true`, deletes and re-migrates the database on startup (integration tests only).

## Run locally

```powershell
cd src/Progesi.Api
dotnet run
```

Open Swagger UI: [https://localhost:7xxx/swagger](https://localhost:5001/swagger) (port from `Properties/launchSettings.json`).

## Endpoints

| Resource | Routes |
|---|---|
| Variables | `GET/POST /api/variables`, `GET/PUT/DELETE /api/variables/{id}` |
| Metadata | `GET/POST /api/metadata`, `GET/PUT/DELETE /api/metadata/{id}` |
| Clusters | `GET/POST /api/clusters`, `GET/PUT/DELETE /api/clusters/{id}` |

All responses use API DTOs only (no Core types on the wire).

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

- `ProgesiDbContext` and EF repositories are **scoped per request** (see `Progesi.Infrastructure.EF/README.md`).
- Schema is applied via `Database.Migrate()` on startup.
- Cloud DB provisioning and Rhino sync are out of scope for this MVP.
