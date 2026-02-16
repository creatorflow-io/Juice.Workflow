using Juice.Workflows.Api;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class WorkflowBehaviorMediatorExtensions
    {
        public static MediatorBuilder AddWorkflowApiServices(this MediatorBuilder builder)
        {
            builder.RegisterServicesFromAssemblyContaining<WorkflowApiAssemblySelector>();
            return builder;
        }
    }
}
