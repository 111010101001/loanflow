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

    public async Task InsertAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        try
        {
            await Applications.InsertOneAsync(application, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // already saved
        }
    }

    public async Task<LoanApplication?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Applications.Find(Builders<LoanApplication>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<LoanApplication>> ListRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await Applications.Find(
            Builders<LoanApplication>.Filter.Empty)
                .SortByDescending(x => x.Id)
                .Limit(limit)
                .ToListAsync(cancellationToken);
    }


    public async Task<bool> TryTransitionAsync(
        Guid id,
        ApplicationStatus expected,
        StatusChange change,
        CancellationToken cancellationToken = default)
    {
        var combinedFilter = Builders<LoanApplication>.Filter.Eq(x => x.Id, id) & Builders<LoanApplication>.Filter.Eq(x => x.Status, expected);
        var result = await Applications.UpdateOneAsync(
            combinedFilter,
            Builders<LoanApplication>.Update.Set(x => x.Status, change.Status)
            .Push(x => x.History, change),
            cancellationToken: cancellationToken
        );
        return result.ModifiedCount == 1;
    }
}
