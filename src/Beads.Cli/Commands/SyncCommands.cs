using System.CommandLine;
using Beads.Net.Models;

namespace Beads.Cli.Commands;

internal static class SyncCommands
{
    internal static void Register(RootCommand root)
    {
        RegisterSync(root);
        RegisterConfig(root);
    }

    private static void RegisterSync(RootCommand root)
    {
        var sync = new Command("sync", "Sync operations");

        var flushCmd = new Command("flush", "Flush dirty issues to JSONL");
        var flushPath = new Option<string?>("--output") { Description = "Output path" };
        var flushExternal = new Option<bool>("--allow-external-jsonl") { Description = "Allow external JSONL path" };
        var flushManifest = new Option<bool>("--manifest") { Description = "Write manifest" };
        var flushPolicy = new Option<string>("--error-policy") { Description = "Error policy (strict|best-effort)", DefaultValueFactory = _ => "strict" };
        flushCmd.Add(flushPath); flushCmd.Add(flushExternal);
        flushCmd.Add(flushManifest); flushCmd.Add(flushPolicy);
        flushCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var result = client.Sync.Flush(new FlushOptions
            {
                OutputPath = pr.GetValue(flushPath),
                AllowExternalJsonl = pr.GetValue(flushExternal),
                Manifest = pr.GetValue(flushManifest),
                ErrorPolicy = pr.GetValue(flushPolicy)!,
            });
            if (Cli.IsJson(pr))
                Cli.WriteJson(result);
            else
                Console.WriteLine($"Flushed {result.ExportedCount} issue(s) to {result.OutputPath} ({result.Skipped} skipped)");
        }));

        var importCmd = new Command("import", "Import issues from JSONL");
        var importPath = new Option<string?>("--input") { Description = "Input JSONL path" };
        var importForce = new Option<bool>("--force") { Description = "Force overwrite" };
        var importExternal = new Option<bool>("--allow-external-jsonl") { Description = "Allow external JSONL" };
        var importRename = new Option<string?>("--rename-prefix") { Description = "Rename prefix" };
        importCmd.Add(importPath); importCmd.Add(importForce);
        importCmd.Add(importExternal); importCmd.Add(importRename);
        importCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        var result = client.Sync.Import(new ImportOptions
            {
                InputPath = pr.GetValue(importPath),
                Force = pr.GetValue(importForce),
                AllowExternalJsonl = pr.GetValue(importExternal),
                RenamePrefix = pr.GetValue(importRename),
            });
            if (Cli.IsJson(pr))
                Cli.WriteJson(result);
            else
            {
                Console.WriteLine($"Imported {result.ImportedCount}, updated {result.UpdatedCount}, skipped {result.SkippedCount}");
                foreach (var e in result.Errors)
                    Console.Error.WriteLine($"  Error: {e}");
            }
        }));

        var statusCmd = new Command("status", "Show sync status");
        statusCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var s = client.Sync.Status();
            if (Cli.IsJson(pr))
                Cli.WriteJson(s);
            else
            {
                Console.WriteLine($"DB:    {s.DbPath} ({s.DbIssueCount} issues)");
                Console.WriteLine($"JSONL: {s.JsonlPath} ({s.JsonlIssueCount} issues)");
                Console.WriteLine($"Dirty: {s.DirtyCount}");
                Console.WriteLine($"Status: {s.Status}");
            }
        }));

        sync.Add(flushCmd);
        sync.Add(importCmd);
        sync.Add(statusCmd);
        root.Add(sync);
    }

    private static void RegisterConfig(RootCommand root)
    {
        var config = new Command("config", "Configuration");

        var listCmd = new Command("list", "Show effective configuration");
        listCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var c = client.Config;
            if (Cli.IsJson(pr))
                Cli.WriteJson(new { db = c.Db, prefix = c.Prefix, id_prefix = c.IdPrefix, actor = c.Actor });
            else
            {
                Console.WriteLine($"db       = {c.Db}");
                Console.WriteLine($"prefix   = {c.Prefix}");
                Console.WriteLine($"id_prefix= {c.IdPrefix}");
                Console.WriteLine($"actor    = {c.Actor}");
            }
        }));

        var getCmd = new Command("get", "Get a config/metadata value");
        var getKey = new Argument<string>("key") { Description = "Key" };
        getCmd.Add(getKey);
        getCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var val = client.Schema.GetMetadata(pr.GetRequiredValue(getKey));
            Console.WriteLine(val ?? "");
        }));

        var setCmd = new Command("set", "Set a metadata value");
        var setKey = new Argument<string>("key") { Description = "Key" };
        var setVal = new Argument<string>("value") { Description = "Value" };
        setCmd.Add(setKey); setCmd.Add(setVal);
        setCmd.SetAction(pr => Cli.Run(pr, client =>
        {
                        client.Schema.SetMetadata(pr.GetRequiredValue(setKey), pr.GetRequiredValue(setVal));
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Set.");
        }));

        var delCmd = new Command("delete", "Delete a metadata value");
        var delKey = new Argument<string>("key") { Description = "Key" };
        delCmd.Add(delKey);
        delCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            client.Schema.SetMetadata(pr.GetRequiredValue(delKey), "");
            if (!Cli.IsQuiet(pr)) Console.WriteLine("Deleted.");
        }));

        var pathCmd = new Command("path", "Show database path");
        pathCmd.SetAction(pr => Cli.Run(pr, client =>
            Console.WriteLine(client.WhereDb())));

        config.Add(listCmd);
        config.Add(getCmd);
        config.Add(setCmd);
        config.Add(delCmd);
        config.Add(pathCmd);
        root.Add(config);
    }
}
