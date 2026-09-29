namespace LoanFlow.Core.Ledger;

/// <summary>
/// Annuity maths: every monthly payment is the same size, and the interest part shrinks
/// as the principal is paid down. Used for the offer (step 2) and the schedule (step 4).
/// </summary>
public static class Annuity
{
    /// <summary>
    /// The fixed monthly payment, rounded to whole öre (2 decimals).
    /// Payment = P · r · f / (f − 1), where r = annual rate / 12 and f = (1 + r)^months.
    /// </summary>
    /// <param name="principal">Amount borrowed.</param>
    /// <param name="annualInterestRate">As a fraction: 0.079 means 7.9 %.</param>
    /// <param name="months">Term in months.</param>
    public static decimal MonthlyPayment(decimal principal, decimal annualInterestRate, int months)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(principal);
        ArgumentOutOfRangeException.ThrowIfNegative(annualInterestRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(months);

        if (annualInterestRate == 0m)
        {
            return Math.Round(principal / months, 2, MidpointRounding.AwayFromZero);
        }

        // decimal has no Pow, and money shouldn't go through double, so multiply it out.
        var monthlyRate = annualInterestRate / 12m;
        var factor = 1m;
        for (var i = 0; i < months; i++)
        {
            factor *= 1m + monthlyRate;
        }

        var payment = principal * monthlyRate * factor / (factor - 1m);
        return Math.Round(payment, 2, MidpointRounding.AwayFromZero);
    }
}
