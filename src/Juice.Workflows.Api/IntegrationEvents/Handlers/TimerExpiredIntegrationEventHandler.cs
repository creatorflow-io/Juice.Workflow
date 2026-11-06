using Juice.Messaging;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Microsoft.Extensions.Logging;

namespace Juice.Workflows.Api.IntegrationEvents.Handlers
{
    public class TimerExpiredIntegrationEventHandler : IIntegrationEventHandler<TimerExpiredIntegrationEvent>
    {
        private IMediator _mediator;
        private readonly IEventRepository _eventRepository;
        private readonly ILogger _logger;
        public TimerExpiredIntegrationEventHandler(IMediator mediator, IEventRepository eventRepository,
            ILogger<TimerExpiredIntegrationEventHandler> logger)
        {
            _mediator = mediator;
            _eventRepository = eventRepository;
            _logger = logger;
        }
        public async Task HandleAsync(TimerExpiredIntegrationEvent @event)
        {
            if (@event.Issuer == "workflow")
            {
                if (Guid.TryParse(@event.CorrelationId, out var callbackId))
                {
                    var result = await _mediator.Send(new DispatchWorkflowEventCommand(callbackId, true, default));
                    if (!result.Succeeded)
                    {
                        // A retry is safe: an event that completed answers "already completed". And it's needed: the timer
                        // publishes once, so the workflow would wait forever. Only a deleted event can't be helped by one.
                        if (await _eventRepository.GetAsync(callbackId, default) != null)
                        {
                            throw new InvalidOperationException($"Failed to dispatch workflow event for timer {@event.CorrelationId}: {result.Message}");
                        }
                        _logger.LogError("Failed to dispatch workflow event for timer. {id}, {error}", @event.CorrelationId ?? "", result.ToString());
                    }
                }
                else
                {
                    _logger.LogWarning("Could not parse timer correlationId to Guid. {id}", @event.CorrelationId ?? "");
                }
            }
        }
    }
}
