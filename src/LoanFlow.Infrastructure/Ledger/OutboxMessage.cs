using System.Text.Json;

namespace LoanFlow.Infrastructure.Ledger;

/// <summary>
/// A row in the transactional outbox.
///
/// The problem it solves: saving a loan to SQL Server and publishing LoanOpened to RabbitMQ
/// are two separate systems. If the process dies between the two, you either have a loan
/// nobody heard about, or an event about a loan that was never saved. Instead, the event is
/// saved as a row in the same transaction as the loan (both or neither), and OutboxRelay
/// in the Worker publishes pending rows afterwards.
///
/// That makes delivery at-least-once: if the relay crashes after publishing but before
/// marking the row processed, the event goes out again. Consumers must be idempotent.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>RabbitMQ routing key, one of RoutingKeys.</summary>
    public required string RoutingKey { get; init; }

    /// <summary>Contract type name, e.g. "LoanOpened". Consumers use it to pick the record to deserialize.</summary>
    public required string Type { get; init; }

    /// <summary>The event serialized as JSON.</summary>
    public required string Payload { get; init; }

    public DateTime OccurredAtUtc { get; init; }

    /// <summary>Null until the relay has published it.</summary>
    public DateTime? ProcessedAtUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public static OutboxMessage For<TEvent>(string routingKey, TEvent @event, DateTime utcNow)
        where TEvent : notnull =>
        new()
        {
            RoutingKey = routingKey,
            Type = typeof(TEvent).Name,
            Payload = JsonSerializer.Serialize(@event),
            OccurredAtUtc = utcNow,
        };
}
