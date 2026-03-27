using System.CommandLine;
using Beads.Net;

namespace Beads.Cli.Commands;

internal static class DiagCommands
{
    internal static void Register(RootCommand root)
    {
        RegisterStats(root);
        RegisterDoctor(root);
        RegisterVersion(root);
        RegisterInfo(root);
        RegisterWhere(root);
        RegisterAudit(root);
        RegisterHistory(root);
        RegisterChangelog(root);
        RegisterLint(root);
        RegisterGraph(root);
    }

    private static void RegisterStats(RootCommand root)
    {
        var cmd = new Command("stats", "Show project statistics");
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var s = client.Stats.GetStats();
            if (Cli.IsJson(pr)) { Cli.WriteJson(s); return; }
            Console.WriteLine($"Total: {s.TotalIssues}  Open: {s.OpenCount}  Closed: {s.ClosedCount}  Blocked: {s.BlockedCount}  Overdue: {s.OverdueCount}");
            if (s.ByStatus.Count > 0) { Console.WriteLine("By Status:"); foreach (var (k, v) in s.ByStatus) Console.WriteLine($"  {k}: {v}"); }
            if (s.ByType.Count > 0) { Console.WriteLine("By Type:"); foreach (var (k, v) in s.ByType) Console.WriteLine($"  {k}: {v}"); }
            if (s.ByPriority.Count > 0) { Console.WriteLine("By Priority:"); foreach (var (k, v) in s.ByPriority) Console.WriteLine($"  {k}: {v}"); }
        }));
        root.Add(cmd);
    }

    private static void RegisterDoctor(RootCommand root)
    {
        var cmd = new Command("doctor", "Run diagnostics");
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var r = client.Doctor.Run();
            if (Cli.IsJson(pr)) { Cli.WriteJson(r); return; }
            Console.WriteLine($"Schema:  v{r.SchemaVersion} ({(r.SchemaOk ? "OK" : "MISMATCH")})");
            Console.WriteLine($"Journal: {r.JournalMode}");
            Console.WriteLine($"FK:      {(r.ForeignKeyIntegrity ? "OK" : "ERRORS")}");
            Console.WriteLine($"Dirty:   {r.DirtyCount}");
            if (r.OrphanedSubtasks.Count > 0) Console.WriteLine($"Orphaned subtasks: {string.Join(", ", r.OrphanedSubtasks)}");
            if (r.OrphanedDependencies.Count > 0) Console.WriteLine($"Orphaned deps: {string.Join(", ", r.OrphanedDependencies)}");
            if (r.DependencyCycles.Count > 0) Console.WriteLine($"Cycles: {r.DependencyCycles.Count}");
            foreach (var w in r.Warnings) Console.WriteLine($"  ⚠ {w}");
            Console.WriteLine(r.Warnings.Count == 0 ? "All checks passed." : $"{r.Warnings.Count} warning(s).");
        }));
        root.Add(cmd);
    }

    private static void RegisterVersion(RootCommand root)
    {
        var cmd = new Command("version", "Show version");
        cmd.SetAction(pr =>
        {
            var v = typeof(BeadsClient).Assembly.GetName().Version;
            if (Cli.IsJson(pr))
                Cli.WriteJson(new { version = v?.ToString() ?? "0.0.0" });
            else
                Console.WriteLine($"beads {v?.ToString() ?? "0.0.0"}");
        });
        root.Add(cmd);
    }

    private static void RegisterInfo(RootCommand root)
    {
        var cmd = new Command("info", "Show workspace info");
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var i = client.GetInfo();
            if (Cli.IsJson(pr))
                Cli.WriteJson(i);
            else
            {
                Console.WriteLine($"DB:      {i.DbPath}");
                Console.WriteLine($"Prefix:  {i.Prefix}");
                Console.WriteLine($"ID Pfx:  {i.IdPrefix}");
                Console.WriteLine($"Actor:   {i.Actor}");
                Console.WriteLine($"Schema:  v{i.SchemaVersion}");
            }
        }));
        root.Add(cmd);
    }

    private static void RegisterWhere(RootCommand root)
    {
        var cmd = new Command("where", "Show database path");
        cmd.SetAction(pr => Cli.Run(pr, client =>
            Console.WriteLine(client.WhereDb())));
        root.Add(cmd);
    }

    private static void RegisterAudit(RootCommand root)
    {
        var cmd = new Command("audit", "Show audit trail (events)");
        var issueOpt = new Option<string?>("--issue") { Description = "Filter by issue ID" };
        var typeOpt = new Option<string?>("--type") { Description = "Filter by event type" };
        cmd.Add(issueOpt); cmd.Add(typeOpt);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var events = client.Events.List(pr.GetValue(issueOpt), pr.GetValue(typeOpt));
            if (Cli.IsJson(pr))
                Cli.WriteJson(events);
            else
                Cli.Table(
                    ["TIME", "ISSUE", "EVENT", "OLD", "NEW"],
                    events.Select(e => new[]
                    {
                        e.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        e.IssueId, e.EventType,
                        e.OldValue ?? "", e.NewValue ?? ""
                    }));
        }));
        root.Add(cmd);
    }

    private static void RegisterHistory(RootCommand root)
    {
        var history = new Command("history", "Backup history");

        var listCmd = new Command("list", "List backups");
        listCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var entries = client.Sync.HistoryList();
            if (Cli.IsJson(pr))
                Cli.WriteJson(entries);
            else
                Cli.Table(
                    ["NAME", "DATE", "SIZE"],
                    entries.Select(e => new[] { e.Name, e.CreatedAt.ToString("yyyy-MM-dd HH:mm"), $"{e.Size:N0}" }));
        }));

        var restoreCmd = new Command("restore", "Restore a backup");
        var backupArg = new Argument<string>("backup") { Description = "Backup name" };
        restoreCmd.Add(backupArg);
        restoreCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            client.Sync.HistoryRestore(pr.GetRequiredValue(backupArg));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Restored.");
        }));

        history.Add(listCmd);
        history.Add(restoreCmd);
        root.Add(history);
    }

    private static void RegisterChangelog(RootCommand root)
    {
        var cmd = new Command("changelog", "Generate changelog");
        var sinceOpt = new Option<string?>("--since") { Description = "Since date (YYYY-MM-DD)" };
        var fmtOpt = new Option<string>("--format") { Description = "Format (markdown|json)", DefaultValueFactory = _ => "markdown" };
        cmd.Add(sinceOpt); cmd.Add(fmtOpt);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        Console.Write(client.Changelog(Cli.ParseDate(pr.GetValue(sinceOpt)), pr.GetValue(fmtOpt)!));
        }));
        root.Add(cmd);
    }

    private static void RegisterLint(RootCommand root)
    {
        var cmd = new Command("lint", "Lint issues for problems");
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var r = client.Lint();
            if (Cli.IsJson(pr)) { Cli.WriteJson(r); return; }
            Console.WriteLine($"Checked {r.TotalChecked} issue(s).");
            if (r.IsClean) { Console.WriteLine("No warnings."); return; }
            foreach (var w in r.Warnings)
                Console.WriteLine($"  [{w.IssueId}] {w.Message}");
        }));
        root.Add(cmd);
    }

    private static void RegisterGraph(RootCommand root)
    {
        var cmd = new Command("graph", "Show dependency graph");
        var idArg = new Argument<string>("id") { Description = "Issue ID" };
        var fmtOpt = new Option<string>("--format") { Description = "Format (text|mermaid)", DefaultValueFactory = _ => "text" };
        cmd.Add(idArg); cmd.Add(fmtOpt);
        cmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        Console.Write(client.Graph(pr.GetRequiredValue(idArg), pr.GetValue(fmtOpt)!));
        }));
        root.Add(cmd);
    }
}
