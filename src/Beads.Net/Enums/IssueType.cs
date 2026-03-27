namespace Beads.Net.Enums;

/// <summary>Issue types. Unknown strings are treated as custom types.</summary>
public enum IssueType
{
    Task,
    Bug,
    Feature,
    Epic,
    Chore,
    Docs,
    Question
}

public static class IssueTypeExtensions
{
    public static string ToDbString(this IssueType t) => t switch
    {
        IssueType.Task => "task",
        IssueType.Bug => "bug",
        IssueType.Feature => "feature",
        IssueType.Epic => "epic",
        IssueType.Chore => "chore",
        IssueType.Docs => "docs",
        IssueType.Question => "question",
        _ => "task"
    };
}

public static class IssueTypeParser
{
    public static (IssueType? Parsed, string Raw) Parse(string input)
    {
        var normalized = input.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "task" => (IssueType.Task, "task"),
            "bug" => (IssueType.Bug, "bug"),
            "feature" => (IssueType.Feature, "feature"),
            "epic" => (IssueType.Epic, "epic"),
            "chore" => (IssueType.Chore, "chore"),
            "docs" or "doc" or "documentation" => (IssueType.Docs, "docs"),
            "question" => (IssueType.Question, "question"),
            _ => (null, normalized) // Custom type
        };
    }

    public static string Normalize(string input)
    {
        var (parsed, raw) = Parse(input);
        return parsed.HasValue ? parsed.Value.ToDbString() : raw;
    }
}
