using LoanFlow.Infrastructure;
using LoanFlow.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Same infrastructure registrations as the Web app, plus the credit bits only the Worker uses.
builder.Services
    .AddLoanFlowMongo(builder.Configuration)
    .AddLoanFlowServiceBus(builder.Configuration)
    .AddLoanFlowRabbitMq(builder.Configuration)
    .AddLoanFlowLedger(builder.Configuration)
    .AddLoanFlowCredit(builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);

// Hosted services are singletons, started in registration order when the host starts and
// stopped on Ctrl+C. The migrator goes first so the database is ready before anything uses it.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<LedgerMigrator>();
}

builder.Services.AddHostedService<ApplicationProcessor>();
builder.Services.AddHostedService<OutboxRelay>();

// TODO(step 2): a second processor for the offer-expiry queue (ExpireOffer commands).
// TODO(step 3): hosted services for the notifier and audit consumers (RabbitMQ),
//               or a separate Worker project for them if you want to scale them apart.

var host = builder.Build();
host.Run();
