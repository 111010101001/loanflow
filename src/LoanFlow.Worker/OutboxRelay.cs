namespace LoanFlow.Worker;

/// <summary>
/// Publishes rows from the ledger's outbox table to RabbitMQ (the transactional outbox;
/// see OutboxMessage for why it exists).
///
/// TODO(step 4):
///  - Loop with a PeriodicTimer (say every 2 seconds) until stoppingToken fires.
///  - Each tick: create a scope, get LedgerDbContext and IEventPublisher, and load a batch of
///    unprocessed rows, oldest first:
///      db.OutboxMessages.Where(m => m.ProcessedAtUtc == null).OrderBy(m => m.OccurredAtUtc).Take(50)
///  - For each row, publish Payload with its RoutingKey, then set ProcessedAtUtc. On failure,
///    increment Attempts, store LastError, and leave it for the next tick.
///  - SaveChangesAsync once per batch.
///  - Crash between publish and save? The row gets published again next time. That's the
///    at-least-once guarantee, and why the RabbitMQ consumers from step 3 must be idempotent.
///  - Stretch: clean up old processed rows with a set-based delete that never loads them:
///      db.OutboxMessages.Where(m => m.ProcessedAtUtc &lt; cutoff).ExecuteDeleteAsync()
///
/// IEventPublisher currently publishes an object; for the relay you'll want an overload
/// that takes the already-serialized JSON and the type name.
/// </summary>
public sealed class OutboxRelay(ILogger<OutboxRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxRelay is a stub until step 4");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
