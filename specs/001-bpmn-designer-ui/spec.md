# Feature Specification: Workflow Designer Backend API

**Feature Branch**: `001-bpmn-designer-ui`
**Created**: 2026-02-28
**Revised**: 2026-03-01 — scope reduced to backend-only (frontend removed)
**Status**: Implemented

## Scope Summary

This feature delivers a **backend REST API** for managing workflow definitions, including creation, retrieval, update, rename, publish, archive, and deletion. The frontend (bpmn-js visual canvas, Monaco text editor, SPA) is **out of scope** and has been removed. A future feature will add a frontend UI.

## Clarifications

### Session 2026-02-28

- When should the BPMN XML → engine Data conversion be triggered after a save? → Automatic on save — convert RawData → Data atomically in the same save operation; reject save if conversion fails.
- Which representation is stored for editing? → RawData (BPMN XML or YAML text) — the original source is preserved; execution-ready data is derived on every save.
- Two supported definition formats: **BPMN XML** and **YAML**; each is saved as `RawData` with a `RawFormat` tag.
- How is a new definition's ID assigned? → User provides a display Name; the system auto-generates a unique string ID via `StringIdGenerator`.
- Status lifecycle: three states — **Draft** (saved but not live), **Active** (published, available for execution), **Archived** (retired). Save always produces/updates a Draft. An explicit Publish action promotes Draft → Active. Archive moves Active → Archived.

## Requirements

### Functional Requirements

- **FR-001**: The API MUST accept a BPMN XML or YAML string when creating or updating a workflow definition, atomically store `RawData`/`RawFormat`, and derive execution-ready `Data` in the same operation. If derivation fails, the entire operation is rejected and nothing is written.
- **FR-002**: A successfully created definition starts in **Draft** status. Draft definitions cannot be used to start new workflow instances.
- **FR-003**: An explicit **Publish** action promotes a Draft definition to **Active** status. Publishing requires valid execution-ready data; if the definition has none, publishing is rejected.
- **FR-004**: An explicit **Archive** action moves an Active definition to **Archived** status. Archived definitions cannot be re-published; running instances are unaffected.
- **FR-005**: The API MUST allow renaming a definition's display name. The name MUST be unique across all definitions; a duplicate name is rejected. The definition ID is immutable.
- **FR-006**: The API MUST allow deleting a definition permanently. Running workflow instances referencing the deleted definition continue unaffected.
- **FR-007**: The API MUST provide a list endpoint that returns all definitions (with `id`, `name`, `rawFormat`, `status`, `modifiedDate`) and supports optional filtering by status.
- **FR-008**: When creating a definition with a name already used by another definition, the operation MUST be rejected with an "already exists" error.

### API Endpoints

| Method   | Path                                        | Description                        |
|----------|---------------------------------------------|------------------------------------|
| `GET`    | `/api/workflow-definitions`                 | List all definitions (optional `?status=Draft|Active|Archived`) |
| `GET`    | `/api/workflow-definitions/{id}`            | Get single definition with RawData |
| `POST`   | `/api/workflow-definitions`                 | Create new definition              |
| `PUT`    | `/api/workflow-definitions/{id}`            | Update RawData (re-derive Data)    |
| `PATCH`  | `/api/workflow-definitions/{id}/name`       | Rename display name                |
| `POST`   | `/api/workflow-definitions/{id}/publish`    | Promote Draft → Active             |
| `POST`   | `/api/workflow-definitions/{id}/archive`    | Move Active → Archived             |
| `DELETE` | `/api/workflow-definitions/{id}`            | Delete definition                  |

### MediatR Commands / Queries

Implemented in `Juice.Workflows.Designer`:

- `CreateWorkflowDefinitionCommand(name, rawData, rawFormat)` → `IOperationResult<string>` (returns new ID)
- `UpdateWorkflowDefinitionCommand(id, rawData, rawFormat)` → `IOperationResult`
- `PublishWorkflowDefinitionCommand(id)` → `IOperationResult`
- `ArchiveWorkflowDefinitionCommand(id)` → `IOperationResult`
- `DeleteWorkflowDefinitionCommand(id)` → `IOperationResult`
- `RenameWorkflowDefinitionCommand(id, name)` → `IOperationResult`
- `ListWorkflowDefinitionsQuery(status?)` → `IOperationResult<IEnumerable<WorkflowDefinitionSummary>>`

### Key Entities

- **WorkflowDefinition**: `Id` (system-generated, immutable), `Name` (user-provided, unique, renameable), `RawData` (BPMN XML or YAML source), `RawFormat` ("BPMN" | "YAML"), `Status` (`Draft` | `Active` | `Archived`), `ModifiedDate`, execution-ready `Data` (nodes, flows, processes).
- **WorkflowDefinitionStatus**: `Draft`, `Active`, `Archived`.
- **WorkflowDefinitionSummary**: Read-only projection for list view — `Id`, `Name`, `RawFormat`, `Status`, `ModifiedDate`.

## Integration

- **Library project**: `src/Juice.Workflows.Designer` — class library (`Microsoft.NET.Sdk`) that consumers embed via `AddWorkflowDesigner()`.
- **DI extension**: `services.AddWorkflowDesigner()` registers MediatR handlers and BPMN/YAML builders.
- **Controller registration**: consumers call `services.AddControllers()` and the controllers in this assembly are discovered automatically.
- **Consumed by**: `test/Juice.Workflows.Tests.Host` embeds the designer backend; tests in `test/Juice.Workflows.Designer.Tests` test commands in isolation.

## Assumptions

- No frontend SPA is included in this feature. A future feature will add a browser-based UI.
- Authentication and access control are handled by the consuming host; no new auth mechanisms are added.
- In-memory repositories are used in tests; EF repositories are used in the Tests.Host.
- BPMN and YAML are the only supported `RawFormat` values; other formats are rejected.

## Scope Boundaries

**In scope**:
- REST API controller (`WorkflowDefinitionsController`)
- MediatR command/query handlers for all CRUD + lifecycle operations
- `WorkflowDefinition.Status` lifecycle (Draft / Active / Archived)
- Atomic save: RawData stored and execution-ready Data derived in one operation
- `WorkflowDefinitionSummary` read model for list
- EF migrations for `Status` column
- Unit tests for all commands in `Juice.Workflows.Designer.Tests`

**Out of scope**:
- Frontend SPA (bpmn-js canvas, Monaco editor, React/Vite — deferred to a future feature)
- Real-time collaborative editing
- Workflow version history / rollback
- Simulation or execution preview
- Role-based access control within the designer
