using Juice.EventBus;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Api.IntegrationEvents.Handlers;

namespace Juice.Workflows.Api
{
    public static class WfApiEventBusExtensions
    {
        public static async Task InitWorkflowIntegrationEventsAsync(this IEventBus eventBus)
        {
            await eventBus.SubscribeAsync<TimerExpiredIntegrationEvent, TimerExpiredIntegrationEventHandler>();

            await eventBus.SubscribeAsync<MessageCatchIntegrationEvent, MessageCatchIntegrationEventHandler>("wfcatch.*.#");
        }
    }
}
