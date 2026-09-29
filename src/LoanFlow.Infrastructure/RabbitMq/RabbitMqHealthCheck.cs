using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoanFlow.Infrastructure.RabbitMq;

public sealed class RabbitMqHealthCheck(RabbitMqConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var amqp = await connection.GetAsync(cancellationToken);

            return amqp.IsOpen
                ? HealthCheckResult.Healthy($"Connected to {amqp.Endpoint}.")
                : HealthCheckResult.Degraded("Connection lost; the client is trying to recover.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Could not connect. Is the rabbitmq container running, and is the port in ConnectionStrings:rabbitmq 5673?",
                ex);
        }
    }
}
