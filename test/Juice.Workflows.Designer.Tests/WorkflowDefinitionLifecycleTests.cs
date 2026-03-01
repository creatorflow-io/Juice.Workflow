using Juice.Workflows;
using Juice.Workflows.Models;

namespace Juice.Workflows.Designer.Tests
{
    public class WorkflowDefinitionLifecycleTests
    {
        private static IServiceProvider BuildServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddWorkflowServices();
            services.AddInMemoryReposistories();
            services.AddMediatR(options =>
                options.RegisterServicesFromAssemblyContaining<DeleteWorkflowDefinitionCommand>());
            return services.BuildServiceProvider();
        }

        private static async Task<WorkflowDefinition> CreateDraftAsync(
            IDefinitionRepository repo, string name, CancellationToken cancellationToken = default)
        {
            var def = new WorkflowDefinition(Guid.NewGuid().ToString(), name);
            def.UpdateRawData("nodes: []", "YAML");
            await repo.CreateAsync(def, cancellationToken);
            return def;
        }

        private static async Task<WorkflowDefinition> CreateActiveAsync(
            IDefinitionRepository repo, string name, CancellationToken cancellationToken = default)
        {
            var def = new WorkflowDefinition(Guid.NewGuid().ToString(), name);
            def.UpdateRawData("nodes: []", "YAML");
            def.SetData(Array.Empty<ProcessRecord>(), Array.Empty<NodeData>(), Array.Empty<FlowData>());
            def.Publish();
            await repo.CreateAsync(def, cancellationToken);
            return def;
        }

        // ── Delete ──────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_RemovesDefinition_WhenIdExistsAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var def = await CreateDraftAsync(repo, "ToDelete");

            var result = await mediator.Send(new DeleteWorkflowDefinitionCommand(def.Id), CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            var found = await repo.GetAsync(def.Id, CancellationToken.None);
            found.Should().BeNull();
        }

        [Fact]
        public async Task Delete_Fails_WhenIdNotFoundAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(new DeleteWorkflowDefinitionCommand("nonexistent-id"), CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }

        // ── Rename ──────────────────────────────────────────────────────

        [Fact]
        public async Task Rename_ChangesName_WhenNoConflictAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var def = await CreateDraftAsync(repo, "Original Name");

            var result = await mediator.Send(
                new RenameWorkflowDefinitionCommand(def.Id, "New Name"), CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            var updated = await repo.GetAsync(def.Id, CancellationToken.None);
            updated!.Name.Should().Be("New Name");
        }

        [Fact]
        public async Task Rename_Fails_WhenNameAlreadyExistsAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            await CreateDraftAsync(repo, "Existing Name");
            var def2 = await CreateDraftAsync(repo, "To Rename");

            var result = await mediator.Send(
                new RenameWorkflowDefinitionCommand(def2.Id, "Existing Name"), CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.Message.Should().Contain("already exists");
        }

        [Fact]
        public async Task Rename_Succeeds_WhenRenamingToOwnNameAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var def = await CreateDraftAsync(repo, "Same Name");

            // Renaming to the same name should not be a conflict (excludeId = def.Id)
            var result = await mediator.Send(
                new RenameWorkflowDefinitionCommand(def.Id, "Same Name"), CancellationToken.None);

            result.Succeeded.Should().BeTrue();
        }

        // ── Archive ─────────────────────────────────────────────────────

        [Fact]
        public async Task Archive_Succeeds_WhenDefinitionIsActiveAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var def = await CreateActiveAsync(repo, "Active Workflow");

            var result = await mediator.Send(new ArchiveWorkflowDefinitionCommand(def.Id), CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            var updated = await repo.GetAsync(def.Id, CancellationToken.None);
            updated!.Status.Should().Be(WorkflowDefinitionStatus.Archived);
        }

        [Fact]
        public async Task Archive_Fails_WhenDefinitionIsDraftAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var def = await CreateDraftAsync(repo, "Draft Workflow");

            var result = await mediator.Send(new ArchiveWorkflowDefinitionCommand(def.Id), CancellationToken.None);

            result.Succeeded.Should().BeFalse();
        }

        [Fact]
        public async Task Archive_Fails_WhenDefinitionIsAlreadyArchivedAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var def = await CreateActiveAsync(repo, "Active to Archive");
            await mediator.Send(new ArchiveWorkflowDefinitionCommand(def.Id), CancellationToken.None);

            // Try to archive again
            var result = await mediator.Send(new ArchiveWorkflowDefinitionCommand(def.Id), CancellationToken.None);

            result.Succeeded.Should().BeFalse();
        }

        [Fact]
        public async Task Archive_Fails_WhenDefinitionNotFoundAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(
                new ArchiveWorkflowDefinitionCommand("nonexistent"), CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }
    }
}
