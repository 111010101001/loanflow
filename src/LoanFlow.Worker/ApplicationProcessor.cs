using Azure.Messaging.ServiceBus;
using LoanFlow.Core.Applications;
using LoanFlow.Core.Credit;
using LoanFlow.Core.Ledger;
using LoanFlow.Core.Messaging;
using Microsoft.Extensions.Options;


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
            case nameof(AcceptOffer):
                {
                    var command = args.Message.Body.ToObjectFromJson<AcceptOffer>();
                    if (command is null)
                    {
                        await args.DeadLetterMessageAsync(args.Message, "InvalidBody", "Body was empty or null");
                        return;
                    }
                    await AcceptAsync(command, args.CancellationToken);

                    await args.CompleteMessageAsync(args.Message);
                }
                break;
            default:
                // dead-letter: unknown subject
                await args.DeadLetterMessageAsync(args.Message, "UnknownSubject", $"unknown subject {args.Message.Subject}");
                break;
        }

    }

    private async Task AcceptAsync(AcceptOffer command, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var repository = scope.ServiceProvider.GetRequiredService<ILoanApplicationRepository>();

        var application = await repository.GetAsync(command.ApplicationId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (application is null)
        {
            logger.LogWarning("Could not find Application {ApplicationId}", command.ApplicationId);
            return;
        }
        if (application.Offer is null)
        {
            logger.LogWarning("Could not find any offer to accept {ApplicationId}", command.ApplicationId);
            return;
        }

        bool result = await repository.TryTransitionAsync(application.Id, ApplicationStatus.Approved, new StatusChange(
            ApplicationStatus.OfferAccepted,
            now,
            Reason: "Offer accepted by user"),
            null,
            cancellationToken);
        if (!result)
        {
            logger.LogWarning("Application {ApplicationId} could not be accepted", command.ApplicationId);
            return;
        }
        if (application.Offer?.ExpirySequenceNumber is long number)
        {
            try
            {
                await sender.CancelScheduledAsync(QueueNames.OfferExpiry, number, cancellationToken);
            }
            catch (ServiceBusException ex)
            {
                logger.LogWarning("Could not cancel offer for Application {ApplicationId} {ex}", command.ApplicationId, ex);
            }
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
        var decision = CreditDecision.MakeDecision(application, report, policy, rate);
        long? sequenceNumber = null;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var offerId = Guid.CreateVersion7();
        if (decision.Outcome == ApplicationStatus.Approved)
        {
            var expire = now + policy.OfferLifetime;
            sequenceNumber = await sender.ScheduleAsync(QueueNames.OfferExpiry, new ExpireOffer(application.Id, offerId), expire, application.Id.ToString(), cancellationToken);
            application.Offer = new LoanOffer()
            {
                ExpiresAtUtc = expire,
                AnnualInterestRate = rate,
                MonthlyPayment = Annuity.MonthlyPayment(application.Amount, rate, application.TermMonths),
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
        
        // TODO(step 3): publish ApplicationDecided with IEventPublisher.
        // TODO(step 4): handling AcceptOffer. Get LedgerDbContext from the same scope, then in ONE
        // SaveChangesAsync add Loan.Open(...) and OutboxMessage.For(RoutingKeys.LoanOpened, new LoanOpened(...), now).
        // Order matters across the two databases: move the application to OfferAccepted in MongoDB
        // first; if SQL then fails, the retried message finds OfferAccepted and must still create
        // the loan. The unique index on Loan.ApplicationId stops that retry from creating two.
        logger.LogInformation(
            "Application {ApplicationId}: outcome {Outcome}, score {Score}, remarks {Remarks}",
            application.Id, decision.Outcome, report.Score, report.PaymentRemarks);
    }
}
