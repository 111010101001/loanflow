namespace LoanFlow.Core.Messaging;

/// <summary>
/// Service Bus queue names. They must match servicebus/Config.json, which is where
/// the emulator creates them (sessions, duplicate detection and retry limits are set there).
/// </summary>
public static class QueueNames
{
    /// <summary>
    /// Session-enabled (SessionId = application id) and duplicate detection on.
    /// Carries ProcessApplication, WithdrawApplication and RecordManualDecision.
    /// </summary>
    public const string LoanApplications = "loan-applications";

    /// <summary>Plain queue for scheduled ExpireOffer messages.</summary>
    public const string OfferExpiry = "offer-expiry";
}

/// <summary>
/// RabbitMQ routing keys on the "loan.events" topic exchange.
/// Bind queues with patterns like "application.*" or "#".
/// </summary>
public static class RoutingKeys
{
    public const string ApplicationSubmitted = "application.submitted";
    public const string ApplicationApproved = "application.approved";
    public const string ApplicationRejected = "application.rejected";
    public const string ApplicationManualReview = "application.manual-review";
    public const string ApplicationWithdrawn = "application.withdrawn";
    public const string OfferAccepted = "offer.accepted";
    public const string OfferExpired = "offer.expired";
    public const string LoanOpened = "loan.opened";
}
