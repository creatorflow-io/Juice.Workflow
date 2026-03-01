# Quickstart: Implementing a Custom Node or Repository

**Feature**: 002-separate-layers

After this feature is implemented, custom node types and repository implementations can be built with a single package reference — no dependency on the execution engine required.

---

## Implementing a Custom Node

### 1. Create a class library

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Juice.Workflows.Abstractions" Version="*" />
  </ItemGroup>
</Project>
```

### 2. Implement `INode`

```csharp
using Juice.Workflows.Models;
using Juice.Workflows.Execution;

public class HttpCallNode : INode, IActivity
{
    public LocalizedString DisplayText => new("HTTP Call");
    public LocalizedString Category    => new("Integration");

    public IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext ctx, NodeContext node)
        => [new Outcome("Done"), new Outcome("Failed")];

    public async Task<NodeExecutionResult> StartAsync(
        WorkflowContext ctx, NodeContext node, FlowContext? flow, CancellationToken token)
    {
        var url = node.Properties.GetValueOrDefault("Url")?.ToString();
        // ... perform HTTP call ...
        return new NodeExecutionResult(WorkflowStatus.Finished, ["Done"]);
    }

    public Task<NodeExecutionResult> ResumeAsync(
        WorkflowContext ctx, NodeContext node, CancellationToken token)
        => Task.FromResult(NodeExecutionResult.Empty);

    public void Dispose() { }
}
```

### 3. Register the node in the host

In the consuming host project (which references `Juice.Workflows`):

```csharp
services.AddWorkflowServices(options =>
{
    options.RegisterNodes(typeof(HttpCallNode).Assembly);
});
```

---

## Implementing a Custom Repository

### 1. Create a class library

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Juice.Workflows.Abstractions" Version="*" />
  </ItemGroup>
</Project>
```

### 2. Implement one or more repository interfaces

```csharp
using Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate;

public class MongoDefinitionRepository : IDefinitionRepository
{
    public async Task<IOperationResult> CreateAsync(
        WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        // ... save to MongoDB ...
        return OperationResult.Success();
    }

    public async Task<WorkflowDefinition?> GetAsync(string id, CancellationToken cancellationToken)
    {
        // ... retrieve from MongoDB ...
    }

    // ... implement remaining interface members ...
}
```

### 3. Register in the host

```csharp
services.AddWorkflowServices();
services.AddTransient<IDefinitionRepository, MongoDefinitionRepository>();
// (replaces the default in-memory or EF implementation)
```

---

## Implementing a Custom Start Event Node

If your custom node is a start event, implement `IStartNode` so the engine can discover it:

```csharp
public class ScheduledStartEvent : INode, IStartNode, IEventNode
{
    public LocalizedString DisplayText => new("Scheduled Start");
    public LocalizedString Category    => new("Events");

    // ... implement INode ...
}
```

`WorkflowContext.GetStartNode()` and `NodeContext.IsStart()` will correctly identify this node as a start event without any changes to the engine.
