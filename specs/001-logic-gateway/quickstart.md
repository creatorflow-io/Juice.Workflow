# Quickstart: Logic Gateway

**Date**: 2026-02-26
**Branch**: `001-logic-gateway`

---

## What is the Logic Gateway?

`LogicGateway` routes workflow execution along one or more paths based on conditions evaluated
against the current context. It supports:

- **Exclusive mode** (default) — exactly one path is taken (first matching condition wins)
- **Inclusive mode** — all matching paths are taken simultaneously

It can act as a pure **diverging** gateway (one incoming flow, multiple outgoing) or as a
**converge-then-decide** gateway (multiple incoming flows that are first joined, then evaluated).

---

## Setup

```csharp
// Program.cs / Startup.cs
services.AddWorkflowServices();
// LogicGateway and SimpleExpressionEvaluator are registered automatically
```

No additional packages are required.

---

## Example 1 — Exclusive Routing (Fluent Builder)

Route an order to an approval or rejection task based on context input:

```csharp
services.RegisterWorkflow("order-approval", builder =>
{
    builder
        .Start()
        .Then<SubmitOrderTask>("submit")
        .Logic("route", GatewayRoutingMode.Exclusive)
            .Fork().Then<ApproveTask>("approve",   condition: "total <= 1000")
            .Fork().Then<ReviewTask>("review",      condition: "total > 1000")
        .Merge()
        .End();
});
```

Execute with input:

```csharp
var result = await workflow.StartAsync("order-approval", new Dictionary<string, object?>
{
    { "total", 500 }
});
// → "approve" task is executed; "review" task is skipped
```

---

## Example 2 — Inclusive Routing (Notifications)

Notify multiple teams when conditions overlap:

```csharp
services.RegisterWorkflow("incident-notify", builder =>
{
    builder
        .Start()
        .Then<LogIncidentTask>("log")
        .Logic("notify", GatewayRoutingMode.Inclusive)
            .Fork().Then<NotifyManagerTask>("mgr",  condition: "severity > 5")
            .Fork().Then<NotifyOnCallTask>("oncall", condition: "type == outage")
        .Merge()   // InclusiveGateway convergence downstream
        .End();
});
```

With `severity = 8, type = outage` both tasks run in parallel.
With `severity = 2, type = outage` only `oncall` runs.

---

## Example 3 — Default Fallback Flow

Ensure the workflow always continues even when no condition matches:

```csharp
builder
    .Logic("triage")
        .Fork().Then<FastTrackTask>("fast",    condition: "priority == high")
        .Fork().Then<StandardTask>("standard", isDefault: true)   // fallback
    .Merge()
    .End();
```

---

## Example 4 — YAML Definition

```yaml
- name: Approval Process
  steps:
    - name: Start
      type: StartEvent
    - name: Submit
      type: UserTask
    - name: Route
      type: LogicGateway
      parameters:
        mode: exclusive
      branches:
        - steps:
            - name: Auto Approve
              type: ServiceTask
              condition: "total <= 1000"
        - steps:
            - name: Manual Review
              type: UserTask
      merge_branches:
        - Auto Approve
        - Manual Review
    - name: End
      type: EndEvent
```

---

## Example 5 — Custom Condition Evaluator

Replace the built-in evaluator with one that calls your own logic:

```csharp
public class RuleEngineEvaluator : ILogicConditionEvaluator
{
    private readonly IRuleEngine _rules;

    public RuleEngineEvaluator(IRuleEngine rules) => _rules = rules;

    public Task<bool> EvaluateAsync(
        string expression, WorkflowContext context, NodeContext source)
        => _rules.EvaluateAsync(expression, context.Input);
}

// Register (replaces SimpleExpressionEvaluator):
services.AddTransient<ILogicConditionEvaluator, RuleEngineEvaluator>();
```

---

## Expression Syntax Reference (Built-in Evaluator)

### Comparisons

| Expression              | Meaning                                        |
|-------------------------|------------------------------------------------|
| `total > 100`           | Input/Output "total" is greater than 100       |
| `status == approved`    | Input/Output "status" equals "approved"        |
| `retries >= 3`          | Numeric greater-than-or-equal                  |
| `role != admin`         | String inequality                              |
| `count == 0`            | Zero check                                     |

### Logical Operators

Both keyword and symbol forms are accepted and equivalent:

| Expression                                               | Meaning                           |
|----------------------------------------------------------|-----------------------------------|
| `total > 100 AND status == approved`                     | Both conditions must be true      |
| `total > 100 && status == approved`                      | Same as above                     |
| `status == approved OR status == pending`                | Either condition must be true     |
| `status == approved \|\| status == pending`              | Same as above                     |
| `(status == approved OR status == pending) AND priority > 5` | Grouped OR, then AND check   |
| `(status == approved \|\| status == pending) && priority > 5` | Same as above               |
| `role != admin OR level >= 3`                            | Admin bypass or sufficient level  |

**Precedence**: `AND` / `&&` binds tighter than `OR` / `||`. Use parentheses `( )` to override.

### Variable Resolution

Variables are resolved from `context.Input` first, then `context.Output`.
Missing variables evaluate to `false` — no fault is raised; the gateway handles the
"no flow activated" scenario via the default flow or a fault result.

---

## Behaviour Summary

| Scenario | Result |
|---------|--------|
| Single incoming flow, one condition matches (exclusive) | That flow activates |
| Single incoming flow, multiple match (exclusive) | First matching flow only |
| Single incoming flow, multiple match (inclusive) | All matching flows activate |
| Multiple incoming flows, not all arrived yet | Gateway waits (Noop) |
| All incoming flows arrived, then evaluates | Normal evaluation proceeds |
| No condition matches, default flow configured | Default flow activates |
| No condition matches, no default flow | Workflow faults with clear message |

---

## Running Tests

```bash
dotnet test Workflow.sln --filter "FullyQualifiedName~LogicGateway" -f net9.0
```
