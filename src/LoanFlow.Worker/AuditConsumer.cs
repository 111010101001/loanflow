using System;
using System.Collections.Generic;
using System.Text;
using LoanFlow.Core.Messaging;
using LoanFlow.Infrastructure.RabbitMq;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace LoanFlow.Worker;

public sealed class AuditConsumer(RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IMongoDatabase database,
    TimeProvider timeProvider,
    ILogger<AuditConsumer> logger) : BackgroundService
{
    private IChannel? channel;
    private readonly IMongoCollection<AuditEntry> collection = database.GetCollection<AuditEntry>("audit");
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var con = await connection.GetAsync(cancellationToken);
        channel = await con.CreateChannelAsync(cancellationToken: cancellationToken);

        try
        {
            await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
            var queue = await channel.QueueDeclareAsync("audit", durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(queue, options.Value.Exchange, "#", cancellationToken: cancellationToken);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: cancellationToken);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += OnReceivedAsync;

            await channel.BasicConsumeAsync("audit", autoAck: false, consumer: consumer, cancellationToken: cancellationToken);

            await Task.Delay(Timeout.Infinite, cancellationToken);   // listen until shutdown

        }
        finally
        {
            await channel.DisposeAsync();
        }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var entry = new AuditEntry(ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString(), ea.BasicProperties.Type, ea.RoutingKey, timeProvider.GetUtcNow().UtcDateTime, Encoding.UTF8.GetString(ea.Body.Span));
        try
        {
            await collection.InsertOneAsync(entry);

        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            logger.LogInformation("Duplicate audit event {MessageId}, already stored", entry.Id);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not store audit event {MessageId}", entry.Id);
            await channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }
        await channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);


    }

    private record AuditEntry(string Id, string? Type, string RoutingKey, DateTime ReceivedAtUtc, string Body);
}

