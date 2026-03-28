using System.CommandLine;
using Beads.Net.Models;

namespace Beads.Cli.Commands;

internal static class ProjectCommands
{
    internal static void Register(RootCommand root)
    {
        RegisterProject(root);
        RegisterBoard(root);
    }

    private static void RegisterProject(RootCommand root)
    {
        var proj = new Command("project", "Manage projects");

        var createCmd = new Command("create", "Create a project");
        var nameArg = new Argument<string>("name") { Description = "Project name" };
        var descOpt = new Option<string?>("--description") { Description = "Description" };
        var colorOpt = new Option<string?>("--color") { Description = "Color hex code" };
        var metadataOpt = new Option<string?>("--metadata") { Description = "Metadata JSON object" };
        createCmd.Add(nameArg); createCmd.Add(descOpt); createCmd.Add(colorOpt); createCmd.Add(metadataOpt);
        createCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var p = client.Projects.Create(pr.GetRequiredValue(nameArg), pr.GetValue(descOpt), pr.GetValue(colorOpt), pr.GetValue(metadataOpt));
            if (Cli.IsJson(pr)) Cli.WriteJson(p);
            else Console.WriteLine($"Created project {p.Id}: {p.Name}");
        }));

        var listCmd = new Command("list", "List projects");
        var allOpt = new Option<bool>("--all") { Description = "Include archived" };
        listCmd.Add(allOpt);
        listCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var projects = client.Projects.List(pr.GetValue(allOpt));
            if (Cli.IsJson(pr)) { Cli.WriteJson(projects); return; }
            Cli.Table(
                ["ID", "NAME", "STATUS", "DESCRIPTION"],
                projects.Select(p => new[] { p.Id, p.Name, p.Status, Cli.Truncate(p.Description ?? "", 40) }));
        }));

        var showCmd = new Command("show", "Show project details");
        var showArg = new Argument<string>("id") { Description = "Project ID or name" };
        showCmd.Add(showArg);
        showCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var p = client.Projects.Get(pr.GetRequiredValue(showArg))
                ?? throw new Beads.Net.Errors.BeadsNotFoundException("Project not found.");
            if (Cli.IsJson(pr)) Cli.WriteJson(p);
            else
            {
                Console.WriteLine($"ID:     {p.Id}");
                Console.WriteLine($"Name:   {p.Name}");
                Console.WriteLine($"Status: {p.Status}");
                if (!string.IsNullOrEmpty(p.Description)) Console.WriteLine($"Desc:   {p.Description}");
                if (!string.IsNullOrWhiteSpace(p.Color)) Console.WriteLine($"Color:  {p.Color}");
                if (!string.IsNullOrWhiteSpace(p.Metadata) && p.Metadata != "{}") Console.WriteLine($"Meta:   {p.Metadata}");
                Console.WriteLine($"Created:{p.CreatedAt:yyyy-MM-dd HH:mm}");
            }
        }));

        var updateCmd = new Command("update", "Update a project");
        var updateArg = new Argument<string>("id") { Description = "Project ID or name" };
        var updateNameOpt = new Option<string?>("--name") { Description = "New name" };
        var updateDescOpt = new Option<string?>("--description") { Description = "New description" };
        var updateMetadataOpt = new Option<string?>("--metadata") { Description = "Metadata JSON object" };
        updateCmd.Add(updateArg);
        updateCmd.Add(updateNameOpt);
        updateCmd.Add(updateDescOpt);
        updateCmd.Add(updateMetadataOpt);
        updateCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var p = client.Projects.Update(pr.GetRequiredValue(updateArg), pr.GetValue(updateNameOpt), pr.GetValue(updateDescOpt), pr.GetValue(updateMetadataOpt));
            if (Cli.IsJson(pr)) Cli.WriteJson(p);
            else Console.WriteLine($"Updated project {p.Id}: {p.Name}");
        }));

        var archiveCmd = new Command("archive", "Archive a project");
        var archiveArg = new Argument<string>("id") { Description = "Project ID or name" };
        archiveCmd.Add(archiveArg);
        archiveCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            client.Projects.Archive(pr.GetRequiredValue(archiveArg));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Archived.");
        }));

        var deleteCmd = new Command("delete", "Delete a project");
        var deleteArg = new Argument<string>("id") { Description = "Project ID or name" };
        deleteCmd.Add(deleteArg);
        deleteCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            client.Projects.Delete(pr.GetRequiredValue(deleteArg));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Deleted.");
        }));

        proj.Add(createCmd);
        proj.Add(listCmd);
        proj.Add(showCmd);
        proj.Add(updateCmd);
        proj.Add(archiveCmd);
        proj.Add(deleteCmd);
        root.Add(proj);
    }

    private static void RegisterBoard(RootCommand root)
    {
        var board = new Command("board", "Manage boards");

        var createCmd = new Command("create", "Create a board");
        var projArg = new Argument<string>("project") { Description = "Project ID" };
        var nameArg = new Argument<string>("name") { Description = "Board name" };
        createCmd.Add(projArg); createCmd.Add(nameArg);
        createCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var b = client.Boards.Create(pr.GetRequiredValue(projArg), pr.GetRequiredValue(nameArg));
            if (Cli.IsJson(pr)) Cli.WriteJson(b);
            else Console.WriteLine($"Created board {b.Id}: {b.Name}");
        }));

        var listCmd = new Command("list", "List boards for project");
        var listProj = new Argument<string>("project") { Description = "Project ID" };
        listCmd.Add(listProj);
        listCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var boards = client.Boards.List(pr.GetRequiredValue(listProj));
            if (Cli.IsJson(pr)) { Cli.WriteJson(boards); return; }
            Cli.Table(["ID", "NAME", "POS"], boards.Select(b => new[] { b.Id, b.Name, b.Position.ToString() }));
        }));

        var colsCmd = new Command("columns", "List columns for board");
        var colsBoardArg = new Argument<string>("board") { Description = "Board ID" };
        colsCmd.Add(colsBoardArg);
        colsCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var cols = client.Boards.ListColumns(pr.GetRequiredValue(colsBoardArg));
            if (Cli.IsJson(pr)) { Cli.WriteJson(cols); return; }
            Cli.Table(["ID", "NAME", "POS", "WIP"], cols.Select(c => new[] { c.Id, c.Name, c.Position.ToString(), c.WipLimit?.ToString() ?? "–" }));
        }));

        var addColCmd = new Command("add-column", "Add column to board");
        var addBoardArg = new Argument<string>("board") { Description = "Board ID" };
        var addNameArg = new Argument<string>("name") { Description = "Column name" };
        var wipOpt = new Option<int?>("--wip-limit") { Description = "WIP limit" };
        var colorOpt = new Option<string?>("--color") { Description = "Color" };
        addColCmd.Add(addBoardArg); addColCmd.Add(addNameArg);
        addColCmd.Add(wipOpt); addColCmd.Add(colorOpt);
        addColCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var col = client.Boards.CreateColumn(pr.GetRequiredValue(addBoardArg), pr.GetRequiredValue(addNameArg),
                pr.GetValue(wipOpt), pr.GetValue(colorOpt));
            if (Cli.IsJson(pr)) Cli.WriteJson(col);
            else Console.WriteLine($"Created column {col.Id}: {col.Name}");
        }));

        var moveCmd = new Command("move", "Move issue to column");
        var moveIssueArg = new Argument<string>("issue-id") { Description = "Issue ID" };
        var moveColArg = new Argument<string>("column") { Description = "Column ID" };
        var posOpt = new Option<int?>("--position") { Description = "Position" };
        moveCmd.Add(moveIssueArg); moveCmd.Add(moveColArg); moveCmd.Add(posOpt);
        moveCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        client.Boards.MoveIssue(pr.GetRequiredValue(moveIssueArg), pr.GetRequiredValue(moveColArg), pr.GetValue(posOpt));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Moved.");
        }));

        board.Add(createCmd);
        board.Add(listCmd);
        board.Add(colsCmd);
        board.Add(addColCmd);
        board.Add(moveCmd);
        root.Add(board);
    }
}
