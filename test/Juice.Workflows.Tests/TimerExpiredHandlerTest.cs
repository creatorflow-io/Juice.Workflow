using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.MediatR;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Api.IntegrationEvents.Handlers;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Juice.Workflows.Tests
{
    /// <summary>
    /// A timer publishes its expiry once: if dispatching it fails and the message is acknowledged, the workflow waits forever.
    /// </summary>
    public class TimerExpiredHandlerTest
    {
        private static Task HandleAsync(IOperationResult result, EventRecord? record, string issuer = "workflow", string? correlationId = null)
            => new TimerExpiredIntegrationEventHandler(new FixedResultMediator(result), new FixedEventRepository(record),
                    NullLogger<TimerExpiredIntegrationEventHandler>.Instance)
                .HandleAsync(new TimerExpiredIntegrationEvent(issuer, correlationId ?? Guid.NewGuid().ToString(), DateTimeOffset.UtcNow));

        [Fact]
        public async Task A_failed_dispatch_fails_the_messageAsync()
        {
            var act = () => HandleAsync(OperationResult.Failed("database down"), new EventRecord());

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*database down*");
        }

        [Fact]
        public async Task A_timer_whose_event_is_gone_is_acknowledgedAsync()
        {
            var act = () => HandleAsync(OperationResult.Failed("no longer exist"), record: null);

            await act.Should().NotThrowAsync("a retry could never succeed");
        }

        [Fact]
        public async Task A_successful_dispatch_completes_the_messageAsync()
        {
            var act = () => HandleAsync(OperationResult.Success, new EventRecord());

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task A_correlation_id_that_is_not_an_event_is_acknowledgedAsync()
        {
            var act = () => HandleAsync(OperationResult.Failed("unused"), new EventRecord(), correlationId: "not-a-guid");

            await act.Should().NotThrowAsync("a retry could never succeed");
        }

        [Fact]
        public async Task Another_issuers_timer_is_ignoredAsync()
        {
            var act = () => HandleAsync(OperationResult.Failed("unused"), new EventRecord(), issuer: "other");

            await act.Should().NotThrowAsync();
        }

        private sealed class FixedResultMediator(IOperationResult result) : IMediator
        {
            public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
                where TNotification : INotification => default;

            public ValueTask Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest => default;

            public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
                => new((TResponse)result);

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private sealed class FixedEventRepository(EventRecord? record) : IEventRepository
        {
            public Task<EventRecord?> GetAsync(Guid id, CancellationToken token) => Task.FromResult(record);
            public Task<IOperationResult> CreateUniqueByWorkflowAsync(EventRecord @event, CancellationToken token) => throw new NotSupportedException();
            public Task<IOperationResult> UpdateAsync(EventRecord @event, CancellationToken token) => throw new NotSupportedException();
            public Task<IOperationResult> RemoveAsync(EventRecord @event, CancellationToken token) => throw new NotSupportedException();
            public Task<IOperationResult> UpdateStartNodesAsync(string workflowId, EventRecord[] events, CancellationToken token) => throw new NotSupportedException();
            public Task<IEnumerable<EventRecord>> FindAllAsync(Expression<Func<EventRecord, bool>> predicate, CancellationToken token) => throw new NotSupportedException();
        }
    }
}
