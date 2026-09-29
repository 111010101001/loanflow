using LoanFlow.Core.Applications;

namespace LoanFlow.Tests;

public class LoanApplicationTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Submit_starts_in_Submitted_with_one_history_entry()
    {
        var application = LoanApplication.Submit("Ada Lovelace", "ada@example.com", 50_000m, 36, 42_000m, 18_000m, Now);

        Assert.Equal(ApplicationStatus.Submitted, application.Status);
        Assert.Equal(Now, application.SubmittedAtUtc);
        var entry = Assert.Single(application.History);
        Assert.Equal(ApplicationStatus.Submitted, entry.Status);
    }

    [Fact]
    public void Submit_trims_name_and_email()
    {
        var application = LoanApplication.Submit("  Ada  ", " ada@example.com ", 50_000m, 36, 42_000m, 18_000m, Now);

        Assert.Equal("Ada", application.ApplicantName);
        Assert.Equal("ada@example.com", application.Email);
    }

    [Fact]
    public void Submit_rejects_local_time()
    {
        var local = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            LoanApplication.Submit("Ada", "ada@example.com", 50_000m, 36, 42_000m, 18_000m, local));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Submit_rejects_non_positive_amount(int amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoanApplication.Submit("Ada", "ada@example.com", amount, 36, 42_000m, 18_000m, Now));
    }

    // TODO(step 5): tests for your decision rules (reject / manual review / approve) and
    // for offer expiry using FakeTimeProvider from Microsoft.Extensions.TimeProvider.Testing.
}
