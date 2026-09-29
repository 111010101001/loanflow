namespace LoanFlow.Core.Messaging;

/// <summary>
/// Sends commands to Service Bus queues.
///
/// This abstraction is deliberately leaky: sessions, duplicate detection and scheduling
/// are Service Bus concepts, and pretending otherwise would hide exactly what you're
/// here to learn. It still earns its place: the Web app and tests depend on this
/// interface, not on ServiceBusClient.
/// </summary>
public interface ICommandSender
{
    /// <param name="queueName">One of <see cref="QueueNames"/>.</param>
    /// <param name="sessionId">Required for session-enabled queues (use the application id), null otherwise.</param>
    /// <param name="messageId">Stable id for duplicate detection. Same id within the window = dropped by the broker.</param>
    Task SendAsync<TCommand>(
        string queueName,
        TCommand command,
        string? sessionId,
        string messageId,
        CancellationToken cancellationToken = default)
        where TCommand : notnull;

    /// <returns>The sequence number, needed to cancel the scheduled message later.</returns>
    Task<long> ScheduleAsync<TCommand>(
        string queueName,
        TCommand command,
        DateTimeOffset enqueueAt,
        string messageId,
        CancellationToken cancellationToken = default)
        where TCommand : notnull;

    Task CancelScheduledAsync(string queueName, long sequenceNumber, CancellationToken cancellationToken = default);
}

/// <summary>Publishes events to the RabbitMQ topic exchange.</summary>
public interface IEventPublisher
{
    /// <param name="routingKey">One of <see cref="RoutingKeys"/>.</param>
    Task PublishAsync<TEvent>(string routingKey, TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
