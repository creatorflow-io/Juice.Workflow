using Juice.Messaging.Outbox.Delivery;
using Juice.Messaging.Outbox.Delivery.Processing;
using Juice.Workflows.EF;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class WorkflowDeliveryBuilderExtensions
    {
        public static DeliveryBuilder AddWorkflowDelivery(this DeliveryBuilder builder, string publisher, Action<DeliveryProcessorBuilder>? configure = null)
        {
            builder.AddDeliveryProcessor<WorkflowDbContext>(publisher, configure);
            return builder;
        }
    }
}
