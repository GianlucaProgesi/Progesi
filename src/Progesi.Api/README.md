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

## Architecture notes

- `ProgesiDbContext` and EF repositories are **scoped per request** (see `Progesi.Infrastructure.EF/README.md`).
- Schema is applied via `Database.Migrate()` on startup.
- Authentication, cloud DB provisioning, and Rhino sync are out of scope for this MVP.
