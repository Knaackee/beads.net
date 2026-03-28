using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Beads.Net;
using Beads.Net.Errors;
using Beads.Net.Models;

namespace Beads.Cli.Commands;

internal static class Cli
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    internal static BeadsClient Client(ParseResult pr)
    {
        var db = pr.GetValue(Globals.Db) ?? ".beads/beads.db";
        var prefix = pr.GetValue(Globals.Prefix);
        var actor = pr.GetValue(Globals.Actor);
        return new BeadsClient(db, cfg =>
        {
            if (prefix != null) cfg.Prefix = prefix;
            if (actor != null) cfg.Actor = actor;
        });
    }

    internal static bool IsJson(ParseResult pr) => pr.GetValue(Globals.Json);
    internal static bool IsQuiet(ParseResult pr) => pr.GetValue(Globals.Quiet);

    internal static void WriteJson<T>(T obj) =>
        Console.WriteLine(JsonSerializer.Serialize(obj, JsonOpts));

    internal static void Table(string[] h, IEnumerable<string[]> rows)
    {
        var all = rows.ToList();
        var w = new int[h.Length];
        for (int i = 0; i < h.Length; i++) w[i] = h[i].Length;
        foreach (var r in all)
            for (int i = 0; i < Math.Min(h.Length, r.Length); i++)
                w[i] = Math.Max(w[i], (r[i] ?? "").Length);

        for (int i = 0; i < h.Length; i++) Console.Write(h[i].PadRight(w[i] + 2));
        Console.WriteLine();
        foreach (var r in all)
        {
            for (int i = 0; i < h.Length; i++)
                Console.Write((i < r.Length ? r[i] ?? "" : "").PadRight(w[i] + 2));
            Console.WriteLine();
        }
    }

    internal static void IssueTable(ParseResult pr, IssueListResult result)
    {
        if (IsJson(pr)) { WriteJson(result); return; }
        Table(
            ["ID", "TYPE", "P", "STATUS", "ASSIGNEE", "TITLE"],
            result.Issues.Select(i => new[]
            {
                i.Id, i.IssueType, $"P{i.Priority}", i.Status,
                i.Assignee ?? "", Truncate(i.Title, 50)
            }));
        if (result.HasMore)
            Console.WriteLine($"({result.Total} total, showing {result.Issues.Count})");
    }

    internal static void IssueDetail(ParseResult pr, Issue issue)
    {
        if (IsJson(pr)) { WriteJson(issue); return; }
        Console.WriteLine($"ID:          {issue.Id}");
        Console.WriteLine($"Title:       {issue.Title}");
        Console.WriteLine($"Status:      {issue.Status}");
        Console.WriteLine($"Type:        {issue.IssueType}");
        Console.WriteLine($"Priority:    P{issue.Priority}");
        if (issue.Assignee != null) Console.WriteLine($"Assignee:    {issue.Assignee}");
        if (issue.Owner != null) Console.WriteLine($"Owner:       {issue.Owner}");
        if (!string.IsNullOrEmpty(issue.Description)) Console.WriteLine($"Description: {issue.Description}");
        if (issue.DueAt.HasValue) Console.WriteLine($"Due:         {issue.DueAt.Value:yyyy-MM-dd}");
        Console.WriteLine($"Created:     {issue.CreatedAt:yyyy-MM-dd HH:mm}");
        Console.WriteLine($"Updated:     {issue.UpdatedAt:yyyy-MM-dd HH:mm}");
        if (issue.Labels?.Count > 0) Console.WriteLine($"Labels:      {string.Join(", ", issue.Labels)}");
        if (!string.IsNullOrWhiteSpace(issue.Metadata) && issue.Metadata != "{}")
            Console.WriteLine($"Metadata:    {issue.Metadata}");
    }

    internal static int Run(ParseResult pr, Action<BeadsClient> action)
    {
        try
        {
            using var client = Client(pr);
            action(client);
            return 0;
        }
        catch (BeadsException ex)
        {
            if (IsJson(pr))
                WriteJson(new { error_code = ex.ExitCode, message = ex.Message, kind = ErrorKind(ex) });
            else
                Console.Error.WriteLine($"Error: {ex.Message}");
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Internal error: {ex.Message}");
            return 1;
        }
    }

    internal static DateTime? ParseDate(string? s) =>
        s != null ? DateTime.Parse(s, CultureInfo.InvariantCulture) : null;

    internal static int? ParsePriority(string? s)
    {
        if (s == null) return null;
        if (s.StartsWith('P') || s.StartsWith('p')) return int.Parse(s[1..], CultureInfo.InvariantCulture);
        return int.Parse(s, CultureInfo.InvariantCulture);
    }

    internal static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string ErrorKind(BeadsException ex) =>
        ex.GetType().Name.Replace("Beads", "").Replace("Exception", "").ToLowerInvariant();
}
