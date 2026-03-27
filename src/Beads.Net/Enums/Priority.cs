namespace Beads.Net.Enums;

/// <summary>Priority levels: 0=Critical, 1=High, 2=Medium (default), 3=Low, 4=Backlog.</summary>
public enum Priority
{
    Critical = 0,
    High = 1,
    Medium = 2,
    Low = 3,
    Backlog = 4
}

public static class PriorityExtensions
{
    public static string ToDisplay(this Priority p) => $"P{(int)p}";

    public static string ToLabel(this Priority p) => p switch
    {
        Priority.Critical => "Critical",
        Priority.High => "High",
        Priority.Medium => "Medium",
        Priority.Low => "Low",
        Priority.Backlog => "Backlog",
        _ => $"P{(int)p}"
    };
}

public static class PriorityParser
{
    public static Priority Parse(string input)
    {
        var s = input.Trim().ToLowerInvariant();

        if (s.StartsWith('p'))
            s = s[1..];

        if (int.TryParse(s, out var n) && n >= 0 && n <= 4)
            return (Priority)n;

        throw new ArgumentException($"Invalid priority: '{input}'. Expected P0-P4, 0-4.");
    }

    public static bool TryParse(string input, out Priority priority)
    {
        try
        {
            priority = Parse(input);
            return true;
        }
        catch
        {
            priority = Priority.Medium;
            return false;
        }
    }
}
