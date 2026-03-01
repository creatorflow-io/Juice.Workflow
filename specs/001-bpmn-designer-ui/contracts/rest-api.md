# REST API Contract: Workflow Designer UI

**Base path**: `/api/workflow-definitions`
**Content-Type**: `application/json`
**Auth**: inherited from host (not specified in this feature; host handles auth middleware)

---

## GET /api/workflow-definitions

**Description**: List all workflow definitions (lightweight summaries). Supports optional status filter.

**Query parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `status` | `string` | No | Filter by status: `Draft`, `Active`, or `Archived`. Omit to return all. |

**Response 200**:
```json
[
  {
    "id": "abc123",
    "name": "Order Processing",
    "rawFormat": "BPMN",
    "status": "Active",
    "modifiedAt": "2026-02-28T10:30:00Z"
  },
  {
    "id": "def456",
    "name": "Invoice Review",
    "rawFormat": "YAML",
    "status": "Draft",
    "modifiedAt": "2026-02-28T09:15:00Z"
  }
]
```

**Status values** (string enum serialised as string):
- `"Draft"` — saved but not published
- `"Active"` — published and available for execution
- `"Archived"` — retired

---

## GET /api/workflow-definitions/{id}

**Description**: Get full detail of a single workflow definition, including `rawData`.

**Path parameters**:
| Name | Type | Description |
|------|------|-------------|
| `id` | `string` | Definition ID |

**Response 200**:
```json
{
  "id": "abc123",
  "name": "Order Processing",
  "rawData": "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<bpmn:definitions ...>...</bpmn:definitions>",
  "rawFormat": "BPMN",
  "status": "Active",
  "modifiedAt": "2026-02-28T10:30:00Z"
}
```

**Response 404**:
```json
{ "error": "Workflow definition 'abc123' not found." }
```

---

## POST /api/workflow-definitions

**Description**: Create a new workflow definition. Saves as `Draft` and converts RawData atomically.

**Request body**:
```json
{
  "name": "Order Processing",
  "rawData": "<?xml version=\"1.0\" ...>...</bpmn:definitions>",
  "rawFormat": "BPMN"
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `name` | `string` | Yes | Non-empty; must be unique (case-insensitive) |
| `rawData` | `string` | Yes | BPMN 2.0 XML or YAML text |
| `rawFormat` | `string` | Yes | `"BPMN"` or `"YAML"` |

**Response 201**:
```json
{ "id": "abc123" }
```
*Location header*: `/api/workflow-definitions/abc123`

**Response 409** (duplicate name):
```json
{ "error": "A workflow named 'Order Processing' already exists." }
```

**Response 422** (parse/conversion failure):
```json
{
  "error": "Failed to convert BPMN definition to execution data.",
  "details": "Unsupported element type: bpmn:ComplexGateway"
}
```

---

## PUT /api/workflow-definitions/{id}

**Description**: Update the raw definition source (RawData). Triggers atomic re-parse. Status unchanged.

**Path parameters**: `id` — definition ID.

**Request body**:
```json
{
  "rawData": "<?xml version=\"1.0\" ...>...",
  "rawFormat": "BPMN"
}
```

**Response 200**: `{}` (empty body on success)

**Response 404**: Definition not found.

**Response 422**: Parse/conversion failure (same format as POST 422).

---

## PATCH /api/workflow-definitions/{id}/name

**Description**: Rename a workflow definition. Only changes the display name; ID is immutable.

**Path parameters**: `id` — definition ID.

**Request body**:
```json
{ "name": "New Display Name" }
```

**Response 200**: `{}` (empty body on success)

**Response 404**: Definition not found.

**Response 409** (name conflict):
```json
{ "error": "A workflow named 'New Display Name' already exists." }
```

---

## POST /api/workflow-definitions/{id}/publish

**Description**: Promote a `Draft` definition to `Active`. Requires that the definition has been saved (Data is set).

**Path parameters**: `id` — definition ID.

**Request body**: None.

**Response 200**: `{}` (empty body on success)

**Response 404**: Definition not found.

**Response 409** (invalid state transition):
```json
{ "error": "Only Draft definitions can be published. Current status: Active." }
```

**Response 422** (no execution data):
```json
{ "error": "Definition has no execution data. Save the definition before publishing." }
```

---

## POST /api/workflow-definitions/{id}/archive

**Description**: Retire an `Active` definition (move to `Archived`).

**Path parameters**: `id` — definition ID.

**Request body**: None.

**Response 200**: `{}` (empty body on success)

**Response 404**: Definition not found.

**Response 409** (invalid state):
```json
{ "error": "Only Active definitions can be archived. Current status: Draft." }
```

---

## DELETE /api/workflow-definitions/{id}

**Description**: Delete a workflow definition regardless of status. Existing workflow instances referencing this definition are unaffected (they hold a snapshot of execution data).

**Path parameters**: `id` — definition ID.

**Request body**: None.

**Response 204**: No content — deletion successful.

**Response 404**: Definition not found.

---

## Common Error Envelope

All error responses use this shape:

```json
{
  "error": "Human-readable error message.",
  "details": "Optional technical detail (e.g., parse exception message)."
}
```

HTTP status mapping:
| HTTP | Meaning |
|------|---------|
| 200 | Success (with or without body) |
| 201 | Created (POST with new resource) |
| 204 | No content (DELETE) |
| 404 | Not found |
| 409 | Conflict (duplicate name, invalid state transition) |
| 422 | Unprocessable (parse/conversion failure, validation failure) |
| 500 | Unexpected server error |
