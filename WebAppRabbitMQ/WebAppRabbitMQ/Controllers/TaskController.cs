using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppRabbitMQ.Models;

namespace WebAppRabbitMQ;

[ApiController]  
[Route("tasks")]
public class TasksController(TaskDbContext db, IEventPublisher events) : ControllerBase
{ 
    [HttpPost]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> Create(CreateTaskRequest request)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title!.Trim(),  
            Priority = request.Priority ?? Priority.Medium,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow 
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = task.Id }, TaskResponse.From(task));
    }
     
    [HttpGet]
    [ProducesResponseType<IEnumerable<TaskResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetAll(CancellationToken ct)
    {
        var tasks = await db.Tasks.AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync(ct);
        return Ok(tasks.Select(TaskResponse.From));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetById(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        return task is null ? NotFound() : Ok(TaskResponse.From(task));
    }

    [HttpPut("{id:guid}/complete")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponse>> Complete(Guid id)
    { 
        var task = await db.Tasks.FindAsync(id);
        if (task is null) return NotFound();
        if (task.IsCompleted) return AlreadyCompleted();

        task.IsCompleted = true;
        task.CompletedAt = DateTimeOffset.UtcNow;

        try
        { 
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return AlreadyCompleted();
        }
         
        events.Publish(new TaskCompletedMessage(task.Id, task.Title, task.CompletedAt.Value, task.Priority));

        return Ok(TaskResponse.From(task));
    }

    ///Жёсткое удаление
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await db.Tasks.Where(t => t.Id == id).ExecuteDeleteAsync();
        return deleted == 0 ? NotFound() : NoContent();
    }

    private ActionResult AlreadyCompleted() =>
        Problem(title: "Task is already completed", statusCode: StatusCodes.Status409Conflict);
}