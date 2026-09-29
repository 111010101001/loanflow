namespace LoanFlow.Core.Applications;

/// <summary>The offer attached to an approved application.</summary>
public sealed class LoanOffer
{
    public decimal AnnualInterestRate { get; init; }
    public decimal MonthlyPayment { get; init; }
    public DateTime ExpiresAtUtc { get; init; }

    /// <summary>
    /// Sequence number of the scheduled ExpireOffer message on Service Bus.
    /// Store it so you can cancel the scheduled message when the offer is accepted (step 2).
    /// </summary>
    public long? ExpirySequenceNumber { get; set; }
}
