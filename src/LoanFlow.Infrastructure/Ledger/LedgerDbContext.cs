using LoanFlow.Core.Ledger;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Infrastructure.Ledger;

/// <summary>
/// EF Core context for the ledger database in SQL Server.
///
/// Registered with AddDbContextFactory (see DependencyInjection.AddLoanFlowLedger), which gives
/// you two ways to get one:
///  - IDbContextFactory&lt;LedgerDbContext&gt; (singleton): Blazor components create a short-lived
///    context per operation with it.
///  - LedgerDbContext itself (scoped): the Worker takes it from the per-message scope.
/// </summary>
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<Loan> Loans => Set<Loan>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // Mapping lives in one IEntityTypeConfiguration class per entity (Ledger/Configuration),
    // rather than one long OnModelCreating.
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);
}
