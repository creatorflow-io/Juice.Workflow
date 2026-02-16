using Juice.Messaging;
using Juice.Workflows.Api;
using Juice.Workflows.EF;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class WfOutboxExtensions
    {
        public static MessagingBuilder AddWorkflowOutbox(this MessagingBuilder messaging, Action<OutboxBuilder>? configure = default)
        {
            messaging.AddOutbox(configure);
            messaging.Services.AddOutboxProxy<IWorkflowOutboxService, WorkflowDbContext>();
            return messaging;
        }
    }
}
