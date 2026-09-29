using System.ComponentModel.DataAnnotations;
using LoanFlow.Core.Credit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoanFlow.Infrastructure.Credit;

public sealed class FakeCreditBureauOptions
{
    public const string SectionName = "FakeCreditBureau";

    /// <summary>Share of calls that fail with <see cref="CreditBureauUnavailableException"/>. 0.2 = one in five.</summary>
    [Range(0.0, 1.0)]
    public double FailureRate { get; set; } = 0.2;

    /// <summary>Each call waits a random 0..MaxLatencyMs, like a slow external API.</summary>
    [Range(0, 30_000)]
    public int MaxLatencyMs { get; set; } = 1_500;
}

/// <summary>
/// Stands in for a real credit bureau. Deliberately slow and flaky, so retries,
/// lock durations and dead-lettering have something to do in step 2.
///
/// The report itself is deterministic per email address, so a retried message
/// gets the same answer as the attempt that failed.
/// </summary>
public sealed class FakeCreditBureau(
    IOptions<FakeCreditBureauOptions> options,
    ILogger<FakeCreditBureau> logger) : ICreditBureau
{
    public async Task<CreditReport> GetReportAsync(string applicantEmail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantEmail);
        var settings = options.Value;

        if (settings.MaxLatencyMs > 0)
        {
            await Task.Delay(Random.Shared.Next(0, settings.MaxLatencyMs + 1), cancellationToken);
        }

        if (Random.Shared.NextDouble() < settings.FailureRate)
        {
            // No email in the log: it's personal data, and logs get copied around.
            logger.LogWarning("Simulated credit bureau outage");
            throw new CreditBureauUnavailableException("Credit bureau did not respond (simulated).");
        }

        var rng = new Random(StableSeed(applicantEmail));
        var score = rng.Next(450, 851);
        var existingDebt = rng.Next(0, 16) * 500m;
        var remarks = rng.Next(0, 12) == 0 ? 1 : 0;

        return new CreditReport(score, existingDebt, remarks);
    }

    /// <summary>
    /// FNV-1a. string.GetHashCode() is randomized per process in .NET, so it can't be
    /// used for anything that must be stable across restarts.
    /// </summary>
    private static int StableSeed(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value.Trim().ToUpperInvariant())
            {
                hash = (hash ^ c) * 16777619u;
            }

            return (int)hash;
        }
    }
}
