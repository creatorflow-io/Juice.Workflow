using Juice.Extensions;
using Juice.Messaging.Outbox;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Nodes;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public abstract class StartTimerCommandHandlerBase<TEvent> : StartCatchCommandHandlerBase<StartEventCommand<TEvent>>,
        IRequestHandler<StartEventCommand<TEvent>, IOperationResult>
        where TEvent : Event
    {
        private readonly IOutboxService _outbox;
        private readonly IEventRepository _eventRepository;
        public StartTimerCommandHandlerBase(IOutboxService outbox, IEventRepository eventRepository)
            : base(eventRepository)
        {
            _outbox = outbox;
            _eventRepository = eventRepository;
        }
        public override async ValueTask<IOperationResult> Handle(StartEventCommand<TEvent> request, CancellationToken cancellationToken)
        {
            try
            {
                var after = request.Node.Properties.GetOption<TimeSpan?>("After")
                    ?? TimeSpan.FromHours(2);

                var catchKey = GetCatchEventKey(request);

                var callbackEvent = new EventRecord(request.WorkflowId, request.Node.Record.Id, false,
                                        request.CorrelationId, catchKey, request.Node.Record.Name);

                var callbackEventRs = await _eventRepository.CreateUniqueByWorkflowAsync(callbackEvent, cancellationToken);
                if (!callbackEventRs.Succeeded)
                {
                    return callbackEventRs;
                }

                var @event = new TimerStartIntegrationEvent("workflow", callbackEvent.Id.ToString(), DateTimeOffset.Now.Add(after));

                await _outbox.AddEventAsync(@event);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
