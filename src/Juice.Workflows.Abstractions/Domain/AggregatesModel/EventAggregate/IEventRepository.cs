using System.Linq.Expressions;

namespace Juice.Workflows.Domain.AggregatesModel.EventAggregate
{
    public interface IEventRepository
    {
        Task<IOperationResult> CreateUniqueByWorkflowAsync(EventRecord @event, CancellationToken token);
        Task<IOperationResult> UpdateAsync(EventRecord @event, CancellationToken token);
        Task<EventRecord?> GetAsync(Guid id, CancellationToken token);
        Task<IOperationResult> RemoveAsync(EventRecord @event, CancellationToken token);
        Task<IOperationResult> UpdateStartNodesAsync(string workflowId, EventRecord[] events, CancellationToken token);
        Task<IEnumerable<EventRecord>> FindAllAsync(Expression<Func<EventRecord, bool>> predicate, CancellationToken token);
    }
}
