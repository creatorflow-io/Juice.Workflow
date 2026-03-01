# Implementation Plan: Workflow Designer UI

**Branch**: `001-bpmn-designer-ui` | **Date**: 2026-02-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-bpmn-designer-ui/spec.md`

---

## Summary

Add a web-based BPMN workflow designer to `Juice.Workflows`. The feature comprises:
1. A new `Juice.Workflows.Designer` ASP.NET Core project (SPA host + REST API controller) that dispatches to the existing MediatR command pipeline.
2. A new `Juice.Workflows.Designer.Frontend` Vite/React SPA using `bpmn-js` for the visual BPMN editor and `@monaco-editor/react` + `monaco-yaml` for the YAML text editor.
3. Domain changes: a `WorkflowDefinitionStatus` enum and lifecycle methods (`Publish`, `Archive`) on `WorkflowDefinition`, plus `ListAsync`/`ExistsByNameAsync` on `IDefinitionRepository`.
4. EF migrations for the new `Status` column in both SQL Server and PostgreSQL providers.

---

## Technical Context

**Language/Version**: C# .NET 6/8/9 (backend, multi-target per Principle V); TypeScript/React 18 (frontend, browser)
**Primary Dependencies**:
- Backend: ASP.NET Core, MediatR, EF Core (version per TFM), `Juice.Workflows.Bpmn`, `Juice.Workflows.Yaml`
- Frontend: `bpmn-js` 18.x, `@monaco-editor/react`, `monaco-yaml`, Vite 6, React 18, TypeScript
**Storage**: Existing `WorkflowDbContext` (SQL Server / PostgreSQL via EF Core); new `Status int` column via EF migration
**Testing**: xUnit + in-memory repositories (`AddInMemoryReposistories()`) for backend unit tests; no infrastructure required for unit tests
**Target Platform**: ASP.NET Core web host (serves SPA static files + REST API); modern browsers (ES2020+)
**Project Type**: Web application — ASP.NET Core backend SPA host + Vite/React SPA frontend (two separate projects)
**Performance Goals**: Definition list loads in < 2s for up to 100 definitions; save + convert round-trip < 500ms
**Constraints**: Backend must multi-target net6.0/net8.0/net9.0 (Constitution Principle V); no breaking changes to `INode` or gRPC proto
**Scale/Scope**: Single-tenant initially; ~100s of workflow definitions per installation; 1–5 concurrent designers

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I — Modular Library Design | ✅ PASS | `Juice.Workflows.Designer` is a new opt-in package. Core engine (`Juice.Workflows`) is extended minimally (Status field + lifecycle methods). Adding `Status` to the core entity is justified: it is domain data, not infrastructure. |
| II — BPMN-Compatible Node Graph | ✅ PASS | No new node types added. The designer only creates/edits definitions; execution model is unchanged. |
| III — Test-First with In-Memory Isolation | ✅ PASS | All MediatR command handlers must have xUnit tests using in-memory repositories. Frontend tests are out of scope for this plan cycle. |
| IV — Distributed Reliability | ✅ PASS | Designer commands are definition-management operations (not runtime execution); Outbox pattern is not required. The existing Outbox on `DefinitionDataChangedDomainEvent` covers propagation of definition changes to downstream consumers. |
| V — Multi-Framework Compatibility | ✅ PASS | `Juice.Workflows.Designer.csproj` must target `$(AppTargetFramework)` (net6/8/9). `Juice.Workflows` core changes must not break any TFM. EF migrations are provider-specific (SqlServer + PostgreSQL). |
| Database migrations | ✅ REQUIRED | `AddWorkflowDefinitionStatus` migration needed in both `Juice.Workflows.EF.SqlServer` and `Juice.Workflows.EF.PostgreSQL`. |

**No constitution violations. No Complexity Tracking table required.**

---

## Project Structure

### Documentation (this feature)

```text
specs/001-bpmn-designer-ui/
├── plan.md              # This file
├── research.md          # Phase 0: library decisions, API facts
├── data-model.md        # Phase 1: entities, commands, state machine
├── quickstart.md        # Phase 1: dev setup guide
├── contracts/
│   └── rest-api.md      # Phase 1: REST API contract
└── tasks.md             # Phase 2: task list (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Juice.Workflows/                                          # MODIFIED
│   └── Domain/AggregatesModel/DefinitionAggregate/
│       ├── WorkflowDefinition.cs                            # + Status, Publish(), Archive()
│       ├── WorkflowDefinitionStatus.cs                      # NEW: enum Draft/Active/Archived
│       ├── WorkflowDefinitionSummary.cs                     # NEW: lightweight read-model
│       └── IDefinitionRepository.cs                         # + ListAsync, ExistsByNameAsync
│
├── Juice.Workflows.EF/                                       # MODIFIED
│   ├── Repositories/DefinitionRepository.cs                 # + ListAsync, ExistsByNameAsync
│   └── WorkflowDbContext.cs                                 # + Status column config
│
├── Juice.Workflows.EF.SqlServer/                             # MODIFIED
│   └── Migrations/                                          # + AddWorkflowDefinitionStatus
│
├── Juice.Workflows.EF.PostgreSQL/                            # MODIFIED
│   └── Migrations/                                          # + AddWorkflowDefinitionStatus
│
├── Juice.Workflows.Designer/                                 # NEW (directory exists, empty)
│   ├── Juice.Workflows.Designer.csproj                      # NEW: ASP.NET Core, net6/8/9
│   ├── Program.cs                                           # NEW: SPA host + API routes
│   ├── Controllers/
│   │   └── WorkflowDefinitionsController.cs                 # NEW: REST CRUD + lifecycle
│   ├── Commands/
│   │   ├── CreateWorkflowDefinitionCommand.cs               # NEW: MediatR command
│   │   ├── UpdateWorkflowDefinitionCommand.cs               # NEW
│   │   ├── RenameWorkflowDefinitionCommand.cs               # NEW
│   │   ├── PublishWorkflowDefinitionCommand.cs              # NEW
│   │   ├── ArchiveWorkflowDefinitionCommand.cs              # NEW
│   │   ├── DeleteWorkflowDefinitionCommand.cs               # NEW
│   │   └── ListWorkflowDefinitionsQuery.cs                  # NEW: MediatR query
│   ├── DependencyInjection/
│   │   └── DesignerServiceCollectionExtensions.cs           # NEW: DI registration
│   └── wwwroot/                                             # built SPA assets (git-ignored)
│
└── Juice.Workflows.Designer.Frontend/                        # NEW: Vite/React SPA
    ├── package.json
    ├── tsconfig.json
    ├── vite.config.ts
    ├── index.html
    └── src/
        ├── main.tsx                                         # Monaco env + app entry
        ├── App.tsx                                          # Router root
        ├── yaml.worker.js                                   # monaco-yaml worker re-export
        ├── pages/
        │   ├── DefinitionList.tsx                           # Dashboard list + search
        │   ├── BpmnEditor.tsx                               # Visual editor page
        │   └── YamlEditor.tsx                               # Monaco YAML page
        ├── components/
        │   ├── BpmnCanvas.tsx                               # bpmn-js useRef wrapper
        │   ├── PropertiesPanel.tsx                          # Node properties sidebar
        │   └── XmlSourceTab.tsx                             # Read-only Monaco XML tab
        └── api/
            └── definitionsApi.ts                            # Typed fetch → REST API

test/
├── Juice.Workflows.Tests/                                    # EXISTING (in-memory tests)
└── Juice.Workflows.Designer.Tests/                           # NEW (directory exists, empty)
    ├── Juice.Workflows.Designer.Tests.csproj                 # NEW: xUnit test project
    └── WorkflowDefinitionCommandTests.cs                     # NEW: command handler tests
```

**Structure Decision**: Option 2 (Web application) — separate backend (ASP.NET Core) and frontend (Vite/React) projects. Frontend source in `src/Juice.Workflows.Designer.Frontend/`; build output to `src/Juice.Workflows.Designer/wwwroot/`. All inter-project communication via REST API.

---

## Phase 0 Artifacts

- [research.md](./research.md) — All library decisions resolved; no NEEDS CLARIFICATION items remain.

## Phase 1 Artifacts

- [data-model.md](./data-model.md) — Entities, repository interface changes, command signatures, state machine.
- [contracts/rest-api.md](./contracts/rest-api.md) — REST API contract for all 7 endpoints.
- [quickstart.md](./quickstart.md) — Developer setup guide.

## Next Step

Run `/speckit.tasks` to generate the ordered task list (`tasks.md`) for implementation.
