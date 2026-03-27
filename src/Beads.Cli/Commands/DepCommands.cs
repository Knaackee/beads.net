using System.CommandLine;

namespace Beads.Cli.Commands;

internal static class DepCommands
{
    internal static void Register(RootCommand root)
    {
        var dep = new Command("dep", "Manage dependencies");

        var addCmd = new Command("add", "Add dependency");
        var addId = new Argument<string>("id") { Description = "Issue ID" };
        var addDep = new Argument<string>("depends-on") { Description = "Depends on ID" };
        var addType = new Option<string>("--type") { Description = "Dependency type", DefaultValueFactory = _ => "blocks" };
        addCmd.Add(addId); addCmd.Add(addDep); addCmd.Add(addType);
        addCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        client.Dependencies.Add(pr.GetRequiredValue(addId), pr.GetRequiredValue(addDep), pr.GetRequiredValue(addType)!);
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Dependency added.");
        }));

        var rmCmd = new Command("remove", "Remove dependency");
        var rmId = new Argument<string>("id") { Description = "Issue ID" };
        var rmDep = new Argument<string>("depends-on") { Description = "Depends on ID" };
        rmCmd.Add(rmId); rmCmd.Add(rmDep);
        rmCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            client.Dependencies.Remove(pr.GetRequiredValue(rmId), pr.GetRequiredValue(rmDep));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Dependency removed.");
        }));

        var listCmd = new Command("list", "List dependencies");
        var listId = new Argument<string>("id") { Description = "Issue ID" };
        listCmd.Add(listId);
        listCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var deps = client.Dependencies.List(pr.GetRequiredValue(listId));
            if (Cli.IsJson(pr))
                Cli.WriteJson(deps);
            else
                Cli.Table(["ISSUE", "DEPENDS_ON", "TYPE"], deps.Select(d => new[] { d.IssueId, d.DependsOnId, d.DepType }));
        }));

        var treeCmd = new Command("tree", "Show dependency tree");
        var treeId = new Argument<string>("id") { Description = "Issue ID" };
        treeCmd.Add(treeId);
        treeCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var tree = client.Dependencies.Tree(pr.GetRequiredValue(treeId));
            if (Cli.IsJson(pr))
                Cli.WriteJson(tree);
            else
                Console.Write(client.Graph(pr.GetRequiredValue(treeId), "text"));
        }));

        var cyclesCmd = new Command("cycles", "Detect dependency cycles");
        cyclesCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var cycles = client.Dependencies.FindCycles();
            if (Cli.IsJson(pr))
                Cli.WriteJson(cycles);
            else if (cycles.Count == 0)
                Console.WriteLine("No cycles detected.");
            else
                foreach (var cycle in cycles)
                    Console.WriteLine(string.Join(" → ", cycle));
        }));

        dep.Add(addCmd);
        dep.Add(rmCmd);
        dep.Add(listCmd);
        dep.Add(treeCmd);
        dep.Add(cyclesCmd);
        root.Add(dep);
    }
}
