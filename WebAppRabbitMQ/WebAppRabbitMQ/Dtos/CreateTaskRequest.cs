using System.ComponentModel.DataAnnotations;
using WebAppRabbitMQ.Models;

namespace WebAppRabbitMQ;

public class CreateTaskRequest
{
   
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, ErrorMessage = "Title must be at most 200 characters")]
    public string? Title { get; set; }
     
    [EnumDataType(typeof(Priority), ErrorMessage = "Unknown priority")]
    public Priority? Priority { get; set; }
}

public record TaskResponse(
    Guid Id,
    string Title,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    Priority Priority)
{
    public static TaskResponse From(TaskItem t) =>
        new(t.Id, t.Title, t.IsCompleted, t.CreatedAt, t.CompletedAt, t.Priority);
}