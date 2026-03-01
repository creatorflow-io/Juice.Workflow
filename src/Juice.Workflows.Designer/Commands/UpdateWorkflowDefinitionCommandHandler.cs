using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;

namespace Juice.Workflows.Designer.Commands
{
    public class UpdateWorkflowDefinitionCommandHandler
        : IRequestHandler<UpdateWorkflowDefinitionCommand, IOperationResult>
    {
        private readonly IDefinitionRepository _repository;
        private readonly Juice.Workflows.Bpmn.Builder.WorkflowContextBuilder _bpmnBuilder;
        private readonly Juice.Workflows.Yaml.Builder.WorkflowContextBuilder _yamlBuilder;
        private readonly ILogger<UpdateWorkflowDefinitionCommandHandler> _logger;

        public UpdateWorkflowDefinitionCommandHandler(
            IDefinitionRepository repository,
            Juice.Workflows.Bpmn.Builder.WorkflowContextBuilder bpmnBuilder,
            Juice.Workflows.Yaml.Builder.WorkflowContextBuilder yamlBuilder,
            ILogger<UpdateWorkflowDefinitionCommandHandler> logger)
        {
            _repository = repository;
            _bpmnBuilder = bpmnBuilder;
            _yamlBuilder = yamlBuilder;
            _logger = logger;
        }

        public async ValueTask<IOperationResult> Handle(
            UpdateWorkflowDefinitionCommand request, CancellationToken cancellationToken)
        {
            var definition = await _repository.GetAsync(request.Id, cancellationToken);
            if (definition == null)
            {
                return OperationResult.Failed($"Workflow definition '{request.Id}' not found.");
            }

            definition.UpdateRawData(request.RawData, request.RawFormat);

            try
            {
                var dummyRecord = new WorkflowRecord(request.Id, request.Id, null, definition.Name);
                if (request.RawFormat.Equals("BPMN", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StringReader(request.RawData);
                    var context = _bpmnBuilder.Build(reader, dummyRecord);
                    definition.SetData(
                        context.Processes,
                        context.Nodes.Values.Select(n => new NodeData(n.Record, n.Node.GetType().FullName!, n.IsStart(), n.Properties)),
                        context.Flows.Select(f => new FlowData(f.Record, f.Flow.GetType().FullName!)));
                }
                else if (request.RawFormat.Equals("YAML", StringComparison.OrdinalIgnoreCase))
                {
                    var context = _yamlBuilder.Build(request.RawData, dummyRecord);
                    definition.SetData(
                        context.Processes,
                        context.Nodes.Values.Select(n => new NodeData(n.Record, n.Node.GetType().FullName!, n.IsStart(), n.Properties)),
                        context.Flows.Select(f => new FlowData(f.Record, f.Flow.GetType().FullName!)));
                }
                else
                {
                    return OperationResult.Failed($"Unsupported raw format '{request.RawFormat}'.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse {Format} for definition '{Id}'", request.RawFormat, request.Id);
                return OperationResult.Failed(ex, $"Failed to parse {request.RawFormat}: {ex.Message}");
            }

            return await _repository.UpdateAsync(definition, cancellationToken);
        }
    }
}
