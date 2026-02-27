# Tasks: Logic Gateway

**Input**: Design documents from `/specs/001-logic-gateway/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/api.md ✅, quickstart.md ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify the branch and ensure the project builds cleanly before any new code is added.

- [x] T001 Verify solution builds on all targets: `dotnet build Workflow.sln`
- [x] T002 Run existing tests to establish green baseline: `dotnet test Workflow.sln -f net9.0`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core contracts and interfaces that MUST exist before any user story can be implemented.
All downstream tasks depend on these types compiling.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T003 Create `ILogicConditionEvaluator` interface in `src/Juice.Workflows/Models/ILogicConditionEvaluator.cs` — extends `IConditionEvaluator`, marker interface only (no new members)
- [x] T004 Add `GatewayRoutingMode` enum (`Exclusive`, `Inclusive`) in `src/Juice.Workflows/Nodes/Gateways/GatewayRoutingMode.cs`
- [x] T005 Create `LogicGateway` skeleton class in `src/Juice.Workflows/Nodes/Gateways/LogicGateway.cs` — extend `Gateway`, inject `ILogicConditionEvaluator` and `ILogger<LogicGateway>`, `DisplayText` = "Logic Gateway", all method stubs (call base)
- [x] T006 Register `ILogicConditionEvaluator` in `src/Juice.Workflows/DependencyInjection/WorkflowServiceCollectionExtensions.cs` — `TryAddTransient<ILogicConditionEvaluator, SimpleExpressionEvaluator>()` (forward reference; will compile once T009 exists — add T009 first or register as placeholder)
- [x] T007 Build solution to confirm all new types resolve: `dotnet build Workflow.sln`

**Checkpoint**: Foundation ready — user story implementation can begin.

---

## Phase 3: User Story 1 — Use Built-in Condition Expressions (Priority: P1) 🎯 MVP

**Goal**: A developer can define a workflow with a `LogicGateway`, write conditions like `total > 100` or `status == approved AND total <= 10000` directly in the workflow definition, and have the gateway route correctly at runtime — with no custom code required.

**Independent Test**: Build a workflow (fluent builder) with a Logic Gateway in exclusive mode, two conditional branches, and verify only the expected branch executes for a given context input. Run with: `dotnet test Workflow.sln --filter "FullyQualifiedName~LogicGateway" -f net9.0`

### Implementation for User Story 1

- [x] T008 [US1] Implement `SimpleExpressionEvaluator` in `src/Juice.Workflows/Models/SimpleExpressionEvaluator.cs`:
  - Implements `ILogicConditionEvaluator`
  - Recursive descent parser: `expression = term ( ("AND"|"&&"|"OR"|"||") term )*`
  - `term = "(" expression ")" | comparison`
  - `comparison = variable operator literal`
  - Operators: `==`, `!=`, `>`, `<`, `>=`, `<=`
  - Variable resolution: `context.Input` first, then `context.Output` (dot-path keys)
  - Missing variable → returns `false` (no fault)
  - Malformed expression → returns `false`, logs warning (no throw)
  - Precedence: `AND`/`&&` binds tighter than `OR`/`||`; parentheses override

- [x] T009 [US1] Implement `LogicGateway.StartAsync` in `src/Juice.Workflows/Nodes/Gateways/LogicGateway.cs`:
  - Guard: `flow == null` → return `Fault("...")`
  - Convergence: `ctx.AnyIncompleteActivePathTo(node)` → return `Noop` (wait for remaining tokens)
  - Otherwise: return `Outcomes("Done")`

- [x] T010 [US1] Implement `LogicGateway.PreSelectOutgoingFlowAsync` in `src/Juice.Workflows/Nodes/Gateways/LogicGateway.cs`:
  - Exclusive mode: if `ctx.AnyActiveFlowFrom(source)` → return `false` (another flow already won)
  - `flow.ConditionExpression == null` → return `null` (unconditional; let SequenceFlow decide)
  - Otherwise: return `await _evaluator.EvaluateAsync(expression, ctx, source)`
  - Inclusive mode: skip the `AnyActiveFlowFrom` check; evaluate all flows independently

- [x] T011 [US1] Implement `LogicGateway.PostExecuteCheckAsync` in `src/Juice.Workflows/Nodes/Gateways/LogicGateway.cs`:
  - If `!ctx.AnyActiveFlowFrom(node)` → return `Fault("No condition matched and no default flow configured for Logic Gateway '{name}'.")`
  - Otherwise return `null` (pass)

- [x] T012 [US1] Add `GetRoutingMode` private helper in `src/Juice.Workflows/Nodes/Gateways/LogicGateway.cs`:
  - `node.Properties["mode"] == "inclusive"` → `GatewayRoutingMode.Inclusive`
  - else → `GatewayRoutingMode.Exclusive` (default)

- [x] T013 [US1] Add `tComplexGateway` → `LogicGateway` mapping in `src/Juice.Workflows.Bpmn/Models/Constants.cs` (BPMN support, needed for integration test)

- [x] T014 [P] [US1] Write unit tests for `SimpleExpressionEvaluator` in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Numeric comparisons: `total > 100` with total=150 → true; total=50 → false
  - String equality: `status == approved` with status="approved" → true; "rejected" → false
  - Inequality: `role != admin` with role="user" → true
  - AND/&&: `total > 100 AND status == approved` — both true → true; one false → false
  - OR/||: `status == approved OR status == pending` — either true → true; both false → false
  - Parentheses: `(status == approved OR status == pending) AND priority > 5` — correct grouping
  - Mixed: `role != admin OR level >= 3`
  - Missing variable → false
  - Malformed expression → false (no throw)

- [x] T015 [US1] Write integration test for exclusive mode routing in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Fluent builder workflow: Start → DummyTask → LogicGateway(exclusive) → [branch1: condition `total <= 1000`] → [branch2: condition `total > 1000`] → Merge → End
  - With `total = 500`: assert branch1 executes, branch2 skipped
  - With `total = 2000`: assert branch2 executes, branch1 skipped

- [x] T016 [US1] Write integration test for inclusive mode routing in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Fluent builder workflow with `GatewayRoutingMode.Inclusive`, two overlapping conditions
  - With both conditions matching: assert both branches execute (parallel paths)
  - With one matching: assert only one branch executes

- [ ] T017 [US1] Write integration test for convergence (multiple incoming flows) in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Workflow with parallel split feeding into LogicGateway from two paths
  - Assert gateway waits for both tokens before evaluating conditions (Noop on first arrival, evaluates on second)

- [x] T018 [US1] Write integration test for "no condition matches, no default → Fault" in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`

- [x] T019 [US1] Build and run US1 tests to confirm all pass: `dotnet test Workflow.sln --filter "FullyQualifiedName~LogicGateway" -f net9.0`

**Checkpoint**: User Story 1 fully functional — built-in condition evaluation works end-to-end.

---

## Phase 4: User Story 2 — Inherit and Customize Decision Logic (Priority: P2)

**Goal**: A developer can create a custom evaluator implementing `ILogicConditionEvaluator`, or subclass `LogicGateway` directly overriding `PreSelectOutgoingFlowAsync`, register it via DI, and have the workflow engine use their custom logic.

**Independent Test**: Implement a custom `ILogicConditionEvaluator` stub that always returns `true` for a named expression. Register it. Run a workflow using `LogicGateway` and verify the custom evaluator's logic fires. Run with: `dotnet test Workflow.sln --filter "FullyQualifiedName~LogicGateway" -f net9.0`

### Implementation for User Story 2

- [x] T020 [US2] Verify `LogicGateway` constructor signature matches custom subclassing contract in `src/Juice.Workflows/Nodes/Gateways/LogicGateway.cs`:
  - Constructor: `(ILogicConditionEvaluator evaluator, ILogger<LogicGateway> logger, IStringLocalizerFactory localizer)`
  - `protected` access on key methods so subclasses can override

- [x] T021 [US2] Write test: custom `ILogicConditionEvaluator` replacing the default in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Register a `AlwaysTrueEvaluator : ILogicConditionEvaluator` in test DI
  - Run a workflow with LogicGateway and multiple branches — verify all branches activate (inclusive-like behavior from evaluator returning true)
  - Confirm default `SimpleExpressionEvaluator` is replaced (not stacked)

- [x] T022 [US2] Write test: custom `LogicGateway` subclass in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Implement `FixedRouteGateway : LogicGateway` that overrides `PreSelectOutgoingFlowAsync` to always activate a flow named "fixed"
  - Register with `services.RegisterNodes(typeof(FixedRouteGateway))`
  - Run workflow, verify "fixed" branch executes

- [x] T023 [US2] Write test: exception in custom evaluator → workflow faults in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - `ThrowingEvaluator` always throws `InvalidOperationException`
  - Assert workflow transitions to `Faulted` and exception detail recorded

- [x] T024 [US2] Run all tests: `dotnet test Workflow.sln --filter "FullyQualifiedName~LogicGateway" -f net9.0`

**Checkpoint**: User Story 2 fully functional — custom subclassing and DI replacement work end-to-end.

---

## Phase 5: User Story 3 — Configure Gateway Behavior Declaratively (Priority: P3)

**Goal**: A developer can set routing mode and default fallback flow in YAML (`parameters: { mode: inclusive }`) or BPMN (`default="flowId"`) without writing any code, and the gateway respects those settings at runtime.

**Independent Test**: Define a YAML workflow with `mode: inclusive` on the Logic Gateway. Load it via `Juice.Workflows.Yaml` and execute — verify multiple branches activate. Then define another YAML workflow with `mode: exclusive` and a default fallback — verify the default activates when no conditions match.

### Implementation for User Story 3

- [x] T025 [US3] Verify YAML builder passes `parameters` dict to `NodeContext.Properties` for `LogicGateway` — inspect `src/Juice.Workflows.Yaml/Builder/WorkflowContextBuilder.cs`; no changes needed if properties dict is already forwarded, document finding

- [x] T026 [US3] Add `.Logic()` fluent method to fluent builder in `src/Juice.Workflows/Builder/WorkflowContextBuilder.cs`:
  - Signature: `public WorkflowContextBuilder Logic(string? name = default, GatewayRoutingMode mode = GatewayRoutingMode.Exclusive)`
  - Sets `Properties["mode"] = mode.ToString().ToLower()` on the gateway node context
  - Chains `Fork()`/`Merge()` correctly (follow `.Exclusive()`/`.Inclusive()` existing pattern)

- [ ] T027 [P] [US3] Create YAML test workflow file `test/Juice.Workflows.Tests/workflows/logic-gateway-inclusive.yaml`:
  - Two branches, both conditions always true (e.g., `total > 0`)
  - `mode: inclusive`

- [ ] T028 [P] [US3] Create YAML test workflow file `test/Juice.Workflows.Tests/workflows/logic-gateway-default-flow.yaml`:
  - Two guarded branches, one unguarded default
  - `mode: exclusive`
  - Context will have no matching conditions → default activates

- [x] T029 [US3] Write integration test: YAML inclusive mode in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Load `logic-gateway-inclusive.yaml`, execute with `total = 500`
  - Assert both branches execute

- [x] T030 [US3] Write integration test: YAML default fallback flow in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Load `logic-gateway-default-flow.yaml`, execute with no matching condition input
  - Assert default branch executes, no fault

- [x] T031 [US3] Write integration test: no default + no match → Fault in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - YAML workflow, all conditions false, no default flow
  - Assert workflow reaches `Faulted` state with descriptive message

- [x] T032 [US3] Write integration test: BPMN `<bpmn:complexGateway>` loads and routes correctly in `test/Juice.Workflows.Tests/LogicGatewayTests.cs`:
  - Create minimal BPMN file `test/Juice.Workflows.Tests/workflows/logic-gateway.bpmn` with `<bpmn:complexGateway>` and two conditional sequence flows
  - Assert it loads as `LogicGateway` and routes correctly

- [x] T033 [US3] Run all tests: `dotnet test Workflow.sln --filter "FullyQualifiedName~LogicGateway" -f net9.0`

**Checkpoint**: All user stories fully functional and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Completeness, integration with existing tests, and documentation validation.

- [x] T034 [P] Run full solution test suite to confirm no regressions: `dotnet test Workflow.sln -f net9.0`
- [ ] T035 [P] Validate `quickstart.md` examples compile and run — manually trace through each code snippet against implemented API
- [ ] T036 Ensure `LogicGateway` appears correctly in any existing visualization/display tests (search for `DisplayText` usage in test project)
- [ ] T037 [P] Check `LogicGateway` is covered by execution history tracking — verify it appears in `WorkflowState` history after execution (spot-check existing `WorkflowExecutor` instrumentation)
- [ ] T038 Commit all changes with a descriptive message referencing the feature branch

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2 — core gateway behavior
- **User Story 2 (Phase 4)**: Depends on Phase 2 (T003–T007 must exist); T020 depends on T005/T009/T010
- **User Story 3 (Phase 5)**: Depends on Phase 3 (LogicGateway must be fully functional); T025–T026 depend on T005
- **Polish (Phase 6)**: Depends on all desired user stories complete

### User Story Dependencies

- **US1 (P1)**: Only requires Foundation (Phase 2) — fully independent
- **US2 (P2)**: Requires Foundation (Phase 2); logically assumes US1 complete for meaningful tests
- **US3 (P3)**: Requires US1 (Phase 3) complete — fluent builder `.Logic()` method must exist and gateway must be functional

### Within Each User Story

- `SimpleExpressionEvaluator` (T008) before `LogicGateway.PreSelectOutgoingFlowAsync` (T010) — evaluator must exist to be called
- `LogicGateway.StartAsync` (T009) before `PostExecuteCheckAsync` (T011) — start must succeed first
- `GetRoutingMode` helper (T012) before `PreSelectOutgoingFlowAsync` (T010) — needed internally
- Tests (T014–T018) can be written in parallel with T008–T013 (TDD encouraged)

### Parallel Opportunities

| Group | Tasks | Can run in parallel |
|-------|-------|---------------------|
| Foundation | T003, T004 | ✅ different files |
| US1 eval + gateway | T008, T009 | ✅ different files |
| US1 gateway methods | T010, T011, T012 | ✅ same file but independent methods |
| US1 BPMN mapping | T013 | ✅ different project |
| US1 tests | T014, T015, T016, T017, T018 | ✅ different test methods |
| US3 YAML files | T027, T028 | ✅ different files |
| Polish | T034, T035, T036, T037 | ✅ independent checks |

---

## Parallel Example: User Story 1

```bash
# Step 1 — implement evaluator and gateway skeleton in parallel:
Task: "Implement SimpleExpressionEvaluator in src/Juice.Workflows/Models/SimpleExpressionEvaluator.cs"
Task: "Implement LogicGateway.StartAsync in src/Juice.Workflows/Nodes/Gateways/LogicGateway.cs"

# Step 2 — implement remaining gateway methods (same file, sequential) + BPMN mapping in parallel:
Task: "Implement PreSelectOutgoingFlowAsync (T010)"
Task: "Add tComplexGateway mapping in src/Juice.Workflows.Bpmn/Models/Constants.cs (T013)"

# Step 3 — write all US1 tests in parallel:
Task: "Unit tests for SimpleExpressionEvaluator (T014)"
Task: "Integration test: exclusive routing (T015)"
Task: "Integration test: inclusive routing (T016)"
Task: "Integration test: convergence (T017)"
Task: "Integration test: no-match fault (T018)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational — interfaces, skeleton, DI registration
3. Complete Phase 3: User Story 1 — `SimpleExpressionEvaluator` + full `LogicGateway`
4. **STOP and VALIDATE**: run `dotnet test Workflow.sln --filter "FullyQualifiedName~LogicGateway" -f net9.0`
5. Demo exclusive routing with `total > 100` expressions

### Incremental Delivery

1. Foundation → buildable skeleton
2. US1 → working gateway with built-in expressions (MVP)
3. US2 → custom evaluator/subclassing extensibility
4. US3 → declarative YAML/BPMN configuration
5. Polish → full suite green, visualization confirmed

---

## Notes

- [P] tasks target different files or independent test methods — safe to parallelise
- Each user story phase ends with a test run checkpoint
- `SimpleExpressionEvaluator` must have zero external dependencies (Constitution Principle I)
- Default routing mode is `Exclusive` when `Properties["mode"]` is absent or unrecognised
- `ILogicConditionEvaluator` is separate from `IConditionEvaluator` — both must be registered independently
- BPMN `default` attribute on `<bpmn:complexGateway>` is already handled by `WorkflowContext.IsDefaultOutgoing()` — no additional parser changes needed
- All tests use in-memory repositories — no database or message broker required (Constitution Principle III)
