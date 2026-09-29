namespace LoanFlow.Core.Applications;

/// <summary>One entry in an application's embedded history.</summary>
/// <param name="Status">The status the application moved to.</param>
/// <param name="AtUtc">When it happened (UTC).</param>
/// <param name="Reason">Short human-readable reason, e.g. "Score 712 above threshold".</param>
/// <param name="MessageId">The message that caused the change, if any. Handy when checking idempotency.</param>
public sealed record StatusChange(
    ApplicationStatus Status,
    DateTime AtUtc,
    string? Reason = null,
    string? MessageId = null);
