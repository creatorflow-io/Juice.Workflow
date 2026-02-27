# API Contracts: Logic Gateway

**Date**: 2026-02-26
**Branch**: `001-logic-gateway`

---

## 1. Fluent Builder (In-Code)

### New method on `WorkflowContextBuilder`

```csharp
// Signature
public WorkflowContextBuilder Logic(
    string? name = default,
    GatewayRoutingMode mode = GatewayRoutingMode.Exclusive)
```

### Usage

```csharp
services.RegisterWorkflow(workflowId, builder =>
{
    builder
        .Start()
        .Then<UserTask>("review")
        .Logic("approval-decision", GatewayRoutingMode.Exclusive)
            .Fork().Then<ServiceTask>("approve-task",  condition: "status == approved AND total <= 10000")
            .Fork().Then<ServiceTask>("senior-review", condition: "status == approved AND total > 10000")
            .Fork().Then<ServiceTask>("reject-task",   condition: "status == rejected")
            .Fork().Then<ServiceTask>("escalate-task", isDefault: true)
        .Merge()
        .End();
});

// Inclusive mode — multiple paths can be taken simultaneously
builder
    .Start()
    .Then<UserTask>("classify")
    .Logic("route-decision", GatewayRoutingMode.Inclusive)
        .Fork().Then<ServiceTask>("notify-manager", condition: "priority > 5")
        .Fork().Then<ServiceTask>("notify-team",    condition: "type == urgent")
    .Merge()
    .End();
```

### Condition placement

`condition` is placed on the `.Then<T>()` or `.Wait<T>()` call **after** `.Fork()`. It becomes
`FlowRecord.ConditionExpression` on the flow leading into that node.

### Default flow

`isDefault: true` marks the flow as the fallback when no condition matches. A Logic Gateway
with no matching conditions and no default flow faults.

---

## 2. YAML Definition Format

### Schema extension (new `type` value)

```yaml
- name: My Process
  steps:
    - name: Start
      type: StartEvent

    - name: Decision
      type: LogicGateway
      parameters:
        mode: exclusive         # "exclusive" (default) | "inclusive"
      branches:
        - steps:
            - name: Approve
              type: ServiceTask
              condition: "status == approved"
        - steps:
            - name: Reject
              type: ServiceTask
              condition: "status == rejected"
        - steps:
            - name: Escalate     # default fallback (no condition)
              type: ServiceTask
      merge_branches:
        - Approve
        - Reject
        - Escalate

    - name: End
      type: EndEvent
```

### `parameters.mode` values

| Value         | Behaviour |
|---------------|-----------|
| `exclusive`   | First matching condition activates its flow (default) |
| `inclusive`   | All matching conditions activate their flows |

### `condition` placement

`condition` is set on the first step **inside** a branch. It maps to `FlowRecord.ConditionExpression`
on the flow from the gateway into that branch's first node. Branches with no `condition` and no
sibling that has `isDefault` set are treated as unconditional (always selected after exclusive check).

### Convergence (multiple incoming flows)

Use `merge_branches` on the Logic Gateway to converge parallel paths before deciding:

```yaml
    - name: Review Decision
      type: LogicGateway
      parameters:
        mode: inclusive
      merge_branches:        # converge these named nodes before evaluating conditions
        - Legal Review
        - Finance Review
      branches:
        - steps:
            - name: Full Approval
              type: ServiceTask
              condition: "legal == approved"
        - steps:
            - name: Partial Approval
              type: ServiceTask
              condition: "finance == approved"
```

---

## 3. BPMN XML Format

### Element mapping

| BPMN element          | Maps to          |
|-----------------------|------------------|
| `<bpmn:complexGateway>` | `LogicGateway` |

Added to `Constants.NodeTypesMapping`:
```
"tComplexGateway" → "LogicGateway"
```

### Example BPMN

```xml
<bpmn:process id="process1">

  <bpmn:startEvent id="start" />

  <bpmn:complexGateway id="gw1" name="Approval Decision" default="f3">
    <!-- mode attribute is a custom extension; defaults to exclusive -->
  </bpmn:complexGateway>

  <bpmn:serviceTask id="approve" name="Approve" />
  <bpmn:serviceTask id="reject"  name="Reject"  />
  <bpmn:serviceTask id="escalate" name="Escalate" />

  <bpmn:endEvent id="end" />

  <bpmn:sequenceFlow id="f0" sourceRef="start"   targetRef="gw1" />
  <bpmn:sequenceFlow id="f1" sourceRef="gw1"     targetRef="approve">
    <bpmn:conditionExpression>status == approved</bpmn:conditionExpression>
  </bpmn:sequenceFlow>
  <bpmn:sequenceFlow id="f2" sourceRef="gw1"     targetRef="reject">
    <bpmn:conditionExpression>status == rejected</bpmn:conditionExpression>
  </bpmn:sequenceFlow>
  <bpmn:sequenceFlow id="f3" sourceRef="gw1"     targetRef="escalate" />
  <!-- f3 is the default flow (no condition); referenced by default="f3" on the gateway -->

  <bpmn:sequenceFlow id="f4" sourceRef="approve"  targetRef="end" />
  <bpmn:sequenceFlow id="f5" sourceRef="reject"   targetRef="end" />
  <bpmn:sequenceFlow id="f6" sourceRef="escalate" targetRef="end" />

</bpmn:process>
```

### `default` attribute

The `default` attribute on `<bpmn:complexGateway>` maps to `NodeRecord.Default`, which is
already handled by `WorkflowContext.IsDefaultOutgoing()` — no additional BPMN parser changes
needed beyond the type mapping.

---

## 4. DI Registration

```csharp
// In AddWorkflowServices() (added automatically):
services.TryAddTransient<ILogicConditionEvaluator, SimpleExpressionEvaluator>();

// To replace with a custom evaluator:
services.AddTransient<ILogicConditionEvaluator, MyCustomEvaluator>();
// or implement ILogicConditionEvaluator on your gateway subclass and inject as needed
```

---

## 5. Custom Subclassing Contract

```csharp
// Override EvaluateAsync in a custom evaluator
public class DatabaseConditionEvaluator : ILogicConditionEvaluator
{
    private readonly IOrderRepository _repo;

    public DatabaseConditionEvaluator(IOrderRepository repo) => _repo = repo;

    public async Task<bool> EvaluateAsync(
        string expression, WorkflowContext context, NodeContext source)
    {
        // expression is the raw ConditionExpression string from the flow definition
        var orderId = context.Input["orderId"]?.ToString();
        var order = await _repo.GetAsync(orderId);
        return expression switch
        {
            "is-vip"     => order?.IsVip ?? false,
            "overdue"    => order?.DaysOverdue > 30,
            _            => false
        };
    }
}

// Registration
services.AddTransient<ILogicConditionEvaluator, DatabaseConditionEvaluator>();
```

Alternatively, subclass `LogicGateway` directly:

```csharp
public class OrderRoutingGateway : LogicGateway
{
    private readonly IOrderRepository _repo;

    public OrderRoutingGateway(
        IOrderRepository repo,
        ILogicConditionEvaluator evaluator,
        ILogger<LogicGateway> logger,
        IStringLocalizerFactory localizer)
        : base(evaluator, logger, localizer)
    {
        _repo = repo;
    }

    public override LocalizedString DisplayText => Localizer["Order Routing"];

    // Override PreSelectOutgoingFlowAsync for fully custom logic
    public override async Task<bool?> PreSelectOutgoingFlowAsync(
        WorkflowContext ctx, NodeContext source, NodeContext dest, FlowContext flow)
    {
        // Custom: look up order state and decide
        var orderId = ctx.Input["orderId"]?.ToString();
        var order = await _repo.GetAsync(orderId, CancellationToken.None);
        return flow.Record.Name switch
        {
            "vip-path"      => order?.IsVip,
            "standard-path" => !order?.IsVip,
            _               => null
        };
    }
}

// Registration
services.RegisterNodes(typeof(OrderRoutingGateway));
```
