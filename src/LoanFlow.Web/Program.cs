using LoanFlow.Infrastructure;
using LoanFlow.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Same infrastructure registrations as the Worker; lifetimes are explained in
// LoanFlow.Infrastructure/DependencyInjection.cs.
builder.Services
    .AddLoanFlowMongo(builder.Configuration)
    .AddLoanFlowServiceBus(builder.Configuration)
    .AddLoanFlowRabbitMq(builder.Configuration)
    .AddLoanFlowLedger(builder.Configuration)
    .AddLoanFlowHealthChecks();

// Inject TimeProvider instead of calling DateTime.UtcNow, so tests can swap in
// FakeTimeProvider (Microsoft.Extensions.TimeProvider.Testing) and control time.
builder.Services.AddSingleton(TimeProvider.System);

// TODO(step 3): live updates. Register a singleton "hub" that components subscribe to
// (e.g. ApplicationUpdates with an event or a Channel<T>), plus a hosted service that
// consumes RabbitMQ and pushes into it. Each web instance should declare its own
// exclusive, auto-delete queue bound to "#", so every instance sees every event.

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Plain-text health endpoint for scripts and container probes; /status is the human version.
app.MapHealthChecks("/health");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
