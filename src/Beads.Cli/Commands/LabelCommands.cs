using System.CommandLine;

namespace Beads.Cli.Commands;

internal static class LabelCommands
{
    internal static void Register(RootCommand root)
    {
        var label = new Command("label", "Manage labels");

        var addCmd = new Command("add", "Add labels to issue");
        var addId = new Argument<string>("id") { Description = "Issue ID" };
        var addLabels = new Argument<string[]>("labels") { Description = "Labels to add", Arity = ArgumentArity.OneOrMore };
        addCmd.Add(addId); addCmd.Add(addLabels);
        addCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        client.Labels.Add(pr.GetRequiredValue(addId), pr.GetRequiredValue(addLabels));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Labels added.");
        }));

        var rmCmd = new Command("remove", "Remove labels from issue");
        var rmId = new Argument<string>("id") { Description = "Issue ID" };
        var rmLabels = new Argument<string[]>("labels") { Description = "Labels to remove", Arity = ArgumentArity.OneOrMore };
        rmCmd.Add(rmId); rmCmd.Add(rmLabels);
        rmCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        client.Labels.Remove(pr.GetRequiredValue(rmId), pr.GetRequiredValue(rmLabels));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Labels removed.");
        }));

        var listCmd = new Command("list", "List labels for issue");
        var listId = new Argument<string?>("id") { Description = "Issue ID (omit for all labels)", DefaultValueFactory = _ => null };
        listCmd.Add(listId);
        listCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var id = pr.GetValue(listId);
            var labels = id != null ? client.Labels.List(id) : client.Labels.List();
            if (Cli.IsJson(pr))
                Cli.WriteJson(labels);
            else
                foreach (var l in labels) Console.WriteLine(l);
        }));

        var listAllCmd = new Command("list-all", "List all labels across issues");
        listAllCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var labels = client.Labels.ListAll();
            if (Cli.IsJson(pr))
                Cli.WriteJson(labels);
            else
                foreach (var l in labels) Console.WriteLine(l);
        }));

        var renameCmd = new Command("rename", "Rename a label");
        var oldArg = new Argument<string>("old") { Description = "Old label name" };
        var newArg = new Argument<string>("new") { Description = "New label name" };
        renameCmd.Add(oldArg); renameCmd.Add(newArg);
        renameCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        client.Labels.Rename(pr.GetRequiredValue(oldArg), pr.GetRequiredValue(newArg));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Label renamed.");
        }));

        label.Add(addCmd);
        label.Add(rmCmd);
        label.Add(listCmd);
        label.Add(listAllCmd);
        label.Add(renameCmd);
        root.Add(label);
    }
}
