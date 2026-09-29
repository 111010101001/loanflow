using LoanFlow.Core.Applications;
using LoanFlow.Core.Credit;
using LoanFlow.Core.Messaging;

namespace LoanFlow.Worker;

/// <summary>
/// Consumes commands from the session-enabled "loan-applications" queue.
///
/// Until step 2 it only logs that it's a stub, but the DI shape is already here:
/// hosted services are singletons, while ILoanApplicationRepository is scoped. Injecting
/// the repository into this constructor would be a captive dependency (one repository
/// for the lifetime of the app), and the host refuses to start in Development if you try.
/// Instead, each message gets its own scope; see HandleAsync.
/// </summary>
public sealed class ApplicationProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<ApplicationProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO(step 2): replace this with a ServiceBusSessionProcessor.
        //
        //  - Inject ServiceBusClient (singleton) and create the processor:
        //      client.CreateSessionProcessor(QueueNames.LoanApplications, new ServiceBusSessionProcessorOptions
        //      {
        //          AutoCompleteMessages = false,   // complete only after the work succeeded
        //          MaxConcurrentSessions = 8,      // 8 applications in parallel, each strictly in order
        //      });
        //  - processor.ProcessMessageAsync += ...; processor.ProcessErrorAsync += ...;
        //    await processor.StartProcessingAsync(stoppingToken);
        //    then wait until stoppingToken fires, and StopProcessingAsync + DisposeAsync.
        //  - In the message handler: look at args.Message.Subject to pick the contract type,
        //    deserialize with args.Message.Body.ToObjectFromJson<T>(), call HandleAsync, then:
        //      success                          -> args.CompleteMessageAsync(args.Message)
        //      CreditBureauUnavailableException -> args.AbandonMessageAsync(args.Message)
        //                                          (redelivered until MaxDeliveryCount, then dead-lettered)
        //      anything unexpected / bad data   -> args.DeadLetterMessageAsync(args.Message, reason, description)
        logger.LogInformation(
            "ApplicationProcessor is a stub until step 2. Queue: {Queue}", QueueNames.LoanApplications);

        await Task.Delay(Timeout.Infinite, stoppingToken);
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
