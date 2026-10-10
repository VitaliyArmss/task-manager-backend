using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Models;

namespace TaskManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TasksController(AppDbContext db) : ControllerBase
{
    private static readonly string[] AllowedPriorities = ["low", "medium", "high"];

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? priority,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        IQueryable<TaskItem> query = db.Tasks.AsNoTracking().Where(x => x.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, $"%{term}%") ||
                (x.Description != null && EF.Functions.ILike(x.Description, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Category == category);

        if (!string.IsNullOrWhiteSpace(priority) && !priority.Equals("all", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Priority == priority);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => !x.IsCompleted);
            else if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => x.IsCompleted);
            else if (status.Equals("overdue", StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => !x.IsCompleted && x.DueDate != null && x.DueDate < DateTime.UtcNow);
        }

        query = sort?.ToLowerInvariant() switch
        {
            "duedate" => query.OrderBy(x => x.DueDate == null).ThenBy(x => x.DueDate).ThenByDescending(x => x.CreatedAtUtc),
            "priority" => query.OrderByDescending(x => x.Priority == "high")
                                .ThenByDescending(x => x.Priority == "medium")
                                .ThenByDescending(x => x.CreatedAtUtc),
            "title" => query.OrderBy(x => x.Title),
            _ => query.OrderBy(x => x.IsCompleted).ThenBy(x => x.DueDate == null).ThenBy(x => x.DueDate).ThenByDescending(x => x.CreatedAtUtc)
        };

        return Ok(await query.ToListAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskItem>> GetById(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var task = await db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, ct);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpGet("stats")]
    public async Task<ActionResult> GetStats(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var now = DateTime.UtcNow;
        var userTasks = db.Tasks.Where(x => x.UserId == userId.Value);
        var total = await userTasks.CountAsync(ct);
        var completed = await userTasks.CountAsync(x => x.IsCompleted, ct);
        var active = total - completed;
        var overdue = await userTasks.CountAsync(x => !x.IsCompleted && x.DueDate != null && x.DueDate < now, ct);
        var highPriority = await userTasks.CountAsync(x => !x.IsCompleted && x.Priority == "high", ct);

        return Ok(new
        {
            total,
            active,
            completed,
            overdue,
            highPriority,
            completionRate = total == 0 ? 0 : Math.Round(completed * 100.0 / total, 1)
        });
    }

    public record TaskRequest(
        string Title,
        string? Description,
        string? Category,
        string? Priority,
        DateTime? DueDate,
        bool IsCompleted = false);

    [HttpPost]
    public async Task<ActionResult<TaskItem>> Create([FromBody] TaskRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var validation = Validate(request);
        if (validation is not null) return BadRequest(new { message = validation });

        var now = DateTime.UtcNow;
        var completed = request.IsCompleted;
        var task = new TaskItem
        {
            UserId = userId.Value,
            Title = request.Title.Trim(),
            Description = NormalizeDescription(request.Description),
            Category = NormalizeCategory(request.Category),
            Priority = NormalizePriority(request.Priority),
            DueDate = NormalizeDueDate(request.DueDate),
            IsCompleted = completed,
            CompletedAtUtc = completed ? now : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TaskItem>> Update(int id, [FromBody] TaskRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, ct);
        if (task is null) return NotFound();

        var validation = Validate(request);
        if (validation is not null) return BadRequest(new { message = validation });

        var wasCompleted = task.IsCompleted;
        task.Title = request.Title.Trim();
        task.Description = NormalizeDescription(request.Description);
        task.Category = NormalizeCategory(request.Category);
        task.Priority = NormalizePriority(request.Priority);
        task.DueDate = NormalizeDueDate(request.DueDate);
        task.IsCompleted = request.IsCompleted;
        task.CompletedAtUtc = !wasCompleted && request.IsCompleted
            ? DateTime.UtcNow
            : request.IsCompleted ? task.CompletedAtUtc : null;
        task.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(task);
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<ActionResult<TaskItem>> Toggle(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, ct);
        if (task is null) return NotFound();

        task.IsCompleted = !task.IsCompleted;
        task.CompletedAtUtc = task.IsCompleted ? DateTime.UtcNow : null;
        task.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(task);
    }

    [HttpDelete("completed")]
    public async Task<IActionResult> DeleteCompleted(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var completed = await db.Tasks.Where(x => x.UserId == userId.Value && x.IsCompleted).ToListAsync(ct);
        if (completed.Count == 0) return NoContent();

        db.Tasks.RemoveRange(completed);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, ct);
        if (task is null) return NotFound();

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    private static string? Validate(TaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return "Название задачи обязательно.";
        if (request.Title.Trim().Length > 200) return "Название задачи должно быть не длиннее 200 символов.";
        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 2000)
            return "Описание должно быть не длиннее 2000 символов.";
        if (!string.IsNullOrWhiteSpace(request.Priority) && !AllowedPriorities.Contains(request.Priority.Trim().ToLowerInvariant()))
            return "Недопустимый приоритет.";
        return null;
    }

    private static string? NormalizeDescription(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeCategory(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Другое" : value.Trim();

    private static string NormalizePriority(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "medium" : value.Trim().ToLowerInvariant();

    private static DateTime? NormalizeDueDate(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc) : null;
}
