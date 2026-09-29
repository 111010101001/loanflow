using LoanFlow.Core.Applications;

namespace LoanFlow.Core.Messaging;

// Naming convention used throughout:
//   Commands are imperative ("do this"), have exactly one handler, and go over Service Bus.
//   Events are past tense ("this happened"), have zero or more subscribers, and go over RabbitMQ.
// Keep contracts as plain records: they are serialized to JSON and must stay backward
// compatible once messages are in flight (add fields, don't rename or remove them).

// ---- Commands (Service Bus) ----

/// <summary>Score and decide a newly submitted application.</summary>
/// <param name="SubmissionId">Generated when the form is rendered; used as MessageId so a double-click is deduplicated.</param>
public sealed record ProcessApplication(Guid ApplicationId, Guid SubmissionId);

public sealed record WithdrawApplication(Guid ApplicationId);

public sealed record RecordManualDecision(Guid ApplicationId, bool Approve, string CaseHandler, string? Note);

public sealed record AcceptOffer(Guid ApplicationId);

/// <summary>Scheduled for the offer's expiry time; a no-op if the offer was accepted first.</summary>
public sealed record ExpireOffer(Guid ApplicationId);

// ---- Events (RabbitMQ) ----

public sealed record ApplicationSubmitted(Guid ApplicationId, DateTime OccurredAtUtc);

public sealed record ApplicationDecided(
    Guid ApplicationId,
    ApplicationStatus Outcome,
    string? Reason,
    DateTime OccurredAtUtc);

public sealed record OfferAccepted(Guid ApplicationId, DateTime OccurredAtUtc);

public sealed record OfferExpired(Guid ApplicationId, DateTime OccurredAtUtc);

/// <summary>
/// Published through the transactional outbox (step 4): the loan row and the outbox row
/// are saved in the same SQL Server transaction, and a relay publishes it afterwards.
/// </summary>
public sealed record LoanOpened(
    Guid LoanId,
    Guid ApplicationId,
    decimal Principal,
    decimal MonthlyPayment,
    DateOnly FirstDueDate,
    DateTime OccurredAtUtc);
