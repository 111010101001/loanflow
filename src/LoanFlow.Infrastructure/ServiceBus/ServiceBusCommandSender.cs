using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
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
public sealed class ServiceBusCommandSender(ServiceBusClient service) : ICommandSender
{
    private readonly ConcurrentDictionary<string, ServiceBusSender> buss = new();
    public Task SendAsync<TCommand>(
        string queueName,
        TCommand command,
        string? sessionId,
        string messageId,
        CancellationToken cancellationToken = default)
        where TCommand : notnull
    {
        BinaryData data = BinaryData.FromObjectAsJson(command);
        ServiceBusSender sender = buss.GetOrAdd(queueName, name => (service.CreateSender(name)));
        ServiceBusMessage message = new ServiceBusMessage(data)
        {
            MessageId = messageId,
            SessionId = sessionId,
            Subject = typeof(TCommand).Name,
            ContentType = "application/json",
        };
        return sender.SendMessageAsync(message, cancellationToken);
    }

    public Task<long> ScheduleAsync<TCommand>(
        string queueName,
        TCommand command,
        DateTimeOffset enqueueAt,
        string messageId,
        CancellationToken cancellationToken = default)
        where TCommand : notnull
    {
        BinaryData data = BinaryData.FromObjectAsJson(command);
        ServiceBusSender sender = buss.GetOrAdd(queueName, name => (service.CreateSender(name)));
        ServiceBusMessage message = new ServiceBusMessage(data)
        {
            MessageId = messageId,
            Subject = typeof(TCommand).Name,
            ContentType = "application/json",
        };
        return sender.ScheduleMessageAsync(message, enqueueAt, cancellationToken);
    }

    public Task CancelScheduledAsync(string queueName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        ServiceBusSender sender = buss.GetOrAdd(queueName, name => (service.CreateSender(name)));
        return sender.CancelScheduledMessageAsync(sequenceNumber, cancellationToken);
    }
}
