# CommerceOps

Product operations platform: product intake, media pipeline, SEO content, and
marketplace publishing.

- Backend: ASP.NET Core / .NET 10, modular monolith with vertical slices
- Frontend: React + TypeScript + Vite
- Database: PostgreSQL 18 (via Docker Compose — **not** installed natively)

Architecture decisions live in [`docs/architecture/`](docs/architecture/).

> **Status: Phase 0 (Bootstrap) complete.** The skeleton runs end to end: the
> database starts from Compose, the API connects and exposes `/health`, the web
> app polls it, and unit plus integration tests pass locally. No domain
> features yet — those begin in Phase 1.

---

## Prerequisites

| Tool | Version used | winget id |
|---|---|---|
| Git | 2.55 | `Git.Git` |
| .NET SDK | **10.0.401** (pinned in `global.json`) | `Microsoft.DotNet.SDK.10` |
| Node.js | 24.19.0 LTS | `OpenJS.NodeJS.LTS` |
| Docker Desktop | 4.91 (Linux containers) | `Docker.DockerDesktop` |

Install from an **elevated** PowerShell:

```powershell
winget install --id Git.Git                 -e --source winget
winget install --id Microsoft.DotNet.SDK.10 -e --source winget
winget install --id OpenJS.NodeJS.LTS       -e --source winget
winget install --id Docker.DockerDesktop    -e --source winget
```

**Reboot afterwards.** Docker Desktop enables the WSL2 / Virtual Machine
Platform Windows features, which do not take effect until restart. Then open
Docker Desktop once and let the engine start.

Verify:

```powershell
git --version
dotnet --list-sdks          # must include 10.0.401
node --version
docker compose version
docker run --rm hello-world
```

`global.json` pins the SDK to exactly `10.0.401` with `rollForward: disable`.
A different SDK refuses to build rather than silently using another compiler.
Upgrading is a deliberate edit to that file.

---

## One-time setup

```powershell
cd D:\Git\wiseflow

# 1. Database credentials for Docker Compose.
Copy-Item .env.example .env
#    Open .env and set POSTGRES_PASSWORD to a value of your choosing.
#    "docker compose up" stops with an explicit message until you do.
#    .env is git-ignored and must never be committed.

# 2. Local .NET tools (dotnet-ef 10.0.12).
dotnet tool restore

# 3. API connection string -- stored outside the repository.
dotnet user-secrets set "ConnectionStrings:CommerceOpsDb" "Host=127.0.0.1;Port=5432;Database=commerceops;Username=commerceops;Password=PASTE_FROM_DOTENV" --project src/Api

# 4. Frontend dependencies.
cd src\WebApp
npm ci
cd ..\..
```

There is no connection string in `appsettings.json`. If it is missing at
startup the API stops immediately with a message naming the exact command to
run.

> **Use `127.0.0.1`, not `localhost`.** Compose publishes the database on the
> IPv4 loopback only, while `localhost` resolves to `::1` (IPv6) on Windows, so
> `Host=localhost` cannot connect.

> `npm ci` installs `package-lock.json` exactly and fails if it disagrees with
> `package.json`. Use `npm install` only when deliberately changing dependencies.

---

## Running locally

Three things run side by side: the database in Docker, the API, and the web app.

```powershell
# Terminal 0 -- database (leave running)
docker compose up -d --wait

# Apply migrations (first run, and whenever new ones arrive)
dotnet ef database update --project src/Api

# Terminal 1 -- API           ->  http://localhost:5080
dotnet run --project src/Api

# Terminal 2 -- web app       ->  http://localhost:5173
cd src\WebApp
npm run dev
```

Open <http://localhost:5173>. The page shows API health, re-checked every 5
seconds, including a live database check.

The API listens on **HTTP only** in development and the Vite dev server proxies
`/health` to it, so there is no HTTPS certificate to trust and no CORS to
configure. TLS is terminated by the reverse proxy in deployment.

---

## Verifying

```powershell
dotnet build
dotnet test                                    # unit + integration

cd src\WebApp
npm run lint
npm run typecheck
npm run build
```

Integration tests start their own throwaway PostgreSQL 18 container via
Testcontainers, so they neither need nor touch the development database, and
they never read your user secrets.

To reproduce exactly what CI runs:

```powershell
dotnet build -c Release -p:ContinuousIntegrationBuild=true
dotnet test  -c Release --no-build -p:ContinuousIntegrationBuild=true
```

`ContinuousIntegrationBuild=true` turns warnings into errors. Check the gate is
live with:

```powershell
dotnet msbuild src\Api\CommerceOps.Api.csproj -getProperty:TreatWarningsAsErrors
dotnet msbuild src\Api\CommerceOps.Api.csproj -getProperty:TreatWarningsAsErrors -p:ContinuousIntegrationBuild=true
```

The first prints `false`, the second `true`.

---

## Database operations

```powershell
docker compose ps                  # status, including health
docker compose logs -f postgres
docker compose stop postgres       # useful for watching /health go unhealthy
docker compose start postgres

docker compose down                # stop containers, KEEP data
docker compose up -d --wait        # bring them back, data intact
```

Open a SQL prompt:

```powershell
docker compose exec postgres psql -U commerceops -d commerceops
```

### Resetting — destructive, not routine

```powershell
docker compose down -v             # DELETES the volume and all data
```

Only when you intend to wipe the database. It is not part of any verification
flow.

---

## Layout

```
.
├── docker-compose.yml         PostgreSQL 18 for local development
├── .env.example               template for .env (git-ignored)
├── global.json                SDK pin + test runner selection
├── Directory.Build.props      shared MSBuild settings
├── .config/dotnet-tools.json  dotnet-ef, version pinned
├── docs/architecture/         ADRs and conventions
├── src/
│   ├── Api/                   ASP.NET Core minimal API
│   │   ├── Modules/           vertical slices, by business capability
│   │   └── Infrastructure/    persistence, configuration, error handling
│   └── WebApp/                React + TypeScript + Vite
└── tests/
    ├── UnitTests/
    └── IntegrationTests/
```

Read [`docs/architecture/module-conventions.md`](docs/architecture/module-conventions.md)
before adding a feature.

---

## Secrets

Nothing secret is committed. Ever.

| Value | Development | CI / production |
|---|---|---|
| Database password (Compose) | `.env`, git-ignored | environment variable |
| API connection string | .NET User Secrets | `ConnectionStrings__CommerceOpsDb` |

`.gitignore` covers `.env`, `*.local.json` and `secrets.json`. If you add a new
credential, add its shape to `.env.example` — never its value.

---

## Troubleshooting

**`docker compose up` fails: POSTGRES_PASSWORD is empty or unset**
`.env` is missing or the password is blank. Copy `.env.example` and set a value.

**API exits at startup complaining about `CommerceOpsDb`**
The connection string is not configured. Run the `dotnet user-secrets set`
command from *One-time setup*.

**API starts but `/health` reports the database unhealthy after ~2 seconds**
Usually `Host=localhost` in the connection string: the database is published on
`127.0.0.1` only, and `localhost` resolves to IPv6 on Windows. Use `127.0.0.1`.
Otherwise check `docker compose ps` — `postgres` should read `running (healthy)`.

**Web page says the API is unreachable**
The API is not running, or it is not on port 5080. The port is set in
`src/Api/Properties/launchSettings.json` and must match the proxy target in
`src/WebApp/vite.config.ts`.

**Build fails with "file is being used by another process"**
A `dotnet run` instance is still holding the build output. Stop it first.

**Port 5432 already in use**
Set `POSTGRES_PORT` in `.env` to a free port, and update the port in the User
Secrets connection string to match.

**Tools "not found" right after installing them**
Open a new terminal. A running shell keeps the `PATH` it started with.
