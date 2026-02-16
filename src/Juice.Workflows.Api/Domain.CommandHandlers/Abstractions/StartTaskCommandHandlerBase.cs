using Juice.Extensions;
using Juice.Messaging.Outbox;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Nodes;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public abstract class StartTaskCommandHandlerBase<TTask> : StartCatchCommandHandlerBase<StartTaskCommand<TTask>>,
        IRequestHandler<StartTaskCommand<TTask>, IOperationResult>
        where TTask : Activity
    {
        protected readonly IOutboxService _outbox;
        protected readonly IEventRepository _eventRepository;
        public StartTaskCommandHandlerBase(IOutboxService outbox, IEventRepository eventRepository)
            : base(eventRepository)
        {
            _outbox = outbox;
            _eventRepository = eventRepository;
        }

        public override async ValueTask<IOperationResult> Handle(StartTaskCommand<TTask> request, CancellationToken cancellationToken)
        {
            try
            {
                var catchKey = GetCatchEventKey(request);
                var callbackEvent = new EventRecord(request.WorkflowId, request.Node.Record.Id, false,
                                            request.CorrelationId, catchKey, request.Node.Record.Name);
                var callbackEventRs = await _eventRepository.CreateUniqueByWorkflowAsync(callbackEvent, cancellationToken);
                if (!callbackEventRs.Succeeded)
                {
                    return callbackEventRs;
                }

                var properties = request.Node.GetSharedProperties();
                var @event = new MessageThrowIntegrationEvent(GetThrowEventKey(request), callbackEvent.Id, request.CorrelationId, properties);

                await _outbox.AddEventAsync(@event);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        protected virtual string GetThrowEventKey(StartTaskCommand<TTask> request)
        {

            var provider = request.Node.Properties.GetOption<string?>("Provider") ?? "general";

            var taskName = typeof(TTask).Name.ToLower();

            return $"wfthrow.{taskName}.{provider}";
        }
    }

}
