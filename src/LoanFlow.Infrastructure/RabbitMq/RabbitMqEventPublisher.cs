using LoanFlow.Core.Messaging;

namespace LoanFlow.Infrastructure.RabbitMq;

/// <summary>
/// RabbitMQ implementation of <see cref="IEventPublisher"/>. Registered as a singleton.
///
/// TODO(step 3):
///  - Take RabbitMqConnection and IOptions&lt;RabbitMqOptions&gt; in the constructor.
///  - Get the connection, create a channel (connection.CreateChannelAsync). A publisher can
///    keep one channel for its lifetime, but don't publish on it from several threads at
///    once: guard it with a SemaphoreSlim, or use one channel per concurrent caller.
///  - Declare the exchange once: ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true).
///    Declaring is idempotent, so doing it at startup from both publisher and consumers is fine.
///  - Body: JsonSerializer.SerializeToUtf8Bytes(@event).
///  - Properties: new BasicProperties { ContentType = "application/json", Persistent = true,
///    MessageId = Guid.NewGuid().ToString(), Type = typeof(TEvent).Name }
///  - BasicPublishAsync(exchange, routingKey, mandatory: false, basicProperties: props, body: body).
///  - Stretch: publisher confirms, by creating the channel with
///    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true).
///    Then BasicPublishAsync only completes once the broker has the message.
/// </summary>
public sealed class RabbitMqEventPublisher : IEventPublisher
{
    public Task PublishAsync<TEvent>(string routingKey, TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull =>
        throw new NotImplementedException("Step 3: publish the event to the loan.events topic exchange.");
}
