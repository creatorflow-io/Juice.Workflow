# Data Model: Workflow Designer UI (001-bpmn-designer-ui)

**Generated**: 2026-02-28 | **Plan phase**: Phase 1

---

## Entities

### WorkflowDefinition *(existing — extended)*

**Location**: `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/WorkflowDefinition.cs`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `string` | MaxLength 128, auto-generated, immutable | `StringIdGenerator.Instance.GenerateUniqueId()` |
| `Name` | `string` | Required, MaxLength = Constants.NameLength, unique per tenant | Display name; editable via `RenameWorkflowDefinitionCommand` |
| `RawData` | `string?` | Nullable | BPMN XML or YAML source text as entered/saved |
| `RawFormat` | `string?` | MaxLength = Constants.NameLength | `"BPMN"` or `"YAML"` |
| `Data` | `string?` | Nullable | JSON-serialised `(Processes, Nodes, Flows)` — execution-ready snapshot |
| `Status` | `WorkflowDefinitionStatus` | **NEW**, default `Draft` | State machine: Draft → Active → Archived |
| `CreatedAt` | `DateTimeOffset` | Set on create (from `AuditAggregarteRoot`) | |
| `ModifiedAt` | `DateTimeOffset?` | Updated on each save (from `AuditAggregarteRoot`) | |

**New behaviour on `WorkflowDefinition`**:
- `Publish()` — transitions `Draft → Active`; throws if `Data` is null (must save first) or status is not `Draft`.
- `Archive()` — transitions `Active → Archived`; throws if status is not `Active`.
- Status cannot be reversed once `Active` or `Archived`.

---

### WorkflowDefinitionStatus *(new enum)*

**Location**: `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/WorkflowDefinitionStatus.cs`

```csharp
public enum WorkflowDefinitionStatus
{
    Draft    = 0,  // Saved but not yet live; default on creation
    Active   = 1,  // Published; available for new workflow executions
    Archived = 2   // Retired; no new executions allowed
}
```

---

### WorkflowDefinitionSummary *(read-model / DTO)*

Used by `ListWorkflowDefinitionsQuery` to return a lightweight list without loading `RawData`/`Data`.

| Field | Type | Source |
|-------|------|--------|
| `Id` | `string` | `WorkflowDefinition.Id` |
| `Name` | `string` | `WorkflowDefinition.Name` |
| `RawFormat` | `string?` | `WorkflowDefinition.RawFormat` |
| `Status` | `WorkflowDefinitionStatus` | `WorkflowDefinition.Status` |
| `ModifiedAt` | `DateTimeOffset?` | `WorkflowDefinition.ModifiedAt` |

---

## Repository Interface Changes

**Location**: `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/IDefinitionRepository.cs`

New methods added to `IDefinitionRepository`:

```csharp
/// <summary>Returns a paged, lightweight summary list. Filter by status if provided.</summary>
Task<IEnumerable<WorkflowDefinitionSummary>> ListAsync(
    WorkflowDefinitionStatus? status,
    CancellationToken token);

/// <summary>Returns true if a definition with the given name (case-insensitive) exists,
/// optionally excluding a specific ID (for rename validation).</summary>
Task<bool> ExistsByNameAsync(string name, string? excludeId, CancellationToken token);
```

Both methods must be implemented in:
- `InMemorDefinitionRepository` (`src/Juice.Workflows/InMemory/`)
- `DefinitionRepository<TContext>` (`src/Juice.Workflows.EF/Repositories/`)

---

## EF Schema Change

**Table**: `WorkflowDefinition`
**Change**: Add `Status int NOT NULL DEFAULT 0` column
**Migration projects**:
- `src/Juice.Workflows.EF.SqlServer/` — add migration `AddWorkflowDefinitionStatus`
- `src/Juice.Workflows.EF.PostgreSQL/` — add migration `AddWorkflowDefinitionStatus`

**EF Configuration** (in `WorkflowDbContext.cs`):
```csharp
entity.Property(e => e.Status)
    .HasConversion<int>()
    .HasDefaultValue(WorkflowDefinitionStatus.Draft);
```

---

## MediatR Commands & Queries

**Location (handlers)**: `src/Juice.Workflows.Designer/`

### CreateWorkflowDefinitionCommand

```csharp
public record CreateWorkflowDefinitionCommand(
    string Name,
    string RawData,
    string RawFormat   // "BPMN" | "YAML"
) : IRequest<IOperationResult<string>>;  // returns generated Id
```

**Handler logic**:
1. Check name uniqueness via `ExistsByNameAsync(Name, null)` → 409 if duplicate.
2. Parse `RawData` with the format-appropriate parser (BPMN or YAML) → produce `(Processes, Nodes, Flows)`.
3. Create new `WorkflowDefinition` with `Status = Draft`.
4. Call `definition.SetData(processes, nodes, flows)`.
5. Call `repository.CreateAsync(definition)`.
6. Return generated `Id` on success; return failure with error message if parse fails.

---

### UpdateWorkflowDefinitionCommand

```csharp
public record UpdateWorkflowDefinitionCommand(
    string Id,
    string RawData,
    string RawFormat
) : IRequest<IOperationResult>;
```

**Handler logic**:
1. Load definition; return 404 if not found.
2. Parse `RawData` → `(Processes, Nodes, Flows)`; return 422 on parse failure.
3. Call `definition.UpdateRawData(RawData, RawFormat)` then `definition.SetData(...)`.
4. Call `repository.UpdateAsync(definition)`.
5. Status remains unchanged (still `Draft` if it was `Draft`; re-saves to Active if already Active).

---

### RenameWorkflowDefinitionCommand

```csharp
public record RenameWorkflowDefinitionCommand(
    string Id,
    string NewName
) : IRequest<IOperationResult>;
```

**Handler logic**:
1. Check `ExistsByNameAsync(NewName, Id)` → 409 if another definition uses the name.
2. Load definition; update `Name` via domain method.
3. Call `repository.UpdateAsync(definition)`.

---

### PublishWorkflowDefinitionCommand

```csharp
public record PublishWorkflowDefinitionCommand(
    string Id
) : IRequest<IOperationResult>;
```

**Handler logic**:
1. Load definition; return 404 if not found.
2. Call `definition.Publish()` — throws domain exception if `Status != Draft` or `Data == null`.
3. Call `repository.UpdateAsync(definition)`.

---

### ArchiveWorkflowDefinitionCommand

```csharp
public record ArchiveWorkflowDefinitionCommand(
    string Id
) : IRequest<IOperationResult>;
```

**Handler logic**:
1. Load definition; return 404 if not found.
2. Call `definition.Archive()` — throws domain exception if `Status != Active`.
3. Call `repository.UpdateAsync(definition)`.

---

### DeleteWorkflowDefinitionCommand

```csharp
public record DeleteWorkflowDefinitionCommand(
    string Id
) : IRequest<IOperationResult>;
```

**Handler logic**:
1. Load definition; return 404 if not found.
2. Call `repository.DeleteAsync(Id)` (internally calls `definition.ClearData()` before delete).

---

### ListWorkflowDefinitionsQuery

```csharp
public record ListWorkflowDefinitionsQuery(
    WorkflowDefinitionStatus? Status = null
) : IRequest<IEnumerable<WorkflowDefinitionSummary>>;
```

**Handler logic**:
1. Call `repository.ListAsync(Status)`.
2. Return result ordered by `ModifiedAt DESC`.

---

## State Machine Diagram

```
         ┌──────────┐
  create  │          │  Publish()
 ────────►│  Draft   ├──────────────► Active
         │          │                   │
         └──────────┘                   │ Archive()
                                        ▼
                                    Archived
```

- `Draft`: can be saved (updated), renamed, published, deleted.
- `Active`: can be archived, deleted (with warning). Save updates RawData but keeps status Active.
- `Archived`: read-only; can only be deleted.
- There is no path back to Draft from Active or Archived.
