using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LoanFlow.Infrastructure.Ledger;

/// <summary>
/// Used only by the dotnet-ef tool (migrations add/remove/list, database update), never by
/// the apps. With it, the tool doesn't have to start the Worker's host to get a context.
///
/// It reads the same user secrets as the apps. "migrations add" never connects to the
/// database, so it works even before you've set the secret; commands that do connect
/// ("database update", "migrations remove") need it.
/// </summary>
public sealed class LedgerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    /// <summary>Must match &lt;UserSecretsId&gt; in LoanFlow.Web.csproj and LoanFlow.Worker.csproj.</summary>
    public const string UserSecretsId = "loanflow-dev";

    private const string Placeholder =
        "Server=localhost,14330;Database=LoanFlowLedger;User Id=sa;Password=not-set;TrustServerCertificate=True";

    public LedgerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(UserSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(LedgerOptions.ConnectionStringName) ?? Placeholder;

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new LedgerDbContext(options);
    }
}
