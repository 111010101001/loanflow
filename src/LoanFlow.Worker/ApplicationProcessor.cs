using Azure.Messaging.ServiceBus;
using LoanFlow.Core.Applications;
using LoanFlow.Core.Credit;
using LoanFlow.Core.Ledger;
using LoanFlow.Core.Messaging;
using Microsoft.Extensions.Options;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace LoanFlow.Worker;

/// <summary>
/// Consumes commands from the session-enabled "loan-applications" queue.
/// </summary>
public sealed class ApplicationProcessor(
    IServiceScopeFactory scopeFactory,
    ICommandSender sender,
    ServiceBusClient client,
    IOptions<CreditPolicyOptions> options,
    TimeProvider timeProvider,
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
        var policy = options.Value;
        var rate = (decimal)policy.AnnualInterestRate;
        var decision = CreditDecision.MakeDecision(application, report, policy, (decimal)policy.AnnualInterestRate);
        long? sequenceNumber = null;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (decision.Outcome == ApplicationStatus.Approved)
        {
            var expire = now + policy.OfferLifetime;
            sequenceNumber = await sender.ScheduleAsync(QueueNames.OfferExpiry, new ExpireOffer(application.Id), expire, application.Id.ToString(), cancellationToken);
            application.Offer = new LoanOffer()
            {
                ExpiresAtUtc = expire,
                AnnualInterestRate = (decimal)policy.AnnualInterestRate,
                MonthlyPayment = Annuity.MonthlyPayment(application.Amount, (decimal)policy.AnnualInterestRate, application.TermMonths),
                ExpirySequenceNumber = sequenceNumber
            };

        }

        bool result = await repository.TryTransitionAsync(application.Id, ApplicationStatus.Submitted, new StatusChange(
            decision.Outcome,
            now,
            Reason: decision.Reason),
            application.Offer,
            cancellationToken);

        if (!result)
        {
            logger.LogWarning("Application {ApplicationId} duplicate or arrived late; nothing to do", command.ApplicationId);
            if (sequenceNumber is long number)
                await sender.CancelScheduledAsync(QueueNames.OfferExpiry, number, cancellationToken);
            return;
        }
        

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
