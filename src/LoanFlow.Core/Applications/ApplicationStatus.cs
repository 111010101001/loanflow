namespace LoanFlow.Core.Applications;

/// <summary>
/// Where an application is in its lifecycle. Stored as a string in MongoDB
/// (see MongoConventions), so renaming a member is a data migration.
/// </summary>
public enum ApplicationStatus
{
    Submitted,
    ManualReview,
    Approved,
    Rejected,
    OfferAccepted,
    OfferExpired,
    Withdrawn,
}
