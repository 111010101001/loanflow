using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LoanFlow.Infrastructure.Mongo;

/// <summary>Pings the database. Shown on the /status page and at /health.</summary>
public sealed class MongoHealthCheck(IMongoDatabase database, IOptions<MongoOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy($"Ping OK, database '{options.Value.DatabaseName}'.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Ping failed. Is mongod running where ConnectionStrings:mongodb points?",
                ex);
        }
    }
}
