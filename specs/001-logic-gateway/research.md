# Research: Logic Gateway

**Date**: 2026-02-26
**Branch**: `001-logic-gateway`

## Decision 1: Expression Evaluation Approach

**Question**: How should condition strings like `status == approved` or `total > 100` be
evaluated against the workflow context at runtime?

**Decision**: Ship a built-in `SimpleExpressionEvaluator` with no external dependencies,
backed by a pluggable `ILogicConditionEvaluator` interface so developers can substitute a
richer evaluator (e.g., NCalc, Dynamic LINQ) via DI.

**Rationale**:
- Keeps `Juice.Workflows` dependency-minimal (Constitution Principle I).
- The `ILogicConditionEvaluator` pattern is already established by `IConditionEvaluator`;
  extending it for LogicGateway follows the same pattern.
- A separate interface (`ILogicConditionEvaluator`) avoids keyed-service workarounds and
  keeps `SequenceFlow`'s default `OutcomeConditionEvaluator` untouched.

**Supported syntax in `SimpleExpressionEvaluator`**:

```
expression  = term ( ("AND" | "&&" | "OR" | "||") term )*
term        = "(" expression ")" | comparison
comparison  = variable operator literal
variable    = dot-path key  (e.g., "total", "order.status", "user.role")
operator    = "==" | "!=" | ">" | "<" | ">=" | "<="
literal     = unquoted-string | integer | decimal
```

- **Variable**: dot-path key resolved from `context.Input` first, then `context.Output`;
  missing variable evaluates to `false`
- **Operators**: `==`, `!=`, `>`, `<`, `>=`, `<=`
- **Logical**: `AND` / `&&` (both sides must be true), `OR` / `||` (either side must be true);
  standard precedence — `AND` / `&&` binds tighter than `OR` / `||`
- **Grouping**: parentheses `( ... )` override precedence
- **Literals**: unquoted string, integer, or decimal

Examples:
```
total > 100
status == approved
total > 100 AND status == approved
(status == approved OR status == pending) AND priority > 5
role != admin OR level >= 3
```

**Alternatives considered**:
| Alternative | Rejected because |
|-------------|-----------------|
| Dynamic LINQ / NCalc | External dependency; violates Constitution Principle I (dependency-minimal core) |
| System.Linq.Expressions compiler | Complex to implement safely; overkill for simple conditions |
| Reuse `OutcomeConditionEvaluator` as-is | Only does outcome-name string matching; cannot compare values |
| Scripting (Roslyn) | Far too heavy for the use case |

---

## Decision 2: Convergence Semantics (Multiple Incoming Flows)

**Question**: When the Logic Gateway has multiple incoming flows, how does it decide when to
evaluate conditions? Does it evaluate on first arrival or wait for all tokens?

**Decision**: Reuse `WorkflowContext.AnyIncompleteActivePathTo(node)` — the same convergence
check used by `InclusiveGateway`. Conditions are evaluated only after all active incoming
paths have delivered their tokens.

**Rationale**:
- Consistent with the "converge-then-decide" semantics specified in the spec (FR-002a).
- Reuses proven, already-tested infrastructure — no new state tracking needed.
- When the gateway has only a single incoming flow, `AnyIncompleteActivePathTo` returns
  false immediately (no paths in-flight), so there is no wait and no performance cost.

**Execution flow in `StartAsync`**:
1. Guard: `flow == null` → Fault
2. `AnyIncompleteActivePathTo(node)` → Noop (wait for remaining tokens)
3. Return `Outcomes("Done")` → executor proceeds to flow selection

---

## Decision 3: Flow Selection — Where Conditions Are Evaluated

**Question**: Should condition expressions be evaluated in `StartAsync` (returning matching
conditions as outcome names) or in `PreSelectOutgoingFlowAsync` (per-flow, at selection time)?

**Decision**: Evaluate in `PreSelectOutgoingFlowAsync` — override it in `LogicGateway` to
call the `ILogicConditionEvaluator` for each candidate outgoing flow.

**Rationale**:
- `StartAsync` does not have access to individual outgoing flow records; doing evaluation
  there would require inspecting `workflowContext.GetOutgoings(node)` manually, coupling
  the gateway to the executor's flow-selection loop.
- `PreSelectOutgoingFlowAsync` receives the specific `FlowContext` including
  `FlowRecord.ConditionExpression` — exactly the right place to evaluate it.
- Exclusive mode is enforced by checking `AnyActiveFlowFrom(source)` before evaluating,
  identical to how `ExclusiveGateway.PreSelectOutgoingFlowAsync` works.

**Execution path (exclusive mode)**:
```
PreSelectOutgoingFlowAsync(ctx, source, dest, flow)
  → if AnyActiveFlowFrom(source) → false  (another flow already won)
  → if flow.ConditionExpression == null → null  (unconditional; SequenceFlow decides)
  → evaluator.EvaluateAsync(expression, ctx, source) → true/false
```

**Execution path (inclusive mode)**:
```
PreSelectOutgoingFlowAsync(ctx, source, dest, flow)
  → if flow.ConditionExpression == null → null  (unconditional; SequenceFlow decides)
  → evaluator.EvaluateAsync(expression, ctx, source) → true/false
  (no AnyActiveFlowFrom check — multiple flows may activate)
```

---

## Decision 4: Routing Mode Configuration

**Question**: How is the routing mode (exclusive / inclusive) stored and read per gateway
instance?

**Decision**: Store as a string in `NodeContext.Properties["mode"]`. Default is exclusive.
All three definition formats set this property:
- **Fluent builder**: `Logic(mode: GatewayRoutingMode.Inclusive)` → sets property in builder
- **YAML**: `parameters: { mode: inclusive }` → passed via `Step.Parameters` dict
- **BPMN**: custom attribute `mode="inclusive"` on the gateway element

**Rationale**:
- `NodeContext.Properties` is the established mechanism for per-node configuration across
  all three definition formats.
- No schema changes to `NodeRecord` or database are needed (properties are ephemeral).

---

## Decision 5: BPMN Type Mapping

**Question**: BPMN 2.0 has no standard `logicGateway`. What BPMN element should map to
`LogicGateway`?

**Decision**: Map `tComplexGateway` → `LogicGateway` in `Constants.NodeTypesMapping`.
BPMN's `ComplexGateway` is semantically the closest match (arbitrary activation conditions).

**Rationale**:
- `ComplexGateway` is part of the BPMN 2.0 spec and is rarely used by other tools, so
  repurposing it does not conflict with existing BPMN workflows.
- Keeps the BPMN parser clean — no custom extension elements needed.
- If a workflow originally designed for another BPMN engine uses `ComplexGateway`, it will
  import correctly and route via the `ILogicConditionEvaluator`.

---

## Decision 6: `PostExecuteCheckAsync` Behavior

**Question**: What should happen when no outgoing flow matches (no condition evaluates true
and no default flow is configured)?

**Decision**: Override `PostExecuteCheckAsync` in `LogicGateway` to return `Fault(...)` if
`!AnyActiveFlowFrom(node)` — consistent with the fix applied to `ExclusiveGateway` and
`EventBasedGateway`.

---

## Decision 7: Separate `ILogicConditionEvaluator` Interface

**Question**: Should `LogicGateway` reuse `IConditionEvaluator` (already registered) or get
its own interface?

**Decision**: New interface `ILogicConditionEvaluator : IConditionEvaluator`. Registered
separately via `TryAddTransient<ILogicConditionEvaluator, SimpleExpressionEvaluator>`.

**Rationale**:
- .NET 6 compatibility rules out keyed/named service registrations.
- A separate interface allows `SequenceFlow` and `LogicGateway` to each have their own
  default evaluator without one overriding the other.
- Developers wanting the same evaluator for both can register a single implementation for
  both interfaces.
