using Azure.Messaging.ServiceBus;
using LoanFlow.Core.Applications;
using LoanFlow.Core.Credit;
using LoanFlow.Core.Messaging;

namespace LoanFlow.Worker;

/// <summary>
/// Consumes commands from the session-enabled "loan-applications" queue.
/// </summary>
public sealed class ApplicationProcessor(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient client,
    ILogger<ApplicationProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServiceBusSessionProcessor processor = client.CreateSessionProcessor(QueueNames.LoanApplications, new ServiceBusSessionProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentSessions = 8,
        });

        // handlers
        processor.ProcessMessageAsync += OnMessageAsync;
        processor.ProcessErrorAsync += OnErrorAsync;
        await processor.StartProcessingAsync(stoppingToken);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken); // wait until Ctrl+C
        }
        finally
        {
            await processor.StopProcessingAsync();
            await processor.DisposeAsync();
        }

    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus error in {ErrorSource} on {EntityPath}", args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    private async Task OnMessageAsync(ProcessSessionMessageEventArgs args)
    {

        switch (args.Message.Subject)
        {
            case nameof(ProcessApplication):
                {
                    var command = args.Message.Body.ToObjectFromJson<ProcessApplication>();
                    if (command is null)
                    {
                        await args.DeadLetterMessageAsync(args.Message, "InvalidBody", "Body was empty or null");
                        return;
                    }
                    await HandleAsync(command, args.CancellationToken);
                    await args.CompleteMessageAsync(args.Message);
                }
                break;

            default:
                // dead-letter: unknown subject
                await args.DeadLetterMessageAsync(args.Message, "UnknownSubject", $"unknown subject {args.Message.Subject}");
                break;
        }

    }

    /// <summary>
    /// The per-message scope pattern. Everything resolved from <c>scope.ServiceProvider</c>
    /// lives exactly as long as this message is being handled.
    /// </summary>
    private async Task HandleAsync(ProcessApplication command, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILoanApplicationRepository>();
        var bureau = scope.ServiceProvider.GetRequiredService<ICreditBureau>();

        var application = await repository.GetAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            logger.LogWarning("Application {ApplicationId} not found; nothing to do", command.ApplicationId);
            return;
        }

        var report = await bureau.GetReportAsync(application.Email, cancellationToken);

        // TODO(step 2): decide with CreditPolicyOptions (reject / manual review / approve),
        // then repository.TryTransitionAsync(id, expected: Submitted, change, ...).
        // If it returns false the message is a duplicate or arrived late: log and complete it.
        // On approval also schedule ExpireOffer at now + OfferLifetime and store the sequence number.
        // TODO(step 3): publish ApplicationDecided with IEventPublisher.
        // TODO(step 4): handling AcceptOffer. Get LedgerDbContext from the same scope, then in ONE
        // SaveChangesAsync add Loan.Open(...) and OutboxMessage.For(RoutingKeys.LoanOpened, new LoanOpened(...), now).
        // Order matters across the two databases: move the application to OfferAccepted in MongoDB
        // first; if SQL then fails, the retried message finds OfferAccepted and must still create
        // the loan. The unique index on Loan.ApplicationId stops that retry from creating two.
        logger.LogInformation(
            "Application {ApplicationId}: score {Score}, remarks {Remarks}",
            application.Id, report.Score, report.PaymentRemarks);
    }
}
