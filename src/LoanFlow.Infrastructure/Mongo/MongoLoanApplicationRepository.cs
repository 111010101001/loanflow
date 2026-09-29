using LoanFlow.Core.Applications;
using MongoDB.Driver;

namespace LoanFlow.Infrastructure.Mongo;

/// <summary>
/// MongoDB implementation of <see cref="ILoanApplicationRepository"/>.
/// Registered as scoped in DependencyInjection.AddLoanFlowMongo.
/// </summary>
public sealed class MongoLoanApplicationRepository(IMongoDatabase database) : ILoanApplicationRepository
{
    public const string CollectionName = "applications";

    // IMongoCollection is thread-safe and cheap to get; no need to cache it anywhere else.
    private IMongoCollection<LoanApplication> Applications { get; } =
        database.GetCollection<LoanApplication>(CollectionName);

    // TODO(step 1): InsertOneAsync.
    public Task InsertAsync(LoanApplication application, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Step 1: insert the application document.");

    // TODO(step 1): Find by id. Builders<LoanApplication>.Filter.Eq(a => a.Id, id), then FirstOrDefaultAsync.
    public Task<LoanApplication?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Step 1: load one application.");

    // TODO(step 1): newest first. Guid v7 ids are time-ordered, so sorting by _id works;
    // sorting by SubmittedAtUtc reads more clearly. Add an index on whichever you sort by.
    public Task<IReadOnlyList<LoanApplication>> ListRecentAsync(int limit, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Step 1: list recent applications.");

    // TODO(step 1): the important one. A single UpdateOneAsync whose FILTER includes the
    // expected status:
    //   filter: Id == id AND Status == expected
    //   update: Set(Status, change.Status) + Push(History, change)
    // ModifiedCount == 1 means you won; 0 means the status had already moved on
    // (duplicate delivery, or a competing command). No read-then-write race, no transaction.
    public Task<bool> TryTransitionAsync(
        Guid id,
        ApplicationStatus expected,
        StatusChange change,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Step 1: conditional status update.");
}
