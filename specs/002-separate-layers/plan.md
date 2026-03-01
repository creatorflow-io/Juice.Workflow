# Implementation Plan: Introduce Abstractions Package

**Branch**: `002-separate-layers` | **Date**: 2026-03-01 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-separate-layers/spec.md`

---

## Summary

Introduce `Juice.Workflows.Abstractions` — a new class library containing all interfaces, domain aggregates, execution-model data types, and node/flow contracts. This allows custom node and repository authors to reference only this lightweight package, with zero transitive dependency on the execution engine (`Juice.Workflows`) or any database package.

The single required code fix is adding `IStartNode` marker interface and updating `NodeContext.IsStart()` to use it instead of the concrete `StartEvent` class. All other types move without behaviour changes. Namespaces are preserved; existing consumers are unaffected.

---

## Technical Context

**Language/Version**: C# 12, .NET 6 / 8 / 9 (multi-target mandatory)
**Primary Dependencies**: `Juice.Extensions`, `Microsoft.Extensions.Localization.Abstractions`, `Newtonsoft.Json`, `Juice.MediatR` (domain events)
**Storage**: N/A — no new persistent entities; no migrations required
**Testing**: xUnit with in-memory repositories (existing test infrastructure)
**Target Platform**: NuGet library — consumed by .NET 6/8/9 hosts
**Project Type**: Class library (NuGet package)
**Performance Goals**: N/A — pure structural change, no runtime behaviour changes
**Constraints**: Zero breaking changes to public API; all existing tests must pass unchanged
**Scale/Scope**: One new project, ~40 files moved, 3 files modified (NodeContext, StartEvent, ProcessStartedDomainEvent), 1 csproj updated (Designer)

---

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Library Design | ✅ PASS | This feature directly implements Principle I — extracting contracts into a standalone package |
| II. Standard Workflow Model | ✅ PASS | `INode` contract is preserved unchanged; node graph model is unmodified |
| III. Test-First with In-Memory Isolation | ✅ PASS | Existing tests cover all moved types; no test infrastructure changes required |
| IV. Distributed Reliability | ✅ PASS | No changes to MediatR pipelines, Outbox, or event bus |
| V. Multi-Framework Compatibility | ✅ PASS | New project multi-targets `net6.0;net8.0;net9.0`; no API requires higher than net6 |

No violations. Complexity Tracking table not required.

---

## Project Structure

### Documentation (this feature)

```text
specs/002-separate-layers/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── abstractions-api.md   ← Phase 1 output
├── checklists/
│   └── requirements.md
└── tasks.md             ← Phase 2 output (/speckit.tasks)
```

### Source Code (affected paths)

```text
src/
├── Juice.Workflows.Abstractions/          ← NEW project
│   ├── Juice.Workflows.Abstractions.csproj
│   ├── GlobalUsings.cs
│   ├── Models/
│   │   ├── INode.cs                       ← + IStartNode marker interface
│   │   ├── IFlow.cs
│   │   ├── Outcome.cs
│   │   ├── WorkflowStatus.cs
│   │   ├── IConditionEvaluator.cs
│   │   ├── ILogicConditionEvaluator.cs
│   │   └── SequenceFlow.cs
│   ├── Execution/
│   │   ├── WorkflowContext.cs
│   │   ├── NodeContext.cs                 ← IsStart() uses IStartNode (not StartEvent)
│   │   ├── FlowContext.cs
│   │   ├── NodeExecutionResult.cs
│   │   ├── WorkflowExecutionResult.cs
│   │   ├── ProcessRecord.cs
│   │   ├── NodeRecord.cs
│   │   └── FlowRecord.cs
│   └── Domain/
│       ├── AggregatesModel/
│       │   ├── DefinitionAggregate/       ← all files moved
│       │   ├── WorkflowAggregate/         ← all files moved
│       │   ├── WorkflowStateAggregate/    ← all files moved
│       │   └── EventAggregate/            ← all files moved
│       └── Events/
│           ├── ProcessStartedDomainEvent.cs   ← Node: NodeContext → NodeRecord
│           └── DefinitionDataChangedDomainEvent.cs
│
├── Juice.Workflows/                       ← UPDATED
│   ├── Juice.Workflows.csproj             ← + ref to Juice.Workflows.Abstractions
│   ├── GlobalUsings.cs                    ← + global using Juice.Workflows.Abstractions types
│   ├── Models/
│   │   ├── Node.cs                        ← stays (base class with Halt/Fault/Outcomes helpers)
│   │   └── SimpleExpressionEvaluator.cs   ← stays (concrete implementation)
│   └── Nodes/
│       └── Events/
│           └── StartEvent.cs              ← + implements IStartNode
│
└── Juice.Workflows.Designer/             ← UPDATED
    └── Juice.Workflows.Designer.csproj   ← Juice.Workflows ref → Juice.Workflows.Abstractions

test/
└── Juice.Workflows.Tests/                ← unchanged (all tests continue to pass)
    └── (existing test files unchanged)
```

**Structure Decision**: Single new project extracted from `Juice.Workflows`. All moved files keep their existing namespaces. `Juice.Workflows` is updated to reference and re-expose Abstractions so existing consumers need no changes.

---

## Phase 0: Research

Complete. See [research.md](research.md).

Key decisions:
1. **Scope**: Move interfaces, aggregates, and execution-model data types. Leave implementations.
2. **`IStartNode`**: New marker interface fixes the `NodeContext` → `StartEvent` concrete coupling.
3. **`ProcessStartedDomainEvent`**: `Node` property changed from `NodeContext` to `NodeRecord`.
4. **WorkflowContext interface checks**: Already use marker interfaces (`IIntermediate`, `ICatching`, etc.) — no changes needed.
5. **Namespaces unchanged**: No breaking changes to consumer `using` directives.
6. **Designer**: Direct `Juice.Workflows` reference removed; BPMN/YAML references retained.

---

## Phase 1: Design & Contracts

Complete.

- **[data-model.md](data-model.md)**: Package dependency graph, type ownership table, change to `NodeContext`, `IStartNode` definition.
- **[contracts/abstractions-api.md](contracts/abstractions-api.md)**: Full public API surface for custom node and repository authors.
- **[quickstart.md](quickstart.md)**: Step-by-step guide for implementing a custom node and a custom repository using only `Juice.Workflows.Abstractions`.

---

## Implementation Notes

### Step 1 — Create `Juice.Workflows.Abstractions` project
Create `src/Juice.Workflows.Abstractions/Juice.Workflows.Abstractions.csproj` targeting `net6.0;net8.0;net9.0`. Add to `Workflow.sln`.

### Step 2 — Add `IStartNode` to `INode.cs`
Add `public interface IStartNode : INode { }` in `Models/INode.cs`.

### Step 3 — Move files (copy then delete originals)
Move all files listed in the data-model to the new project. Keep namespaces identical.

### Step 4 — Fix `NodeContext.IsStart()`
Replace `Node is StartEvent` with `Node is IStartNode` in both methods.

### Step 5 — Fix `ProcessStartedDomainEvent`
Change `public NodeContext Node` to `public NodeRecord Node`. Update the single call site inside `Juice.Workflows` that publishes this event.

### Step 6 — Update `Juice.Workflows.csproj`
Add `<ProjectReference>` to `Juice.Workflows.Abstractions`. Add `global using` for all moved namespaces in `GlobalUsings.cs` so existing internal files compile without changes.

### Step 7 — Update `StartEvent.cs`
Add `: IStartNode` to its interface list.

### Step 8 — Update `Juice.Workflows.Designer.csproj`
Replace `<ProjectReference>` to `Juice.Workflows` with `Juice.Workflows.Abstractions`.

### Step 9 — Verify
Run `dotnet build Workflow.sln` and `dotnet test Workflow.sln`. All 25 Designer tests and all existing workflow tests must pass.
