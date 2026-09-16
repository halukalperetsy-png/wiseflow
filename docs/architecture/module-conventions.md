# Module and Vertical Slice Conventions

How code is organised inside `src/Api`. Read this before adding a feature.

See [ADR-001](ADR-001-modular-monolith.md) for why.

---

## Two top-level folders

```
src/Api/
├── Modules/           business capabilities, as vertical slices
└── Infrastructure/    technical concerns shared across modules
```

`Modules/` holds behaviour the business would recognise.
`Infrastructure/` holds plumbing the business would not: persistence wiring,
error handling, configuration resolution.

If you are unsure where something goes, ask whether a product owner would name
it. If yes, it is a module.

---

## Module folders are created when their phase starts

The design document names seven domain modules:

```
Identity  Catalog  Media  Ai  Marketplace  Workflow  Audit
```

Each is created by the phase that first needs it. Empty folders with `.gitkeep`
files are not structure, they are noise:

| Module        | Created in |
|---------------|------------|
| `Identity`    | Phase 1 |
| `Catalog`     | Phase 1 (product groups only), the rest in Phase 2 |
| `Media`       | Phase 3 |
| `Ai`          | Phase 4 |
| `Marketplace` | Phase 6 |
| `Workflow`    | Phase 6-7 |
| `Audit`       | Phase 7 |

`Catalog` opened early for one reason: product groups are the unit of data
authorization, so Phase 1 needs them, and `catalog.products` will take a foreign
key to them in Phase 2. It holds that one slice and nothing else.

---

## Anatomy of a slice

A slice is one folder holding everything that feature needs:

```
Modules/<Module>/<Feature>/
├── <Feature>Endpoints.cs     route + wiring
├── <Feature>Request.cs       input contract   (when there is one)
├── <Feature>Response.cs      output contract
├── <Feature>Handler.cs       the actual work  (when non-trivial)
└── <Feature>Validator.cs     input rules      (when there are any)
```

Create only the files the feature actually needs. A read-only endpoint that
projects straight from the database does not need a handler class.

Types default to `internal`. A slice's types are not a public API for other
modules. When two modules genuinely need to share a contract, that is a
deliberate decision, made visibly — not a side effect of a default.

---

## Registration is explicit

Every slice exposes one static extension method. `Program.cs` calls it by name:

```csharp
// Modules/Platform/Health/HealthEndpoints.cs
internal static class HealthEndpoints
{
    internal static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // ... map routes ...
        return app;
    }
}
```

```csharp
// Program.cs
app.MapHealthEndpoints();
app.MapProductEndpoints();     // Phase 2
```

**No `IModule` interface. No assembly scanning. No reflection.**

The list in `Program.cs` is the complete, readable, greppable inventory of what
this application exposes. "Find all references" works. Deleting a slice's file
breaks the build at the call site, which is exactly what should happen.

If the list ever becomes genuinely painful to maintain, revisit this then —
with a real problem to point at.

---

## Route naming

Business endpoints live under `/api`. Command endpoints name the action:

```
GET    /api/products
POST   /api/products
GET    /api/products/{id}
PATCH  /api/products/{id}

POST   /api/products/{id}/media/generate
POST   /api/products/{id}/content/approve
POST   /api/products/{id}/marketplaces/amazon/publish
```

Infrastructure endpoints sit outside `/api`:

```
GET    /health
```

---

## Persistence

One `CommerceOpsDbContext` for the whole application, in
`Infrastructure/Persistence/`.

Entity configuration uses `IEntityTypeConfiguration<T>` classes, placed beside
the entity they configure inside the owning module. `ApplyConfigurationsFromAssembly`
picks them up — so adding an entity means adding two files in one module folder
and nothing else.

**Modules do not add `DbSet` properties.** Module code reads and writes through
`context.Set<T>()`, so `Infrastructure` does not acquire a `using` for every
module that ever ships. The one exception is ASP.NET Core Identity's own sets,
which come from the `IdentityDbContext` base type (see ADR-003).

Names are snake_case, applied by `Infrastructure/Persistence/SnakeCaseNaming.cs`
at the end of `OnModelCreating` — not by a naming-convention plugin. ADR-002
records why.

Modules map to PostgreSQL **schemas** (`identity`, `catalog`, `media`, …). Each
schema is created by the migration that adds its first table. `public` holds no
domain tables; EF's own `__EFMigrationsHistory` is the only thing there.

---

## Authorization (from Phase 1 onward)

Two layers, both enforced server-side:

1. **RBAC** — permission policies on the endpoint.
2. **ProductGroup scope** — a data filter in the query.

A permission check alone is never sufficient. A user holding `Product.Edit` may
still only touch products in their assigned product groups.

Hiding a control in the frontend is a usability decision. It is never a security
control.

The permission catalog is code, not rows: `Modules/Identity/Authorization/Permissions.cs`
holds the names, `RolePermissions.cs` maps roles to them, and every policy is
generated from that catalog so a policy name that does not exist cannot be
referenced. ADR-003 explains the reasoning.

A record outside the caller's scope answers **404**, not 403. A user who may not
see something should not learn that it exists.

---

## Naming

All identifiers are **English** — types, members, routes, database objects,
configuration keys, commit messages.

C# uses PascalCase types/members and `_camelCase` private fields, enforced by
`.editorconfig`. PostgreSQL uses `snake_case`, mapped automatically rather than
spelled out per property.
