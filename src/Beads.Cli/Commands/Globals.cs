using System.CommandLine;

namespace Beads.Cli.Commands;

internal static class Globals
{
    internal static readonly Option<string?> Db = new("--db") { Description = "SQLite database path (default: .beads/beads.db)", Recursive = true };
    internal static readonly Option<string?> Prefix = new("--prefix") { Description = "Table prefix (default: beads_)", Recursive = true };
    internal static readonly Option<string?> Actor = new("--actor") { Description = "Actor for audit trail", Recursive = true };
    internal static readonly Option<bool> Json = new("--json") { Description = "JSON output", Recursive = true };
    internal static readonly Option<bool> Quiet = new("--quiet", "-q") { Description = "Only show errors", Recursive = true };
    internal static readonly Option<bool> Verbose = new("--verbose", "-v") { Description = "Verbose logging", Recursive = true };

    internal static void AddTo(RootCommand root)
    {
        root.Add(Db);
        root.Add(Prefix);
        root.Add(Actor);
        root.Add(Json);
        root.Add(Quiet);
        root.Add(Verbose);
    }
}
