namespace Beads.Net.Models;

public sealed class Event
{
    public long Id { get; set; }
    public string IssueId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Actor { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
