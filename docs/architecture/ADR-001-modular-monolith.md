# ADR-001 — Modular Monolith with Vertical Slices

- **Status:** Accepted
- **Date:** 2026-09-14
- **Phase:** 0 (Bootstrap)

## Context

CommerceOps replaces a single-user desktop pipeline (Tkinter + local files + Excel)
with a multi-user web platform. The domain spans catalog, media, AI generation,
marketplace publishing, workflow and audit.

The team is small. The first production target is a single Linux VPS. Deployment,
operational monitoring and on-call capacity all have to stay proportionate to that.

The alternatives considered were microservices per bounded context, and a layered
monolith (Domain / Application / Infrastructure projects).

## Decision

A **modular monolith** deployed as one ASP.NET Core process, organised in
**vertical slices**.

- One `CommerceOps.Api` project. Modules are folders under `Modules/`, not
  separate assemblies.
- Each feature slice owns its endpoint, handler and request/response types, in
  one folder, next to each other.
- Cross-module technical concerns (persistence, error handling, configuration)
  live under `Infrastructure/`.
- Slices register themselves through an explicit static extension method called
  from `Program.cs`. No `IModule` interface, no reflection-based discovery.
- Module folders are created when their phase starts, not up front.

See `module-conventions.md` for the concrete layout and naming rules.

## Consequences

**Gained**

- One process to run, deploy, log and debug. One database transaction boundary.
- Refactoring across module boundaries is a compiler-checked rename, not a
  contract negotiation.
- Reading a feature means reading one folder, not tracing a request through
  four layers in four projects.
- A module can later be extracted into its own service if — and only if — load
  actually demands it. The folder boundary is the seam.

**Accepted costs**

- Module isolation is a convention, not a compiler guarantee. A slice *can*
  reach into another module's types. This is enforced by review, and by keeping
  shared contracts explicit.
- The whole application scales as one unit. At current volume (batches of ~10
  images) this is not a constraint.
- A single deployment means one failing module can affect the process. Mitigated
  by workflow-level retry and idempotency rather than by process isolation.

## Explicitly not chosen

Microservices, an event bus, Kafka/RabbitMQ, Redis, Kubernetes, and a separate
worker executable. Background work runs in-process when it arrives in a later
phase. None of these are added ahead of a measured need.
