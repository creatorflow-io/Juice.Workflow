using System.Threading;
using Juice.Workflows.Domain.Commands;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    internal class StartTimerIntermediateCatchEventCommandHandler
                : IRequestHandler<StartEventCommand<TimerIntermediateCatchEvent>, IOperationResult>
    {
        private EventQueue _queue;
        private ILogger _logger;

        public StartTimerIntermediateCatchEventCommandHandler(ILoggerFactory logger, EventQueue eventQueue)
        {
            _logger = logger.CreateLogger(GetType());
            _queue = eventQueue;
        }

        public async Task<IOperationResult> Handle(StartEventCommand<TimerIntermediateCatchEvent> request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handle command");
            _queue.Throw(request.Node.Record.Id);
            return OperationResult.Success;
        }
    }
}
