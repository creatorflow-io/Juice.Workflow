# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build the solution
dotnet build Workflow.sln

# Run all tests
dotnet test Workflow.sln

# Run a specific test project
dotnet test test/Juice.Workflows.Tests/Juice.Workflows.Tests.csproj

# Run a specific test class or method
dotnet test --filter "FullyQualifiedName~ExclusiveGatewayTests"

# Pack NuGet packages
dotnet pack Workflow.sln -c Release
```

Build output goes to `./build/bin/{Configuration}/{ProjectName}`. NuGet packages go to `./build/publish/{Configuration}/`.

The solution multi-targets `net6.0`, `net8.0`, and `net9.0`. EF version varies by target framework (7.x for net6, 8.x for net8, 9.x for net9).

## NuGet Sources

The solution depends on private GitHub Packages from `creatorflow-io`. Credentials for `https://nuget.pkg.github.com/creatorflow-io/index.json` must be configured locally (via `dotnet nuget add source` or `~/.nuget/NuGet/NuGet.Config`).

## Architecture Overview

This is a **workflow engine** library targeting media management workflows. It follows DDD with a node-based execution model.

### Core Execution Model (`src/Juice.Workflows`)

Workflows are composed of **nodes** (tasks, gateways, events) connected by **flows**. Execution is orchestrated through:

- `WorkflowContext` — holds the full workflow state: nodes, flows, process snapshots, execution history
- `NodeContext` / `FlowContext` — per-node and per-flow execution context
- `INode` — core interface all workflow elements implement: `StartAsync()`, `ResumeAsync()`, `GetPossibleOutcomes()`
- `WorkflowStatus` — `Idle | Executing | Halted | Finished | Aborted | Faulted`

**Node categories:**
- Activities: `Task`, `UserTask`, `ServiceTask`, `SendTask`, `ReceiveTask`, `BusinessRuleTask`, `SubProcess`
- Events: `StartEvent`, `EndEvent`, `IntermediateCatchEvent`, `IntermediateThrowEvent`, `BoundaryEvent`, timer and message variants
- Gateways: `ExclusiveGateway`, `InclusiveGateway`, `ParallelGateway`, `EventBasedGateway`

### Domain Aggregates

Located under `src/Juice.Workflows/Domain/`:
- `WorkflowRecord` — persisted workflow instance
- `WorkflowDefinition` — workflow template/schema
- `WorkflowState` — execution state snapshot
- `EventRecord` — workflow event log

Repository interfaces: `IWorkflowRepository`, `IDefinitionRepository`, `IWorkflowStateRepository`, `IEventRepository`. In-memory implementations are used in tests; EF implementations are in `src/Juice.Workflows.EF/`.

### MediatR Command Pipeline

Workflow operations are invoked as MediatR commands:
- `StartWorkflowCommand` / `ResumeWorkflowCommand` — core commands
- Behaviors add cross-cutting: idempotency (Redis-backed), logging, validation

The API project (`src/Juice.Workflows.Api/`) provides command handlers specialized by task/event type and exposes gRPC services.

### Workflow Definition Formats

Three ways to define workflows:
1. **Fluent in-code builder** — `AddWorkflowServices()` with builder pattern
2. **BPMN XML** — parsed by `Juice.Workflows.Bpmn`
3. **YAML** — parsed by `Juice.Workflows.Yaml` (uses YamlDotNet)
4. **Database-driven** — loaded at runtime via `RegisterDbWorkflows()`

### Database Layer

Two `DbContext`s in `src/Juice.Workflows.EF/`:
- `WorkflowDbContext` — workflow definitions + records + Outbox for transactional event publishing
- `WorkflowPersistDbContext` — workflow execution state

Provider-specific migration projects exist for SQL Server and PostgreSQL under `src/Juice.Workflows.EF.SqlServer/` and `src/Juice.Workflows.EF.PostgreSQL/`.

DI registration: `StoreWorkflowToEFRepo()`, `PersistStateToEFRepo()`.

### API & Integration

- **gRPC** (`src/Juice.Workflows.Api.Contracts/Protos/workflow.proto`): `Start`, `Resume`, `Catch` operations
- **Event bus**: RabbitMQ via Juice.Messaging with Outbox pattern for reliable delivery
- **Idempotency**: Redis-based request deduplication via `Juice.MediatR.RequestManager.Redis`
- **Multi-tenancy**: Finbuckle integration

### Testing Pattern

Tests in `test/Juice.Workflows.Tests/` use:
- In-memory repositories for isolation
- `appsettings.json` configures RabbitMQ and delivery policies (required for integration tests)
- `test/Juice.Workflows.Tests.Host/` is an ASP.NET Core host for full integration scenarios
- BPMN/YAML workflow files are in `test/Juice.Workflows.Tests/workflows/`
