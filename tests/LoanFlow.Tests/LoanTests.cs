using LoanFlow.Core.Ledger;

namespace LoanFlow.Tests;

public class LoanTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc);

    private static Loan OpenLoan(int months = 36) =>
        Loan.Open(Guid.NewGuid(), "Ada Lovelace", 50_000m, 0.079m, months, Now);

    [Fact]
    public void Monthly_payment_matches_the_textbook_example()
    {
        // 100 000 at 12 % a year (1 % a month) over 12 months.
        Assert.Equal(8_884.88m, Annuity.MonthlyPayment(100_000m, 0.12m, 12));
    }

    [Fact]
    public void Zero_interest_splits_the_principal_evenly()
    {
        Assert.Equal(1_000m, Annuity.MonthlyPayment(12_000m, 0m, 12));
    }

    [Fact]
    public void Schedule_has_one_installment_per_month_starting_next_month()
    {
        var loan = OpenLoan();

        Assert.Equal(36, loan.Installments.Count);
        Assert.Equal(new DateOnly(2026, 11, 15), loan.Installments[0].DueDate);
        Assert.Equal(new DateOnly(2029, 10, 15), loan.Installments[^1].DueDate);
    }

    [Fact]
    public void Principal_parts_add_up_to_exactly_the_loan_amount()
    {
        var loan = OpenLoan();

        Assert.Equal(50_000m, loan.Installments.Sum(i => i.Principal));
    }

    [Fact]
    public void Every_installment_but_the_last_is_the_monthly_payment()
    {
        var loan = OpenLoan();

        Assert.All(loan.Installments.SkipLast(1), i => Assert.Equal(loan.MonthlyPayment, i.Amount));
    }

    [Fact]
    public void Paying_every_installment_repays_the_loan()
    {
        var loan = OpenLoan(months: 3);

        foreach (var installment in loan.Installments.ToList())
        {
            Assert.Equal(LoanStatus.Active, loan.Status);
            loan.RegisterPayment(installment.Number, Now);
        }

        Assert.Equal(LoanStatus.Repaid, loan.Status);
    }

    [Fact]
    public void Paying_the_same_installment_twice_throws()
    {
        var loan = OpenLoan();
        loan.RegisterPayment(1, Now);

        Assert.Throws<InvalidOperationException>(() => loan.RegisterPayment(1, Now));
    }
}
