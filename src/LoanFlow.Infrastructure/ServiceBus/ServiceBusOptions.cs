using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Infrastructure.ServiceBus;

public sealed class ServiceBusOptions
{
    /// <summary>Name under ConnectionStrings.</summary>
    public const string ConnectionStringName = "servicebus";

    /// <summary>
    /// For the local emulator:
    /// Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
    /// The key is a fixed, documented value; it is not a secret.
    /// </summary>
    [Required(ErrorMessage = "Set ConnectionStrings:servicebus (see appsettings.Development.json).")]
    public string ConnectionString { get; set; } = "";
}
