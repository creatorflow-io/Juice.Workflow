using Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate;
using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;

namespace Juice.Workflows.Designer.Commands
{
    public class CreateWorkflowDefinitionCommandHandler
        : IRequestHandler<CreateWorkflowDefinitionCommand, IOperationResult<string>>
    {
        private readonly IDefinitionRepository _repository;
        private readonly Juice.Workflows.Bpmn.Builder.WorkflowContextBuilder _bpmnBuilder;
        private readonly Juice.Workflows.Yaml.Builder.WorkflowContextBuilder _yamlBuilder;
        private readonly ILogger<CreateWorkflowDefinitionCommandHandler> _logger;

        public CreateWorkflowDefinitionCommandHandler(
            IDefinitionRepository repository,
            Juice.Workflows.Bpmn.Builder.WorkflowContextBuilder bpmnBuilder,
            Juice.Workflows.Yaml.Builder.WorkflowContextBuilder yamlBuilder,
            ILogger<CreateWorkflowDefinitionCommandHandler> logger)
        {
            _repository = repository;
            _bpmnBuilder = bpmnBuilder;
            _yamlBuilder = yamlBuilder;
            _logger = logger;
        }

        public async ValueTask<IOperationResult<string>> Handle(
            CreateWorkflowDefinitionCommand request, CancellationToken cancellationToken)
        {
            var nameConflict = await _repository.ExistsByNameAsync(
                request.Name, null, cancellationToken);
            if (nameConflict)
            {
                return OperationResult.Failed<string>(
                    $"A workflow definition named '{request.Name}' already exists.");
            }

            var id = Guid.NewGuid().ToString();
            var definition = new WorkflowDefinition(id, request.Name);
            definition.UpdateRawData(request.RawData, request.RawFormat);

            try
            {
                var dummyRecord = new WorkflowRecord(id, id, null, request.Name);
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
                    return OperationResult.Failed<string>(
                        $"Unsupported raw format '{request.RawFormat}'. Supported: BPMN, YAML.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse {Format} for definition '{Name}'", request.RawFormat, request.Name);
                return OperationResult.Failed<string>(ex, $"Failed to parse {request.RawFormat}: {ex.Message}");
            }

            var result = await _repository.CreateAsync(definition, cancellationToken);
            if (!result.Succeeded)
            {
                return OperationResult.Failed<string>(result.Message);
            }

            return OperationResult.Result(id, "Created");
        }
    }
}
