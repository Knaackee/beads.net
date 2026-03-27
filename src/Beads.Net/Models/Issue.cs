using System.Text.Json.Serialization;

namespace Beads.Net.Models;

/// <summary>Core issue entity — full parity with beads_rust Issue struct.</summary>
public sealed record Issue
{
    // Core
    public string Id { get; set; } = "";
    public string? ContentHash { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Design { get; set; } = "";
    public string AcceptanceCriteria { get; set; } = "";
    public string Notes { get; set; } = "";

    // Classification
    public string Status { get; set; } = "open";
    public int Priority { get; set; } = 2;
    public string IssueType { get; set; } = "task";
    public string? Assignee { get; set; }
    public string? Owner { get; set; }
    public int? EstimatedMinutes { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public string? ClosedBySession { get; set; }

    // Scheduling
    public DateTime? DueAt { get; set; }
    public DateTime? DeferUntil { get; set; }

    // External
    public string? ExternalRef { get; set; }
    public string? SourceSystem { get; set; }
    public string SourceRepo { get; set; } = ".";

    // Tombstone
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public string? DeleteReason { get; set; }
    public string? OriginalType { get; set; }

    // Compaction
    public int CompactionLevel { get; set; }
    public DateTime? CompactedAt { get; set; }
    public string? CompactedAtCommit { get; set; }
    public int? OriginalSize { get; set; }

    // Flags
    public string? Sender { get; set; }
    public bool Ephemeral { get; set; }
    public bool Pinned { get; set; }
    public bool IsTemplate { get; set; }

    // Relations (loaded separately, used for display/export)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Labels { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dependency>? Dependencies { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Comment>? Comments { get; set; }

    // Extensions
    public string? ProjectId { get; set; }
    public string? ColumnId { get; set; }
    public int Position { get; set; }

    /// <summary>True if this issue has been soft-deleted.</summary>
    public bool IsTombstone => Status == "tombstone";

    /// <summary>True if status is closed or tombstone.</summary>
    public bool IsTerminal => Enums.IssueStatusExtensions.IsTerminal(Status);

    /// <summary>Checks semantic equality for sync (ignores timestamps, relation order).</summary>
    public bool SyncEquals(Issue other)
    {
        if (other is null) return false;

        return Id == other.Id &&
               Title == other.Title &&
               Description == other.Description &&
               Design == other.Design &&
               AcceptanceCriteria == other.AcceptanceCriteria &&
               Notes == other.Notes &&
               Status == other.Status &&
               Priority == other.Priority &&
               IssueType == other.IssueType &&
               Assignee == other.Assignee &&
               Owner == other.Owner &&
               EstimatedMinutes == other.EstimatedMinutes &&
               CloseReason == other.CloseReason &&
               ExternalRef == other.ExternalRef &&
               SourceSystem == other.SourceSystem &&
               SourceRepo == other.SourceRepo &&
               DeleteReason == other.DeleteReason &&
               OriginalType == other.OriginalType &&
               CompactionLevel == other.CompactionLevel &&
               Sender == other.Sender &&
               Ephemeral == other.Ephemeral &&
               Pinned == other.Pinned &&
               IsTemplate == other.IsTemplate &&
               LabelsEqual(Labels, other.Labels);
    }

    private static bool LabelsEqual(List<string>? a, List<string>? b)
    {
        var setA = new HashSet<string>(a ?? []);
        var setB = new HashSet<string>(b ?? []);
        return setA.SetEquals(setB);
    }

    /// <summary>Check if a tombstone has expired beyond the given retention period.</summary>
    public bool IsExpiredTombstone(TimeSpan retention) =>
        IsTombstone && DeletedAt.HasValue && (DateTime.UtcNow - DeletedAt.Value) > retention;
}
