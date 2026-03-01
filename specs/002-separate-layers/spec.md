# Feature Specification: Introduce Abstractions Package for Node and Repository Extensions

**Feature Branch**: `002-separate-layers`
**Created**: 2026-03-01
**Status**: Draft
**Input**: User description: "Separate execution, abstractions and data layer"

## Context

The current `Juice.Workflows` library bundles contracts (interfaces, domain aggregates, execution models) together with the full execution engine, built-in nodes, and in-memory implementations in a single assembly. A developer who wants to implement a custom node type or a custom repository must take a compile-time dependency on the entire execution engine — even though they need only the interfaces and data types.

This feature introduces a new `Juice.Workflows.Abstractions` package containing only the contracts required to implement nodes and repositories. The execution engine (`Juice.Workflows`) and data layer (`Juice.Workflows.EF`) become consumers of Abstractions rather than the sole home of the contracts.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Implement a Custom Node Without Referencing the Execution Engine (Priority: P1)

A developer building a plugin (e.g., an HTTP call node, an AI-evaluation node, or a domain-specific approval task) wants to implement and ship a custom node type. They should only need to reference the lightweight `Juice.Workflows.Abstractions` package — not the full execution engine with all its built-in nodes and orchestration logic.

**Why this priority**: This is the primary value of the feature. Without it, every node extension author carries an unwanted and potentially version-conflicting dependency on the execution engine.

**Independent Test**: Create a new class-library project referencing only `Juice.Workflows.Abstractions`, implement `INode`, and confirm the project compiles with no transitive reference to `Juice.Workflows` (execution engine).

**Acceptance Scenarios**:

1. **Given** a project referencing only `Juice.Workflows.Abstractions`, **When** a developer implements `INode` (including `StartAsync`, `ResumeAsync`, `GetPossibleOutcomes`), **Then** the project compiles successfully with zero transitive dependency on the execution engine or database packages.
2. **Given** a custom node library referencing only `Juice.Workflows.Abstractions`, **When** the node is registered in a host that references `Juice.Workflows`, **Then** the execution engine invokes the custom node correctly within a running workflow.
3. **Given** a custom node using `WorkflowContext` and `NodeContext` from `Juice.Workflows.Abstractions`, **When** it reads node properties or signals an outcome, **Then** all required types and methods are available without referencing execution internals.

---

### User Story 2 — Implement a Custom Repository Without Referencing the Execution Engine (Priority: P1)

A developer providing an alternative persistence backend (e.g., MongoDB, a cloud table store, or an in-process store for testing) wants to implement the repository interfaces. They should only need `Juice.Workflows.Abstractions` — no dependency on the execution engine or EF packages.

**Why this priority**: Repository implementors need only domain aggregates and interface contracts. Forcing them to reference the execution engine creates version coupling and bloated deployments.

**Independent Test**: Create a class-library project referencing only `Juice.Workflows.Abstractions`, implement all four repository interfaces (`IDefinitionRepository`, `IWorkflowRepository`, `IWorkflowStateRepository`, `IEventRepository`), and confirm the project compiles with no transitive reference to `Juice.Workflows` or `Juice.Workflows.EF`.

**Acceptance Scenarios**:

1. **Given** a project referencing only `Juice.Workflows.Abstractions`, **When** a developer implements all four repository interfaces, **Then** the project compiles with no transitive dependency on the execution engine or EF.
2. **Given** a custom repository library registered in a host, **When** the execution engine starts and runs a workflow, **Then** it resolves and uses the custom repositories correctly without modification to the engine code.
3. **Given** `Juice.Workflows.Abstractions`, **When** a developer accesses domain aggregates (`WorkflowDefinition`, `WorkflowRecord`, `WorkflowState`, `EventRecord`) needed to fulfil the repository contracts, **Then** all aggregate types and their public methods are available.

---

### Edge Cases

- What happens if a consumer adds `Juice.Workflows` without explicitly adding `Juice.Workflows.Abstractions`? → `Juice.Workflows` declares `Juice.Workflows.Abstractions` as a required dependency, so contracts are always transitively available.
- What happens to existing code that already references `Juice.Workflows` directly for node or repository implementations? → No breaking changes; existing code continues to compile. `Juice.Workflows` re-exports or forwards everything that moves to Abstractions to preserve backwards compatibility.
- Can `NodeContext.IsStart()` be used from Abstractions without referencing the concrete `StartEvent` class? → Yes — an `IStartEvent` marker interface is introduced in Abstractions; `NodeContext.IsStart()` checks `Node is IStartEvent` rather than `Node is StartEvent`. The concrete `StartEvent` in the execution layer implements `IStartEvent`.
- What if a custom node needs access to base-class helpers (`Halt()`, `Fault()`, `Outcomes()`)? → The optional `Node`, `Activity`, `Event`, and `Gateway` base classes remain in `Juice.Workflows`. Developers who want those helpers reference `Juice.Workflows`; those who do not can implement `INode` directly from Abstractions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A `Juice.Workflows.Abstractions` package MUST be introduced containing:
  - All repository interfaces: `IDefinitionRepository`, `IWorkflowRepository`, `IWorkflowStateRepository`, `IEventRepository`
  - All domain aggregates: `WorkflowDefinition`, `WorkflowRecord`, `WorkflowState`, `EventRecord`
  - All execution-model types required by `INode` signatures: `WorkflowContext`, `NodeContext`, `FlowContext`, `NodeExecutionResult`, `WorkflowExecutionResult`, `NodeRecord`, `FlowRecord`, `ProcessRecord`, `Outcome`, `SequenceFlow`, `WorkflowStatus`
  - All node and flow contracts: `INode`, `IActivity`, `IGateway`, `IEventNode`, `ICatching`, `IThrowing`, `IIntermediate`, `IBoundary`, `IExclusive`, `IEventBased`, `ISelectiveGateway`
  - A new `IStartEvent` marker interface

- **FR-002**: `Juice.Workflows.Abstractions` MUST have zero compile-time dependencies on `Juice.Workflows` (execution engine), `Juice.Workflows.EF`, or any database-specific package.

- **FR-003**: `NodeContext.IsStart()` and `NodeContext.IsStartOf()` MUST be updated to check `Node is IStartEvent` instead of `Node is StartEvent`. The concrete `StartEvent` class in `Juice.Workflows` MUST implement `IStartEvent`.

- **FR-004**: `Juice.Workflows` MUST declare `Juice.Workflows.Abstractions` as a dependency and continue to expose all currently public types with unchanged namespaces, preserving full backwards compatibility for existing consumers.

- **FR-005**: All existing public DI extension methods (`AddWorkflowServices()`, `AddInMemoryRepositories()`, `RegisterWorkflow()`, `RegisterDbWorkflows()`) MUST remain available with identical signatures.

- **FR-006**: All existing unit and integration tests MUST pass without modification after the introduction of `Juice.Workflows.Abstractions`.

- **FR-007**: `Juice.Workflows.Designer` MUST be updated to reference `Juice.Workflows.Abstractions` instead of `Juice.Workflows`, since the designer only issues MediatR commands and does not invoke the execution engine directly.

### Key Entities

- **`Juice.Workflows.Abstractions`** (new project): All interfaces, domain aggregates, execution-model types, and node/flow contracts. No implementations, no database dependencies, no execution logic.
- **`Juice.Workflows`** (existing, updated): Adds `Juice.Workflows.Abstractions` as a dependency; retains execution engine, node base classes, built-in nodes, in-memory repos, context builders, DI helpers. All existing public APIs unchanged.
- **`IStartEvent`** (new interface in Abstractions): Marker interface implemented by `StartEvent`; used by `NodeContext.IsStart()` to avoid a direct reference to the concrete class.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A project referencing only `Juice.Workflows.Abstractions` can implement `INode` and all four repository interfaces and compile successfully, with zero transitive references to the execution engine or database assemblies.
- **SC-002**: All existing tests pass with zero regressions after `Juice.Workflows.Abstractions` is introduced.
- **SC-003**: Existing hosts using `AddWorkflowServices()` require zero code changes to continue working after the change.
- **SC-004**: `Juice.Workflows.Designer` no longer carries `Juice.Workflows` (execution engine) as a transitive dependency.

## Assumptions

- Internal execution coupling within `Juice.Workflows` (e.g., `WorkflowExecutor`, `WorkflowContextResolver`) is not restructured in this feature — only the contract boundary is extracted.
- `Juice.Workflows.EF` dependency reduction (making it depend on Abstractions rather than `Juice.Workflows`) is out of scope for this feature; it is a follow-on improvement.
- `Juice.Workflows.Bpmn` and `Juice.Workflows.Yaml` dependency graphs are out of scope; they are unchanged.
- `Juice.Workflows.Api` and `Juice.Workflows.Api.Contracts` (gRPC) are out of scope; they are unchanged.
- `NodeLibrary` (the static node type registry) stays in `Juice.Workflows`; custom nodes are registered by the consuming host.
- No public namespace changes are required; moved types keep their existing namespaces.
- NuGet versioning and publish workflow are out of scope.
