using System.Text.Json;
using System.Threading.Channels;
using LoanFlow.Core.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LoanFlow.Infrastructure.RabbitMq;

/// <summary>
/// RabbitMQ implementation of <see cref="IEventPublisher"/>. Registered as a singleton.
///
/// TODO(step 3):
///  - Stretch: publisher confirms, by creating the channel with
///    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true).
///    Then BasicPublishAsync only completes once the broker has the message.
/// </summary>
public sealed class RabbitMqEventPublisher(RabbitMqConnection connection, IOptions<RabbitMqOptions> options) : IEventPublisher
{
    private IChannel? channel;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    public async Task PublishAsync<TEvent>(string routingKey, TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (channel is null || channel.IsClosed)
            {
                var con = await connection.GetAsync(cancellationToken);
                var newChannel = await con.CreateChannelAsync(cancellationToken:cancellationToken);
                await newChannel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
                channel = newChannel;
            }
            var props = new BasicProperties
            {
                ContentType = "application/json",
                Persistent = true,
                MessageId = Guid.NewGuid().ToString(),
                Type = typeof(TEvent).Name
            };
            await channel.BasicPublishAsync(options.Value.Exchange, routingKey, mandatory: false, basicProperties: props, body: JsonSerializer.SerializeToUtf8Bytes(@event), cancellationToken: cancellationToken);
            
        }
        finally 
        {
            semaphore.Release();
        }
    }
    public async Task DisposeAsync()
    {
        if (channel is not null)
        {
            await channel.CloseAsync();

        }
    }
}
