using System.CommandLine;
using Beads.Net.Models;

namespace Beads.Cli.Commands;

internal static class WorkflowCommands
{
    internal static void Register(RootCommand root)
    {
        RegisterDefer(root);
        RegisterUndefer(root);
        RegisterQuery(root);
    }

    private static void RegisterDefer(RootCommand root)
    {
        var cmd = new Command("defer", "Defer issues");
        var idsArg = new Argument<string[]>("ids") { Description = "Issue IDs", Arity = ArgumentArity.OneOrMore };
        var untilOpt = new Option<string?>("--until") { Description = "Defer until date (YYYY-MM-DD)" };
        cmd.Add(idsArg); cmd.Add(untilOpt);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var until = Cli.ParseDate(pr.GetValue(untilOpt));
            foreach (var id in pr.GetRequiredValue(idsArg))
            {
                client.Issues.Defer(id, until);
                if (!Cli.IsQuiet(pr)) Console.WriteLine($"Deferred {id}");
            }
        }));
        root.Add(cmd);
    }

    private static void RegisterUndefer(RootCommand root)
    {
        var cmd = new Command("undefer", "Undefer issues");
        var idsArg = new Argument<string[]>("ids") { Description = "Issue IDs", Arity = ArgumentArity.OneOrMore };
        cmd.Add(idsArg);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            foreach (var id in pr.GetRequiredValue(idsArg))
            {
                client.Issues.Undefer(id);
                if (!Cli.IsQuiet(pr)) Console.WriteLine($"Undeferred {id}");
            }
        }));
        root.Add(cmd);
    }

    private static void RegisterQuery(RootCommand root)
    {
        var query = new Command("query", "Saved queries");

        var saveCmd = new Command("save", "Save a query");
        var saveName = new Argument<string>("name") { Description = "Query name" };
        saveCmd.Add(saveName);
        Filters.AddTo(saveCmd);
        saveCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var name = pr.GetRequiredValue(saveName);
            client.Queries.Save(name, Filters.Build(pr));
            if (!Cli.IsQuiet(pr)) Console.WriteLine($"Query '{name}' saved.");
        }));

        var runCmd = new Command("run", "Run a saved query");
        var runName = new Argument<string>("name") { Description = "Query name" };
        runCmd.Add(runName);
        runCmd.SetAction(pr => Cli.Run(pr, client =>
            Cli.IssueTable(pr, client.Queries.Run(pr.GetRequiredValue(runName)))));

        var listCmd = new Command("list", "List saved queries");
        listCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var names = client.Queries.List();
            if (Cli.IsJson(pr))
                Cli.WriteJson(names);
            else
                foreach (var n in names) Console.WriteLine(n);
        }));

        var delCmd = new Command("delete", "Delete a saved query");
        var delName = new Argument<string>("name") { Description = "Query name" };
        delCmd.Add(delName);
        delCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var name = pr.GetRequiredValue(delName);
            client.Queries.Delete(name);
            if (!Cli.IsQuiet(pr)) Console.WriteLine($"Query '{name}' deleted.");
        }));

        query.Add(saveCmd);
        query.Add(runCmd);
        query.Add(listCmd);
        query.Add(delCmd);
        root.Add(query);
    }
}
