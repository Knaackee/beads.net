using Beads.Net.Internal;
using Beads.Net.Models;
using Beads.Net.Schema;
using Beads.Net.Services;
using Microsoft.Data.Sqlite;

namespace Beads.Net;

public sealed class BeadsClient : IDisposable
{
    private readonly Db _db;
    private readonly bool _ownsConnection;

    public BeadsConfig Config { get; }
    public IssueService Issues { get; private set; } = null!;
    public DependencyService Dependencies { get; private set; } = null!;
    public LabelService Labels { get; private set; } = null!;
    public CommentService Comments { get; private set; } = null!;
    public EpicService Epics { get; private set; } = null!;
    public EventService Events { get; private set; } = null!;
    public QueryService Queries { get; private set; } = null!;
    public SyncService Sync { get; private set; } = null!;
    public DoctorService Doctor { get; private set; } = null!;
    public StatsService Stats { get; private set; } = null!;
    public ProjectService Projects { get; private set; } = null!;
    public BoardService Boards { get; private set; } = null!;
    public SchemaManager Schema { get; private set; } = null!;

    public BeadsClient(string dbPath, Action<BeadsConfig>? configure = null)
        : this(BuildConfig(dbPath, configure))
    {
    }

    public BeadsClient(BeadsConfig config)
    {
        Config = config;
        var dir = Path.GetDirectoryName(Path.GetFullPath(config.Db));
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _db = new Db(config.Db, config.Prefix);
        _ownsConnection = true;
        InitializeServices();
        Schema = new SchemaManager(_db);
        Schema.EnsureSchema();

        // Wire up remaining services that require schema
        Doctor = new DoctorService(_db, Schema, Dependencies);
    }

    public BeadsClient(SqliteConnection connection, Action<BeadsConfig>? configure = null)
    {
        Config = new BeadsConfig();
        configure?.Invoke(Config);
        _db = new Db(connection, Config.Prefix);
        _ownsConnection = false;
        InitializeServices();
        Schema = new SchemaManager(_db);
        Schema.EnsureSchema();
        Doctor = new DoctorService(_db, Schema, Dependencies);
    }

    private void InitializeServices()
    {
        Events = new EventService(_db, Config);
        Issues = new IssueService(_db, Config, Events);
        Dependencies = new DependencyService(_db, Config, Events);
        Labels = new LabelService(_db, Events);
        Comments = new CommentService(_db, Config, Events);
        Epics = new EpicService(_db);
        Queries = new QueryService(_db, Issues);
        Sync = new SyncService(_db, Config);
        Stats = new StatsService(_db);
        Projects = new ProjectService(_db);
        Boards = new BoardService(_db);
    }

    /// <summary>Initialize a new beads workspace (creates .beads/ directory and DB).</summary>
    public static BeadsClient Init(string? dbPath = null, string? prefix = null, bool force = false)
    {
        var path = dbPath ?? ".beads/beads.db";
        if (File.Exists(path) && !force)
            throw new Errors.BeadsSchemaException($"Database already exists: {path}. Use --force to overwrite.");

        if (File.Exists(path) && force)
            File.Delete(path);

        return new BeadsClient(path, cfg =>
        {
            if (prefix != null) cfg.Prefix = prefix;
        });
    }

    /// <summary>Get runtime info about this beads instance.</summary>
    public BeadsInfo GetInfo()
    {
        return new BeadsInfo
        {
            DbPath = Path.GetFullPath(Config.Db),
            Prefix = Config.Prefix,
            IdPrefix = Config.IdPrefix,
            Actor = Config.Actor,
            SchemaVersion = Schema.GetSchemaVersion(),
        };
    }

    /// <summary>Get the absolute DB path.</summary>
    public string WhereDb() => Path.GetFullPath(Config.Db);

    /// <summary>Run lint checks on all issues.</summary>
    public LintResult Lint()
    {
        var warnings = new List<LintWarning>();
        var issues = Issues.List(new IssueFilter { IncludeClosed = true, Limit = 0 }).Issues;

        foreach (var issue in issues)
        {
            if (string.IsNullOrWhiteSpace(issue.Title))
                warnings.Add(new LintWarning { IssueId = issue.Id, Message = "Empty title" });
            if (issue.Priority < 0 || issue.Priority > 4)
                warnings.Add(new LintWarning { IssueId = issue.Id, Message = $"Invalid priority: {issue.Priority}" });
            if (issue.Status == "closed" && issue.ClosedAt == null)
                warnings.Add(new LintWarning { IssueId = issue.Id, Message = "Closed issue missing closed_at" });
            if (issue.DueAt.HasValue && issue.DueAt < issue.CreatedAt)
                warnings.Add(new LintWarning { IssueId = issue.Id, Message = "Due date before creation date" });
        }

        return new LintResult
        {
            TotalChecked = issues.Count,
            Warnings = warnings,
        };
    }

    /// <summary>Generate a changelog from events.</summary>
    public string Changelog(DateTime? since = null, string format = "markdown")
    {
        var events = Events.List();
        if (since.HasValue)
            events = events.Where(e => e.CreatedAt >= since.Value).ToList();

        if (format == "json")
        {
            return System.Text.Json.JsonSerializer.Serialize(events, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
        }

        // Markdown format
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Changelog");
        sb.AppendLine();
        var grouped = events.GroupBy(e => e.CreatedAt.Date).OrderByDescending(g => g.Key);
        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key:yyyy-MM-dd}");
            foreach (var e in group.OrderByDescending(x => x.CreatedAt))
            {
                sb.AppendLine($"- **{e.EventType}** {e.IssueId}" +
                    (e.NewValue != null ? $": {e.NewValue}" : ""));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>Generate a dependency graph in text or mermaid format.</summary>
    public string Graph(string issueId, string format = "text")
    {
        var tree = Dependencies.Tree(issueId);
        if (format == "mermaid")
            return RenderMermaid(tree.Root);

        return RenderTextTree(tree.Root, "", true);
    }

    public void Dispose()
    {
        if (_ownsConnection)
            _db.Dispose();
    }

    private static BeadsConfig BuildConfig(string dbPath, Action<BeadsConfig>? configure)
    {
        var cfg = new BeadsConfig { Db = dbPath };
        configure?.Invoke(cfg);
        return cfg;
    }

    private static string RenderTextTree(DependencyNode node, string indent, bool isLast)
    {
        var sb = new System.Text.StringBuilder();
        var marker = isLast ? "└─ " : "├─ ";
        sb.AppendLine($"{indent}{marker}{node.IssueId}");
        var childIndent = indent + (isLast ? "   " : "│  ");
        for (int i = 0; i < node.Children.Count; i++)
            sb.Append(RenderTextTree(node.Children[i], childIndent, i == node.Children.Count - 1));
        return sb.ToString();
    }

    private static string RenderMermaid(DependencyNode node)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("graph TD");
        RenderMermaidNode(sb, node, new HashSet<string>());
        return sb.ToString();
    }

    private static void RenderMermaidNode(System.Text.StringBuilder sb, DependencyNode node, HashSet<string> visited)
    {
        if (!visited.Add(node.IssueId)) return;
        foreach (var child in node.Children)
        {
            sb.AppendLine($"    {node.IssueId} --> {child.IssueId}");
            RenderMermaidNode(sb, child, visited);
        }
    }
}

public record BeadsInfo
{
    public string DbPath { get; init; } = "";
    public string Prefix { get; init; } = "";
    public string IdPrefix { get; init; } = "";
    public string Actor { get; init; } = "";
    public int SchemaVersion { get; init; }
}
