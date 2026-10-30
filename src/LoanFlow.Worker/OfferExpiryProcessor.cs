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
public sealed class OfferExpiryProcessor(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient client,
    TimeProvider timeProvider,
    ILogger<OfferExpiryProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServiceBusProcessor processor = client.CreateProcessor(QueueNames.OfferExpiry, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false
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

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {

        switch (args.Message.Subject)
        {
            case nameof(ExpireOffer):
                {
                    var command = args.Message.Body.ToObjectFromJson<ExpireOffer>();
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
    private async Task HandleAsync(ExpireOffer command, CancellationToken cancellationToken)
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
        if (application.Offer?.Id != command.OfferId) 
        {
            logger.LogWarning("Application {ApplicationId} and Offer {OfferId} not matching", command.ApplicationId, command.OfferId);
            return;
        }


        var now = timeProvider.GetUtcNow().UtcDateTime;


        bool result = await repository.TryTransitionAsync(application.Id, ApplicationStatus.Approved, new StatusChange(
            ApplicationStatus.OfferExpired,
            now,
            Reason: "Expired"),
            application.Offer,
            cancellationToken);

        if (!result)
        {
            logger.LogWarning("Application {ApplicationId} already accepted; nothing to do", command.ApplicationId);
            return;
        }

    }
}
