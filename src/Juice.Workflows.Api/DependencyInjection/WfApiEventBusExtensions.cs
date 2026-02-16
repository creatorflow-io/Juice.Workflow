using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Api.IntegrationEvents.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Api
{
    public static class WfApiEventBusExtensions
    {
        public static EventBusBuilder SubscribeWorkflowIntegrationEvents(this EventBusBuilder eventBus)
        {
            eventBus.AddConsumerServices(subs =>
            {
                subs.Subscribe<TimerExpiredIntegrationEvent, TimerExpiredIntegrationEventHandler>("timer.expired.workflow");
                subs.Subscribe<MessageCatchIntegrationEvent, MessageCatchIntegrationEventHandler>("wfcatch.*.#");
            });
            return eventBus;
        }
    }
}
