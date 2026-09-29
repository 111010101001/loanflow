using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Infrastructure.RabbitMq;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>Name under ConnectionStrings.</summary>
    public const string ConnectionStringName = "rabbitmq";

    /// <summary>
    /// AMQP URI, e.g. amqp://guest:guest@localhost:5673/
    /// Port 5673 on the host because the Service Bus emulator already listens on 5672.
    /// </summary>
    [Required(ErrorMessage = "Set ConnectionStrings:rabbitmq, e.g. amqp://guest:guest@localhost:5673/")]
    public string ConnectionString { get; set; } = "";

    /// <summary>Topic exchange that all LoanFlow events are published to.</summary>
    [Required]
    public string Exchange { get; set; } = "loan.events";

    /// <summary>Shows up in the RabbitMQ management UI under Connections, so you can tell apps apart.</summary>
    public string ClientName { get; set; } = "loanflow";
}
