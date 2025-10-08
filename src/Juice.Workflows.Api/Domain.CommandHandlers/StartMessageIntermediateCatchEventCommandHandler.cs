using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Nodes.Events;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartMessageIntermediateCatchEventCommandHandler :
        StartCatchCommandHandlerBase<StartEventCommand<MessageIntermediateCatchEvent>>,
        IRequestHandler<StartEventCommand<MessageIntermediateCatchEvent>, IOperationResult>
    {
        public StartMessageIntermediateCatchEventCommandHandler(IEventRepository eventRepository) : base(eventRepository)
        {
        }
    }
}
