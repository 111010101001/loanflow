using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using LoanFlow.Core.Applications;
using LoanFlow.Core.Ledger;
using static System.Net.Mime.MediaTypeNames;

namespace LoanFlow.Core.Credit
{
    public sealed record CreditDecision
    {
        public string Reason { get; init; }
        public ApplicationStatus Outcome { get; init; }
        public CreditDecision(ApplicationStatus outcome, string reason)
        {
            this.Outcome = outcome;
            this.Reason = reason;
        }
        private static CreditDecision Reject(string reason) => new(ApplicationStatus.Rejected, reason);
        public static CreditDecision MakeDecision(LoanApplication loanApplication, 
            CreditReport creditReport, 
            CreditPolicyOptions creditPolicyOptions,
            decimal rate)
        {
            if (creditReport.PaymentRemarks > 0)
            {
                return Reject($"{creditReport.PaymentRemarks} PaymentRemarks");
            }
            if (loanApplication.Amount > creditPolicyOptions.MaxAmount)
            {
                return Reject($"{loanApplication.Amount} above maximum {creditPolicyOptions.MaxAmount}");
            }
            if (creditPolicyOptions.RejectBelowScore > creditReport.Score)
            {
                return Reject("low credit score");
            }
            if (loanApplication.MonthlyIncome <= 0)
            {
                return Reject("no income");
            }
            if (((Annuity.MonthlyPayment(loanApplication.Amount, rate, loanApplication.TermMonths) + creditReport.ExistingMonthlyDebtPayments) / loanApplication.MonthlyIncome) > (decimal)creditPolicyOptions.MaxDebtToIncomeRatio)
            {
                return Reject("to much debt");
            }
            if (creditPolicyOptions.ManualReviewBelowScore > creditReport.Score)
            {
                return new CreditDecision(ApplicationStatus.ManualReview, "manual");
            }
            return new CreditDecision(ApplicationStatus.Approved, "pass");
        }


    }
}
