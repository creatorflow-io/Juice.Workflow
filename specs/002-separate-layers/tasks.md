# Tasks: Introduce Abstractions Package for Node and Repository Extensions

**Input**: Design documents from `/specs/002-separate-layers/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2)
- All paths are relative to `D:\Workspaces\Juice\services\workflow\`

---

## Phase 1: Setup (New Project)

**Purpose**: Create the `Juice.Workflows.Abstractions` project and wire it into the solution.

- [X] T001 Create `src/Juice.Workflows.Abstractions/Juice.Workflows.Abstractions.csproj` targeting `net6.0;net8.0;net9.0` with `Microsoft.NET.Sdk`, `FrameworkReference Microsoft.AspNetCore.App`, and the same NuGet dependencies as `Juice.Workflows` (Juice.Extensions, Microsoft.Extensions.Localization.Abstractions, Newtonsoft.Json, Juice.MediatR)
- [X] T002 Add `Juice.Workflows.Abstractions` project to `Workflow.sln`
- [X] T003 [P] Create `src/Juice.Workflows.Abstractions/GlobalUsings.cs` with global usings for `Juice.MediatR`, `Microsoft.Extensions.Localization`, `Newtonsoft.Json`, and `Juice.Extensions`

---

## Phase 2: Foundational (Code Fixes — Blocking Prerequisites)

**Purpose**: Fix the one concrete coupling (`NodeContext → StartEvent`) and the domain event coupling (`ProcessStartedDomainEvent → NodeContext`) before any files move. These changes must be made in-place in `Juice.Workflows` first.

**⚠️ CRITICAL**: Phase 3 and Phase 4 depend on T004. T005 and T006 can run in parallel after T004.

- [X] T004 Add `public interface IStartNode : INode { }` to `src/Juice.Workflows/Models/INode.cs` (inside the existing `Juice.Workflows.Models` namespace, alongside other marker interfaces)
- [X] T005 [P] Update `NodeContext.IsStart()` and `NodeContext.IsStartOf()` in `src/Juice.Workflows/Execution/NodeContext.cs` to check `Node is IStartNode` instead of `Node is StartEvent`
- [X] T006 [P] Change `ProcessStartedDomainEvent.Node` property type from `NodeContext` to `NodeRecord` in `src/Juice.Workflows/Domain/Events/ProcessStartedDomainEvent.cs`; update the single call site that publishes this event inside `src/Juice.Workflows/`

**Checkpoint**: Run `dotnet build src/Juice.Workflows/Juice.Workflows.csproj` — must build with zero errors before proceeding

---

## Phase 3: User Story 1 — Custom Node Contract (Priority: P1) 🎯 MVP

**Goal**: Move all `INode`-related contracts and execution-model types to `Juice.Workflows.Abstractions` so a developer can implement `INode` with only `Juice.Workflows.Abstractions` referenced.

**Independent Test**: Create a throwaway class library referencing only `Juice.Workflows.Abstractions`, implement `INode` and `IStartNode`, and confirm it compiles with zero transitive references to `Juice.Workflows` or any EF package.

### Implementation — Models (all parallelizable)

- [X] T007 [P] [US1] Move `src/Juice.Workflows/Models/INode.cs` (with `IStartNode` and all existing marker interfaces) to `src/Juice.Workflows.Abstractions/Models/INode.cs`; preserve namespace `Juice.Workflows.Models`
- [X] T008 [P] [US1] Move `src/Juice.Workflows/Models/IFlow.cs` to `src/Juice.Workflows.Abstractions/Models/IFlow.cs`; preserve namespace `Juice.Workflows.Models`
- [X] T009 [P] [US1] Move `src/Juice.Workflows/Models/Outcome.cs` to `src/Juice.Workflows.Abstractions/Models/Outcome.cs`; preserve namespace `Juice.Workflows.Models`
- [X] T010 [P] [US1] Move `src/Juice.Workflows/Models/WorkflowStatus.cs` to `src/Juice.Workflows.Abstractions/Models/WorkflowStatus.cs`; preserve namespace `Juice.Workflows.Models`
- [X] T011 [P] [US1] Move `src/Juice.Workflows/Models/IConditionEvaluator.cs` to `src/Juice.Workflows.Abstractions/Models/IConditionEvaluator.cs`; preserve namespace `Juice.Workflows.Models`
- [X] T012 [P] [US1] Move `src/Juice.Workflows/Models/ILogicConditionEvaluator.cs` to `src/Juice.Workflows.Abstractions/Models/ILogicConditionEvaluator.cs`; preserve namespace `Juice.Workflows.Models`
- [X] T013 [P] [US1] Move `src/Juice.Workflows/Models/SequenceFlow.cs` to `src/Juice.Workflows.Abstractions/Models/SequenceFlow.cs`; preserve namespace `Juice.Workflows.Models`

### Implementation — Execution models (all parallelizable)

- [X] T014 [P] [US1] Move `src/Juice.Workflows/Execution/NodeRecord.cs` to `src/Juice.Workflows.Abstractions/Execution/NodeRecord.cs`; preserve namespace `Juice.Workflows.Execution`
- [X] T015 [P] [US1] Move `src/Juice.Workflows/Execution/FlowRecord.cs` to `src/Juice.Workflows.Abstractions/Execution/FlowRecord.cs`; preserve namespace `Juice.Workflows.Execution`
- [X] T016 [P] [US1] Move `src/Juice.Workflows/Execution/ProcessRecord.cs` to `src/Juice.Workflows.Abstractions/Execution/ProcessRecord.cs`; preserve namespace `Juice.Workflows.Execution`
- [X] T017 [P] [US1] Move `src/Juice.Workflows/Execution/NodeExecutionResult.cs` to `src/Juice.Workflows.Abstractions/Execution/NodeExecutionResult.cs`; preserve namespace `Juice.Workflows.Execution`
- [X] T018 [P] [US1] Move `src/Juice.Workflows/Execution/FlowContext.cs` to `src/Juice.Workflows.Abstractions/Execution/FlowContext.cs`; preserve namespace `Juice.Workflows.Execution`

### Implementation — Wire Juice.Workflows to Abstractions

- [X] T019 [US1] Add `<ProjectReference Include="..\Juice.Workflows.Abstractions\Juice.Workflows.Abstractions.csproj" />` to `src/Juice.Workflows/Juice.Workflows.csproj`
- [X] T020 [US1] Add `global using Juice.Workflows.Models;` and `global using Juice.Workflows.Execution;` to `src/Juice.Workflows/GlobalUsings.cs` to restore implicit availability of moved types within `Juice.Workflows`
- [X] T021 [US1] Add `: IStartNode` to `StartEvent` class declaration in `src/Juice.Workflows/Nodes/Events/StartEvent.cs`

### Implementation — Types that require Domain (moved in Phase 4 but needed by US1 types)

- [X] T022 [US1] Move `src/Juice.Workflows/Execution/NodeContext.cs` (already fixed in T005) to `src/Juice.Workflows.Abstractions/Execution/NodeContext.cs`; preserve namespace `Juice.Workflows.Execution` — depends on T015 (NodeRecord) and Domain types moved in Phase 4
- [X] T023 [US1] Move `src/Juice.Workflows/Execution/WorkflowContext.cs` to `src/Juice.Workflows.Abstractions/Execution/WorkflowContext.cs`; preserve namespace `Juice.Workflows.Execution` — depends on Domain/WorkflowState aggregate moved in Phase 4
- [X] T024 [US1] Move `src/Juice.Workflows/Execution/WorkflowExecutionResult.cs` to `src/Juice.Workflows.Abstractions/Execution/WorkflowExecutionResult.cs`; preserve namespace `Juice.Workflows.Execution`

**Checkpoint**: After Phase 4 completes, run `dotnet build src/Juice.Workflows.Abstractions` — must build with zero errors

---

## Phase 4: User Story 2 — Custom Repository Contract (Priority: P1)

**Goal**: Move all domain aggregates and repository interfaces to `Juice.Workflows.Abstractions` so a developer can implement any of the four repository interfaces with only `Juice.Workflows.Abstractions` referenced.

**Independent Test**: Create a throwaway class library referencing only `Juice.Workflows.Abstractions`, implement `IDefinitionRepository`, `IWorkflowRepository`, `IWorkflowStateRepository`, and `IEventRepository`, and confirm it compiles with zero transitive references to `Juice.Workflows` or EF packages.

### Implementation — Domain aggregates (all parallelizable)

- [X] T025 [P] [US2] Move all files in `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/` to `src/Juice.Workflows.Abstractions/Domain/AggregatesModel/DefinitionAggregate/`; preserve namespace `Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate`
- [X] T026 [P] [US2] Move all files in `src/Juice.Workflows/Domain/AggregatesModel/WorkflowAggregate/` to `src/Juice.Workflows.Abstractions/Domain/AggregatesModel/WorkflowAggregate/`; preserve namespace `Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate`
- [X] T027 [P] [US2] Move all files in `src/Juice.Workflows/Domain/AggregatesModel/WorkflowStateAggregate/` to `src/Juice.Workflows.Abstractions/Domain/AggregatesModel/WorkflowStateAggregate/`; preserve namespace `Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate`
- [X] T028 [P] [US2] Move all files in `src/Juice.Workflows/Domain/AggregatesModel/EventAggregate/` to `src/Juice.Workflows.Abstractions/Domain/AggregatesModel/EventAggregate/`; preserve namespace `Juice.Workflows.Domain.AggregatesModel.EventAggregate`
- [X] T029 [P] [US2] Move `src/Juice.Workflows/Domain/Events/ProcessStartedDomainEvent.cs` (already fixed in T006) and `DefinitionDataChangedDomainEvent.cs` to `src/Juice.Workflows.Abstractions/Domain/Events/`; preserve namespace `Juice.Workflows.Domain.Events`

### Implementation — Wire Juice.Workflows to moved Domain types

- [X] T030 [US2] Add `global using` entries for all moved Domain namespaces (`Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate`, `.WorkflowAggregate`, `.WorkflowStateAggregate`, `.EventAggregate`, `Juice.Workflows.Domain.Events`) to `src/Juice.Workflows/GlobalUsings.cs`

**Checkpoint**: Run `dotnet build Workflow.sln` — must build with zero errors (excluding pre-existing Protos errors from `Juice.Workflows.Api.Contracts`)

---

## Phase 5: Integration — Update Dependent Projects

**Purpose**: Update `Juice.Workflows.Designer` to reference Abstractions directly and confirm the full solution and test suite are clean.

- [X] T031 Replace `<ProjectReference Include="..\Juice.Workflows\Juice.Workflows.csproj" />` with `<ProjectReference Include="..\Juice.Workflows.Abstractions\Juice.Workflows.Abstractions.csproj" />` in `src/Juice.Workflows.Designer/Juice.Workflows.Designer.csproj`
- [X] T032 Run `dotnet build Workflow.sln` and confirm zero C# compile errors
- [X] T033 Run `dotnet test test/Juice.Workflows.Designer.Tests/Juice.Workflows.Designer.Tests.csproj` — all 25 tests must pass
- [X] T034 Run `dotnet test test/Juice.Workflows.Tests/Juice.Workflows.Tests.csproj` — all existing workflow tests must pass

---

## Phase 6: Polish & Verification

**Purpose**: Confirm the stated goals from SC-001 and SC-002 are objectively verifiable.

- [X] T035 [P] Verify SC-001: create a minimal class library `test/Juice.Workflows.Abstractions.ContractTest/` referencing only `Juice.Workflows.Abstractions`, implement `INode` and `IDefinitionRepository`, and confirm `dotnet build` succeeds with no reference to `Juice.Workflows` in the output
- [X] T036 [P] Verify SC-004: confirm `Juice.Workflows.Designer.csproj` resolved dependency graph contains no direct reference to `Juice.Workflows` (run `dotnet list src/Juice.Workflows.Designer/ reference` — only `Juice.Workflows.Abstractions` and BPMN/YAML/EF should appear as direct refs)
- [X] T037 Update `specs/002-separate-layers/checklists/requirements.md` to mark all items complete

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS** Phases 3 and 4
- **Phase 3 (US1)**: Depends on Phase 2; T022/T023/T024 also depend on Phase 4 types being in Abstractions
- **Phase 4 (US2)**: Depends on Phase 2; can run in parallel with Phase 3 tasks T007–T021
- **Phase 5 (Integration)**: Depends on Phases 3 and 4 complete
- **Phase 6 (Polish)**: Depends on Phase 5

### User Story Dependencies

- **US1 (P1)**: T007–T024 — start after Phase 2; T022–T024 require Phase 4 to be complete
- **US2 (P1)**: T025–T030 — start after Phase 2; can run in parallel with T007–T021

### Parallel Opportunities Within Phase 3 + 4

```
After Phase 2 completes, launch in parallel:
  US1 Models:     T007, T008, T009, T010, T011, T012, T013
  US1 Execution:  T014, T015, T016, T017, T018
  US2 Domain:     T025, T026, T027, T028, T029

Then sequential:
  T019, T020, T021 (wire Juice.Workflows → Abstractions)
  T030             (wire Domain global usings)
  T022, T023, T024 (complex Execution types that need Domain in place)
```

---

## Implementation Strategy

### MVP First (US1 only — custom node support)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational fixes
3. Complete Phase 3 T007–T021 (Models + simple Execution types + wiring)
4. Complete Phase 4 (Domain types needed by T022–T024)
5. Complete Phase 3 T022–T024
6. Run `dotnet build src/Juice.Workflows.Abstractions` → **VALIDATE US1**

### Full Delivery

1. MVP above (US1)
2. Phase 4 T025–T030 (Domain aggregates + repository interfaces) → **VALIDATE US2**
3. Phase 5 (Integration) → Full solution build + tests pass
4. Phase 6 (Polish) → SC-001 and SC-004 objectively verified

---

## Notes

- [P] tasks operate on different files and can safely run in parallel
- "Move" means: create the file in Abstractions with identical content and namespace, then delete the original from `Juice.Workflows`
- Do NOT change any namespace in any moved file
- After each move batch, add the corresponding `global using` to `Juice.Workflows/GlobalUsings.cs` so internal code continues to compile
- Avoid touching test files — all existing tests must pass without modification (SC-002, SC-003)
- Pre-existing Protos build errors from `Juice.Workflows.Api.Contracts` are not caused by this change and can be ignored
