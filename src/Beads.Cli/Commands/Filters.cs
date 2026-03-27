using System.CommandLine;
using Beads.Net.Models;

namespace Beads.Cli.Commands;

internal static class Filters
{
    internal static readonly Option<string?> Assignee = new("--assignee") { Description = "Filter by assignee" };
    internal static readonly Option<bool> Unassigned = new("--unassigned") { Description = "Only unassigned" };
    internal static readonly Option<string[]> Label = new("--label") { Description = "Filter by label" };
    internal static readonly Option<string?> Type = new("--type") { Description = "Filter by type" };
    internal static readonly Option<string?> Priority = new("--priority") { Description = "Filter by priority (P0-P4)" };
    internal static readonly Option<int> Limit = new("--limit") { Description = "Max results", DefaultValueFactory = _ => 50 };
    internal static readonly Option<int> Offset = new("--offset") { Description = "Offset", DefaultValueFactory = _ => 0 };
    internal static readonly Option<string?> Sort = new("--sort") { Description = "Sort field" };
    internal static readonly Option<bool> Reverse = new("--reverse") { Description = "Reverse sort" };
    internal static readonly Option<bool> IncludeClosed = new("--include-closed") { Description = "Include closed issues" };

    internal static void AddTo(Command cmd)
    {
        cmd.Add(Assignee);
        cmd.Add(Unassigned);
        cmd.Add(Label);
        cmd.Add(Type);
        cmd.Add(Priority);
        cmd.Add(Limit);
        cmd.Add(Offset);
        cmd.Add(Sort);
        cmd.Add(Reverse);
        cmd.Add(IncludeClosed);
    }

    internal static IssueFilter Build(ParseResult pr)
    {
        var labels = pr.GetValue(Label);
        var typeStr = pr.GetValue(Type);
        var prioVal = Cli.ParsePriority(pr.GetValue(Priority));

        return new IssueFilter
        {
            Assignee = pr.GetValue(Assignee),
            Unassigned = pr.GetValue(Unassigned) ? true : null,
            Labels = labels?.Length > 0 ? labels.ToList() : null,
            Types = typeStr != null ? [typeStr] : null,
            Priorities = prioVal.HasValue ? [prioVal.Value] : null,
            Limit = pr.GetValue(Limit),
            Offset = pr.GetValue(Offset),
            SortBy = pr.GetValue(Sort),
            Reverse = pr.GetValue(Reverse),
            IncludeClosed = pr.GetValue(IncludeClosed),
        };
    }
}
