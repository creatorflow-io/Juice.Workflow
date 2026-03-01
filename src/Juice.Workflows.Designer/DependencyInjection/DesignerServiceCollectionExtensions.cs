using Juice.Services;
using Juice.Workflows.Designer.Commands;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DesignerServiceCollectionExtensions
    {
        /// <summary>
        /// Registers MediatR handlers and definition parsers from the Designer assembly.
        /// </summary>
        public static IServiceCollection AddWorkflowDesigner(this IServiceCollection services)
        {
            services.AddDefaultStringIdGenerator();
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(CreateWorkflowDefinitionCommand).Assembly));

            // Register BPMN and YAML workflow context builders as transient so
            // each parse gets a fresh, stateless instance.
            services.AddTransient<Juice.Workflows.Bpmn.Builder.WorkflowContextBuilder>();
            services.AddTransient<Juice.Workflows.Yaml.Builder.WorkflowContextBuilder>();

            return services;
        }
    }
}
