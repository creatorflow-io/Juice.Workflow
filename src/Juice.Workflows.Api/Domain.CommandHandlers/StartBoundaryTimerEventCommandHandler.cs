using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Nodes.Events;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartBoundaryTimerEventCommandHandler : StartTimerCommandHandlerBase<BoundaryTimerEvent>
    {
        public StartBoundaryTimerEventCommandHandler(IWorkflowOutboxService outbox, IEventRepository eventRepository) : base(outbox, eventRepository)
        {
        }
    }
}
