namespace TaskManager.Api.Models;

public class TaskItem
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public AppUser? User { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Category { get; set; } = "Другое";

    public string Priority { get; set; } = "medium";

    public DateTime? DueDate { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
