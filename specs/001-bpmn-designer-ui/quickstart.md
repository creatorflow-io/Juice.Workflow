# Quickstart: Workflow Designer UI (001-bpmn-designer-ui)

**Target audience**: Developer picking up this feature for the first time.

---

## Prerequisites

- .NET 9 SDK (`dotnet --version` ≥ 9.0)
- Node.js ≥ 20 + npm ≥ 10 (or pnpm ≥ 8)
- Private NuGet source configured: `https://nuget.pkg.github.com/creatorflow-io/index.json` (see CLAUDE.md)
- Optionally: SQL Server or PostgreSQL for EF integration (in-memory works for unit tests)

---

## Backend — First Run

```bash
# 1. Restore and build
dotnet build Workflow.sln

# 2. Run all existing tests (must pass before starting)
dotnet test Workflow.sln

# 3. Run the designer tests specifically (once the project is created)
dotnet test test/Juice.Workflows.Designer.Tests/Juice.Workflows.Designer.Tests.csproj
```

---

## Frontend — First Run

```bash
cd src/Juice.Workflows.Designer.Frontend

# Install dependencies
npm install

# Start dev server (proxies /api/* to ASP.NET Core backend at https://localhost:5001)
npm run dev

# Build for production (output goes to ../Juice.Workflows.Designer/wwwroot/)
npm run build
```

**Dev server URL**: `http://localhost:5173` (Vite default)

---

## Key Source Locations

### Backend

| Path | What it is |
|------|------------|
| `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/WorkflowDefinition.cs` | Domain entity — add `Status`, `Publish()`, `Archive()` |
| `src/Juice.Workflows/Domain/AggregatesModel/DefinitionAggregate/IDefinitionRepository.cs` | Repository interface — add `ListAsync`, `ExistsByNameAsync` |
| `src/Juice.Workflows/InMemory/InMemorDefinitionRepository.cs` | In-memory implementation for tests |
| `src/Juice.Workflows.EF/Repositories/DefinitionRepository.cs` | EF implementation |
| `src/Juice.Workflows.EF/WorkflowDbContext.cs` | EF mapping — add Status column config |
| `src/Juice.Workflows.EF.SqlServer/` | Add `AddWorkflowDefinitionStatus` migration |
| `src/Juice.Workflows.EF.PostgreSQL/` | Add `AddWorkflowDefinitionStatus` migration |
| `src/Juice.Workflows.Designer/Controllers/WorkflowDefinitionsController.cs` | REST API controller |
| `src/Juice.Workflows.Designer/` | ASP.NET Core SPA host |
| `test/Juice.Workflows.Designer.Tests/` | xUnit tests for commands + controller |

### Frontend

| Path | What it is |
|------|------------|
| `src/Juice.Workflows.Designer.Frontend/src/pages/DefinitionList.tsx` | Dashboard / list page |
| `src/Juice.Workflows.Designer.Frontend/src/pages/BpmnEditor.tsx` | BPMN visual editor page |
| `src/Juice.Workflows.Designer.Frontend/src/pages/YamlEditor.tsx` | YAML text editor page |
| `src/Juice.Workflows.Designer.Frontend/src/components/BpmnCanvas.tsx` | bpmn-js wrapper component |
| `src/Juice.Workflows.Designer.Frontend/src/components/PropertiesPanel.tsx` | Right panel for selected element |
| `src/Juice.Workflows.Designer.Frontend/src/components/XmlSourceTab.tsx` | Read-only XML tab (Monaco) |
| `src/Juice.Workflows.Designer.Frontend/src/api/definitionsApi.ts` | Typed fetch wrapper for REST API |

---

## Adding a New BPMN Element Type (Backend)

If the BPMN parser rejects an element type during save (HTTP 422), check whether a corresponding `INode` implementation exists in `Juice.Workflows`. If not, the element is unsupported and must be removed from the canvas before saving. To add support for a new element type, follow the existing pattern in `src/Juice.Workflows/` and add a test in `test/Juice.Workflows.Tests/`.

---

## Minimal "Hello World" Workflow

To verify end-to-end save + execution after implementation:

1. Open the designer at `http://localhost:5173`.
2. Click **New BPMN Definition** → enter name "Hello World".
3. Drag a **Start Event** → **Task** (name it "Do Something") → **End Event** → connect with flows.
4. Click **Save** → definition appears in list with `Draft` badge.
5. Click **Publish** → status changes to `Active`.
6. Via gRPC or test, call `StartWorkflowCommand` with the definition ID → workflow executes.

---

## Running EF Migrations

After adding the `Status` column:

```bash
# SQL Server
cd src/Juice.Workflows.EF.SqlServer
dotnet ef migrations add AddWorkflowDefinitionStatus --context WorkflowDbContext
dotnet ef database update --context WorkflowDbContext

# PostgreSQL
cd src/Juice.Workflows.EF.PostgreSQL
dotnet ef migrations add AddWorkflowDefinitionStatus --context WorkflowDbContext
dotnet ef database update --context WorkflowDbContext
```

---

## Vite Proxy Configuration (dev only)

`vite.config.ts` should proxy `/api` requests to the backend:

```ts
export default defineConfig({
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:5001',
        changeOrigin: true,
        secure: false
      }
    }
  }
});
```

This allows the frontend dev server to communicate with the .NET backend without CORS issues during development.
