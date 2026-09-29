using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LoanFlow.Infrastructure.Ledger;

/// <summary>
/// Reports on SQL Server and the ledger database separately, so /status can tell you
/// what to do next:
///   red    = SQL Server unreachable (container down, or wrong password in user secrets)
///   yellow = server fine, but no migrations yet / database not created / migrations pending
///   green  = database exists and is up to date
/// </summary>
public sealed class LedgerHealthCheck(
    IDbContextFactory<LedgerDbContext> dbFactory,
    IOptions<LedgerOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = new SqlConnectionStringBuilder(options.Value.ConnectionString);

        // 1. Is SQL Server up at all? Ask master, which always exists.
        try
        {
            var master = new SqlConnectionStringBuilder(connectionString.ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Can't reach SQL Server. Is the mssql container running, and is the password in ConnectionStrings:ledger (user secrets) the same as MSSQL_SA_PASSWORD in .env?",
                ex);
        }

        // 2. The server is fine. Now the ledger database itself.
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var databaseName = connectionString.InitialCatalog;

            var migrations = db.Database.GetMigrations().ToList();
            if (migrations.Count == 0)
            {
                return HealthCheckResult.Degraded(
                    "SQL Server is up. No EF Core migrations exist yet, which is expected until step 4.");
            }

            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Degraded(
                    $"SQL Server is up, but database '{databaseName}' doesn't exist yet. Start the Worker: it applies migrations in Development.");
            }

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            return pending.Count == 0
                ? HealthCheckResult.Healthy($"Database '{databaseName}' is up to date ({migrations.Count} migrations).")
                : HealthCheckResult.Degraded(
                    $"{pending.Count} pending migration(s): {string.Join(", ", pending)}. Restart the Worker to apply them.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server is up, but checking the ledger database failed.", ex);
        }
    }
}
