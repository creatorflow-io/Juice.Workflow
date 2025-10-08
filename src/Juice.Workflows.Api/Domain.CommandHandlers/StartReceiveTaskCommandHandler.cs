using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Nodes.Activities;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartReceiveTaskCommandHandler :
        StartCatchCommandHandlerBase<StartTaskCommand<ReceiveTask>>,
        IRequestHandler<StartTaskCommand<ReceiveTask>, IOperationResult>
    {
        public StartReceiveTaskCommandHandler(IEventRepository eventRepository) : base(eventRepository)
        {
        }
    }
}
