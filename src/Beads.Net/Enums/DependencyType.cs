namespace Beads.Net.Enums;

/// <summary>Dependency relationship types between issues.</summary>
public enum DependencyType
{
    // Blocking (affects ready work)
    Blocks,
    ParentChild,
    ConditionalBlocks,
    WaitsFor,
    // Non-blocking
    Related,
    DiscoveredFrom,
    RepliesTo,
    RelatesTo,
    Duplicates,
    Supersedes,
    CausedBy
}

public static class DependencyTypeExtensions
{
    public static string ToDbString(this DependencyType t) => t switch
    {
        DependencyType.Blocks => "blocks",
        DependencyType.ParentChild => "parent-child",
        DependencyType.ConditionalBlocks => "conditional-blocks",
        DependencyType.WaitsFor => "waits-for",
        DependencyType.Related => "related",
        DependencyType.DiscoveredFrom => "discovered-from",
        DependencyType.RepliesTo => "replies-to",
        DependencyType.RelatesTo => "relates-to",
        DependencyType.Duplicates => "duplicates",
        DependencyType.Supersedes => "supersedes",
        DependencyType.CausedBy => "caused-by",
        _ => "blocks"
    };

    public static bool IsBlocking(this DependencyType t) =>
        t is DependencyType.Blocks or DependencyType.ParentChild
            or DependencyType.ConditionalBlocks or DependencyType.WaitsFor;

    public static bool IsBlocking(string type) => type is "blocks" or "parent-child"
        or "conditional-blocks" or "waits-for";

    public static bool AffectsReadyWork(this DependencyType t) => t.IsBlocking();

    public static bool AffectsReadyWork(string type) => IsBlocking(type);
}

public static class DependencyTypeParser
{
    public static (DependencyType? Parsed, string Raw) Parse(string input)
    {
        var normalized = input.Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");
        return normalized switch
        {
            "blocks" => (DependencyType.Blocks, "blocks"),
            "parent-child" or "parentchild" => (DependencyType.ParentChild, "parent-child"),
            "conditional-blocks" or "conditionalblocks" => (DependencyType.ConditionalBlocks, "conditional-blocks"),
            "waits-for" or "waitsfor" => (DependencyType.WaitsFor, "waits-for"),
            "related" => (DependencyType.Related, "related"),
            "discovered-from" or "discoveredfrom" => (DependencyType.DiscoveredFrom, "discovered-from"),
            "replies-to" or "repliesto" => (DependencyType.RepliesTo, "replies-to"),
            "relates-to" or "relatesto" => (DependencyType.RelatesTo, "relates-to"),
            "duplicates" => (DependencyType.Duplicates, "duplicates"),
            "supersedes" => (DependencyType.Supersedes, "supersedes"),
            "caused-by" or "causedby" => (DependencyType.CausedBy, "caused-by"),
            _ => (null, normalized)
        };
    }

    public static string Normalize(string input)
    {
        var (parsed, raw) = Parse(input);
        return parsed.HasValue ? parsed.Value.ToDbString() : raw;
    }
}
