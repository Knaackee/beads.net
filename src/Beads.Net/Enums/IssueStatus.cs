namespace Beads.Net.Enums;

/// <summary>Issue status. Unknown strings are treated as custom statuses.</summary>
public enum IssueStatus
{
    Open,
    InProgress,
    Blocked,
    Deferred,
    Draft,
    Closed,
    Tombstone,
    Pinned
}

public static class IssueStatusExtensions
{
    public static string ToDbString(this IssueStatus status) => status switch
    {
        IssueStatus.Open => "open",
        IssueStatus.InProgress => "in_progress",
        IssueStatus.Blocked => "blocked",
        IssueStatus.Deferred => "deferred",
        IssueStatus.Draft => "draft",
        IssueStatus.Closed => "closed",
        IssueStatus.Tombstone => "tombstone",
        IssueStatus.Pinned => "pinned",
        _ => "open"
    };

    public static bool IsTerminal(this IssueStatus status) =>
        status is IssueStatus.Closed or IssueStatus.Tombstone;

    public static bool IsActive(this IssueStatus status) =>
        status is IssueStatus.Open or IssueStatus.InProgress;

    public static bool IsTerminal(string status) =>
        status is "closed" or "tombstone";

    public static bool IsActive(string status) =>
        status is "open" or "in_progress";
}

public static class IssueStatusParser
{
    public static (IssueStatus? Parsed, string Raw) Parse(string input)
    {
        var normalized = input.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "open" => (IssueStatus.Open, "open"),
            "in_progress" or "inprogress" => (IssueStatus.InProgress, "in_progress"),
            "blocked" => (IssueStatus.Blocked, "blocked"),
            "deferred" => (IssueStatus.Deferred, "deferred"),
            "draft" => (IssueStatus.Draft, "draft"),
            "closed" => (IssueStatus.Closed, "closed"),
            "tombstone" => (IssueStatus.Tombstone, "tombstone"),
            "pinned" => (IssueStatus.Pinned, "pinned"),
            _ => (null, normalized) // Custom status
        };
    }

    public static string Normalize(string input)
    {
        var (parsed, raw) = Parse(input);
        return parsed.HasValue ? parsed.Value.ToDbString() : raw;
    }
}
