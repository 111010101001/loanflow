using LoanFlow.Core.Ledger;
using LoanFlow.Infrastructure.Ledger;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Tests;

public class LedgerModelTests
{
    // Accessing db.Model builds the EF Core model and validates it (keys, relationships,
    // owned types) without opening a connection, so a broken mapping fails here in
    // milliseconds instead of at the first query.
    [Fact]
    public void Model_builds_and_installments_are_owned_by_the_loan()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlServer("Server=localhost;Database=never-opened")
            .Options;
        using var db = new LedgerDbContext(options);

        var loan = db.Model.FindEntityType(typeof(Loan));
        var installments = loan?.FindNavigation(nameof(Loan.Installments));

        Assert.NotNull(loan);
        Assert.NotNull(installments);
        Assert.True(installments.TargetEntityType.IsOwned());
    }

    // TODO(step 5): once the migrations exist, add a test that fails when the model has
    // changed without a new migration: Assert.False(db.Database.HasPendingModelChanges()).
}
