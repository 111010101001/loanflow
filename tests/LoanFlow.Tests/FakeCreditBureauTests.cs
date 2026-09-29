using LoanFlow.Core.Credit;
using LoanFlow.Infrastructure.Credit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LoanFlow.Tests;

public class FakeCreditBureauTests
{
    private static FakeCreditBureau Create(double failureRate) =>
        new(
            Options.Create(new FakeCreditBureauOptions { FailureRate = failureRate, MaxLatencyMs = 0 }),
            NullLogger<FakeCreditBureau>.Instance);

    [Fact]
    public async Task Same_applicant_gets_the_same_report()
    {
        var bureau = Create(failureRate: 0);

        var first = await bureau.GetReportAsync("ada@example.com");
        var second = await bureau.GetReportAsync("  ADA@example.com ");

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Score_is_in_range()
    {
        var bureau = Create(failureRate: 0);

        var report = await bureau.GetReportAsync("grace@example.com");

        Assert.InRange(report.Score, 300, 850);
    }

    [Fact]
    public async Task Failure_rate_1_always_throws_the_transient_exception()
    {
        var bureau = Create(failureRate: 1);

        await Assert.ThrowsAsync<CreditBureauUnavailableException>(() => bureau.GetReportAsync("ada@example.com"));
    }
}
