namespace Beads.Net.Enums;

/// <summary>Audit event types.</summary>
public enum EventType
{
    Created,
    Updated,
    StatusChanged,
    PriorityChanged,
    AssigneeChanged,
    Commented,
    Closed,
    Reopened,
    DependencyAdded,
    DependencyRemoved,
    LabelAdded,
    LabelRemoved,
    Compacted,
    Deleted,
    Restored
}

public static class EventTypeExtensions
{
    public static string ToDbString(this EventType e) => e switch
    {
        EventType.Created => "created",
        EventType.Updated => "updated",
        EventType.StatusChanged => "status_changed",
        EventType.PriorityChanged => "priority_changed",
        EventType.AssigneeChanged => "assignee_changed",
        EventType.Commented => "commented",
        EventType.Closed => "closed",
        EventType.Reopened => "reopened",
        EventType.DependencyAdded => "dependency_added",
        EventType.DependencyRemoved => "dependency_removed",
        EventType.LabelAdded => "label_added",
        EventType.LabelRemoved => "label_removed",
        EventType.Compacted => "compacted",
        EventType.Deleted => "deleted",
        EventType.Restored => "restored",
        _ => "updated"
    };
}

public static class EventTypeParser
{
    public static (EventType? Parsed, string Raw) Parse(string input)
    {
        var normalized = input.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "created" => (EventType.Created, "created"),
            "updated" => (EventType.Updated, "updated"),
            "status_changed" => (EventType.StatusChanged, "status_changed"),
            "priority_changed" => (EventType.PriorityChanged, "priority_changed"),
            "assignee_changed" => (EventType.AssigneeChanged, "assignee_changed"),
            "commented" => (EventType.Commented, "commented"),
            "closed" => (EventType.Closed, "closed"),
            "reopened" => (EventType.Reopened, "reopened"),
            "dependency_added" => (EventType.DependencyAdded, "dependency_added"),
            "dependency_removed" => (EventType.DependencyRemoved, "dependency_removed"),
            "label_added" => (EventType.LabelAdded, "label_added"),
            "label_removed" => (EventType.LabelRemoved, "label_removed"),
            "compacted" => (EventType.Compacted, "compacted"),
            "deleted" => (EventType.Deleted, "deleted"),
            "restored" => (EventType.Restored, "restored"),
            _ => (null, normalized) // Custom event type
        };
    }
}
