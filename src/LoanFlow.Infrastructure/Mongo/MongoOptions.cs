using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Infrastructure.Mongo;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    /// <summary>Name under ConnectionStrings (the ASP.NET Core convention, also what Aspire injects).</summary>
    public const string ConnectionStringName = "mongodb";

    [Required(ErrorMessage = "Set ConnectionStrings:mongodb, e.g. mongodb://localhost:27017/?serverSelectionTimeoutMS=5000")]
    public string ConnectionString { get; set; } = "";

    [Required]
    public string DatabaseName { get; set; } = "loanflow";
}
