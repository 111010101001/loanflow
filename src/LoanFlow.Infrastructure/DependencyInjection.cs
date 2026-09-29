using Azure.Messaging.ServiceBus;
using LoanFlow.Core.Applications;
using LoanFlow.Core.Credit;
using LoanFlow.Core.Messaging;
using LoanFlow.Infrastructure.Credit;
using LoanFlow.Infrastructure.Ledger;
using LoanFlow.Infrastructure.Mongo;
using LoanFlow.Infrastructure.RabbitMq;
using LoanFlow.Infrastructure.ServiceBus;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LoanFlow.Infrastructure;

/// <summary>
/// The registrations both apps share. Web and Worker call these from Program.cs,
/// so lifetimes are decided once, here.
///
/// Lifetime rules of thumb used below:
///  - Singleton: thread-safe, expensive to create, holds connections (MongoClient,
///    ServiceBusClient, the RabbitMQ connection, the DbContext factory).
///  - Scoped: per unit of work. One HTTP request, one Blazor circuit, or one message
///    in the Worker (where you create the scope yourself). LedgerDbContext is scoped.
///  - Transient: cheap, stateless, not shared.
/// A singleton must never capture a scoped service ("captive dependency"). In Development
/// the host validates this on startup and refuses to run if you get it wrong.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLoanFlowMongo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SectionName))
            .Configure(o => o.ConnectionString =
                configuration.GetConnectionString(MongoOptions.ConnectionStringName) ?? "")
            .ValidateDataAnnotations()
            // Catch a malformed string at startup. Otherwise the first thing to fail is
            // the MongoClient constructor, deep inside whatever happened to resolve it.
            .Validate(
                o => o.ConnectionString.Length == 0
                     || o.ConnectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase)
                     || o.ConnectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase),
                "ConnectionStrings:mongodb must start with mongodb:// or mongodb+srv://")
            .ValidateOnStart();

        MongoConventions.Register();

        // Singleton: MongoClient is thread-safe and owns the connection pool.
        // One per app is the driver's own recommendation.
        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));

        // IMongoDatabase is a lightweight, thread-safe handle; fine to share.
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>()
            .GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.DatabaseName));

        // Scoped on purpose. This repository has no state and could be a singleton,
        // but most real repositories can't (EF Core's DbContext is the classic example),
        // and scoped is what makes the Worker create a scope per message.
        services.AddScoped<ILoanApplicationRepository, MongoLoanApplicationRepository>();

        return services;
    }

    public static IServiceCollection AddLoanFlowServiceBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServiceBusOptions>()
            .Configure(o => o.ConnectionString =
                configuration.GetConnectionString(ServiceBusOptions.ConnectionStringName) ?? "")
            .ValidateDataAnnotations()
            .Validate(
                o => o.ConnectionString.Length == 0
                     || o.ConnectionString.Contains("Endpoint=sb://", StringComparison.OrdinalIgnoreCase),
                "ConnectionStrings:servicebus must contain Endpoint=sb://...")
            .ValidateOnStart();

        // Singleton: the client owns the AMQP connection and is designed to live as long
        // as the app. The emulator allows only 10 concurrent connections by default, so a
        // client created per request fails quickly (and teaches the lesson).
        // The container disposes it (IAsyncDisposable) on shutdown.
        services.AddSingleton(sp =>
            new ServiceBusClient(sp.GetRequiredService<IOptions<ServiceBusOptions>>().Value.ConnectionString));

        services.AddSingleton<ICommandSender, ServiceBusCommandSender>();

        return services;
    }

    public static IServiceCollection AddLoanFlowRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Configure(o => o.ConnectionString =
                configuration.GetConnectionString(RabbitMqOptions.ConnectionStringName) ?? "")
            .ValidateDataAnnotations()
            .Validate(
                o => o.ConnectionString.Length == 0
                     || (Uri.TryCreate(o.ConnectionString, UriKind.Absolute, out var uri)
                         && uri.Scheme is "amqp" or "amqps"),
                "ConnectionStrings:rabbitmq must be an AMQP URI, e.g. amqp://guest:guest@localhost:5673/")
            .ValidateOnStart();

        // Singleton wrapper around the one connection; see RabbitMqConnection for why
        // IConnection itself isn't registered.
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        return services;
    }

    public static IServiceCollection AddLoanFlowCredit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CreditPolicyOptions>()
            .Bind(configuration.GetSection(CreditPolicyOptions.SectionName))
            .ValidateDataAnnotations()
            // Rules that span several properties don't fit attributes; Validate() takes a predicate.
            .Validate(
                o => o.ManualReviewBelowScore >= o.RejectBelowScore,
                "CreditPolicy:ManualReviewBelowScore must be >= CreditPolicy:RejectBelowScore.")
            .ValidateOnStart();

        services.AddOptions<FakeCreditBureauOptions>()
            .Bind(configuration.GetSection(FakeCreditBureauOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ICreditBureau, FakeCreditBureau>();

        return services;
    }

    public static IServiceCollection AddLoanFlowLedger(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LedgerOptions>()
            .Configure(o => o.ConnectionString =
                configuration.GetConnectionString(LedgerOptions.ConnectionStringName) ?? "")
            .ValidateDataAnnotations()
            .Validate(
                o => o.ConnectionString.Length == 0 || IsSqlServerConnectionString(o.ConnectionString),
                "ConnectionStrings:ledger isn't a valid SQL Server connection string.")
            .ValidateOnStart();

        // AddDbContextFactory registers two things:
        //  - IDbContextFactory<LedgerDbContext> as a singleton. Blazor components use this.
        //    In Blazor Server a scoped service lives as long as the circuit (the whole browser
        //    tab), and a DbContext is neither thread-safe nor meant to live that long, so
        //    components create one per operation: await using var db = await DbFactory.CreateDbContextAsync();
        //  - LedgerDbContext itself as scoped. The Worker takes it from its per-message scope,
        //    so "one context per unit of work" falls out of the scope it already creates.
        services.AddDbContextFactory<LedgerDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<IOptions<LedgerOptions>>().Value.ConnectionString));

        return services;
    }

    /// <summary>
    /// One check per piece of infrastructure. Shown on /status in the web app and at /health.
    /// The timeout keeps a dead dependency from hanging the page.
    /// </summary>
    public static IHealthChecksBuilder AddLoanFlowHealthChecks(this IServiceCollection services)
    {
        var timeout = TimeSpan.FromSeconds(5);
        string[] tags = ["infrastructure"];

        return services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("MongoDB", HealthStatus.Unhealthy, tags, timeout)
            .AddCheck<ServiceBusHealthCheck>("Service Bus", HealthStatus.Unhealthy, tags, timeout)
            .AddCheck<RabbitMqHealthCheck>("RabbitMQ", HealthStatus.Unhealthy, tags, timeout)
            .AddCheck<LedgerHealthCheck>("SQL Server (ledger)", HealthStatus.Unhealthy, tags, timeout);
    }

    private static bool IsSqlServerConnectionString(string value)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(value);
            return !string.IsNullOrWhiteSpace(builder.DataSource);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
        {
            return false;
        }
    }
}
