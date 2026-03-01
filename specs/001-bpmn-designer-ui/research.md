# Research: Workflow Designer UI (001-bpmn-designer-ui)

**Generated**: 2026-02-28 | **Plan phase**: Phase 0

---

## Decision 1: BPMN Visual Editor Library

**Decision**: Use `bpmn-js` v18.x (current stable: 18.12.0) directly — no wrapper library.

**Rationale**: `bpmn-js` is the canonical BPMN 2.0 web modeler. The React wrapper `react-bpmn` is outdated and unmaintained. Direct instantiation via `useRef`/`useEffect` is the pattern recommended by bpmn.io and demonstrated in the official `bpmn-js-example-react-properties-panel` reference repo.

**Alternatives considered**:
- `react-bpmn` — abandoned; last release targets bpmn-js v5; rejected.
- `@bpmn-io/bpmn-js` CDN script tag — not tree-shakeable; rejected for SPA build.

**Key API facts**:
- Import: `import BpmnModeler from 'bpmn-js/lib/Modeler'`
- Load XML: `const { warnings } = await modeler.importXML(xmlString)` — Promise-based since v7. Warnings are non-fatal; rejection = fatal error.
- Save XML: `const { xml } = await modeler.saveXML({ format: true })` — no `exportXML` method exists.
- SVG export: `const { svg } = await modeler.saveSVG()`
- Destroy: `modeler.destroy()` — must call in `useEffect` cleanup to prevent memory leaks (critical in React 18 Strict Mode).
- Required CSS (Vite imports): `bpmn-js/dist/assets/diagram-js.css`, `bpmn-js/dist/assets/bpmn-font/css/bpmn.css`, `bpmn-js/dist/assets/bpmn-js.css` (modeler only)
- Read-only viewer: `import BpmnNavigatedViewer from 'bpmn-js/lib/NavigatedViewer'` — adds mouse-wheel zoom + drag-to-pan; `BpmnViewer` omits these navigation modules.

---

## Decision 2: Change Detection for "Dirty" State

**Decision**: Listen to `commandStack.changed` event on the modeler instance.

**Rationale**: This event fires after any user edit (create/move/delete/property change) AND after undo/redo. It is the primary signal for tracking unsaved state. `element.changed` fires per-element and is more granular — use alongside `commandStack.changed` only for the properties panel sync.

**Key API facts**:
```js
modeler.on('commandStack.changed', () => setIsDirty(true));
modeler.on('import.done', () => setIsDirty(false)); // Reset after load
```
Reset dirty flag after save by tracking the saved XML string.

---

## Decision 3: Properties Panel — Selection Events

**Decision**: Subscribe to `selection.changed` (primary) and `element.changed` (secondary) on the modeler.

**Key API facts**:
```js
modeler.on('selection.changed', (event) => {
  const element = event.newSelection[0]; // undefined if nothing selected
  setSelectedElement(element ?? null);
});
modeler.on('element.changed', (event) => {
  // refresh panel if event.element === currently selected element
});
```
Imperative read: `modeler.get('selection').get()` → current selection array.

**Reference**: `bpmn-io/bpmn-js-example-react-properties-panel` demonstrates this exact pattern.

---

## Decision 4: XML Source Tab — Read-Only Display

**Decision**: Use Monaco Editor in read-only XML mode (no second bpmn-js instance needed for the XML Source tab).

**Rationale**: The XML Source tab (FR-021) displays the live BPMN XML string — a plain string retrieved via `saveXML()`. Rendering a second bpmn-js viewer for the XML tab is unnecessary; Monaco Editor with `language: 'xml'` and `readOnly: true` provides syntax highlighting at zero additional library cost since Monaco is already used for the YAML editor.

**Implementation**: On tab switch to "XML Source", call `modeler.saveXML({ format: true })` and pass the result to the Monaco XML editor model.

---

## Decision 5: YAML Text Editor

**Decision**: Use `@monaco-editor/react` with `monaco-yaml` plugin.

**Rationale**:
- `@monaco-editor/react` abstracts worker setup and handles CDN/loader configuration automatically; lower friction for a Vite/React project than manual `MonacoEnvironment` wiring.
- Monaco's built-in language list does not include YAML. `monaco-yaml` (npm: `monaco-yaml`, maintained by remcohaszing) is the de facto standard — it bundles `yaml-language-server` as a Web Worker, providing syntax highlighting, schema-based validation, code completion, and inline error markers.
- `configureMonacoYaml(monaco, { schemas: [...], validate: true })` wires up schema validation before the editor mounts.

**Alternatives considered**:
- Raw `monaco-editor` + manual `MonacoEnvironment` — more control, more setup boilerplate; rejected in favor of the React wrapper for faster delivery.
- CodeMirror 6 — viable YAML editor but no bpmn-io precedent; Monaco was specified in clarification.

**Key API facts**:
- Package: `@monaco-editor/react` + `monaco-yaml`
- YAML language support: call `configureMonacoYaml(monaco, options)` in app startup (once, before any editor mounts)
- Schema validation: pass JSON schema via `schemas: [{ uri, fileMatch, schema }]`
- Programmatic error markers: `monaco.editor.setModelMarkers(model, 'server-validation', markers)` where each marker has `startLineNumber`, `startColumn`, `endLineNumber`, `endColumn`, `message`, `severity`
- Get content: `editor.getValue()` on the raw editor instance (from `onMount` callback)
- Set content: `editor.setValue(newContent)` (resets undo history); use `pushEditOperations` to preserve undo stack
- Listen to changes: `editor.onDidChangeModelContent(callback)`

---

## Decision 6: Vite Worker Configuration for Monaco

**Decision**: Use `vite-plugin-monaco-editor` for automatic worker bundling; supplement with a local `yaml.worker.js` re-export for `monaco-yaml`.

**Rationale**: The plugin handles `EditorWorker` bundling automatically. `monaco-yaml` requires its own worker import that the plugin doesn't know about — a local re-export file avoids manual `MonacoEnvironment` wiring while remaining compatible.

**Configuration**:
```js
// yaml.worker.js (local file)
import 'monaco-yaml/yaml.worker';

// vite.config.ts
import monacoEditorPlugin from 'vite-plugin-monaco-editor';
export default {
  plugins: [monacoEditorPlugin({ languages: [] })],
  worker: { format: 'es' }
}

// src/main.tsx (before any Monaco import)
window.MonacoEnvironment = {
  getWorker(_, label) {
    if (label === 'yaml') return new Worker(new URL('./yaml.worker.js', import.meta.url));
    return new Worker(new URL('monaco-editor/esm/vs/editor/editor.worker', import.meta.url));
  }
};
```

---

## Decision 7: Juice.Workflows.Designer Backend — Project Setup

**Decision**: Create `Juice.Workflows.Designer.csproj` as a new ASP.NET Core project; add to `Workflow.sln`. Multi-target `net6.0;net8.0;net9.0` (Constitution Principle V).

**Rationale**: The `src/Juice.Workflows.Designer/` directory is scaffolded but empty. The project needs a csproj before it can be added to the solution. It depends on `Juice.Workflows.Api` (for command pipeline), `Juice.Workflows.EF` (for DB access), and `Juice.Workflows.Bpmn` / `Juice.Workflows.Yaml` (for definition parsing during save).

**Key facts from codebase exploration**:
- `IDefinitionRepository` — missing `ListAsync` and `ExistsByNameAsync`; must extend.
- `WorkflowDefinition` entity — missing `Status` field; must add with state machine methods `Publish()`, `Archive()`.
- EF migration required for `Status` column in both SQL Server and PostgreSQL providers.
- `InMemorDefinitionRepository` must also implement the new methods for test isolation.
- `DefinitionRepository<TContext>` (EF) must implement the new methods.

---

## Decision 8: MediatR Command Structure

**Decision**: Add 6 commands and 1 query to the existing MediatR pipeline; dispatch via REST API controller in `Juice.Workflows.Designer`.

**Commands**:
| Command | Operation | HTTP Method |
|---|---|---|
| `CreateWorkflowDefinitionCommand` | Create Draft | POST /api/workflow-definitions |
| `UpdateWorkflowDefinitionCommand` | Update Draft RawData | PUT /api/workflow-definitions/{id} |
| `RenameWorkflowDefinitionCommand` | Rename (name only) | PATCH /api/workflow-definitions/{id}/name |
| `PublishWorkflowDefinitionCommand` | Draft → Active | POST /api/workflow-definitions/{id}/publish |
| `ArchiveWorkflowDefinitionCommand` | Active → Archived | POST /api/workflow-definitions/{id}/archive |
| `DeleteWorkflowDefinitionCommand` | Delete any status | DELETE /api/workflow-definitions/{id} |
| `ListWorkflowDefinitionsQuery` | List + filter | GET /api/workflow-definitions |

**Rationale**: These commands scope all lifecycle transitions through the MediatR pipeline, enabling future cross-cutting behaviors (logging, validation, idempotency) to apply without controller changes.

---

## Decision 9: Definition Status Lifecycle

**Decision**: Introduce `WorkflowDefinitionStatus` enum: `Draft = 0`, `Active = 1`, `Archived = 2`.

**State machine**:
- New definition → `Draft`
- `Publish()` on Draft → `Active` (validates Data is set; rejects if no Data)
- `Archive()` on Active → `Archived`
- Cannot transition: Active → Draft, Archived → any state
- Delete: allowed in any state (guard: warn if Active)

**EF schema change**: Add nullable `int Status` column to `WorkflowDefinition` table (default 0 = Draft). Requires migrations in both `Juice.Workflows.EF.SqlServer` and `Juice.Workflows.EF.PostgreSQL`.

---

## Decision 10: Atomic Save (RawData + Conversion)

**Decision**: The `CreateWorkflowDefinitionCommand` and `UpdateWorkflowDefinitionCommand` handlers parse RawData to execution-ready `Data` within the same DB transaction. If parsing fails, the entire operation is rejected — no partial state is saved.

**Rationale**: Spec FR-005a requires atomic save. The existing `Juice.Workflows.Bpmn` and `Juice.Workflows.Yaml` parsers already produce `(Processes, Nodes, Flows)` tuples that map directly to `WorkflowDefinition.SetData()`. Command handlers inject the appropriate parser by format.

**Error case**: Parser exceptions surface as `IOperationResult` failures returned to the REST controller, which maps to HTTP 422 with the error message.

---

## Decision 11: Frontend SPA Project Location

**Decision**: Frontend SPA source lives in `src/Juice.Workflows.Designer.Frontend/` (a Vite/React TypeScript project). Build output targets `src/Juice.Workflows.Designer/wwwroot/` to be served as static files by the ASP.NET Core host.

**Rationale**: Keeping frontend source in `src/` alongside backend projects follows the existing `src/` convention and makes the project boundary explicit. The ASP.NET Core project uses `app.UseStaticFiles()` and `app.MapFallbackToFile("index.html")` for SPA fallback routing.

**Alternatives considered**:
- Embedding frontend source inside `Juice.Workflows.Designer` — mixing C# and JS in one project directory; rejected for clarity.
- Separate top-level `frontend/` directory — breaks the `src/` convention; rejected.
