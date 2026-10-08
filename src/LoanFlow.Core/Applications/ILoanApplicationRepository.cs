namespace LoanFlow.Core.Applications;

/// <summary>
/// Persistence port for applications. The MongoDB implementation lives in
/// LoanFlow.Infrastructure; tests can swap in a fake through DI.
/// </summary>
public interface ILoanApplicationRepository
{
    /// <summary>
    /// Inserts the loan application. If one with the same Id already exists, does nothing, so retrying with the same id is safe.
    /// </summary>
    Task InsertAsync(LoanApplication application, CancellationToken cancellationToken = default);

    Task<LoanApplication?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoanApplication>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically moves an application from <paramref name="expected"/> to <paramref name="change"/>.Status
    /// and appends <paramref name="change"/> to its history.
    /// </summary>
    /// <returns>
    /// False if the application was not in <paramref name="expected"/>, e.g. because another
    /// consumer got there first or this is a redelivered duplicate. That makes handlers idempotent.
    /// </returns>
    Task<bool> TryTransitionAsync(
        Guid id,
        ApplicationStatus expected,
        StatusChange change,
        CancellationToken cancellationToken = default);
}
