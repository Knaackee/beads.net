namespace Beads.Net.Models;

public sealed class CreateIssueOptions
{
    public string? ProjectId { get; set; }
    public string? ParentId { get; set; }
    public string? IssueType { get; set; }
    public int? Priority { get; set; }
    public string? Description { get; set; }
    public string? Design { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public string? Notes { get; set; }
    public string? Assignee { get; set; }
    public string? Owner { get; set; }
    public int? EstimatedMinutes { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? DeferUntil { get; set; }
    public string? ExternalRef { get; set; }
    public string? Status { get; set; }
    public bool Ephemeral { get; set; }
    public bool Pinned { get; set; }
    public bool IsTemplate { get; set; }
    public bool DryRun { get; set; }
    public List<string>? Labels { get; set; }
    public List<string>? DependsOn { get; set; }
}

public sealed class UpdateIssueOptions
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Design { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
    public int? Priority { get; set; }
    public string? IssueType { get; set; }
    public string? Assignee { get; set; }
    public string? Owner { get; set; }
    public int? EstimatedMinutes { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? DeferUntil { get; set; }
    public string? ExternalRef { get; set; }
    public bool? Pinned { get; set; }
    public bool? Ephemeral { get; set; }
    public bool? IsTemplate { get; set; }
    public string? ProjectId { get; set; }
    public string? ColumnId { get; set; }
    /// <summary>If true, atomically assign + set in_progress.</summary>
    public bool Claim { get; set; }
    public List<string>? AddLabels { get; set; }
    public List<string>? RemoveLabels { get; set; }
}

public sealed class CloseOptions
{
    public string? Reason { get; set; }
    public bool Force { get; set; }
    public bool SuggestNext { get; set; }
    public string? Session { get; set; }
}

public sealed class DeleteOptions
{
    public string? Reason { get; set; }
    public bool Force { get; set; }
}

public sealed record IssueFilter
{
    public string? ProjectId { get; init; }
    public List<string>? Statuses { get; init; }
    public List<string>? Types { get; init; }
    public List<int>? Priorities { get; init; }
    public int? PriorityMin { get; init; }
    public int? PriorityMax { get; init; }
    public string? Assignee { get; init; }
    public bool? Unassigned { get; init; }
    public List<string>? Ids { get; init; }
    public List<string>? Labels { get; init; }
    public List<string>? LabelsAny { get; init; }
    public string? TitleContains { get; init; }
    public string? DescContains { get; init; }
    public bool IncludeClosed { get; init; }
    public bool IncludeDeferred { get; init; }
    public bool? Overdue { get; init; }
    public bool ExcludeTemplates { get; init; } = true;
    public int Limit { get; init; } = 50;
    public int Offset { get; init; }
    public string? SortBy { get; init; }
    public bool Reverse { get; init; }
}

public sealed record IssueListResult
{
    public List<Issue> Issues { get; init; } = [];
    public int Total { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
    public bool HasMore { get; init; }
}

public sealed class CountResult
{
    public int Total { get; set; }
    public Dictionary<string, int> Groups { get; set; } = new();
}

public sealed class DependencyTree
{
    public DependencyNode Root { get; set; } = new();
}

public sealed class DependencyNode
{
    public string IssueId { get; set; } = "";
    public List<DependencyNode> Children { get; set; } = [];
}

public sealed class FlushResult
{
    public int ExportedCount { get; set; }
    public string OutputPath { get; set; } = "";
    public int Skipped { get; set; }
}

public sealed class ImportResult
{
    public int ImportedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class SyncStatus
{
    public string DbPath { get; set; } = "";
    public string JsonlPath { get; set; } = "";
    public DateTime? DbModified { get; set; }
    public DateTime? JsonlModified { get; set; }
    public int DbIssueCount { get; set; }
    public int JsonlIssueCount { get; set; }
    public int DirtyCount { get; set; }
    public string Status { get; set; } = "";
}

public sealed class FlushOptions
{
    public string? OutputPath { get; set; }
    public bool AllowExternalJsonl { get; set; }
    public bool Manifest { get; set; }
    public string ErrorPolicy { get; set; } = "strict";
}

public sealed class ImportOptions
{
    public string? InputPath { get; set; }
    public bool AllowExternalJsonl { get; set; }
    public bool Force { get; set; }
    public string? RenamePrefix { get; set; }
    public string OrphanPolicy { get; set; } = "strict";
}

public sealed class HistoryEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public long Size { get; set; }
}

public sealed class ProjectStats
{
    public int TotalIssues { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> ByType { get; set; } = new();
    public Dictionary<string, int> ByPriority { get; set; } = new();
    public Dictionary<string, int> ByAssignee { get; set; } = new();
    public int OpenCount { get; set; }
    public int ClosedCount { get; set; }
    public int BlockedCount { get; set; }
    public int OverdueCount { get; set; }
    public DateTime? LastActivity { get; set; }
}

public sealed class DoctorReport
{
    public int SchemaVersion { get; set; }
    public bool SchemaOk { get; set; }
    public List<string> OrphanedSubtasks { get; set; } = [];
    public List<string> OrphanedDependencies { get; set; } = [];
    public List<List<string>> DependencyCycles { get; set; } = [];
    public int DirtyCount { get; set; }
    public bool ForeignKeyIntegrity { get; set; }
    public string JournalMode { get; set; } = "";
    public List<string> Warnings { get; set; } = [];
}

public sealed class LintResult
{
    public int TotalChecked { get; set; }
    public List<LintWarning> Warnings { get; set; } = [];
    public bool IsClean => Warnings.Count == 0;
}

public sealed class LintWarning
{
    public string IssueId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
}
