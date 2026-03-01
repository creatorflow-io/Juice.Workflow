# Data Model: Introduce Abstractions Package

**Feature**: 002-separate-layers

This document describes the package structure and type ownership after the change. There are no new persistent entities — this is a structural reorganisation.

---

## Package Dependency Graph (After)

```
Juice.Workflows.Abstractions   (new — no dependencies on this solution)
        ▲               ▲
        │               │
Juice.Workflows     Juice.Workflows.EF    (existing, updated)
        ▲               ▲
        │               │
   Juice.Workflows.Bpmn / Juice.Workflows.Yaml
        ▲
        │
Juice.Workflows.Designer       (existing, direct dep changed → Abstractions)
        ▲
        │
Juice.Workflows.Tests.Host     (existing, unchanged)
```

---

## New Project: `Juice.Workflows.Abstractions`

**SDK**: `Microsoft.NET.Sdk`
**Target frameworks**: `net6.0;net8.0;net9.0`
**NuGet dependencies**: `Juice.Extensions`, `Microsoft.Extensions.Localization.Abstractions`, `Newtonsoft.Json`, `Juice.MediatR` (for `MessageBase`/`INotification` in domain events)

### Types Contained

#### Models (node/flow contracts)

| Type | Kind | Description |
|------|------|-------------|
| `INode` | Interface | Core node contract: `StartAsync`, `ResumeAsync`, `GetPossibleOutcomes` |
| `IStartNode` | Marker interface | **New** — identifies a start event node; replaces concrete `StartEvent` type check |
| `IActivity` | Marker interface | Tags activity nodes |
| `IGateway` | Marker interface | Tags gateway nodes |
| `IEventNode` | Marker interface | Tags event nodes |
| `ICatching` | Marker interface | Tags catch-capable event nodes |
| `IThrowing` | Marker interface | Tags throw-capable event nodes |
| `IBoundary` | Marker interface | Tags boundary event nodes |
| `IIntermediate` | Marker interface | Tags intermediate event nodes |
| `IExclusive` | Marker interface | Tags exclusive-selection nodes |
| `IEventBased` | Marker interface | Tags event-based gateway nodes |
| `ISelectiveGateway` | Marker interface | Tags selective gateway nodes |
| `IFlow` | Interface | Sequence flow contract |
| `SequenceFlow` | Class | Default sequence flow implementation |
| `Outcome` | Class | Represents a named outgoing flow option |
| `WorkflowStatus` | Enum | `Idle`, `Executing`, `Halted`, `Finished`, `Faulted`, `Aborted` |
| `IConditionEvaluator` | Interface | Evaluates flow conditions |
| `ILogicConditionEvaluator` | Interface | Evaluates logic expression conditions |

#### Execution models (parameter/return types of `INode`)

| Type | Kind | Description |
|------|------|-------------|
| `WorkflowContext` | Class | Full workflow runtime state: nodes, flows, processes, history |
| `NodeContext` | Class | Per-node runtime wrapper: `NodeRecord` + `INode` instance + `Properties` |
| `FlowContext` | Class | Per-flow runtime wrapper: `FlowRecord` + `IFlow` instance |
| `NodeRecord` | Record | Node metadata: id, name, type name, process ref, properties |
| `FlowRecord` | Record | Flow metadata: id, name, source/target refs, condition |
| `ProcessRecord` | Record | Process metadata: id, name |
| `NodeExecutionResult` | Class | Result of a node execution: status + outcome names |
| `WorkflowExecutionResult` | Class | Result of a full workflow execution cycle |

#### Domain aggregates

| Type | Kind | Description |
|------|------|-------------|
| `WorkflowDefinition` | Aggregate root | Template: name, raw data, parsed data, status lifecycle |
| `WorkflowDefinitionStatus` | Enum | `Draft`, `Active`, `Archived` |
| `WorkflowDefinitionSummary` | Record | Read-only projection for list views |
| `NodeData` | Record | Serialised node: `NodeRecord` + type name + properties |
| `FlowData` | Record | Serialised flow: `FlowRecord` + type name |
| `WorkflowRecord` | Aggregate root | Workflow instance: definition ref, correlation, status |
| `WorkflowState` | Aggregate root | Runtime state snapshot: node/flow/process snapshots |
| `NodeSnapshot` | Class | Persisted node state within `WorkflowState` |
| `FlowSnapshot` | Class | Persisted flow state within `WorkflowState` |
| `ProcessSnapshot` | Class | Persisted process state within `WorkflowState` |
| `EventRecord` | Aggregate root | Async event tracking for catch/intermediate events |

#### Repository interfaces

| Interface | Description |
|-----------|-------------|
| `IDefinitionRepository` | CRUD + list + name-exists for `WorkflowDefinition` |
| `IWorkflowRepository` | CRUD for `WorkflowRecord` |
| `IWorkflowStateRepository` | Persist and retrieve `WorkflowState` |
| `IEventRepository` | CRUD + search for `EventRecord` |

#### Domain events

| Type | Change |
|------|--------|
| `ProcessStartedDomainEvent` | `Node` property type: `NodeContext` → `NodeRecord` |
| `DefinitionDataChangedDomainEvent` | No change |

---

## Change to `NodeContext`

`NodeContext.IsStart()` and `NodeContext.IsStartOf()` are updated:

**Before** (references concrete execution-layer class):
```
IsStart()     → Node is StartEvent
IsStartOf(id) → Node is StartEvent && Record.ProcessIdRef == id
```

**After** (references Abstractions marker interface):
```
IsStart()     → Node is IStartNode
IsStartOf(id) → Node is IStartNode && Record.ProcessIdRef == id
```

`StartEvent` in `Juice.Workflows` adds `IStartNode` to its interface list. No other call sites change.

---

## Updated `Juice.Workflows.Designer.csproj`

The direct `<ProjectReference>` to `Juice.Workflows` is replaced with `Juice.Workflows.Abstractions`. References to `Juice.Workflows.Bpmn` and `Juice.Workflows.Yaml` are retained (they require the execution-layer context builders).

---

## What Does NOT Change

- All type namespaces are preserved (`Juice.Workflows.Models`, `Juice.Workflows.Execution`, `Juice.Workflows.Domain.*`)
- All existing `using` directives in consumer code continue to compile
- `Juice.Workflows` re-exposes its types unchanged; existing consumers adding only `Juice.Workflows` get everything transitively as before
- `Juice.Workflows.EF` project references are unchanged in this feature (follow-on work)
- No database migrations required
- No gRPC proto changes
