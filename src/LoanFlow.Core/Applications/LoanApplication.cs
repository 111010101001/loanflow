namespace LoanFlow.Core.Applications;

/// <summary>
/// One loan application = one MongoDB document.
///
/// The decision history and the offer are embedded rather than stored in separate
/// collections, so every status change is a single-document update. MongoDB makes
/// single-document writes atomic, which is what lets TryTransitionAsync in the
/// repository work without multi-document transactions (and without a replica set).
/// </summary>
public sealed class LoanApplication
{
    public Guid Id { get; init; }

    public required string ApplicantName { get; init; }
    public required string Email { get; init; }

    public decimal Amount { get; init; }
    public int TermMonths { get; init; }
    public decimal MonthlyIncome { get; init; }
    public decimal MonthlyExpenses { get; init; }

    public ApplicationStatus Status { get; set; }
    public DateTime SubmittedAtUtc { get; init; }

    public LoanOffer? Offer { get; set; }
    public List<StatusChange> History { get; init; } = [];

    /// <summary>
    /// Creates a new application in <see cref="ApplicationStatus.Submitted"/>.
    /// Validation of the raw form input belongs in the form model (step 1);
    /// this only guards against values that would corrupt the domain.
    /// </summary>
    public static LoanApplication Submit(
        Guid id,
        string applicantName,
        string email,
        decimal amount,
        int termMonths,
        decimal monthlyIncome,
        decimal monthlyExpenses,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(termMonths);
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyIncome);
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyExpenses);
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);

        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Pass a UTC time, e.g. timeProvider.GetUtcNow().UtcDateTime.", nameof(utcNow));
        }

        return new LoanApplication
        {
            Id = id,
            ApplicantName = applicantName.Trim(),
            Email = email.Trim(),
            Amount = amount,
            TermMonths = termMonths,
            MonthlyIncome = monthlyIncome,
            MonthlyExpenses = monthlyExpenses,
            Status = ApplicationStatus.Submitted,
            SubmittedAtUtc = utcNow,
            History = [new StatusChange(ApplicationStatus.Submitted, utcNow, "Application received")],
        };
    }
}
