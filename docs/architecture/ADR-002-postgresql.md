# ADR-002 — PostgreSQL 18 as the primary database, run via Docker Compose

- **Status:** Accepted
- **Date:** 2026-09-14
- **Phase:** 0 (Bootstrap)

## Context

The legacy pipeline stored state in `designs.<category>.json` files on disk.
That does not survive multiple concurrent users, authorization scoping, audit
trails, or per-product workflow state.

The data is strongly relational (product groups, products, assets, listings,
workflow steps) but also carries schemaless payloads: AI request/response
metadata, marketplace JSON payloads, and cached Amazon product type schemas.

There is no organisational requirement to use SQL Server, and no licence budget.

## Decision

**PostgreSQL 18** is the primary datastore, accessed through **EF Core** with the
**Npgsql** provider.

**It is not installed natively on developer machines.** It runs as a container
defined in the repository's `docker-compose.yml`.

Development connection strings are supplied by **.NET User Secrets**; CI and
production use the `ConnectionStrings__CommerceOpsDb` environment variable. No
connection string is committed to the repository.

The container publishes its port on **`127.0.0.1` only**. Written without that
prefix, Docker binds `0.0.0.0` and the development database becomes reachable
from every machine on the local network. This is the development counterpart of
the production rule that port 5432 is never exposed beyond the application
network.

Schema evolves **incrementally, one phase at a time** — see ADR-001's phasing.
Phase 0 applies an empty `InitialCreate` migration whose only effect is to create
`__EFMigrationsHistory`, proving the toolchain before any domain schema exists.

## Consequences

**Gained**

- No licence cost, and a strong fit for relational domain data.
- `jsonb` covers marketplace and AI payloads without a second datastore.
- The version is pinned in the compose file, so every machine and CI run uses
  the same PostgreSQL major. No "works on my machine" drift.
- The database resets with `docker compose down -v`, which makes migration and
  seed work cheap to iterate on.
- Full-text and vector extensions are available later without a migration to a
  different engine.

**Accepted costs**

- Docker becomes a hard prerequisite for development. On Windows this means
  Docker Desktop and WSL2, which needs one elevated install and a reboot.
- The container's storage layout must be mounted correctly. PostgreSQL 18 places
  `PGDATA` differently from earlier majors, so the volume is mounted at the
  parent path `/var/lib/postgresql`. Data persistence across
  `docker compose down` / `up` is verified explicitly, not assumed.
- Team members need basic PostgreSQL familiarity (`psql`, schemas, `jsonb`).
- Because the port is bound to the IPv4 loopback, connection strings must use
  `127.0.0.1` rather than `localhost`: on Windows `localhost` resolves to `::1`
  first, and nothing is listening there.

## Notes for the next phase

All table and column names in the design document are `snake_case`. Before the
first real entity migration is generated, add `EFCore.NamingConventions` and call
`UseSnakeCaseNamingConvention()`. Doing this after migrations exist would mean
rewriting them.
