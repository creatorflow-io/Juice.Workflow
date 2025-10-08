using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;

namespace Juice.Workflows.InMemory
{
    internal class InMemoryEventRepository: IEventRepository
    {
        private readonly IDictionary<Guid, EventRecord> _events = new Dictionary<Guid, EventRecord>();
        public InMemoryEventRepository() { }

        public Task<IOperationResult> CreateUniqueByWorkflowAsync(EventRecord @event, CancellationToken token)
        {
            if (_events.Values.Any(e => e.WorkflowId == @event.WorkflowId
                 && !e.IsCompleted
                 && e.NodeId == @event.NodeId
                ))
            {
                return Task.FromResult<IOperationResult>(OperationResult.Failed("The event already exists."));
            }
            @event.Id = Guid.NewGuid();
            _events[@event.Id] = @event;
            return Task.FromResult<IOperationResult>(OperationResult.Success);
        }
        public Task<IEnumerable<EventRecord>> FindAllAsync(Expression<Func<EventRecord, bool>> predicate, CancellationToken token)
        {
            return Task.FromResult(_events.Values.AsQueryable().Where(predicate).AsEnumerable());
        }
        public Task<EventRecord?> GetAsync(Guid id, CancellationToken token)
        {
            return Task.FromResult(_events.ContainsKey(id) ? _events[id] : null);
        }
        public Task<IOperationResult> RemoveAsync(EventRecord @event, CancellationToken token)
        {
            if (_events.ContainsKey(@event.Id))
            {
                _events.Remove(@event.Id);
                return Task.FromResult<IOperationResult>(OperationResult.Success);
            }
            return Task.FromResult<IOperationResult>(OperationResult.Succeeded("The event does not exist."));
        }
        public Task<IOperationResult> UpdateAsync(EventRecord @event, CancellationToken token)
        {
            if (_events.ContainsKey(@event.Id))
            {
                _events[@event.Id] = @event;
                return Task.FromResult<IOperationResult>(OperationResult.Success);
            }
            return Task.FromResult<IOperationResult>(OperationResult.Failed("The event does not exist."));
        }
        public Task<IOperationResult> UpdateStartNodesAsync(string workflowId, EventRecord[] events, CancellationToken token)
        {
            var toUpdate = _events.Values.Where(e => e.WorkflowId == workflowId && e.IsStartEvent).ToList();
            foreach (var ev in toUpdate)
            {
                var match = events.FirstOrDefault(e => e.NodeId == ev.NodeId);
                if (match != null)
                {
                    ev.UpdateDisplayName(match.DisplayName);
                    _events[ev.Id] = ev;
                }
                else
                {
                    _events.Remove(ev.Id);
                }
            }
            foreach (var ev in events)
            {
                if (!toUpdate.Any(e => e.NodeId == ev.NodeId))
                {
                    ev.Id = Guid.NewGuid();
                    _events[ev.Id] = ev;
                }
            }
            return Task.FromResult<IOperationResult>(OperationResult.Success);
        }
    }
}
