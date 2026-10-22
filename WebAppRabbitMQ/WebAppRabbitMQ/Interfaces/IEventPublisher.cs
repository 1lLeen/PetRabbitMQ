using WebAppRabbitMQ.Models;

namespace WebAppRabbitMQ.Interfaces;

public sealed record TaskCompletedMessage(Guid TaskItem, string Title, DateTimeOffset CompletedAt, Priority Priority);
public interface IEventPublisher
{
    void Publish(TaskCompletedMessage message);
}
