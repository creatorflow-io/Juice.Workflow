using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Nodes.Activities;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartServiceTaskCommandHandler : StartTaskCommandHandlerBase<ServiceTask>
    {
        public StartServiceTaskCommandHandler(IWorkflowOutboxService outbox, IEventRepository eventRepository) : base(outbox, eventRepository)
        {

        }
    }
}
