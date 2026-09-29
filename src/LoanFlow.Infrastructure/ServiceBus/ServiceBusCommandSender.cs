using LoanFlow.Core.Messaging;

namespace LoanFlow.Infrastructure.ServiceBus;

/// <summary>
/// Service Bus implementation of <see cref="ICommandSender"/>. Registered as a singleton.
///
/// TODO(step 2):
///  - Take ServiceBusClient in the constructor (it's a singleton in DI).
///  - Keep one ServiceBusSender per queue, e.g. in a ConcurrentDictionary. Senders are
///    thread-safe and meant to be reused; creating one per message is wasteful.
///  - Build ServiceBusMessage with BinaryData.FromObjectAsJson(command), and set
///    MessageId, SessionId (when given), Subject = typeof(TCommand).Name, ContentType = "application/json".
///    The consumer uses Subject to know which record type to deserialize.
///  - ScheduleAsync -> sender.ScheduleMessageAsync(...) returns the sequence number.
///  - CancelScheduledAsync -> sender.CancelScheduledMessageAsync(sequenceNumber).
/// </summary>
public sealed class ServiceBusCommandSender : ICommandSender
{
    public Task SendAsync<TCommand>(
        string queueName,
        TCommand command,
        string? sessionId,
        string messageId,
        CancellationToken cancellationToken = default)
        where TCommand : notnull =>
        throw new NotImplementedException("Step 2: send a command to Service Bus.");

    public Task<long> ScheduleAsync<TCommand>(
        string queueName,
        TCommand command,
        DateTimeOffset enqueueAt,
        string messageId,
        CancellationToken cancellationToken = default)
        where TCommand : notnull =>
        throw new NotImplementedException("Step 2: schedule a command on Service Bus.");

    public Task CancelScheduledAsync(string queueName, long sequenceNumber, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Step 2: cancel a scheduled command.");
}
