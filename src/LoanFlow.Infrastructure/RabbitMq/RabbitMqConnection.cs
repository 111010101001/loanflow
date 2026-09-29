using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LoanFlow.Infrastructure.RabbitMq;

/// <summary>
/// Owns the app's single RabbitMQ connection. Registered as a singleton.
///
/// Why a wrapper instead of registering IConnection directly: RabbitMQ.Client 7 only
/// creates connections asynchronously (CreateConnectionAsync), and DI factories are
/// synchronous. Blocking on it in a factory (.GetAwaiter().GetResult()) works but stalls
/// startup and risks deadlocks, so the connection is opened lazily on first use instead.
///
/// One connection per app, many channels: connections are expensive (TCP + handshake),
/// channels are cheap. Give each consumer its own channel.
/// </summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger)
    : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null)
            {
                return _connection;
            }

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                Uri = new Uri(settings.ConnectionString),
                ClientProvidedName = settings.ClientName,

                // Both default to true; spelled out because they matter. After a network
                // blip the client reconnects and re-declares exchanges, queues, bindings and
                // consumers. While that happens IsOpen is false, which is why this class
                // never replaces an existing connection itself.
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            // If the broker is down this throws, _connection stays null, and the next call retries.
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            logger.LogInformation("Connected to RabbitMQ at {Endpoint}", _connection.Endpoint);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
