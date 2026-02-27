# Implementation Plan: Logic Gateway

**Branch**: `001-logic-gateway` | **Date**: 2026-02-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-logic-gateway/spec.md`

## Summary

Add a `LogicGateway` node to the core workflow engine that evaluates developer-configured
conditions against the workflow execution context to decide which outgoing flow(s) to activate.
Supports both exclusive (first match) and inclusive (all matches) routing modes, accepts one or
more incoming flows (convergence-then-decide), and is extensible via `ILogicConditionEvaluator`.
Ships with a built-in simple-expression evaluator requiring no external dependencies.

## Technical Context

**Language/Version**: C# targeting net6.0 / net8.0 / net9.0 (multi-target, mandatory)
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Localization, Juice.MediatR
**Storage**: N/A — no new persistence entities; gateway is a pure execution node
**Testing**: xunit + FluentAssertions + in-memory repositories (no infrastructure required)
**Target Platform**: Library (`Juice.Workflows` NuGet package)
**Project Type**: Library
**Performance Goals**: Synchronous decision point; negligible overhead vs existing gateways
**Constraints**: Must not break existing gateway behavior; no new NuGet package dependencies in core
**Scale/Scope**: One new node type + one evaluator interface + one default implementation; touches BPMN, YAML, and fluent builder parsers

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Library Design | ✅ PASS | Lives in `Juice.Workflows` core; no new packages; `ILogicConditionEvaluator` is opt-in for custom evaluators |
| II. Standard Workflow Model | ✅ PASS | Implements `Gateway : IGateway : INode`; BPMN-compatible (maps to `ComplexGateway`); fits Gateway category |
| III. Test-First | ✅ PASS | All tests use in-memory repos; test coverage required before implementation is done |
| IV. Distributed Reliability | ✅ N/A | Pure synchronous decision node; no network or message bus involvement |
| V. Multi-Framework Compatibility | ✅ PASS | Core library already multi-targets; no new framework constraints introduced |

## Project Structure

### Documentation (this feature)

```text
specs/001-logic-gateway/
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — entities and interfaces
├── quickstart.md        # Phase 1 — usage guide
├── contracts/
│   └── api.md           # Phase 1 — fluent builder / YAML / BPMN contracts
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/Juice.Workflows/
├── Models/
│   ├── IConditionEvaluator.cs          # existing — IConditionEvaluator + OutcomeConditionEvaluator
│   ├── ILogicConditionEvaluator.cs     # NEW — extends IConditionEvaluator for LogicGateway
│   └── SimpleExpressionEvaluator.cs    # NEW — built-in evaluator (no external deps)
├── Nodes/
│   └── Gateways/
│       └── LogicGateway.cs             # NEW — the gateway node
└── DependencyInjection/
    └── WorkflowServiceCollectionExtensions.cs  # MODIFY — register ILogicConditionEvaluator

src/Juice.Workflows.Bpmn/
└── Models/
    └── Constants.cs                    # MODIFY — add tComplexGateway → LogicGateway mapping

src/Juice.Workflows.Yaml/
└── Builder/
    └── WorkflowContextBuilder.cs       # VERIFY — parameters dict already passed as properties

src/Juice.Workflows/
└── Builder/
    └── WorkflowContextBuilder.cs       # MODIFY — add .Logic() fluent method (in-code builder)

test/Juice.Workflows.Tests/
└── LogicGatewayTests.cs                # NEW — unit tests (in-memory, no infra)
```

**Structure Decision**: Single project (core library). No new projects required. All changes stay
within `Juice.Workflows` core and the two parser projects (BPMN, YAML). Fluent builder gets one
new method.

## Complexity Tracking

> No constitution violations — table not required.
