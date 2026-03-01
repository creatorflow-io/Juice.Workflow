using Juice.Workflows;
using Juice.Workflows.Models;

namespace Juice.Workflows.Designer.Tests
{
    public class WorkflowDefinitionQueryTests
    {
        private static IServiceProvider BuildServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddWorkflowServices();
            services.AddInMemoryReposistories();
            services.AddMediatR(options =>
                options.RegisterServicesFromAssemblyContaining<ListWorkflowDefinitionsQuery>());
            return services.BuildServiceProvider();
        }

        private static async Task<WorkflowDefinition> CreateDraftAsync(
            IDefinitionRepository repo, string name, string rawFormat = "YAML",
            CancellationToken cancellationToken = default)
        {
            var def = new WorkflowDefinition(Guid.NewGuid().ToString(), name);
            def.UpdateRawData("nodes: []", rawFormat);
            await repo.CreateAsync(def, cancellationToken);
            return def;
        }

        private static async Task<WorkflowDefinition> CreateActiveAsync(
            IDefinitionRepository repo, string name, string rawFormat = "YAML",
            CancellationToken cancellationToken = default)
        {
            var def = new WorkflowDefinition(Guid.NewGuid().ToString(), name);
            def.UpdateRawData("nodes: []", rawFormat);
            def.SetData(Array.Empty<ProcessRecord>(), Array.Empty<NodeData>(), Array.Empty<FlowData>());
            def.Publish();
            await repo.CreateAsync(def, cancellationToken);
            return def;
        }

        private static async Task<WorkflowDefinition> CreateArchivedAsync(
            IDefinitionRepository repo, string name, string rawFormat = "YAML",
            CancellationToken cancellationToken = default)
        {
            var def = new WorkflowDefinition(Guid.NewGuid().ToString(), name);
            def.UpdateRawData("nodes: []", rawFormat);
            def.SetData(Array.Empty<ProcessRecord>(), Array.Empty<NodeData>(), Array.Empty<FlowData>());
            def.Publish();
            def.Archive();
            await repo.CreateAsync(def, cancellationToken);
            return def;
        }

        [Fact]
        public async Task List_ReturnsAllDefinitions_WhenNoStatusFilterAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            await CreateDraftAsync(repo, "Workflow A");
            await CreateActiveAsync(repo, "Workflow B");

            var result = await mediator.Send(new ListWorkflowDefinitionsQuery(), CancellationToken.None);

            result.Should().NotBeNull();
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task List_FiltersByStatus_WhenStatusProvidedAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            await CreateDraftAsync(repo, "Draft Workflow");
            await CreateActiveAsync(repo, "Active Workflow");

            var result = await mediator.Send(
                new ListWorkflowDefinitionsQuery(WorkflowDefinitionStatus.Active), CancellationToken.None);

            result.Should().HaveCount(1);
            result.First().Status.Should().Be(WorkflowDefinitionStatus.Active);
        }

        [Fact]
        public async Task List_ReturnsSummariesWithFormatAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            await CreateDraftAsync(repo, "Test", "YAML");

            var result = await mediator.Send(new ListWorkflowDefinitionsQuery(), CancellationToken.None);

            result.Should().HaveCount(1);
            var summary = result.First();
            summary.Id.Should().NotBeNullOrEmpty();
            summary.Name.Should().Be("Test");
            summary.RawFormat.Should().Be("YAML");
            summary.Status.Should().Be(WorkflowDefinitionStatus.Draft);
        }

        [Fact]
        public async Task List_ReturnsEmpty_WhenNoDefinitionsExistAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(new ListWorkflowDefinitionsQuery(), CancellationToken.None);

            result.Should().BeEmpty();
        }
    }
}
