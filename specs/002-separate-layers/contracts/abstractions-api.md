# Public Contract: `Juice.Workflows.Abstractions`

**Feature**: 002-separate-layers

This document defines the public API surface of the new `Juice.Workflows.Abstractions` package — the contracts that custom node and repository authors depend on.

---

## Custom Node Contract

To implement a custom node, reference only `Juice.Workflows.Abstractions` and implement `INode`:

```
INode
  StartAsync(WorkflowContext, NodeContext, FlowContext?, CancellationToken) → Task<NodeExecutionResult>
  ResumeAsync(WorkflowContext, NodeContext, CancellationToken)              → Task<NodeExecutionResult>
  GetPossibleOutcomes(WorkflowContext, NodeContext)                         → IEnumerable<Outcome>
  PreStartCheckAsync(WorkflowContext, NodeContext, CancellationToken)       → Task<bool>
  DisplayText  → LocalizedString
  Category     → LocalizedString
```

Marker interfaces to tag the node category (optional — implement as appropriate):

| Interface | Use |
|-----------|-----|
| `IStartNode` | Start event — enables `NodeContext.IsStart()` |
| `IActivity` | Task/activity node |
| `IGateway` | Gateway node |
| `IEventNode` | Event node |
| `ICatching` | Catch-capable event |
| `IThrowing` | Throw-capable event |
| `IBoundary` | Boundary event |
| `IIntermediate` | Intermediate event |
| `IExclusive` | Exclusive-selection gateway |
| `IEventBased` | Event-based gateway |
| `ISelectiveGateway` | Selective (conditional) gateway |

### `NodeExecutionResult` — node return values

| Factory | Meaning |
|---------|---------|
| `new NodeExecutionResult(WorkflowStatus.Finished, names[])` | Completed, activate named outgoing flows |
| `NodeExecutionResult.Halted` | Node is waiting for a resume signal |
| `NodeExecutionResult.Empty` | No-op / idle |
| `NodeExecutionResult.Fault(message)` | Node failed |

### `WorkflowContext` — read-only surface for node authors

| Member | Description |
|--------|-------------|
| `WorkflowId` | Running workflow instance ID |
| `CorrelationId` | Correlation token |
| `Input` | Workflow input data |
| `Nodes` | Dictionary of all `NodeContext` by node ID |
| `Flows` | All `FlowContext` in the process |
| `Processes` | All `ProcessRecord` in the workflow |
| `GetNode(id)` | Retrieve a `NodeContext` by ID |
| `GetStartNode(processId?)` | Get the start node for a process |
| `Properties` | Shared workflow-level property bag |

### `NodeContext` — per-node context

| Member | Description |
|--------|-------------|
| `Record` | `NodeRecord` — node metadata (id, name, type, process ref) |
| `Node` | The `INode` instance for this context |
| `Properties` | Per-node mutable property bag |
| `DisplayName` | Display name (from record or node's `DisplayText`) |
| `IsStart()` | Returns `true` if this node implements `IStartNode` |
| `IsStartOf(processId)` | Returns `true` if this node is the start of the given process |
| `GetSharedProperties()` | Returns properties whose keys start with `$` (shared across nodes) |

---

## Custom Repository Contract

To implement a custom repository, reference only `Juice.Workflows.Abstractions` and implement one or more of the four repository interfaces:

### `IDefinitionRepository`

```
CreateAsync(WorkflowDefinition, CancellationToken)                   → Task<IOperationResult>
UpdateAsync(WorkflowDefinition, CancellationToken)                   → Task<IOperationResult>
GetAsync(id, CancellationToken)                                      → Task<WorkflowDefinition?>
DeleteAsync(id, CancellationToken)                                   → Task<IOperationResult>
ListAsync(status?, CancellationToken)                                → Task<IEnumerable<WorkflowDefinitionSummary>>
ExistsByNameAsync(name, excludeId?, CancellationToken)               → Task<bool>
```

### `IWorkflowRepository`

```
CreateAsync(WorkflowRecord, CancellationToken)    → Task<IOperationResult>
UpdateAsync(WorkflowRecord, CancellationToken)    → Task<IOperationResult>
GetAsync(id, CancellationToken)                  → Task<WorkflowRecord?>
```

### `IWorkflowStateRepository`

```
PersistAsync(WorkflowState, CancellationToken)   → Task<IOperationResult>
GetAsync(workflowId, CancellationToken)          → Task<WorkflowState?>
```

### `IEventRepository`

```
CreateUniqueByWorkflowAsync(EventRecord, CancellationToken)          → Task<IOperationResult>
UpdateAsync(EventRecord, CancellationToken)                          → Task<IOperationResult>
GetAsync(workflowId, nodeId, CancellationToken)                      → Task<EventRecord?>
RemoveAsync(workflowId, CancellationToken)                           → Task<IOperationResult>
UpdateStartNodesAsync(workflowId, nodeId, correlationId, CancellationToken) → Task<IOperationResult>
FindAllAsync(catchingKey, correlationId?, CancellationToken)         → Task<IEnumerable<EventRecord>>
```

---

## `IStartNode` — New Marker Interface

```csharp
// In: Juice.Workflows.Models namespace
public interface IStartNode : INode { }
```

Custom start event implementations MUST implement `IStartNode` for `NodeContext.IsStart()` and `WorkflowContext.GetStartNode()` to recognise them correctly.

---

## Backwards Compatibility

- All types in `Juice.Workflows.Abstractions` retain their original namespaces.
- Existing code referencing `Juice.Workflows` continues to compile unchanged — `Juice.Workflows` declares `Juice.Workflows.Abstractions` as a dependency, making all Abstractions types transitively available.
- The only breaking change affecting internal code is `ProcessStartedDomainEvent.Node` type changing from `NodeContext` to `NodeRecord`. This affects only the domain-event handler inside `Juice.Workflows` — no external consumer is expected to handle this event directly.
