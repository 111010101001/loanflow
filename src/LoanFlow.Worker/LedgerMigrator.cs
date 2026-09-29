using LoanFlow.Infrastructure.Ledger;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Worker;

/// <summary>
/// Applies EF Core migrations to the ledger database when the Worker starts, in Development only
/// (registered in Program.cs before the other hosted services, so it finishes first).
///
/// Fine for local work. In production you'd apply migrations as a deployment step instead,
/// e.g. an idempotent SQL script (dotnet ef migrations script --idempotent) or a migration
/// bundle, so several app instances don't race to migrate and the app doesn't need
/// permission to change the schema.
/// </summary>
public sealed class LedgerMigrator(
    IDbContextFactory<LedgerDbContext> dbFactory,
    ILogger<LedgerMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!db.Database.GetMigrations().Any())
        {
            logger.LogInformation(
                "No EF Core migrations yet; nothing to apply. Step 4 starts with: scripts/add-migration.sh InitialLedger");
            return;
        }

        // Creates the database if it doesn't exist, then applies whatever is pending.
        // Since EF Core 9 this throws if the model has changes that no migration covers
        // ("pending model changes"): add a migration for them and restart.
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Ledger database is up to date");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
