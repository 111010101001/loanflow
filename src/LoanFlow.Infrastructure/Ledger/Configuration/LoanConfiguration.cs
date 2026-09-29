using LoanFlow.Core.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanFlow.Infrastructure.Ledger.Configuration;

/// <summary>
/// How Loan and its installments map to tables. This is the minimum for a valid model:
/// keys and the owned collection. The TODOs below are what makes it correct. Do them
/// before your first migration; after that, every model change needs a new migration.
/// </summary>
internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> loan)
    {
        loan.ToTable("Loans");
        loan.HasKey(l => l.Id);
        loan.Property(l => l.BorrowerName).HasMaxLength(200);

        // Owned collection: installments live in their own table, but EF only loads and
        // saves them through their loan. There's no DbSet<Installment>, and you don't need
        // Include() to get them.
        loan.OwnsMany(l => l.Installments, installment =>
        {
            installment.ToTable("Installments");
            installment.WithOwner().HasForeignKey("LoanId");

            // Composite key: the owner's id (a shadow property, not on the class) + the number.
            installment.HasKey("LoanId", nameof(Installment.Number));

            // TODO(step 4): HasPrecision(18, 2) on Principal and Interest.
        });

        // TODO(step 4):
        //  - Money: HasPrecision(18, 2) on Principal and MonthlyPayment, and (9, 6) on
        //    AnnualInterestRate. Without it EF warns and falls back to decimal(18,2), which
        //    would cut a 7.9 % rate (0.079) down to two decimals.
        //  - HasIndex(l => l.ApplicationId).IsUnique(). One loan per application: a duplicate
        //    AcceptOffer then fails with DbUpdateException instead of paying out twice.
        //  - Property(l => l.RowVersion).IsRowVersion(). Optimistic concurrency: when two tabs
        //    register a payment on the same loan, the second SaveChanges throws
        //    DbUpdateConcurrencyException instead of silently overwriting the first.
        //    Definitely before the first migration: SQL Server can't alter an existing column
        //    into a rowversion, so adding it later means dropping and re-adding the column.
        //  - Property(l => l.Status).HasConversion<string>().HasMaxLength(20), so the column
        //    reads "Active" instead of 0.
    }
}
