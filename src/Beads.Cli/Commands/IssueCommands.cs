using System.CommandLine;
using Beads.Net;
using Beads.Net.Models;

namespace Beads.Cli.Commands;

internal static class IssueCommands
{
    internal static void Register(RootCommand root)
    {
        RegisterInit(root);
        RegisterCreate(root);
        RegisterQuick(root);
        RegisterShow(root);
        RegisterList(root);
        RegisterUpdate(root);
        RegisterClose(root);
        RegisterReopen(root);
        RegisterDelete(root);
    }

    private static void RegisterInit(RootCommand root)
    {
        var cmd = new Command("init", "Initialize a new beads workspace");
        var force = new Option<bool>("--force") { Description = "Overwrite existing database" };
        cmd.Add(force);
        cmd.SetAction(pr =>
        {
            try
            {
                var db = pr.GetValue(Globals.Db);
                var prefix = pr.GetValue(Globals.Prefix);
                using var client = BeadsClient.Init(db, prefix, pr.GetValue(force));
                if (Cli.IsJson(pr))
                    Cli.WriteJson(new { path = client.WhereDb(), status = "initialized" });
                else
                    Console.WriteLine($"Initialized at {client.WhereDb()}");
                return 0;
            }
            catch (Exception ex) when (ex is Beads.Net.Errors.BeadsException bex)
            {
                Console.Error.WriteLine($"Error: {bex.Message}");
                return bex.ExitCode;
            }
        });
        root.Add(cmd);
    }

    private static void RegisterCreate(RootCommand root)
    {
        var cmd = new Command("create", "Create a new issue");
        var titleArg = new Argument<string>("title") { Description = "Issue title" };
        var typeOpt = new Option<string?>("--type") { Description = "Issue type" };
        var prioOpt = new Option<string?>("--priority") { Description = "Priority (P0-P4)" };
        var descOpt = new Option<string?>("--description") { Description = "Description" };
        var designOpt = new Option<string?>("--design") { Description = "Design notes" };
        var acOpt = new Option<string?>("--acceptance-criteria") { Description = "Acceptance criteria" };
        var notesOpt = new Option<string?>("--notes") { Description = "Notes" };
        var assigneeOpt = new Option<string?>("--assignee") { Description = "Assignee" };
        var ownerOpt = new Option<string?>("--owner") { Description = "Owner" };
        var estimateOpt = new Option<int?>("--estimate") { Description = "Estimated minutes" };
        var dueOpt = new Option<string?>("--due") { Description = "Due date (YYYY-MM-DD)" };
        var deferOpt = new Option<string?>("--defer") { Description = "Defer until (YYYY-MM-DD)" };
        var extRefOpt = new Option<string?>("--external-ref") { Description = "External reference" };
        var labelOpt = new Option<string[]>("--label") { Description = "Labels" };
        var depsOpt = new Option<string[]>("--deps") { Description = "Dependencies (type:id)" };
        var parentOpt = new Option<string?>("--parent") { Description = "Parent issue ID" };
        var projectOpt = new Option<string?>("--project") { Description = "Project ID or name" };
        var ephemeralOpt = new Option<bool>("--ephemeral") { Description = "Ephemeral issue" };
        var pinnedOpt = new Option<bool>("--pinned") { Description = "Pin issue" };
        var templateOpt = new Option<bool>("--template") { Description = "Mark as template" };
        var metadataOpt = new Option<string?>("--metadata") { Description = "Metadata JSON object" };
        var dryRunOpt = new Option<bool>("--dry-run") { Description = "Dry run" };
        var silentOpt = new Option<bool>("--silent") { Description = "Only output ID" };

        cmd.Add(titleArg);
        foreach (var o in new Option[] { typeOpt, prioOpt, descOpt, designOpt, acOpt, notesOpt,
            assigneeOpt, ownerOpt, estimateOpt, dueOpt, deferOpt, extRefOpt, labelOpt, depsOpt,
            parentOpt, projectOpt, ephemeralOpt, pinnedOpt, templateOpt, metadataOpt, dryRunOpt, silentOpt })
            cmd.Add(o);

        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var opts = new CreateIssueOptions
            {
                IssueType = pr.GetValue(typeOpt),
                Priority = Cli.ParsePriority(pr.GetValue(prioOpt)),
                Description = pr.GetValue(descOpt),
                Design = pr.GetValue(designOpt),
                AcceptanceCriteria = pr.GetValue(acOpt),
                Notes = pr.GetValue(notesOpt),
                Assignee = pr.GetValue(assigneeOpt),
                Owner = pr.GetValue(ownerOpt),
                EstimatedMinutes = pr.GetValue(estimateOpt),
                DueAt = Cli.ParseDate(pr.GetValue(dueOpt)),
                DeferUntil = Cli.ParseDate(pr.GetValue(deferOpt)),
                ExternalRef = pr.GetValue(extRefOpt),
                Labels = pr.GetValue(labelOpt)?.ToList(),
                DependsOn = pr.GetValue(depsOpt)?.ToList(),
                ParentId = pr.GetValue(parentOpt),
                ProjectId = pr.GetValue(projectOpt),
                Ephemeral = pr.GetValue(ephemeralOpt),
                Pinned = pr.GetValue(pinnedOpt),
                IsTemplate = pr.GetValue(templateOpt),
                Metadata = pr.GetValue(metadataOpt),
                DryRun = pr.GetValue(dryRunOpt),
            };
            var issue = client.Issues.Create(pr.GetRequiredValue(titleArg), opts);
            if (pr.GetValue(silentOpt))
                Console.WriteLine(issue.Id);
            else if (Cli.IsJson(pr))
                Cli.WriteJson(issue);
            else
                Console.WriteLine($"Created {issue.Id}: {issue.Title}");
        }));
        root.Add(cmd);
    }

    private static void RegisterQuick(RootCommand root)
    {
        var cmd = new Command("q", "Quick capture — create issue, print only ID");
        var titleArg = new Argument<string>("title") { Description = "Issue title" };
        cmd.Add(titleArg);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var id = client.Issues.Quick(pr.GetRequiredValue(titleArg));
            Console.WriteLine(id);
        }));
        root.Add(cmd);
    }

    private static void RegisterShow(RootCommand root)
    {
        var cmd = new Command("show", "Show issue details");
        var idsArg = new Argument<string[]>("ids") { Description = "Issue IDs", Arity = ArgumentArity.OneOrMore };
        cmd.Add(idsArg);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var ids = pr.GetRequiredValue(idsArg);
            if (Cli.IsJson(pr))
            {
                var issues = ids.Select(id => client.Issues.GetOrThrow(id)).ToList();
                Cli.WriteJson(issues);
            }
            else
            {
                foreach (var id in ids)
                {
                    if (ids.Length > 1) Console.WriteLine(new string('─', 40));
                    Cli.IssueDetail(pr, client.Issues.GetOrThrow(id));
                }
            }
        }));
        root.Add(cmd);
    }

    private static void RegisterList(RootCommand root)
    {
        var cmd = new Command("list", "List issues");
        Filters.AddTo(cmd);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var result = client.Issues.List(Filters.Build(pr));
            Cli.IssueTable(pr, result);
        }));
        root.Add(cmd);
    }

    private static void RegisterUpdate(RootCommand root)
    {
        var cmd = new Command("update", "Update issues");
        var idsArg = new Argument<string[]>("ids") { Description = "Issue IDs", Arity = ArgumentArity.OneOrMore };
        var titleOpt = new Option<string?>("--title") { Description = "New title" };
        var descOpt = new Option<string?>("--description") { Description = "Description" };
        var designOpt = new Option<string?>("--design") { Description = "Design notes" };
        var acOpt = new Option<string?>("--acceptance-criteria") { Description = "Acceptance criteria" };
        var notesOpt = new Option<string?>("--notes") { Description = "Notes" };
        var statusOpt = new Option<string?>("--status") { Description = "Status" };
        var prioOpt = new Option<string?>("--priority") { Description = "Priority (P0-P4)" };
        var typeOpt = new Option<string?>("--type") { Description = "Issue type" };
        var assigneeOpt = new Option<string?>("--assignee") { Description = "Assignee" };
        var ownerOpt = new Option<string?>("--owner") { Description = "Owner" };
        var estimateOpt = new Option<int?>("--estimate") { Description = "Estimated minutes" };
        var dueOpt = new Option<string?>("--due") { Description = "Due date (YYYY-MM-DD)" };
        var deferOpt = new Option<string?>("--defer") { Description = "Defer until (YYYY-MM-DD)" };
        var extRefOpt = new Option<string?>("--external-ref") { Description = "External reference" };
        var projectOpt = new Option<string?>("--project") { Description = "Project ID" };
        var columnOpt = new Option<string?>("--column") { Description = "Column ID" };
        var metadataOpt = new Option<string?>("--metadata") { Description = "Metadata JSON object" };
        var claimOpt = new Option<bool>("--claim") { Description = "Claim (assign to self)" };
        var addLabelOpt = new Option<string[]>("--add-label") { Description = "Add labels" };
        var removeLabelOpt = new Option<string[]>("--remove-label") { Description = "Remove labels" };

        cmd.Add(idsArg);
        foreach (var o in new Option[] { titleOpt, descOpt, designOpt, acOpt, notesOpt, statusOpt,
            prioOpt, typeOpt, assigneeOpt, ownerOpt, estimateOpt, dueOpt, deferOpt, extRefOpt,
            projectOpt, columnOpt, metadataOpt, claimOpt, addLabelOpt, removeLabelOpt })
            cmd.Add(o);

        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var opts = new UpdateIssueOptions
            {
                Title = pr.GetValue(titleOpt),
                Description = pr.GetValue(descOpt),
                Design = pr.GetValue(designOpt),
                AcceptanceCriteria = pr.GetValue(acOpt),
                Notes = pr.GetValue(notesOpt),
                Status = pr.GetValue(statusOpt),
                Priority = Cli.ParsePriority(pr.GetValue(prioOpt)),
                IssueType = pr.GetValue(typeOpt),
                Assignee = pr.GetValue(assigneeOpt),
                Owner = pr.GetValue(ownerOpt),
                EstimatedMinutes = pr.GetValue(estimateOpt),
                DueAt = Cli.ParseDate(pr.GetValue(dueOpt)),
                DeferUntil = Cli.ParseDate(pr.GetValue(deferOpt)),
                ExternalRef = pr.GetValue(extRefOpt),
                ProjectId = pr.GetValue(projectOpt),
                ColumnId = pr.GetValue(columnOpt),
                Metadata = pr.GetValue(metadataOpt),
                Claim = pr.GetValue(claimOpt),
                AddLabels = pr.GetValue(addLabelOpt)?.ToList(),
                RemoveLabels = pr.GetValue(removeLabelOpt)?.ToList(),
            };
            foreach (var id in pr.GetRequiredValue(idsArg))
            {
                var issue = client.Issues.Update(id, opts);
                if (Cli.IsJson(pr))
                    Cli.WriteJson(issue);
                else if (!Cli.IsQuiet(pr))
                    Console.WriteLine($"Updated {issue.Id}: {issue.Title}");
            }
        }));
        root.Add(cmd);
    }

    private static void RegisterClose(RootCommand root)
    {
        var cmd = new Command("close", "Close issues");
        var idsArg = new Argument<string[]>("ids") { Description = "Issue IDs", Arity = ArgumentArity.OneOrMore };
        var reasonOpt = new Option<string?>("--reason") { Description = "Close reason" };
        var forceOpt = new Option<bool>("--force") { Description = "Force close" };
        var suggestOpt = new Option<bool>("--suggest-next") { Description = "Suggest next issue" };
        cmd.Add(idsArg);
        cmd.Add(reasonOpt); cmd.Add(forceOpt); cmd.Add(suggestOpt);

        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var opts = new CloseOptions
            {
                Reason = pr.GetValue(reasonOpt),
                Force = pr.GetValue(forceOpt),
                SuggestNext = pr.GetValue(suggestOpt),
            };
            foreach (var id in pr.GetRequiredValue(idsArg))
            {
                var issue = client.Issues.Close(id, opts);
                if (!Cli.IsQuiet(pr))
                    Console.WriteLine($"Closed {issue.Id}: {issue.Title}");
            }
            if (pr.GetValue(suggestOpt))
            {
                var next = client.Issues.Ready(new IssueFilter { Limit = 1 });
                if (next.Issues.Count > 0)
                    Console.WriteLine($"Next: {next.Issues[0].Id} — {next.Issues[0].Title}");
            }
        }));
        root.Add(cmd);
    }

    private static void RegisterReopen(RootCommand root)
    {
        var cmd = new Command("reopen", "Reopen issues");
        var idsArg = new Argument<string[]>("ids") { Description = "Issue IDs", Arity = ArgumentArity.OneOrMore };
        cmd.Add(idsArg);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            foreach (var id in pr.GetRequiredValue(idsArg))
            {
                var issue = client.Issues.Reopen(id);
                if (!Cli.IsQuiet(pr))
                    Console.WriteLine($"Reopened {issue.Id}: {issue.Title}");
            }
        }));
        root.Add(cmd);
    }

    private static void RegisterDelete(RootCommand root)
    {
        var cmd = new Command("delete", "Delete issues");
        var idsArg = new Argument<string[]>("ids") { Description = "Issue IDs", Arity = ArgumentArity.OneOrMore };
        var reasonOpt = new Option<string?>("--reason") { Description = "Delete reason" };
        var forceOpt = new Option<bool>("--force") { Description = "Force delete" };
        cmd.Add(idsArg);
        cmd.Add(reasonOpt); cmd.Add(forceOpt);

        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var opts = new DeleteOptions
            {
                Reason = pr.GetValue(reasonOpt),
                Force = pr.GetValue(forceOpt),
            };
            foreach (var id in pr.GetRequiredValue(idsArg))
            {
                client.Issues.Delete(id, opts);
                if (!Cli.IsQuiet(pr))
                    Console.WriteLine($"Deleted {id}");
            }
        }));
        root.Add(cmd);
    }
}
