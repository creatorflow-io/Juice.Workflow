# Tasks: Workflow Designer UI

**Input**: Design documents from `/specs/001-bpmn-designer-ui/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/rest-api.md ✅, quickstart.md ✅

**Tests**: xUnit tests included for all MediatR command handlers (required by Constitution Principle III).

**Organization**: Tasks grouped by user story. Each story phase is independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no in-phase dependencies)
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Setup

**Purpose**: Project scaffolding and tooling — no logic yet, just file/project creation.

- [x] T001 Create `src/Juice.Workflows.Designer/Juice.Workflows.Designer.csproj` — ASP.NET Core web project, `$(AppTargetFramework)` (net6/8/9), refs `Juice.Workflows.Bpmn`, `Juice.Workflows.Yaml`, `Juice.Workflows.EF`
- [x] T002 [P] Create `test/Juice.Workflows.Designer.Tests/Juice.Workflows.Designer.Tests.csproj` — xUnit project targeting net9.0, refs `Juice.Workflows.Designer` and `Juice.Workflows` (for in-memory repos)
- [x] T003 Add `Juice.Workflows.Designer` and `Juice.Workflows.Designer.Tests` to `Workflow.sln`
- [x] T004 Initialize Vite/React/TypeScript SPA in `src/Juice.Workflows.Designer.Frontend/` — run `npm create vite@latest . -- --template react-ts` and commit generated `package.json`, `tsconfig.json`, `vite.config.ts`, `index.html`, `src/main.tsx`, `src/App.tsx`
- [x] T005 [P] Install frontend npm dependencies in `src/Juice.Workflows.Designer.Frontend/` — `bpmn-js@^18`, `@monaco-editor/react`, `monaco-yaml`, `react-router-dom`, `vite-plugin-monaco-editor`
- [x] T006 [P] Configure `src/Juice.Workflows.Designer.Frontend/vite.config.ts` — add `monacoEditorPlugin`, set `worker.format: 'es'`, configure `server.proxy` to forward `/api` to `https://localhost:5001`, set `build.outDir` to `../Juice.Workflows.Designer/wwwroot`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain model changes, repository extensions, EF migrations, API infrastructure, and frontend routing skeleton — required by ALL user story phases.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T007 Create `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/WorkflowDefinitionStatus.cs` — `enum WorkflowDefinitionStatus { Draft = 0, Active = 1, Archived = 2 }`
- [x] T008 Extend `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/WorkflowDefinition.cs` — add `Status` property (default `Draft`), `Publish()` method (Draft → Active; throws if Data null or Status ≠ Draft), `Archive()` method (Active → Archived; throws if Status ≠ Active)
- [x] T009 [P] Create `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/WorkflowDefinitionSummary.cs` — `record WorkflowDefinitionSummary { string Id; string Name; string? RawFormat; WorkflowDefinitionStatus Status; DateTimeOffset? ModifiedAt; }`
- [x] T010 Extend `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/IDefinitionRepository.cs` — add `Task<IEnumerable<WorkflowDefinitionSummary>> ListAsync(WorkflowDefinitionStatus? status, CancellationToken token)` and `Task<bool> ExistsByNameAsync(string name, string? excludeId, CancellationToken token)`
- [x] T011 [P] Implement `ListAsync` and `ExistsByNameAsync` in `src/Juice.Workflows/InMemory/InMemorDefinitionRepository.cs` — `ListAsync` returns summaries ordered by `ModifiedAt` desc; `ExistsByNameAsync` does case-insensitive name match excluding `excludeId`
- [x] T012 [P] Implement `ListAsync` and `ExistsByNameAsync` in `src/Juice.Workflows.EF/Repositories/DefinitionRepository.cs` — use EF `Select` projection for summaries (avoid loading `RawData`/`Data`); `ExistsByNameAsync` uses `EF.Functions.Like` or `ToLower()` comparison
- [x] T013 Update `src/Juice.Workflows.EF/WorkflowDbContext.cs` — add `entity.Property(e => e.Status).HasConversion<int>().HasDefaultValue(WorkflowDefinitionStatus.Draft)` in `WorkflowDefinition` entity configuration
- [x] T014 Add EF migration `AddWorkflowDefinitionStatus` in `src/Juice.Workflows.EF.SqlServer/` — `dotnet ef migrations add AddWorkflowDefinitionStatus`; verify `Up()` adds `Status int NOT NULL DEFAULT 0` column
- [x] T015 [P] Add EF migration `AddWorkflowDefinitionStatus` in `src/Juice.Workflows.EF.PostgreSQL/` — same migration, verify PostgreSQL-compatible SQL
- [x] T016 Create all 7 MediatR command/query records in `src/Juice.Workflows.Designer/Commands/` — one file per record: `CreateWorkflowDefinitionCommand.cs`, `UpdateWorkflowDefinitionCommand.cs`, `RenameWorkflowDefinitionCommand.cs`, `PublishWorkflowDefinitionCommand.cs`, `ArchiveWorkflowDefinitionCommand.cs`, `DeleteWorkflowDefinitionCommand.cs`, `ListWorkflowDefinitionsQuery.cs` (signatures from data-model.md)
- [x] T017 Create `src/Juice.Workflows.Designer/Controllers/WorkflowDefinitionsController.cs` — `[ApiController][Route("api/workflow-definitions")]` skeleton with all 8 action method stubs (GET list, GET by id, POST, PUT, PATCH name, POST publish, POST archive, DELETE); inject `IMediator`; add standard error envelope helper
- [x] T018 Create `src/Juice.Workflows.Designer/Program.cs` — configure ASP.NET Core services (`AddMediatR`, `AddControllers`, `AddWorkflowServices`, `StoreWorkflowToEFRepo`), enable static files, add SPA fallback (`MapFallbackToFile("index.html")`), map controllers
- [x] T019 [P] Create `src/Juice.Workflows.Designer/DependencyInjection/DesignerServiceCollectionExtensions.cs` — `AddWorkflowDesigner()` extension method wiring MediatR assembly scan for the Designer project
- [x] T020 [P] Create `src/Juice.Workflows.Designer.Frontend/src/api/definitionsApi.ts` — typed `fetch` wrappers for all 8 REST endpoints: `listDefinitions(status?)`, `getDefinition(id)`, `createDefinition(payload)`, `updateDefinition(id, payload)`, `renameDefinition(id, name)`, `publishDefinition(id)`, `archiveDefinition(id)`, `deleteDefinition(id)`; each returns typed response or throws structured error
- [x] T021 [P] Create `src/Juice.Workflows.Designer.Frontend/src/App.tsx` — React Router v6 routes: `/` → `<DefinitionList>`, `/bpmn/new` → `<BpmnEditor>`, `/bpmn/:id` → `<BpmnEditor>`, `/yaml/new` → `<YamlEditor>`, `/yaml/:id` → `<YamlEditor>`

**Checkpoint**: Foundation ready — run `dotnet build Workflow.sln` and `npm run build` (both must pass before user story work begins)

---

## Phase 3: User Story 4 — Browse and Select Definitions (Priority: P1) 🎯

**Goal**: Unified definition list with format/status badges, filter tabs, client-side search, inline rename, delete, Archive action, and navigation routing to the correct editor.

**Independent Test**: Create one BPMN and one YAML definition via API directly; load `http://localhost:5173/`; verify both rows appear with correct badges; click each row to verify routing to the correct editor page.

### xUnit Tests for US4

- [x] T022 [P] [US4] Write xUnit tests for `ListWorkflowDefinitionsQuery` handler in `test/Juice.Workflows.Designer.Tests/WorkflowDefinitionQueryTests.cs` — test: returns all when no filter; filters by status; returns summaries without RawData/Data; returns ordered by ModifiedAt desc
- [x] T023 [P] [US4] Write xUnit tests for `DeleteWorkflowDefinitionCommand`, `RenameWorkflowDefinitionCommand`, `ArchiveWorkflowDefinitionCommand` handlers in `test/Juice.Workflows.Designer.Tests/WorkflowDefinitionLifecycleTests.cs` — test delete by id; rename name-uniqueness conflict; rename with excludeId; archive Active→Archived; archive Draft→throws; archive Archived→throws

### Implementation for US4

- [x] T024 [US4] Implement `ListWorkflowDefinitionsQuery` handler in `src/Juice.Workflows.Designer/Commands/ListWorkflowDefinitionsQueryHandler.cs` — call `IDefinitionRepository.ListAsync(query.Status)`, return ordered summaries
- [x] T025 [US4] Implement `DeleteWorkflowDefinitionCommand` handler in `src/Juice.Workflows.Designer/Commands/DeleteWorkflowDefinitionCommandHandler.cs` — load definition, call `repository.DeleteAsync(id)`, return 404 if not found
- [x] T026 [US4] Implement `RenameWorkflowDefinitionCommand` handler in `src/Juice.Workflows.Designer/Commands/RenameWorkflowDefinitionCommandHandler.cs` — `ExistsByNameAsync(newName, id)` check → 409 conflict; update name; `repository.UpdateAsync`
- [x] T027 [P] [US4] Implement `ArchiveWorkflowDefinitionCommand` handler in `src/Juice.Workflows.Designer/Commands/ArchiveWorkflowDefinitionCommandHandler.cs` — load definition; call `definition.Archive()` (throws on invalid transition); `repository.UpdateAsync`
- [x] T028 [US4] Wire GET `/api/workflow-definitions` (list + optional `?status=`), DELETE `/api/workflow-definitions/{id}`, PATCH `/api/workflow-definitions/{id}/name`, POST `/api/workflow-definitions/{id}/archive` actions in `src/Juice.Workflows.Designer/Controllers/WorkflowDefinitionsController.cs`
- [x] T029 [US4] Implement `src/Juice.Workflows.Designer.Frontend/src/pages/DefinitionList.tsx` — load definitions via `listDefinitions()` on mount; render table with Name, Format badge (BPMN/YAML), Status badge (Draft/Active/Archived), ModifiedAt; filter tabs (All/Active/Draft/Archived); client-side search input filtering by name; row click → navigate to `/bpmn/:id` or `/yaml/:id` based on RawFormat; "New BPMN Workflow" → `/bpmn/new`; "New YAML Workflow" → `/yaml/new`; inline rename (PATCH call); delete with confirm dialog; Archive button (POST .../archive) for Active definitions; read-only indicator for definitions without RawData

**Checkpoint**: US4 independently testable — browse, filter, search, rename, delete, archive all functional

---

## Phase 4: User Story 1 — Create New Workflow Visually (Priority: P1) 🎯

**Goal**: BPMN visual canvas where users drag/drop elements, connect them, save as Draft, and Publish to Active. XML Source tab shows live BPMN XML.

**Independent Test**: Open `/bpmn/new`, drag Start→Task→End, connect flows, click Save, verify definition appears in list as Draft; click Publish, verify status changes to Active.

### xUnit Tests for US1

- [x] T030 [P] [US1] Write xUnit tests for `CreateWorkflowDefinitionCommand` handler in `test/Juice.Workflows.Designer.Tests/WorkflowDefinitionCreateTests.cs` — test: creates Draft; name uniqueness conflict returns 409; BPMN parse failure returns 422; successful create sets Data via BPMN parser; auto-generated ID is non-empty
- [x] T031 [P] [US1] Write xUnit tests for `PublishWorkflowDefinitionCommand` handler in `test/Juice.Workflows.Designer.Tests/WorkflowDefinitionCreateTests.cs` — test: Draft with Data → Active; Draft with null Data → 422; already Active → 409; not found → 404

### Implementation for US1

- [x] T032 [US1] Implement `CreateWorkflowDefinitionCommand` handler in `src/Juice.Workflows.Designer/Commands/CreateWorkflowDefinitionCommandHandler.cs` — `ExistsByNameAsync` check; parse RawData with BPMN or YAML parser (inject by RawFormat); create entity with `Status=Draft`; call `SetData`; call `repository.CreateAsync`; return generated Id; map parse exceptions to `IOperationResult` failure
- [x] T033 [US1] Implement `PublishWorkflowDefinitionCommand` handler in `src/Juice.Workflows.Designer/Commands/PublishWorkflowDefinitionCommandHandler.cs` — load definition; call `definition.Publish()`; call `repository.UpdateAsync`; map domain exceptions to operation result
- [x] T034 [US1] Wire POST `/api/workflow-definitions` and POST `/api/workflow-definitions/{id}/publish` + GET `/api/workflow-definitions/{id}` actions in `src/Juice.Workflows.Designer/Controllers/WorkflowDefinitionsController.cs`
- [x] T035 [US1] Create `src/Juice.Workflows.Designer.Frontend/src/components/BpmnCanvas.tsx` — bpmn-js `BpmnModeler` instantiated in `useEffect` with `useRef` for container and modeler; `importXML(xml)` on load; `saveXML({ format: true })` for save; `commandStack.changed` event for dirty tracking; `import.done` to reset dirty flag; `destroy()` in cleanup; expose `saveXML`, `importXML`, `isDirty`, `onDirtyChange` via ref/props
- [x] T036 [US1] Create `src/Juice.Workflows.Designer.Frontend/src/components/XmlSourceTab.tsx` — Monaco Editor with `language: 'xml'`, `readOnly: true`; receives `xml: string` prop; re-renders when xml prop changes (driven by parent calling `saveXML` on tab switch)
- [x] T037 [US1] Create `src/Juice.Workflows.Designer.Frontend/src/pages/BpmnEditor.tsx` — creates new definition (`/bpmn/new`) or loads existing (via `getDefinition(id)` → `importXML`); tab switcher: "Visual Editor" / "XML Source" (triggers `saveXML()` for XML tab); Save button: calls `createDefinition` or `updateDefinition`; Publish button: calls `publishDefinition`; unsaved changes guard: `beforeunload` event listener + React Router navigation block while `isDirty === true`

**Checkpoint**: US1 independently testable — create, save Draft, Publish to Active

---

## Phase 5: User Story 2 — Edit Existing BPMN Definition (Priority: P2)

**Goal**: Open an existing BPMN definition from the list, modify it on the canvas, and save the updated definition.

**Independent Test**: Create a definition via US1 path, navigate to it from the list, add a task, save, verify definition in list reflects updated `ModifiedAt`.

### xUnit Tests for US2

- [x] T038 [P] [US2] Write xUnit tests for `UpdateWorkflowDefinitionCommand` handler in `test/Juice.Workflows.Designer.Tests/WorkflowDefinitionUpdateTests.cs` — test: updates RawData and Data; not found → 404; BPMN parse failure → 422; status unchanged after update; Data replaced (not merged)

### Implementation for US2

- [x] T039 [US2] Implement `UpdateWorkflowDefinitionCommand` handler in `src/Juice.Workflows.Designer/Commands/UpdateWorkflowDefinitionCommandHandler.cs` — load definition; parse RawData; call `definition.UpdateRawData` + `definition.SetData`; call `repository.UpdateAsync`
- [x] T040 [US2] Wire PUT `/api/workflow-definitions/{id}` action in `src/Juice.Workflows.Designer/Controllers/WorkflowDefinitionsController.cs`
- [x] T041 [US2] Update `src/Juice.Workflows.Designer.Frontend/src/pages/BpmnEditor.tsx` edit mode — on mount with `:id` param, call `getDefinition(id)` and `importXML(definition.rawData)`; Save calls `updateDefinition(id, ...)` instead of `createDefinition`; show definition name in header; navigate to list after successful save

**Checkpoint**: US2 independently testable — edit and update existing BPMN definitions

---

## Phase 6: User Story 5 — Edit YAML Workflow Definition (Priority: P2)

**Goal**: Monaco Editor UI for YAML definitions — create new from skeleton or load and edit existing, with syntax highlighting, inline validation markers, and atomic save.

**Independent Test**: Open `/yaml/new`, paste valid YAML, save → definition appears in list as Draft. Open `/yaml/:id` for an existing YAML definition, change a task name, save, verify update.

### Implementation for US5

- [x] T042 [US5] Create `src/Juice.Workflows.Designer.Frontend/src/yaml.worker.js` — single line: `import 'monaco-yaml/yaml.worker'` (re-export for Vite worker bundling)
- [x] T043 [US5] Update `src/Juice.Workflows.Designer.Frontend/src/main.tsx` — add `window.MonacoEnvironment = { getWorker(_, label) { if (label === 'yaml') return new Worker(new URL('./yaml.worker.js', import.meta.url)); return new Worker(new URL('monaco-editor/esm/vs/editor/editor.worker', import.meta.url)); } }` before any Monaco import; call `configureMonacoYaml(monaco, { validate: true })` in app startup
- [x] T044 [US5] Create `src/Juice.Workflows.Designer.Frontend/src/pages/YamlEditor.tsx` — Monaco Editor with `language: 'yaml'`; on `/yaml/new` pre-populate with minimal YAML skeleton from `definitionsApi.ts` constant; on `/yaml/:id` load via `getDefinition(id)` → `editor.setValue`; `onDidChangeModelContent` for dirty tracking; Save: call `createDefinition` or `updateDefinition` with `rawFormat: 'YAML'`; on 422 response call `monaco.editor.setModelMarkers(model, 'server', markers)` and show summary error above editor; unsaved changes guard: `beforeunload` + navigation block; Publish button

**Checkpoint**: US5 independently testable — create and edit YAML definitions

---

## Phase 7: User Story 6 — Configure Element Properties (Priority: P2)

**Goal**: Properties panel showing configurable attributes for the selected canvas element (name, type, condition expressions on gateway outgoing flows).

**Independent Test**: Open the BPMN editor, click a Task element, verify the properties panel shows the element name field; change the name, verify the canvas label updates immediately; configure a condition expression on an Exclusive Gateway flow, save, verify the expression is stored in the BPMN XML.

### Implementation for US6

- [x] T045 [US6] Create `src/Juice.Workflows.Designer.Frontend/src/components/PropertiesPanel.tsx` — subscribe to `modeler.on('selection.changed', e => setElement(e.newSelection[0]))` and `modeler.on('element.changed', e => refresh if selected)`; render form fields for element name (calls `modeling.updateProperties(element, { name })` on change → canvas updates immediately); element type display; no-selection empty state
- [x] T046 [US6] Add gateway condition expression editor in `src/Juice.Workflows.Designer.Frontend/src/components/PropertiesPanel.tsx` — when selected element is a sequence flow with a gateway source, show condition expression text input; call `modeling.updateProperties(element, { conditionExpression: ... })` on change (FR-012)
- [x] T047 [US6] Wire `PropertiesPanel` into `src/Juice.Workflows.Designer.Frontend/src/pages/BpmnEditor.tsx` — pass `modeler` ref to `PropertiesPanel`; lay out as right sidebar alongside the canvas

**Checkpoint**: US6 independently testable — select any canvas element and configure its properties

---

## Phase 8: User Story 3 — Import and Export BPMN XML (Priority: P3)

**Goal**: Export current diagram as a BPMN 2.0 XML file; import an external BPMN file and render on canvas with unsupported elements highlighted.

**Independent Test**: Export a workflow → open in Camunda Modeler → verify renders correctly. Import an external BPMN file → verify diagram appears on canvas.

### Implementation for US3

- [x] T048 [US3] Add Export button to `src/Juice.Workflows.Designer.Frontend/src/pages/BpmnEditor.tsx` — on click call `modeler.saveXML({ format: true })`; trigger browser download of the XML string as `{name}.bpmn` via `URL.createObjectURL` + `<a download>` pattern (FR-007)
- [x] T049 [US3] Add Import button to `src/Juice.Workflows.Designer.Frontend/src/pages/BpmnEditor.tsx` — hidden `<input type="file" accept=".bpmn,.xml">`; on file select, read as text, call `modeler.importXML(xmlString)`; on success check `warnings` array for unsupported element type warnings; overlay a visual indicator (red border or badge) on each unsupported element in the registry; block Save button until all unsupported elements are resolved; on failure show clear error toast (FR-008)

**Checkpoint**: US3 independently testable — export and import BPMN 2.0 XML

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Validation, accessibility pass, build integration, and end-to-end smoke test.

- [x] T050 Add frontend structural validation guard before save in `src/Juice.Workflows.Designer.Frontend/src/pages/BpmnEditor.tsx` — before calling the API, run client-side check: verify at least one Start Event and one End Event exist in the diagram via `elementRegistry.filter()`; show inline error if validation fails (FR-004, SC-006)
- [x] T051 [P] Verify `dotnet build Workflow.sln` passes with all new projects in `Workflow.sln`; verify `dotnet test Workflow.sln` passes (all xUnit tests green) — fix any compile or test failures
- [x] T052 [P] Configure frontend production build in `src/Juice.Workflows.Designer.Frontend/package.json` — add `"build": "vite build"` script; verify output lands in `src/Juice.Workflows.Designer/wwwroot/` and contains `index.html` + hashed JS/CSS assets
- [x] T053 Follow `specs/001-bpmn-designer-ui/quickstart.md` end-to-end "Hello World" validation — create a Start→Task→End BPMN workflow, save as Draft, Publish to Active, verify it appears in the list with correct badges; also create a minimal YAML definition, verify it saves and appears in list

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user story phases**
- **US4 Browse/Select (Phase 3)**: Depends on Foundational complete
- **US1 Create Visually (Phase 4)**: Depends on Foundational complete; US4 optional (can proceed in parallel)
- **US2 Edit BPMN (Phase 5)**: Depends on US1 complete (shares BpmnEditor.tsx)
- **US5 YAML Editor (Phase 6)**: Depends on Foundational complete; independent of US1/US2
- **US6 Properties Panel (Phase 7)**: Depends on US1 complete (requires BpmnCanvas component)
- **US3 Import/Export (Phase 8)**: Depends on US1 complete (requires BpmnEditor page)
- **Polish (Phase 9)**: Depends on all desired stories complete

### User Story Dependencies

```
Phase 1 (Setup)
    └── Phase 2 (Foundational)
            ├── Phase 3 (US4 — List)       [independent]
            ├── Phase 4 (US1 — Create BPMN) [independent, but US4 is the entry point]
            │       ├── Phase 5 (US2 — Edit BPMN)    [depends on US1: BpmnEditor]
            │       └── Phase 7 (US6 — Properties)   [depends on US1: BpmnCanvas]
            │       └── Phase 8 (US3 — Import/Export) [depends on US1: BpmnEditor]
            └── Phase 6 (US5 — YAML Editor) [independent of US1/US2]
```

### Within Each User Story

- xUnit tests → implementation (Constitution: test-first)
- Domain/command handlers → controller wiring → frontend
- Core component (Canvas/Editor) → page composition → integration

### Parallel Opportunities

All `[P]`-marked tasks within the same phase can run simultaneously. Cross-phase parallelism:
- Phase 3 (US4) and Phase 4 (US1) and Phase 6 (US5) can all start after Phase 2 completes
- Within Phase 2: T009, T011, T012, T015, T019, T020, T021 can all run in parallel

---

## Parallel Example: Phase 4 (US1)

```
# All three can start together after Phase 3 backend is done:
Task T030: Write CreateWorkflowDefinitionCommand tests (backend)
Task T031: Write PublishWorkflowDefinitionCommand tests (backend)
Task T035: Create BpmnCanvas.tsx component (frontend)

# After T030/T031 pass (red), implement:
Task T032: Implement CreateWorkflowDefinitionCommand handler
Task T033: Implement PublishWorkflowDefinitionCommand handler (parallel with T032)
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 4 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US4 (Browse/Select list — the entry point)
4. Complete Phase 4: US1 (Create BPMN visually — the core value)
5. **STOP and VALIDATE**: Can create a workflow, publish it, and see it in the list
6. Deploy/demo if ready

### Incremental Delivery

1. Phases 1–2: Foundation → `dotnet build` + `npm run build` pass
2. Phase 3: US4 → List functional (browse, filter, search, delete, rename)
3. Phase 4: US1 → BPMN create + publish (MVP deliverable)
4. Phase 5: US2 → BPMN edit (update existing definitions)
5. Phase 6: US5 → YAML editor (second editor type)
6. Phase 7: US6 → Properties panel (element configuration)
7. Phase 8: US3 → Import/export (interoperability)
8. Phase 9: Polish → validation, build integration, smoke test

### Parallel Team Strategy

With two developers after Phase 2 completes:
- **Dev A**: Phase 3 (US4 list) → Phase 4 (US1 BPMN) → Phase 5 (US2 edit)
- **Dev B**: Phase 6 (US5 YAML editor) → Phase 7 (US6 properties) → Phase 8 (US3 import/export)

---

## Notes

- `[P]` tasks = different files, no in-phase dependencies
- `[Story]` label maps each task to a specific user story for traceability
- Each user story phase is independently completable and testable
- Test tasks use `AddInMemoryReposistories()` — no database required
- xUnit tests must be written **before** implementing handlers (Constitution Principle III)
- Commit after each task or logical group; run `dotnet test Workflow.sln` after each Phase 2–5 backend task
- Stop at any phase checkpoint to validate that story independently before proceeding
- US4 (Phase 3) is the practical prerequisite for testing US1 in the browser, even though they can be implemented independently
