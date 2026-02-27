# Feature Specification: Logic Gateway

**Feature Branch**: `001-logic-gateway`
**Created**: 2026-02-26
**Status**: Draft
**Input**: User description: "We need support LOGIC gateway to make decision based on the conditions. It must be ready to use, easily inheritance, flexible and configurable."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Use Built-in Condition Expressions (Priority: P1)

A developer defines a workflow where branching depends on values in the workflow's data context
— for example, routing an order to fast-shipping or standard-shipping based on the order total.
The Logic Gateway may receive tokens from one or more incoming flows and, after evaluating
conditions, activates one or more outgoing flows depending on the configured routing mode.
Conditions are configured directly in the workflow definition (YAML, BPMN, or fluent builder)
using simple expression strings, with no custom code required.

**Why this priority**: This is the most common use case and the baseline value the feature must
deliver. Without ready-to-use condition evaluation, developers cannot adopt the gateway without
writing custom code first.

**Independent Test**: Build a workflow with a Logic Gateway, multiple incoming flows converging
on it, and multiple outgoing flows each guarded by a condition. Trigger the workflow and verify
that only the expected outgoing flow(s) are activated based on the current context.

**Acceptance Scenarios**:

1. **Given** a Logic Gateway with a single incoming flow and two outgoing flows with conditions
   (`total > 100`, `total <= 100`), **When** the gateway is reached with `total = 150`,
   **Then** only the flow with condition `total > 100` is activated (exclusive mode).
2. **Given** a Logic Gateway with multiple incoming flows (convergence), **When** the last
   expected incoming token arrives, **Then** the gateway evaluates its conditions once and
   activates the appropriate outgoing flow(s) — it does not evaluate once per incoming token.
3. **Given** a Logic Gateway in inclusive mode with three outgoing flows where two conditions
   match, **When** the gateway evaluates, **Then** both matching flows are activated and
   execution continues on two parallel paths.
4. **Given** a condition expression that references a missing context variable, **When** the
   gateway evaluates it, **Then** the gateway faults gracefully with a descriptive error message
   rather than throwing an unhandled exception.

---

### User Story 2 - Inherit and Customize Decision Logic (Priority: P2)

A developer needs decision logic that cannot be expressed as a simple text expression — for
example, routing based on a database lookup, an external API response, or a complex business
rule object. They create a custom gateway class by extending a base Logic Gateway class and
overriding a single, well-named evaluation method. Their class is registered in the DI container
and referenced in the workflow definition by name or type.

**Why this priority**: "Easily inheritance" and "flexible" are explicit requirements. Without an
extensibility point, the gateway is only useful for trivial conditions.

**Independent Test**: Implement a subclass that always routes to a specific flow based on a
custom rule. Register it and run a workflow that uses it; verify the custom routing fires.

**Acceptance Scenarios**:

1. **Given** a custom gateway subclass that overrides the evaluation method, **When** it is
   referenced in a workflow definition, **Then** the workflow engine discovers and executes it
   through the standard node execution pipeline without requiring core engine changes.
2. **Given** a custom gateway that throws an exception during evaluation, **When** the workflow
   reaches it, **Then** the workflow transitions to Faulted status and the error is recorded in
   the execution history.

---

### User Story 3 - Configure Gateway Behavior Declaratively (Priority: P3)

A workflow operator (or developer configuring a workflow definition) can control the gateway's
routing mode and condition evaluation strategy through the workflow definition file without
changing any code. Options such as exclusive-vs-inclusive routing and a default fallback flow
can be set per gateway instance in YAML or BPMN attributes.

**Why this priority**: "Configurable" is an explicit requirement. Per-instance configuration
allows reusing the same gateway type with different behavior in different workflows.

**Independent Test**: Define two workflow definitions that reference the same Logic Gateway type
but configure it differently (exclusive in one, inclusive in the other). Run both and verify
each behaves according to its own configuration.

**Acceptance Scenarios**:

1. **Given** a Logic Gateway configured with a default fallback flow, **When** none of the
   guarded outgoing flows match, **Then** the default flow is activated instead of faulting.
2. **Given** a Logic Gateway with no default flow and no matching conditions, **When** the
   gateway is reached, **Then** the workflow faults with a clear "no matching condition" error.
3. **Given** a workflow definition in YAML that sets `mode: inclusive`, **When** multiple
   conditions match, **Then** all matching flows are activated (parallel continuation).

---

### Edge Cases

- What happens when two outgoing flows carry identical conditions? The first match wins in
  exclusive mode; both activate in inclusive mode.
- What happens when the gateway has multiple incoming flows and only some have arrived?
  The gateway MUST wait for all expected incoming tokens before evaluating conditions —
  consistent with the convergence behavior of InclusiveGateway.
- What happens when a condition expression is syntactically invalid at definition load time?
  The engine MUST reject the workflow definition with a validation error before any execution
  attempt.
- What happens when the workflow context is null or empty during gateway evaluation? The gateway
  MUST treat all condition references to missing variables as false, activating the default flow
  if configured or faulting otherwise.
- What happens when all outgoing flows are activated in inclusive mode — is a downstream join
  required? Yes; the caller is responsible for placing an appropriate convergence gateway
  (e.g., InclusiveGateway) downstream if the parallel paths must be re-joined.
- What happens when a custom subclass is referenced in the definition but not registered in DI?
  The engine MUST report a clear "unknown gateway type" error at workflow startup.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a Logic Gateway node that accepts one or more incoming
  flows, evaluates conditions against the current workflow execution context, and activates
  one or more outgoing flows based on those conditions.
- **FR-002**: The Logic Gateway MUST support at least two routing modes:
  - **Exclusive** — first matching condition activates its flow; remaining conditions are skipped.
  - **Inclusive** — all conditions are evaluated independently; every matching flow is activated.
- **FR-002a**: When the Logic Gateway has multiple incoming flows, it MUST wait until all
  expected incoming tokens have arrived before evaluating conditions (convergence-then-decide
  semantics), similar to InclusiveGateway convergence behavior.
- **FR-003**: The system MUST ship a ready-to-use built-in condition evaluator that accepts
  condition strings referencing context variables by name and supports:
  - Binary comparisons: `==`, `!=`, `>`, `<`, `>=`, `<=`
  - Logical operators: `AND` / `&&`, `OR` / `||`
  - Grouping with parentheses: `(condition1 AND condition2) OR condition3`
  - No additional developer code or external dependencies required.
- **FR-004**: Developers MUST be able to create custom gateway logic by extending a documented
  base class or implementing a defined interface, overriding a single evaluation method.
- **FR-005**: Custom gateway implementations MUST be registerable via the standard DI container
  and referenceable from workflow definitions by name or type identifier.
- **FR-006**: Each Logic Gateway instance MUST support an optional default fallback flow that
  activates when no condition matches.
- **FR-007**: The Logic Gateway MUST be definable in all three supported workflow definition
  formats: fluent builder, BPMN XML, and YAML.
- **FR-008**: Invalid or syntactically malformed condition expressions MUST be detected at
  workflow definition load/validation time, not at runtime.
- **FR-009**: Condition evaluation errors at runtime (e.g., missing variable, evaluation
  exception) MUST transition the workflow to Faulted status with a descriptive error recorded
  in the execution history.
- **FR-010**: The Logic Gateway MUST participate in the standard workflow execution pipeline
  (MediatR command handling, execution history, `WorkflowStatus` transitions) identically to
  existing gateway types.

### Key Entities

- **LogicGateway**: A gateway node that accepts one or more incoming flows, holds an ordered
  list of condition-guarded outgoing flows, a routing mode (exclusive / inclusive), and an
  optional reference to a default fallback flow. When multiple incoming flows are present, the
  gateway converges them before evaluating conditions.
- **GatewayCondition**: A named condition attached to an outgoing flow; carries an expression
  string or a reference to a custom evaluator; evaluates to true/false given a context.
- **GatewayRoutingMode**: Enumeration — Exclusive (first match) / Inclusive (all matches).
- **ConditionEvaluationResult**: The outcome of evaluating one condition — matched/not-matched
  plus an optional diagnostic message for tracing.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer with no prior knowledge of the gateway can define and execute a
  working condition-based routing workflow using only the built-in expression evaluator and the
  existing documentation, without writing any custom classes.
- **SC-002**: A developer can create and register a custom gateway subclass and have it
  recognized by a running workflow in under 30 minutes of implementation work.
- **SC-003**: All Logic Gateway behavior — built-in expression evaluation, custom subclassing,
  and per-instance configuration — is covered by automated tests that run without any external
  infrastructure (database, message broker).
- **SC-004**: Logic Gateway nodes appear correctly in workflow visualization output and execution
  history alongside existing gateway types.
- **SC-005**: Workflows using Logic Gateways complete without errors for all expected input
  combinations; unexpected inputs (missing variables, no matching conditions) produce structured,
  actionable error messages rather than unhandled exceptions.

## Assumptions

- Condition expressions in the built-in evaluator use the existing workflow context data bag
  (the same variable scope available to other nodes); no separate expression language sandbox
  is introduced.
- "Flexible and configurable" is interpreted as: per-instance routing mode + default flow +
  developer extensibility via inheritance/interface. It does not require a visual rule editor
  or runtime rule management UI.
- The Logic Gateway is a synchronous decision point; it does not wait for external events
  (that is the role of `EventBasedGateway`). It may wait for multiple incoming tokens to
  converge, but the decision itself is made synchronously based on the current context.
- When used as a pure diverging gateway (single incoming flow), no convergence wait is needed.
  When used as a combined converge-then-decide gateway (multiple incoming flows), it waits for
  all active incoming paths before evaluating — the same convergence check as InclusiveGateway.
- Existing exclusive/inclusive gateways are NOT replaced; the Logic Gateway is a new,
  developer-facing complement that makes the decision logic explicit and overridable.
