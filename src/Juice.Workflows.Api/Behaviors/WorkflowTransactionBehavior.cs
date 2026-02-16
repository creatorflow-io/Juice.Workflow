using Juice.MediatR.Behaviors;
using Juice.Messaging.Outbox;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.EF;
using Microsoft.Extensions.Logging;

namespace Juice.Workflows.Api.Behaviors
{
    internal class WorkflowTransactionBehavior<T, R> : TransactionBehavior<T, R, WorkflowDbContext>
        where T : IRequest<R>, INodeCommand
    {
        public WorkflowTransactionBehavior(WorkflowDbContext dbContext,
            IOutboxService<WorkflowDbContext> outboxService,
            IMediator mediator,
            ILogger<WorkflowTransactionBehavior<T, R>> logger) : base(dbContext, outboxService, mediator, logger)
        {
        }
    }
}
