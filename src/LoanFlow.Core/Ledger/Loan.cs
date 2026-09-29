namespace LoanFlow.Core.Ledger;

public enum LoanStatus
{
    Active,
    Repaid,
}

/// <summary>
/// A loan that has been paid out, with its repayment schedule.
///
/// Stored in SQL Server with EF Core, while applications stay in MongoDB. The application
/// workflow is document-shaped (one document, embedded history). Money that has been lent
/// out is relational and transactional: a loan, its installments and the outbox message
/// announcing it are written in one database transaction (step 4).
///
/// The loan and its installments form one aggregate. They're always loaded and saved
/// together, and installments only change through the loan (RegisterPayment), which is
/// why they're mapped as an owned collection.
/// </summary>
public sealed class Loan
{
    // For EF Core, which materializes through a parameterless constructor (it may be private).
    private Loan()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The MongoDB application this loan came from. One loan per application.</summary>
    public Guid ApplicationId { get; private set; }

    public string BorrowerName { get; private set; } = "";

    public decimal Principal { get; private set; }

    /// <summary>As a fraction: 0.079 means 7.9 %.</summary>
    public decimal AnnualInterestRate { get; private set; }

    public int TermMonths { get; private set; }

    public decimal MonthlyPayment { get; private set; }

    public LoanStatus Status { get; private set; }

    public DateTime OpenedAtUtc { get; private set; }

    /// <summary>
    /// Updated by every payment. That puts every payment on the loan's own row, so the
    /// loan's concurrency token catches two payments racing each other (step 4).
    /// </summary>
    public DateTime? LastPaymentAtUtc { get; private set; }

    public List<Installment> Installments { get; private set; } = [];

    /// <summary>Becomes a SQL Server rowversion once you configure it in step 4.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public static Loan Open(
        Guid applicationId,
        string borrowerName,
        decimal principal,
        decimal annualInterestRate,
        int termMonths,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(borrowerName);
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Pass a UTC time, e.g. timeProvider.GetUtcNow().UtcDateTime.", nameof(utcNow));
        }

        var payment = Annuity.MonthlyPayment(principal, annualInterestRate, termMonths);
        var monthlyRate = annualInterestRate / 12m;
        var firstDueDate = DateOnly.FromDateTime(utcNow).AddMonths(1);

        var remaining = principal;
        var installments = new List<Installment>(termMonths);
        for (var number = 1; number <= termMonths; number++)
        {
            var interest = Math.Round(remaining * monthlyRate, 2, MidpointRounding.AwayFromZero);

            // Rounding each payment to öre leaves a few öre over or short at the end.
            // The last installment takes exactly what's left, so the principal parts add up.
            var principalPart = number == termMonths ? remaining : payment - interest;

            installments.Add(new Installment(number, firstDueDate.AddMonths(number - 1), principalPart, interest));
            remaining -= principalPart;
        }

        return new Loan
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            BorrowerName = borrowerName.Trim(),
            Principal = principal,
            AnnualInterestRate = annualInterestRate,
            TermMonths = termMonths,
            MonthlyPayment = payment,
            Status = LoanStatus.Active,
            OpenedAtUtc = utcNow,
            Installments = installments,
        };
    }

    public void RegisterPayment(int installmentNumber, DateTime utcNow)
    {
        var installment = Installments.SingleOrDefault(i => i.Number == installmentNumber)
            ?? throw new ArgumentOutOfRangeException(nameof(installmentNumber), $"No installment {installmentNumber}.");

        if (installment.IsPaid)
        {
            throw new InvalidOperationException($"Installment {installmentNumber} is already paid.");
        }

        installment.MarkPaid(utcNow);
        LastPaymentAtUtc = utcNow;

        if (Installments.All(i => i.IsPaid))
        {
            Status = LoanStatus.Repaid;
        }
    }
}
