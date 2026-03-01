using Juice.Workflows;
using Juice.Workflows.Models;

namespace Juice.Workflows.Designer.Tests
{
    public class WorkflowDefinitionCreateTests
    {
        private const string MinimalBpmn = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" id=""d1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
  <bpmn:process id=""p1"" isExecutable=""true"">
    <bpmn:startEvent id=""start""><bpmn:outgoing>f1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:endEvent id=""end""><bpmn:incoming>f1</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id=""f1"" sourceRef=""start"" targetRef=""end"" />
  </bpmn:process>
</bpmn:definitions>";

        private const string MinimalYaml = @"- name: Test Process
  steps:
    - type: StartEvent
    - type: ServiceTask
    - type: EndEvent";

        private static IServiceProvider BuildServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            services.AddDefaultStringIdGenerator();
            services.AddWorkflowServices();
            services.AddInMemoryReposistories();
            services.AddMediatR(options =>
                options.RegisterServicesFromAssemblyContaining<CreateWorkflowDefinitionCommand>());
            services.AddTransient<Juice.Workflows.Bpmn.Builder.WorkflowContextBuilder>();
            services.AddTransient<Juice.Workflows.Yaml.Builder.WorkflowContextBuilder>();
            return services.BuildServiceProvider();
        }

        // ── Create ──────────────────────────────────────────────────────

        [Fact]
        public async Task Create_CreatesDraftDefinition_WithBpmnAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(
                new CreateWorkflowDefinitionCommand("My BPMN", MinimalBpmn, "BPMN"),
                CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNullOrEmpty();

            var repo = sp.GetRequiredService<IDefinitionRepository>();
            var def = await repo.GetAsync(result.Data!, CancellationToken.None);
            def.Should().NotBeNull();
            def!.Status.Should().Be(WorkflowDefinitionStatus.Draft);
            def.Data.Should().NotBeNullOrEmpty();
            def.RawFormat.Should().Be("BPMN");
        }

        [Fact]
        public async Task Create_CreatesDraftDefinition_WithYamlAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(
                new CreateWorkflowDefinitionCommand("My YAML", MinimalYaml, "YAML"),
                CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            var repo = sp.GetRequiredService<IDefinitionRepository>();
            var def = await repo.GetAsync(result.Data!, CancellationToken.None);
            def!.Status.Should().Be(WorkflowDefinitionStatus.Draft);
            def.RawFormat.Should().Be("YAML");
        }

        [Fact]
        public async Task Create_Fails_WhenNameAlreadyExistsAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            await mediator.Send(
                new CreateWorkflowDefinitionCommand("Duplicate", MinimalBpmn, "BPMN"),
                CancellationToken.None);

            var result = await mediator.Send(
                new CreateWorkflowDefinitionCommand("Duplicate", MinimalBpmn, "BPMN"),
                CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.Message.Should().Contain("already exists");
        }

        [Fact]
        public async Task Create_Fails_WhenBpmnIsInvalidAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(
                new CreateWorkflowDefinitionCommand("Bad BPMN", "this is not xml", "BPMN"),
                CancellationToken.None);

            result.Succeeded.Should().BeFalse();
        }

        [Fact]
        public async Task Create_GeneratesNonEmptyIdAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(
                new CreateWorkflowDefinitionCommand("ID Test", MinimalBpmn, "BPMN"),
                CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNullOrEmpty();
        }

        // ── Publish ─────────────────────────────────────────────────────

        [Fact]
        public async Task Publish_Succeeds_WhenDefinitionIsDraftWithDataAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var createResult = await mediator.Send(
                new CreateWorkflowDefinitionCommand("To Publish", MinimalBpmn, "BPMN"),
                CancellationToken.None);
            createResult.Succeeded.Should().BeTrue();

            var publishResult = await mediator.Send(
                new PublishWorkflowDefinitionCommand(createResult.Data!),
                CancellationToken.None);

            publishResult.Succeeded.Should().BeTrue();

            var repo = sp.GetRequiredService<IDefinitionRepository>();
            var def = await repo.GetAsync(createResult.Data!, CancellationToken.None);
            def!.Status.Should().Be(WorkflowDefinitionStatus.Active);
        }

        [Fact]
        public async Task Publish_Fails_WhenDefinitionNotFoundAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(
                new PublishWorkflowDefinitionCommand("nonexistent-id"),
                CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task Publish_Fails_WhenAlreadyActiveAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var createResult = await mediator.Send(
                new CreateWorkflowDefinitionCommand("Already Active", MinimalBpmn, "BPMN"),
                CancellationToken.None);
            await mediator.Send(new PublishWorkflowDefinitionCommand(createResult.Data!), CancellationToken.None);

            // Try to publish again
            var result = await mediator.Send(
                new PublishWorkflowDefinitionCommand(createResult.Data!),
                CancellationToken.None);

            result.Succeeded.Should().BeFalse();
        }
    }
}
