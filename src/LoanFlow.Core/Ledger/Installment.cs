namespace LoanFlow.Core.Ledger;

/// <summary>One monthly payment in a loan's repayment schedule.</summary>
public sealed class Installment
{
    // For EF Core, which materializes through a parameterless constructor (it may be private).
    private Installment()
    {
    }

    internal Installment(int number, DateOnly dueDate, decimal principal, decimal interest)
    {
        Number = number;
        DueDate = dueDate;
        Principal = principal;
        Interest = interest;
    }

    /// <summary>1 for the first payment, TermMonths for the last. Part of the key.</summary>
    public int Number { get; private set; }

    public DateOnly DueDate { get; private set; }

    /// <summary>The part of the payment that reduces the debt.</summary>
    public decimal Principal { get; private set; }

    /// <summary>The part of the payment that is interest.</summary>
    public decimal Interest { get; private set; }

    public DateTime? PaidAtUtc { get; private set; }

    // Computed, getter-only properties aren't mapped by EF Core: no column, no migration.
    public decimal Amount => Principal + Interest;

    public bool IsPaid => PaidAtUtc is not null;

    internal void MarkPaid(DateTime utcNow) => PaidAtUtc = utcNow;
}
