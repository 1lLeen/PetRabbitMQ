namespace WebAppRabbitMQ.Models;

public enum Priority
{
    Low = 1,
    Medium = 2,
    High = 3
}
public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } // не null, не пустой, максимум 200
    public bool IsCompleted { get; set; } 
    public Priority Priority { get; set; } 
    public string Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; } // когда завершили
}

