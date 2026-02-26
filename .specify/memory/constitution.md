<!--
SYNC IMPACT REPORT
==================
Version change: [TEMPLATE] → 1.0.0
Modified principles: N/A (initial ratification from blank template)
Added sections:
  - Core Principles (5 principles defined)
  - Technology Stack Constraints
  - Development Workflow
  - Governance
Templates reviewed:
  - .specify/templates/plan-template.md ✅ aligned (Constitution Check gate present)
  - .specify/templates/spec-template.md ✅ aligned (no principle-specific constraints violated)
  - .specify/templates/tasks-template.md ✅ aligned (test-first discipline reflected)
  - .specify/templates/agent-file-template.md ✅ no outdated agent-specific references
Deferred TODOs: none
-->

# Juice Workflows Constitution

## Core Principles

### I. Modular Library Design

Every workflow capability MUST be delivered as a self-contained NuGet package with a clear,
single responsibility. The core engine (`Juice.Workflows`) MUST remain dependency-minimal and
independently usable. Integration packages (EF, BPMN, YAML, API, RabbitMQ, Redis) MUST be
opt-in. No feature MUST force consumers to take transitive dependencies they do not need.

**Rationale**: Developers building lightweight, embedded workflows MUST not be forced to pull
in database or messaging infrastructure. Each package MUST be independently testable and
publishable.

### II. Standard Workflow Model (BPMN-Compatible Node Graph)

All workflow elements MUST implement `INode` (`StartAsync`, `ResumeAsync`,
`GetPossibleOutcomes`). The execution model MUST remain a directed node graph of activities,
events, and gateways connected by flows, compatible with BPMN 2.0 semantics. New node types
MUST fit within existing categories: Activities, Events, or Gateways. Proprietary execution
models that cannot be expressed as standard BPMN concepts MUST NOT be introduced without
explicit governance amendment.

**Rationale**: BPMN compatibility ensures workflows can be visualized, imported from
industry-standard tools, and understood by non-developers.

### III. Test-First with In-Memory Isolation

All new node types, gateway logic, and command handlers MUST be covered by unit or integration
tests before the implementation is considered complete. Tests MUST use in-memory repositories
by default; no test MUST require a live database, message broker, or external service unless
explicitly tagged as an integration test. The Red-Green-Refactor cycle MUST be followed for all
non-trivial logic.

**Rationale**: In-memory isolation makes the test suite fast, portable, and runnable without
infrastructure, lowering the barrier to contribution and CI reliability.

### IV. Distributed Reliability

Workflow operations exposed over the network MUST be idempotent. The Outbox pattern MUST be
used for any event published to the message bus to guarantee at-least-once delivery without
data loss on process restart. MediatR command pipelines MUST include idempotency behaviors
(Redis-backed) for all `StartWorkflowCommand` and `ResumeWorkflowCommand` paths. Fire-and-forget
messaging without delivery guarantees MUST NOT be used for state-changing workflow events.

**Rationale**: Workflows orchestrate long-running, multi-step processes. Partial failures MUST
be recoverable; duplicate command delivery MUST NOT corrupt workflow state.

### V. Multi-Framework Compatibility

The solution MUST multi-target `net6.0`, `net8.0`, and `net9.0`. EF Core version MUST track
the target framework (EF 7.x for net6, EF 8.x for net8, EF 9.x for net9). No API or package
MUST require a framework higher than the declared minimum target. Breaking changes to public
API surface MUST be documented and versioned according to SemVer.

**Rationale**: Downstream consumers operate on different LTS cycles. Forcing a framework upgrade
to use the workflow engine would create unacceptable adoption friction.

## Technology Stack Constraints

- **Runtime**: .NET 6 / 8 / 9 (multi-target mandatory; see Principle V)
- **ORM**: Entity Framework Core — version locked per target framework
- **Messaging**: RabbitMQ via Juice.Messaging; Outbox pattern mandatory for state-changing events
- **Idempotency**: Redis via `Juice.MediatR.RequestManager.Redis`
- **API surface**: gRPC (`workflow.proto`) for remote workflow operations; MediatR internally
- **DI**: Microsoft.Extensions.DependencyInjection conventions; no static service locators
- **Workflow formats**: Fluent builder, BPMN XML, YAML — all MUST parse to the same node graph
- **Multi-tenancy**: Finbuckle integration where tenant isolation is required
- **Build output**: `./build/bin/{Configuration}/{ProjectName}`; NuGet packages to
  `./build/publish/{Configuration}/`

## Development Workflow

- All new features MUST start with a spec (user stories + acceptance criteria) before
  implementation tasks are created.
- Database schema changes MUST include provider-specific migrations for both SQL Server and
  PostgreSQL migration projects.
- Public API changes (gRPC proto, MediatR command signatures, INode interface) MUST bump the
  package version following SemVer and be noted in the PR description.
- Test projects MUST reference in-memory implementations; integration tests requiring
  infrastructure MUST be clearly separated and documented in `appsettings.json` configuration.
- NuGet source credentials for `https://nuget.pkg.github.com/creatorflow-io/index.json` MUST
  be configured locally — they MUST NOT be committed to the repository.
- All PRs MUST pass `dotnet build Workflow.sln` and `dotnet test Workflow.sln` before merge.

## Governance

This constitution supersedes all other implicit practices for this repository. Amendments require:

1. A documented rationale explaining why the change is necessary.
2. An updated Sync Impact Report (HTML comment at the top of this file).
3. Review of all dependent templates (`.specify/templates/`) for alignment.
4. Version increment according to SemVer rules defined below.

**Versioning policy**:
- MAJOR: Removal or backward-incompatible redefinition of a principle (e.g., dropping
  multi-target support, changing the node execution model).
- MINOR: New principle added, new mandatory constraint introduced, or material expansion of
  existing guidance.
- PATCH: Clarifications, wording improvements, typo fixes, non-semantic refinements.

**Compliance review**: All PRs and feature plans MUST include a Constitution Check section
verifying that no principle is violated. Violations MUST be justified in a Complexity Tracking
table with the simpler alternative rejected and the reason why.

Use `CLAUDE.md` for runtime build, test, and architecture guidance. This constitution governs
what is built and how; `CLAUDE.md` governs how to operate the toolchain.

**Version**: 1.0.0 | **Ratified**: 2026-02-26 | **Last Amended**: 2026-02-26
