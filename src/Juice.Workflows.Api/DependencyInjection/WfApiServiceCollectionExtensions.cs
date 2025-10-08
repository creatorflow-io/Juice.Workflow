using Juice.Workflows.Api.Domain.CommandHandlers;
using Juice.Workflows.Api.Domain.EventHandlers;
using Juice.Workflows.Api.IntegrationEvents.Handlers;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Domain.Events;
using Juice.Workflows.Nodes.Activities;
using Juice.Workflows.Nodes.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Workflows.Api
{
    public static class WfApiServiceCollectionExtensions
    {
        public static IServiceCollection AddWorkflowIntegrationEventHandlers(this IServiceCollection services)
        {
            services.AddTransient<TimerExpiredIntegrationEventHandler>();
            services.AddTransient<MessageCatchIntegrationEventHandler>();
            return services;
        }

        //public static IServiceCollection AddWorkflowApiServices(this IServiceCollection services)
        //{
        //    services.TryAddTransient<IRequestHandler<StartEventCommand<BoundaryTimerEvent>, IOperationResult>, StartBoundaryTimerEventCommandHandler>();
        //    services.TryAddTransient<IRequestHandler<StartEventCommand<MessageIntermediateCatchEvent>, IOperationResult>, StartMessageIntermediateCatchEventCommandHandler>();
        //    services.TryAddTransient<IRequestHandler<StartEventCommand<TimerIntermediateCatchEvent>, IOperationResult>, StartTimerIntermediateCatchEventCommandHandler>();
        //    services.TryAddTransient<IRequestHandler<StartTaskCommand<ReceiveTask>, IOperationResult>, StartReceiveTaskCommandHandler>();
        //    services.TryAddTransient<IRequestHandler<StartTaskCommand<UserTask>, IOperationResult>, StartUserTaskCommandHandler>();
        //    services.TryAddTransient<IRequestHandler<StartTaskCommand<SendTask>, IOperationResult>, StartSendTaskCommandHandler>();
        //    services.TryAddTransient<IRequestHandler<StartTaskCommand<ServiceTask>, IOperationResult>, StartServiceTaskCommandHandler>();

        //    services.TryAddTransient<INotificationHandler<DefinitionDataChangedDomainEvent>, DefinitionDataChangedDomainEventHandler>();
        //    services.TryAddTransient<INotificationHandler<TimerEventStartDomainEvent>, TimerEventStartDomainEventHandler>();
        //    return services;
        //}
    }
}
