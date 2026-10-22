using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using WebAppRabbitMQ.Models;

namespace WebAppRabbitMQ;

public sealed record TaskCompletedMessage(Guid TaskId, string Title, DateTimeOffset CompletedAt, Priority Priority);

public interface IEventPublisher
{ 
    void Publish(TaskCompletedMessage message);
}
 
public sealed class RabbitMqService(IConfiguration config, ILogger<RabbitMqService> log)
    : BackgroundService, IEventPublisher
{
    private const string Exchange = "task.events";
    private const string RoutingKey = "task.completed";
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() } // "priority": "High"
    };

    private readonly Channel<TaskCompletedMessage> _queue = Channel.CreateBounded<TaskCompletedMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    private readonly CancellationTokenSource _drainCts = new();
    private IConnection? _connection;
    private IChannel? _channel;

    #region Публичный API

    public void Publish(TaskCompletedMessage message)
    { 
        if (!_queue.Writer.TryWrite(message))
            log.LogError("RabbitMQ queue is full or closed, message dropped: {@Message}", message);
    }

    #endregion

    #region Фоновый воркер

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    { 
        try
        {
            await foreach (var msg in _queue.Reader.ReadAllAsync(_drainCts.Token))
                await PublishOneAsync(msg, _drainCts.Token);
        }
        catch (OperationCanceledException)
        {
            log.LogWarning("Drain timeout reached, unsent messages are lost");
        }
    }

    private async Task PublishOneAsync(TaskCompletedMessage msg, CancellationToken ct)
    {
        try
        {
            var channel = await GetChannelAsync(ct);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, Json));
            var props = new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent };
             
            await channel.BasicPublishAsync(Exchange, RoutingKey, mandatory: false, basicProperties: props, body: body, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        { 
            log.LogError(ex, "Failed to publish task.completed to RabbitMQ: {@Message}", msg);
            await DisposeChannelAsync();  
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        await DisposeChannelAsync();

        var factory = new ConnectionFactory
        {
            Uri = new Uri(config["RabbitMq:Uri"] ?? "amqp://guest:guest@localhost:5672/"),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(3) 
        };
        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
        await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        return _channel;
    }

    #endregion

    #region Graceful shutdown

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete(); 

        if (ExecuteTask is { } worker)
        {
            try
            {
                var finished = await Task.WhenAny(worker, Task.Delay(DrainTimeout, cancellationToken));
                if (finished != worker) _drainCts.Cancel(); 
            }
            catch (OperationCanceledException) { _drainCts.Cancel(); }
        }

        await base.StopAsync(cancellationToken);
        await DisposeChannelAsync(); 
    }

    private async Task DisposeChannelAsync()
    {
        try { if (_channel is not null) await _channel.CloseAsync(); }
        catch (Exception ex) { log.LogWarning(ex, "Error closing RabbitMQ channel"); }
        try { if (_connection is not null) await _connection.CloseAsync(); }
        catch (Exception ex) { log.LogWarning(ex, "Error closing RabbitMQ connection"); }

        _channel?.Dispose();
        _connection?.Dispose();
        _channel = null;
        _connection = null;
    }

    #endregion
}