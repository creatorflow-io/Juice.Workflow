using Juice.Workflows;
using Juice.Workflows.Models;

namespace Juice.Workflows.Designer.Tests
{
    public class WorkflowDefinitionUpdateTests
    {
        private const string MinimalBpmn = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" id=""d1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
  <bpmn:process id=""p1"" isExecutable=""true"">
    <bpmn:startEvent id=""start""><bpmn:outgoing>f1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:endEvent id=""end""><bpmn:incoming>f1</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id=""f1"" sourceRef=""start"" targetRef=""end"" />
  </bpmn:process>
</bpmn:definitions>";

        private static IServiceProvider BuildServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            services.AddDefaultStringIdGenerator();
            services.AddWorkflowServices();
            services.AddInMemoryReposistories();
            services.AddMediatR(options =>
                options.RegisterServicesFromAssemblyContaining<UpdateWorkflowDefinitionCommand>());
            services.AddTransient<Juice.Workflows.Bpmn.Builder.WorkflowContextBuilder>();
            services.AddTransient<Juice.Workflows.Yaml.Builder.WorkflowContextBuilder>();
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Update_UpdatesRawDataAndData_WhenDefinitionExistsAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            // Create definition directly
            var def = new WorkflowDefinition(Guid.NewGuid().ToString(), "Update Test");
            def.UpdateRawData(MinimalBpmn, "BPMN");
            def.SetData(Array.Empty<ProcessRecord>(), Array.Empty<NodeData>(), Array.Empty<FlowData>());
            await repo.CreateAsync(def, CancellationToken.None);

            var result = await mediator.Send(
                new UpdateWorkflowDefinitionCommand(def.Id, MinimalBpmn, "BPMN"),
                CancellationToken.None);

            result.Succeeded.Should().BeTrue(result.Message);

            var updated = await repo.GetAsync(def.Id, CancellationToken.None);
            updated!.RawFormat.Should().Be("BPMN");
            updated.Data.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Update_Fails_WhenDefinitionNotFoundAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(
                new UpdateWorkflowDefinitionCommand("nonexistent", MinimalBpmn, "BPMN"),
                CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task Update_Fails_WhenBpmnIsInvalidAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var def = new WorkflowDefinition(Guid.NewGuid().ToString(), "Parse Fail Test");
            await repo.CreateAsync(def, CancellationToken.None);

            var result = await mediator.Send(
                new UpdateWorkflowDefinitionCommand(def.Id, "not xml", "BPMN"),
                CancellationToken.None);

            result.Succeeded.Should().BeFalse();
        }

        [Fact]
        public async Task Update_StatusUnchanged_AfterUpdateAsync()
        {
            var sp = BuildServices();
            var mediator = sp.GetRequiredService<IMediator>();
            var repo = sp.GetRequiredService<IDefinitionRepository>();

            var def = new WorkflowDefinition(Guid.NewGuid().ToString(), "Status Test");
            await repo.CreateAsync(def, CancellationToken.None);

            var result = await mediator.Send(
                new UpdateWorkflowDefinitionCommand(def.Id, MinimalBpmn, "BPMN"),
                CancellationToken.None);

            result.Succeeded.Should().BeTrue(result.Message);

            var updated = await repo.GetAsync(def.Id, CancellationToken.None);
            updated!.Status.Should().Be(WorkflowDefinitionStatus.Draft);
        }
    }
}
