# Research: Introduce Abstractions Package

**Feature**: 002-separate-layers
**Phase**: 0 — Pre-design research

---

## Decision 1: What goes into `Juice.Workflows.Abstractions`

**Decision**: Move all interfaces, domain aggregates, execution-model data types, and node/flow contracts. Leave all implementations (execution engine, node base classes, in-memory repos) in `Juice.Workflows`.

**Rationale**: The boundary is "can this type be consumed by a node or repository implementor without knowing how the engine works?" If yes, it belongs in Abstractions. If it contains execution logic (WorkflowExecutor, Workflow service, Node base class helpers like Halt/Fault/Outcomes), it stays in the execution layer.

**Alternatives considered**:
- Move only interfaces, not execution-model types → rejected because `INode` method signatures take `WorkflowContext`/`NodeContext`; those types must come with the interface.
- Move `Node` abstract base class to Abstractions → rejected because `Node` provides execution helpers (`Halt()`, `Fault()`, `Outcomes()`). Putting these in Abstractions would expose engine-level concepts there. Node authors who want these helpers reference `Juice.Workflows`; those who don't can implement `INode` directly from Abstractions.
- Move `SimpleExpressionEvaluator` to Abstractions → rejected; it is a concrete implementation, not a contract.

---

## Decision 2: Fix for `NodeContext.IsStart()` referencing concrete `StartEvent`

**Decision**: Introduce `IStartNode` marker interface in Abstractions. `StartEvent` in the execution layer implements `IStartNode`. `NodeContext.IsStart()` checks `Node is IStartNode`.

**Rationale**: `NodeContext` must live in Abstractions because it is a parameter type of `INode.StartAsync()` / `INode.ResumeAsync()`. But it currently has `Node is StartEvent` — a reference to a concrete class in the execution layer. Introducing a marker interface breaks the cycle cleanly without changing public behaviour.

**Alternatives considered**:
- Move `IsStart()` out of `NodeContext` into an extension method in the execution layer → rejected because callers inside `WorkflowContext` (which also moves to Abstractions) need `IsStart()`; moving it to an extension would require `WorkflowContext` to also stay out of Abstractions.
- Pass a `bool isStart` flag into `NodeContext` constructor → rejected; this changes the public API surface unnecessarily.

---

## Decision 3: `ProcessStartedDomainEvent` carries `NodeContext`

**Decision**: Change `ProcessStartedDomainEvent.Node` from `NodeContext` to `NodeRecord`.

**Rationale**: Domain events should carry data, not execution context. `NodeContext` is a runtime wrapper holding the live `INode` instance plus mutable `Properties`. A domain event only needs the record metadata (`NodeRecord`) to identify which node fired. This change also removes the last domain-events coupling to the execution-model wrapper type.

**Alternatives considered**:
- Keep `NodeContext` and move the domain event to the execution layer → rejected; domain events belong in the domain layer (Abstractions).
- Carry both `NodeRecord` and the node type name string → unnecessary; `NodeRecord` already contains the type name.

---

## Decision 4: `WorkflowContext` interface-type checks

**Decision**: No change required. `WorkflowContext` uses `node.Node is IIntermediate`, `node.Node is ICatching`, `node.Node is ISelectiveGateway` — all of which are marker interfaces already defined in `Models/INode.cs`. Since those interfaces move to Abstractions alongside `WorkflowContext`, these checks are safe.

**Rationale**: Pattern-matching against interfaces (not concrete classes) is a correct abstraction. No concrete node types are referenced inside `WorkflowContext`.

---

## Decision 5: `Juice.Workflows.Designer` dependency reduction

**Decision**: Remove the direct `<ProjectReference>` to `Juice.Workflows` from `Juice.Workflows.Designer.csproj`. Replace with `Juice.Workflows.Abstractions`. The BPMN/YAML builder references stay because parsing requires execution-layer context builders; those bring `Juice.Workflows` in transitively, which is acceptable.

**Rationale**: The Designer's command handlers only use domain types (`WorkflowDefinition`, `IDefinitionRepository`, `NodeData`, `FlowData`, `ProcessRecord`) and the BPMN/YAML builders. None of these require the execution engine directly. Removing the direct reference makes the intent explicit, even if the transitive dependency via Bpmn/Yaml remains until a future feature decouples those parsers.

---

## Decision 6: Project SDK for `Juice.Workflows.Abstractions`

**Decision**: Use `Microsoft.NET.Sdk` (standard class library). Multi-target `net6.0;net8.0;net9.0` to match the rest of the solution.

**Rationale**: Abstractions has no ASP.NET Core or web-specific dependencies. A plain SDK keeps the package minimal. Multi-targeting is mandated by Constitution Principle V.

---

## Decision 7: Namespace strategy

**Decision**: All types keep their existing namespaces (`Juice.Workflows.Models`, `Juice.Workflows.Execution`, `Juice.Workflows.Domain.AggregatesModel.*`, etc.). The assembly is renamed to `Juice.Workflows.Abstractions` but namespaces are unchanged.

**Rationale**: Changing namespaces would be a breaking change for all existing consumers. Assembly name and namespace do not have to match. Keeping namespaces unchanged means existing `using` directives in consumer code continue to work after adding the new package reference.

---

## File-Level Decision: What moves vs. stays

### Moves to `Juice.Workflows.Abstractions`

| File | Notes |
|------|-------|
| Models/INode.cs | + new `IStartNode` marker |
| Models/IFlow.cs | |
| Models/Outcome.cs | |
| Models/WorkflowStatus.cs | |
| Models/IConditionEvaluator.cs | |
| Models/ILogicConditionEvaluator.cs | |
| Models/SequenceFlow.cs | |
| Execution/WorkflowContext.cs | |
| Execution/NodeContext.cs | IsStart() fixed to use IStartNode |
| Execution/FlowContext.cs | |
| Execution/NodeExecutionResult.cs | |
| Execution/WorkflowExecutionResult.cs | |
| Execution/ProcessRecord.cs | |
| Execution/NodeRecord.cs | |
| Execution/FlowRecord.cs | |
| Domain/AggregatesModel/DefinitionAggregate/* | All files |
| Domain/AggregatesModel/WorkflowAggregate/* | All files |
| Domain/AggregatesModel/WorkflowStateAggregate/* | All files |
| Domain/AggregatesModel/EventAggregate/* | All files |
| Domain/Events/ProcessStartedDomainEvent.cs | NodeContext → NodeRecord |
| Domain/Events/DefinitionDataChangedDomainEvent.cs | |

### Stays in `Juice.Workflows`

| File | Reason |
|------|--------|
| Models/Node.cs | Abstract base with engine helpers (Halt, Fault, Outcomes) |
| Models/SimpleExpressionEvaluator.cs | Concrete implementation |
| Execution/WorkflowExecutionResult.cs | References WorkflowContext but is an engine output type |
| Services/* | Execution engine, orchestration |
| Nodes/* | Concrete node implementations |
| Builder/* | Context builders |
| InMemory/* | In-memory repository implementations |
| DependencyInjection/* | DI registration helpers |
| Domain/CommandHandlers/* | Invoke execution engine directly |
| Domain/Commands/* | MediatR commands for engine operations |
