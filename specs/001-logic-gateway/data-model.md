# Data Model: Logic Gateway

**Date**: 2026-02-26
**Branch**: `001-logic-gateway`

---

## Enumerations

### GatewayRoutingMode

```
GatewayRoutingMode
  Exclusive   — First matching condition activates its flow; remaining flows are skipped.
  Inclusive   — All conditions evaluated independently; every matching flow is activated.
```

**Default**: `Exclusive`
**Stored as**: lowercase string in `NodeContext.Properties["mode"]` (`"exclusive"` / `"inclusive"`)

---

## Interfaces

### ILogicConditionEvaluator

Extends `IConditionEvaluator`. Marker interface for the evaluator used by `LogicGateway`.
Allows independent DI registration from `IConditionEvaluator` (used by `SequenceFlow`).

```
ILogicConditionEvaluator : IConditionEvaluator
  (inherits) Task<bool> EvaluateAsync(string expression, WorkflowContext context, NodeContext source)
```

---

## Classes

### SimpleExpressionEvaluator

Default implementation of `ILogicConditionEvaluator`. Evaluates simple binary expressions
with no external dependencies.

```
SimpleExpressionEvaluator : ILogicConditionEvaluator
  EvaluateAsync(expression, context, source) → bool

Grammar (recursive descent):
  expression  = term ( ("AND" | "&&" | "OR" | "||") term )*
  term        = "(" expression ")" | comparison
  comparison  = variable operator literal
  variable    = dot-path string  (e.g., "total", "order.status")
  operator    = "==" | "!=" | ">" | "<" | ">=" | "<="
  literal     = unquoted-string | integer | decimal

Precedence
  AND / &&  binds tighter than  OR / ||  (standard boolean precedence)
  Parentheses override precedence

Variables
  Dot-path key resolved from context.Input first, then context.Output
  Missing variable → comparison evaluates to false (does not fault)

Operators
  ==    string/numeric equality
  !=    string/numeric inequality
  >     numeric greater-than          (numeric literals only)
  <     numeric less-than             (numeric literals only)
  >=    numeric greater-than-or-equal (numeric literals only)
  <=    numeric less-than-or-equal    (numeric literals only)

Literals
  Unquoted string  (e.g., approved, pending)
  Integer literal  (e.g., 100, 0)
  Decimal literal  (e.g., 99.5)

Examples
  total > 100
  status == approved
  total > 100 AND status == approved
  (status == approved OR status == pending) AND priority > 5
  role != admin OR level >= 3
```

**Error handling**: Malformed expressions (unparseable) → `EvaluateAsync` returns `false`
and logs a warning. Does not throw; the gateway's `PostExecuteCheckAsync` handles the
"no flow activated" scenario if all expressions fail.

---

### LogicGateway

Core gateway node. Located in `src/Juice.Workflows/Nodes/Gateways/LogicGateway.cs`.

```
LogicGateway : Gateway, IGateway
  Dependencies (injected):
    ILogicConditionEvaluator _evaluator
    ILogger<LogicGateway> _logger

  Properties (NodeContext.Properties["mode"]):
    mode : string → GatewayRoutingMode  (default: "exclusive")

  Methods:
    DisplayText → "Logic Gateway"

    StartAsync(ctx, node, flow, token) → NodeExecutionResult
      • flow == null                          → Fault
      • ctx.AnyIncompleteActivePathTo(node)   → Noop  (wait for convergence)
      • else                                  → Outcomes("Done")

    PreSelectOutgoingFlowAsync(ctx, source, dest, flow) → bool?
      • mode == Exclusive AND ctx.AnyActiveFlowFrom(source) → false  (already selected)
      • flow.ConditionExpression == null       → null   (defer to SequenceFlow unconditional)
      • else                                  → await _evaluator.EvaluateAsync(expression, ctx, source)

    PreSelectIncomingFlowAsync(ctx, source, dest, flow) → bool?
      • (inherits Gateway base → null; no restriction on incoming flow selection)

    PostExecuteCheckAsync(ctx, node, token) → NodeExecutionResult?
      • !ctx.AnyActiveFlowFrom(node)          → Fault("No condition matched...")
      • else                                  → null  (pass)
```

**Routing mode helper** (private):
```
GetRoutingMode(NodeContext node) → GatewayRoutingMode
  node.Properties["mode"] == "inclusive" → Inclusive
  else                                   → Exclusive  (default)
```

---

## Configuration Contract (NodeContext.Properties)

| Key    | Type   | Values                     | Default       |
|--------|--------|----------------------------|---------------|
| `mode` | string | `"exclusive"`, `"inclusive"` | `"exclusive"` |

All three definition formats write to this property bag:
- **Fluent builder**: parameter on `.Logic()` method
- **YAML**: `parameters.mode` key
- **BPMN**: `mode` attribute on the `<bpmn:complexGateway>` element

---

## Relationships to Existing Types

```
INode
  └── IGateway
        └── Gateway (abstract)          ← existing base
              └── LogicGateway          ← NEW

IConditionEvaluator                     ← existing interface
  └── ILogicConditionEvaluator          ← NEW interface
        └── SimpleExpressionEvaluator   ← NEW implementation

OutcomeConditionEvaluator               ← existing (SequenceFlow default, unchanged)
```

---

## No Persistence Changes

`LogicGateway` is a stateless execution node. `NodeContext.Properties` is runtime-only
(not persisted to `WorkflowState`). No database migrations are required.
