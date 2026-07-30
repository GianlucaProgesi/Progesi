# Progesi.Infrastructure.EF

Entity Framework persistence for the **web tier** (ASP.NET). The Rhino/Grasshopper tier uses direct SQLite repositories instead.

## Lifetime and concurrency

- **`ProgesiDbContext` and EF repository implementations are not thread-safe.** Register them **scoped per HTTP request / unit-of-work** in DI (one context per scope).
- Do not share a single repository or DbContext across parallel tasks or threads.
- Connection strings get a default `busy_timeout` (`Default Timeout=5`) and EF uses a SQLite busy/locked retry execution strategy for WAL contention — parity with the direct SQLite tier.

## Schema

Schema evolves through EF migrations (`Database.Migrate()`). Do not use `EnsureCreated()` for production or test bootstrap.
