using System.CommandLine;
using Beads.Net.Models;

namespace Beads.Cli.Commands;

internal static class FilterCommands
{
    internal static void Register(RootCommand root)
    {
        RegisterReady(root);
        RegisterBlocked(root);
        RegisterSearch(root);
        RegisterCount(root);
        RegisterStale(root);
        RegisterOrphans(root);
    }

    private static void RegisterReady(RootCommand root)
    {
        var cmd = new Command("ready", "Show ready issues");
        Filters.AddTo(cmd);
        cmd.SetAction(pr => Cli.Run(pr, client =>
            Cli.IssueTable(pr, client.Issues.Ready(Filters.Build(pr)))));
        root.Add(cmd);
    }

    private static void RegisterBlocked(RootCommand root)
    {
        var cmd = new Command("blocked", "Show blocked issues");
        Filters.AddTo(cmd);
        cmd.SetAction(pr => Cli.Run(pr, client =>
            Cli.IssueTable(pr, client.Issues.Blocked(Filters.Build(pr)))));
        root.Add(cmd);
    }

    private static void RegisterSearch(RootCommand root)
    {
        var cmd = new Command("search", "Search issues");
        var queryArg = new Argument<string>("query") { Description = "Search query" };
        cmd.Add(queryArg);
        Filters.AddTo(cmd);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var q = pr.GetRequiredValue(queryArg);
            Cli.IssueTable(pr, client.Issues.Search(q, Filters.Build(pr)));
        }));
        root.Add(cmd);
    }

    private static void RegisterCount(RootCommand root)
    {
        var cmd = new Command("count", "Count issues");
        var byOpt = new Option<string?>("--by") { Description = "Group by: status, type, priority, assignee, label" };
        cmd.Add(byOpt);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var result = client.Issues.Count(pr.GetValue(byOpt));
            if (Cli.IsJson(pr))
                Cli.WriteJson(result);
            else
            {
                Console.WriteLine($"Total: {result.Total}");
                foreach (var (k, v) in result.Groups)
                    Console.WriteLine($"  {k}: {v}");
            }
        }));
        root.Add(cmd);
    }

    private static void RegisterStale(RootCommand root)
    {
        var cmd = new Command("stale", "Show stale issues");
        var daysOpt = new Option<int>("--days") { Description = "Days threshold", DefaultValueFactory = _ => 14 };
        cmd.Add(daysOpt);
        cmd.SetAction(pr => Cli.Run(pr, client =>
            Cli.IssueTable(pr, client.Issues.Stale(pr.GetValue(daysOpt)))));
        root.Add(cmd);
    }

    private static void RegisterOrphans(RootCommand root)
    {
        var cmd = new Command("orphans", "Show orphaned issues");
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var orphans = client.Issues.Orphans();
            if (Cli.IsJson(pr))
                Cli.WriteJson(orphans);
            else
                Cli.Table(
                    ["ID", "TYPE", "STATUS", "TITLE"],
                    orphans.Select(i => new[] { i.Id, i.IssueType, i.Status, Cli.Truncate(i.Title, 50) }));
        }));
        root.Add(cmd);
    }
}
