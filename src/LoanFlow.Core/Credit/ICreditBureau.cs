namespace LoanFlow.Core.Credit;

/// <summary>What the credit bureau knows about an applicant.</summary>
/// <param name="Score">Credit score, 300–850. Higher is better.</param>
/// <param name="ExistingMonthlyDebtPayments">What the applicant already pays per month on other loans.</param>
/// <param name="PaymentRemarks">Number of registered payment defaults (betalningsanmärkningar).</param>
public sealed record CreditReport(int Score, decimal ExistingMonthlyDebtPayments, int PaymentRemarks);

/// <summary>External credit bureau. The only implementation is a deliberately flaky fake.</summary>
public interface ICreditBureau
{
    /// <exception cref="CreditBureauUnavailableException">Transient failure: retrying later may succeed.</exception>
    Task<CreditReport> GetReportAsync(string applicantEmail, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown for transient bureau failures. Handlers should let the message be retried
/// (abandon it) rather than dead-letter it straight away.
/// </summary>
public sealed class CreditBureauUnavailableException(string message) : Exception(message);
