using System.CommandLine;

namespace Beads.Cli.Commands;

internal static class CommentCommands
{
    internal static void Register(RootCommand root)
    {
        var comments = new Command("comments", "Manage comments");

        var addCmd = new Command("add", "Add a comment");
        var addId = new Argument<string>("id") { Description = "Issue ID" };
        var bodyArg = new Argument<string>("body") { Description = "Comment body" };
        addCmd.Add(addId); addCmd.Add(bodyArg);
        addCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var c = client.Comments.Add(pr.GetRequiredValue(addId), pr.GetRequiredValue(bodyArg));
            if (Cli.IsJson(pr))
                Cli.WriteJson(c);
            else if (!Cli.IsQuiet(pr))
                Console.WriteLine($"Comment #{c.Id} added.");
        }));

        var listCmd = new Command("list", "List comments for issue");
        var listId = new Argument<string>("id") { Description = "Issue ID" };
        listCmd.Add(listId);
        listCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var coms = client.Comments.List(pr.GetRequiredValue(listId));
            if (Cli.IsJson(pr))
                Cli.WriteJson(coms);
            else
                foreach (var c in coms)
                    Console.WriteLine($"[{c.CreatedAt:yyyy-MM-dd HH:mm}] {c.Author}: {c.Body}");
        }));

        comments.Add(addCmd);
        comments.Add(listCmd);
        root.Add(comments);
    }
}
