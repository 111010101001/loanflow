using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Core.Credit;

/// <summary>
/// Thresholds for the automatic decision (bound from the "CreditPolicy" section).
/// Registered with ValidateOnStart, so a bad value stops the app at startup
/// instead of failing on the first message.
/// </summary>
public sealed class CreditPolicyOptions
{
    public const string SectionName = "CreditPolicy";

    /// <summary>Below this score: reject.</summary>
    [Range(300, 850)]
    public int RejectBelowScore { get; set; } = 580;

    /// <summary>Between RejectBelowScore and this: send to a case handler.</summary>
    [Range(300, 850)]
    public int ManualReviewBelowScore { get; set; } = 660;

    /// <summary>Max share of monthly income that may go to debt payments, including the new loan.</summary>
    [Range(0.05, 1.0)]
    public double MaxDebtToIncomeRatio { get; set; } = 0.45;

    [Range(1_000, 10_000_000)]
    public int MaxAmount { get; set; } = 500_000;

    /// <summary>
    /// How long an approved offer stays open. Minutes in development: the Service Bus
    /// emulator caps message time-to-live at 1 hour and forgets everything on restart.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:30", "30.00:00:00")]
    public TimeSpan OfferLifetime { get; set; } = TimeSpan.FromMinutes(2);
}
