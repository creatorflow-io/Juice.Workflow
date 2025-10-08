using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Nodes.Activities;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartUserTaskCommandHandler : StartTaskCommandHandlerBase<UserTask>
    {
        public StartUserTaskCommandHandler(IWorkflowEventBus eventBus, IEventRepository eventRepository) : base(eventBus, eventRepository)
        {

        }
    }
}
