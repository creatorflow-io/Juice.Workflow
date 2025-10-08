using System.Threading;
using Juice.Workflows.Domain.Commands;

namespace Juice.Workflows.Tests.Domain.CommandHandlers
{
    internal class StartMessageIntermediateCatchEventCommandHandler :
        IRequestHandler<StartEventCommand<MessageIntermediateCatchEvent>, IOperationResult>
    {
        private EventQueue _queue;
        private ILogger _logger;
        public StartMessageIntermediateCatchEventCommandHandler(ILogger<StartMessageIntermediateCatchEventCommandHandler> logger,
            EventQueue eventQueue)
        {
            _logger = logger;
            _queue = eventQueue;
        }

        public ValueTask<IOperationResult> Handle(StartEventCommand<MessageIntermediateCatchEvent> request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handle command");
            _queue.Throw(request.Node.Record.Id);
            return ValueTask.FromResult(OperationResult.Success);
        }
    }
}
