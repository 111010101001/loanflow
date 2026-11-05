using System;
using System.Collections.Generic;
using System.Text;
using LoanFlow.Core.Applications;
using LoanFlow.Core.Credit;

namespace LoanFlow.Tests
{
    public class CreditDecisionTests
    {
        private static readonly DateTime Now = new(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc);

        private static readonly CreditReport GoodReport = new(Score: 750, ExistingMonthlyDebtPayments: 0m, PaymentRemarks: 0);
        private static LoanApplication Application(decimal amount = 2_000m, int termMonths = 20, decimal income = 10_000m) =>
    LoanApplication.Submit(Guid.CreateVersion7(), "Ada Lovelace", "ada@lovelace.me", amount, termMonths, income, 2_000m, Now);
        private static readonly CreditPolicyOptions Policy = new();

        [Fact]
        public void Good_application_is_approved()
        {
            var decision = CreditDecision.MakeDecision(Application(), GoodReport, Policy, 0m);
            Assert.Equal(ApplicationStatus.Approved, decision.Outcome);
        }
        [Fact]
        public void Reject_no_income()
        {
            var decision = CreditDecision.MakeDecision(Application(income:0m), GoodReport, Policy, 0m);
            Assert.Equal(ApplicationStatus.Rejected, decision.Outcome);
        }

        [Fact]
        public void Reject_no_low_credit_score()
        {
            var decision = CreditDecision.MakeDecision(Application(), GoodReport with { Score = Policy.RejectBelowScore - 1 }, Policy, 0m);
            Assert.Equal(ApplicationStatus.Rejected, decision.Outcome);
        }

        [Fact]
        public void Reject_high_amount()
        {
            var decision = CreditDecision.MakeDecision(Application(amount: Policy.MaxAmount + 1), GoodReport, Policy, 0m);
            Assert.Equal(ApplicationStatus.Rejected, decision.Outcome);
        }

        [Fact]
        public void Debt_ratio_at_limit_is_approved()
        {
            var decision = CreditDecision.MakeDecision(Application(amount: 12000m, termMonths: 12, income: 10000m), GoodReport with { ExistingMonthlyDebtPayments = 3500m }, Policy, 0m);
            Assert.Equal(ApplicationStatus.Approved, decision.Outcome);
        }


        [Fact]
        public void Debt_ratio_over_limit_is_rejected()
        {
            var decision = CreditDecision.MakeDecision(Application(amount: 12000m,termMonths:12,income:10000m), GoodReport with { ExistingMonthlyDebtPayments = 3501m }, Policy, 0m);
            Assert.Equal(ApplicationStatus.Rejected, decision.Outcome);
        }

        [Fact]
        public void Application_sent_to_manual_review()
        {
            var decision = CreditDecision.MakeDecision(Application(), GoodReport with { Score = Policy.ManualReviewBelowScore  - 1 }, Policy, 0m);
            Assert.Equal(ApplicationStatus.ManualReview, decision.Outcome);
        }
    }
}
