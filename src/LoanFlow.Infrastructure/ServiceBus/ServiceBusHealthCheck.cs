using Azure.Messaging.ServiceBus;
using LoanFlow.Core.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoanFlow.Infrastructure.ServiceBus;

/// <summary>
/// Peeks one of the queues. Peeking doesn't lock or remove anything, so it's a safe way
/// to prove the broker is reachable and the queue from Config.json exists.
/// </summary>
public sealed class ServiceBusHealthCheck(ServiceBusClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var receiver = client.CreateReceiver(QueueNames.OfferExpiry);
            await receiver.PeekMessageAsync(cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy($"Queue '{QueueNames.OfferExpiry}' reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Peek failed. Is the emulator container running (docker compose ps), and does servicebus/Config.json define the queues?",
                ex);
        }
    }
}
