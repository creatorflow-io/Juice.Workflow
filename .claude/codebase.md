# Juice Workflows — Codebase Quick Reference

## Project Layout

```
src/
  Juice.Workflows/               # Core engine (no infra dependencies)
  Juice.Workflows.Api/           # gRPC command handlers
  Juice.Workflows.Api.Contracts/ # workflow.proto definitions
  Juice.Workflows.Bpmn/          # BPMN 2.0 XML parser
  Juice.Workflows.Yaml/          # YAML parser (YamlDotNet)
  Juice.Workflows.EF/            # EF DbContexts + Outbox
  Juice.Workflows.EF.SqlServer/  # SQL Server migrations
  Juice.Workflows.EF.PostgreSQL/ # PostgreSQL migrations
test/
  Juice.Workflows.Tests/         # Unit/integration tests (in-memory repos)
  Juice.Workflows.Tests.Host/    # Full ASP.NET Core integration host
```

Multi-targets: `net6.0`, `net8.0`, `net9.0`. EF version locked per TFM (7/8/9).

---

## Core Interfaces

### INode — every workflow element implements this
```csharp
public interface INode : IDisposable
{
    LocalizedString DisplayText { get; }
    LocalizedString Category { get; }
    IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext ctx, NodeContext node);
    Task<NodeExecutionResult> StartAsync(WorkflowContext ctx, NodeContext node,
        FlowContext? flow, CancellationToken token);
    Task<NodeExecutionResult> ResumeAsync(WorkflowContext ctx, NodeContext node,
        CancellationToken token);
}
```

### IGateway — gateways add post-execution check
```csharp
public interface IGateway : INode
{
    Task PostExecuteCheckAsync(WorkflowContext ctx, NodeContext node, CancellationToken token);
}
public interface IExclusive : IGateway { }   // ExclusiveGateway, EventBasedGateway
public interface IEventBased : IGateway { }  // EventBasedGateway
```

### IFlow — controls which outgoing flows are selected
```csharp
public interface IFlow : IDisposable
{
    Task<bool> PreSelectCheckAsync(WorkflowContext ctx, NodeContext source,
        NodeContext dest, FlowContext flow);
}
```

---

## Base Classes

### Node (abstract)
- `src/Juice.Workflows/Models/Node.cs`
- Protected helpers: `Outcomes(params string[] names)`, `Halt()`, `Fault(string msg)`, `Noop()`, `Executing()`

### Gateway (abstract) : Node, IGateway
- `src/Juice.Workflows/Nodes/Nodes.cs`
- `GetPossibleOutcomes` — collects outcomes from all incoming source nodes
- `JoinnedOutcomes(ctx, node)` — returns combined outcomes of all incoming sources
- `SourceOutcomes(ctx, flow)` — returns outcomes of a single incoming source node
- `PostExecuteCheckAsync` — no-op by default; override for validation

### Activity / Event — also in `Nodes.cs`

---

## Gateway Implementations

### ExclusiveGateway
- File: `src/Juice.Workflows/Nodes/Gateways/ExclusiveGateway.cs`
- Implements: `Gateway`, `IExclusive`
- Logic: requires exactly ONE incoming active flow; returns `SourceOutcomes(flow)`
- PostCheck: throws if no outgoing flow was activated
- Condition matching: via `SequenceFlow.PreSelectCheckAsync` (outcome name == `ConditionExpression`)

### InclusiveGateway
- File: `src/Juice.Workflows/Nodes/Gateways/InclusiveGateway.cs`
- Logic: waits until no incomplete active paths remain (`AnyIncompleteActivePathTo`)
- Returns: `JoinnedOutcomes` (union of all incoming outcomes)

### ParallelGateway
- File: `src/Juice.Workflows/Nodes/Gateways/ParallelGateway.cs`
- Logic: requires ALL incoming flows to be active (`AllFlowActiveTo`); activates ALL outgoing flows
- No conditions on flows

### EventBasedGateway
- File: `src/Juice.Workflows/Nodes/Gateways/EventBasedGateway.cs`
- Implements: `Gateway`, `IExclusive`, `IEventBased`
- Logic: same as ExclusiveGateway but targets must be IntermediateCatchEvents
- PostCheck: same as ExclusiveGateway

---

## Flow & Condition Model

### FlowRecord (immutable data)
```csharp
public record FlowRecord
{
    public string Id { get; init; }
    public string SourceRef { get; init; }
    public string DestinationRef { get; init; }
    public string? Name { get; init; }
    public string? ProcessIdRef { get; init; }
    public string? ConditionExpression { get; init; }  // outcome name or expression string
}
```

### FlowContext (runtime wrapper)
```csharp
public class FlowContext
{
    public FlowRecord Record { get; init; }
    public IFlow Flow { get; init; }
    public string DisplayName => Record.Name ?? Record.ConditionExpression ?? "";
}
```

### SequenceFlow.PreSelectCheckAsync — condition evaluation logic
Priority order:
1. Default flow → always passes
2. IExclusive source + active outgoing flow already exists → block (exclusive constraint)
3. IExclusive destination + active incoming flow exists → block
4. ParallelGateway source or dest → always pass
5. IEventBased source → dest must be IIntermediate+ICatching
6. No ConditionExpression → pass (unconditional flow)
7. **ConditionExpression present → must match one of `ctx.GetOutcomes(source.Id)`**

**Key insight**: conditions are currently outcome-name strings, not evaluated expressions.
The Logic Gateway feature will introduce actual expression evaluation.

---

## WorkflowContext — Key Members

```csharp
// Navigation
NodeContext? GetNode(string? id)
IEnumerable<FlowContext> GetIncomings(NodeContext node)
IEnumerable<FlowContext> GetOutgoings(NodeContext node)
bool IsDefaultOutgoing(FlowContext flow, NodeContext? node)

// Flow state
void ActiveFlow(FlowContext flow)
void DeativeFlow(FlowContext flow)
bool AnyActiveFlowFrom(NodeContext source)          // any outgoing flow active?
bool AnyActiveFlowTo(NodeContext dest, string? exceptedId)
bool AllFlowActiveTo(NodeContext dest)               // ALL incoming flows active?
bool AnyIncompleteActivePathTo(NodeContext dest)     // any path still in-flight?

// Outcome tracking
IEnumerable<string> GetOutcomes(string nodeId)      // outcomes set by executed node
void ProcessNodeExecutionResult(NodeContext node, NodeExecutionResult result)

// Execution state
IDictionary<string, object?> Input { get; }
IDictionary<string, object?> Output { get; }
IList<ExecutedNode> ExecutedNodes { get; }
IList<FaultedNode> FaultedNodes { get; }
IList<BlockingNode> BlockingNodes { get; }
WorkflowState State { get; }
```

---

## NodeContext

```csharp
public class NodeContext
{
    public NodeRecord Record { get; init; }    // Id, Name, Incomings[], Outgoings[], Default
    public INode Node { get; init; }
    public Dictionary<string, object?> Properties { get; set; }  // keys starting "$" are shared
    public string DisplayName => Record.Name ?? Node.DisplayText.Value;
}
```

---

## DI Registration

```csharp
// Core setup — auto-discovers all concrete INode types in the assembly
services.AddWorkflowServices().AddInMemoryReposistories();

// Register extra custom nodes manually
services.RegisterNodes(typeof(MyCustomGateway));

// Register workflow via fluent builder
services.RegisterWorkflow(workflowId, builder => {
    builder.Start()
           .Then<UserTask>("step1")
           .Exclusive()
               .Fork().Then<UserTask>("a", condition: "yes")
               .Fork().Then<UserTask>("b", condition: "no")
           .Merge()
           .End();
});

// EF persistence
services.StoreWorkflowToEFRepo();
services.PersistStateToEFRepo();
```

Nodes are registered as **Transient**. `INodeLibrary` is a singleton registry.

---

## YAML Format

```yaml
- name: My Process
  steps:
    - name: Start
      type: StartEvent
    - name: Branch
      type: ExclusiveGateway
      branches:
        - steps:
            - name: Task A
              type: UserTask
              condition: "yes"   # ConditionExpression on the incoming flow
        - steps:
            - name: Task B
              type: UserTask
              condition: "no"
      merge_branches:
        - Task A
        - Task B
    - name: End
      type: EndEvent
```

`condition` on a step = `FlowRecord.ConditionExpression` on the flow **into** that step.

---

## BPMN Format

- Gateways mapped via `Constants.NodeTypesMapping`:
  - `tExclusiveGateway` → `ExclusiveGateway`
  - `tInclusiveGateway` → `InclusiveGateway`
  - `tParallelGateway` → `ParallelGateway`
  - `tEventBasedGateway` → `EventBasedGateway`
- `<conditionExpression>` on `<sequenceFlow>` → `FlowRecord.ConditionExpression`

---

## Fluent Builder Methods

```csharp
builder.Start()
       .Then<T>(name, condition, isDefault)   // activity
       .Wait<T>(name, condition, isDefault)   // intermediate catch event
       .Exclusive(name)                        // ExclusiveGateway
       .Inclusive(name)                        // InclusiveGateway
       .Parallel(name)                         // ParallelGateway
       .ExclusiveEventbased(name)              // EventBasedGateway
       .Fork()                                 // go back to last gateway, start new branch
       .Merge()                                // converge branches to gateway
       .End()
       .Terminate();
```

---

## Test Pattern

```csharp
// 1. Setup DI with in-memory repos
services.AddWorkflowServices().AddInMemoryReposistories();
services.RegisterNodes(typeof(MyCustomNode));
services.RegisterWorkflow(id, builder => { ... });

// 2. Execute
var result = await WorkflowTestHelper.ExecuteAsync(
    sp, output, workflowId,
    new Dictionary<string, object?> { { "key", value } });

// 3. Assert
result.Status.Should().Be(WorkflowStatus.Finished);
result.Context.ExecutedNodes.Should().Contain(...);
```

Helpers: `WorkflowTestHelper`, `ContextPrintHelper.Visualize()`, `DependencyResolver`

---

## Key File Paths (Quick Navigation)

| Concern | Path |
|---------|------|
| INode, IGateway interfaces | `src/Juice.Workflows/Models/INode.cs` |
| Node, Gateway base classes | `src/Juice.Workflows/Nodes/Nodes.cs` |
| ExclusiveGateway | `src/Juice.Workflows/Nodes/Gateways/ExclusiveGateway.cs` |
| InclusiveGateway | `src/Juice.Workflows/Nodes/Gateways/InclusiveGateway.cs` |
| ParallelGateway | `src/Juice.Workflows/Nodes/Gateways/ParallelGateway.cs` |
| EventBasedGateway | `src/Juice.Workflows/Nodes/Gateways/EventBasedGateway.cs` |
| SequenceFlow (condition logic) | `src/Juice.Workflows/Models/SequenceFlow.cs` |
| FlowRecord / FlowContext | `src/Juice.Workflows/Execution/FlowRecord.cs` / `FlowContext.cs` |
| WorkflowContext | `src/Juice.Workflows/Execution/WorkflowContext.cs` |
| NodeContext / NodeRecord | `src/Juice.Workflows/Execution/NodeContext.cs` |
| DI extensions | `src/Juice.Workflows/DependencyInjection/WorkflowServiceCollectionExtensions.cs` |
| Fluent builder | `src/Juice.Workflows/Builder/` |
| BPMN builder | `src/Juice.Workflows.Bpmn/Builder/WorkflowContextBuilder.cs` |
| BPMN type mapping | `src/Juice.Workflows.Bpmn/Models/Constants.cs` |
| YAML builder | `src/Juice.Workflows.Yaml/Builder/WorkflowContextBuilder.cs` |
| Gateway tests | `test/Juice.Workflows.Tests/ExclusiveGatewayTests.cs` |
