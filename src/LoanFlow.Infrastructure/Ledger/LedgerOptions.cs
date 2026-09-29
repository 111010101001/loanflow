using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Infrastructure.Ledger;

public sealed class LedgerOptions
{
    /// <summary>Name under ConnectionStrings.</summary>
    public const string ConnectionStringName = "ledger";

    /// <summary>
    /// Holds the SQL Server password, so it lives in user secrets rather than in
    /// appsettings (which ends up on GitHub). See README, step 0.
    /// </summary>
    [Required(ErrorMessage =
        "Set ConnectionStrings:ledger with dotnet user-secrets (README, step 0). It contains the SQL Server password, so it isn't in appsettings.")]
    public string ConnectionString { get; set; } = "";
}
