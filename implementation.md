# beads.net — Implementation Guide

## Übersicht

**beads.net** ist ein .NET-Port von [`beads_rust`](https://github.com/Dicklesworthstone/beads_rust) als Library + CLI.
100% Feature-Parity mit `br`, erweitert um eine optionale Projekt-/Board-Ebene.
Speichert alles in SQLite — neue oder bestehende Datenbank.
Konfigurierbarer Tabellen-Prefix (Default: `beads_`).

**Kein Footprint:** Keine Dateien in `%APPDATA%`, kein `~/.config/`, keine Environment-Variablen.

---

## Config-Hierarchie

Konfiguration wird aus 4 Quellen geladen (höchste Priorität zuerst):

| Prio | Quelle | Beschreibung |
|------|--------|-------------|
| 1 | CLI-Flags | `--db`, `--prefix`, `--actor`, `--id-prefix`, etc. |
| 2 | `--config <path>` | Expliziter Pfad zu einer YAML-Config |
| 3 | Config neben der exe | `beads.yaml` im selben Ordner wie `beads.exe` |
| 4 | `.beads/config.yaml` | Projekt-lokale Config im Workspace |

**Keine** User-Config, **keine** Environment-Variablen, **keine** Spuren auf dem System.

### Config-Format (YAML)

```yaml
# beads.yaml
db: .beads/beads.db
prefix: beads_
id_prefix: bd
actor: alice
defaults:
  priority: 2
  type: task
output:
  color: true
  date_format: "%Y-%m-%d"
sync:
  auto_import: false
  auto_flush: false
```

### Config-Loading in C#

```csharp
public class BeadsConfig
{
    public string? Db { get; set; }
    public string Prefix { get; set; } = "beads_";
    public string IdPrefix { get; set; } = "bd";
    public string Actor { get; set; } = Environment.UserName;
    public DefaultsConfig Defaults { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public SyncConfig Sync { get; set; } = new();
}

public static class ConfigLoader
{
    /// Lädt Config aus allen 4 Quellen, merged nach Priorität.
    public static BeadsConfig Load(string? explicitConfigPath = null);
}
```

---

## Architektur

```
Solution: Beads.Net.sln
│
├── src/
│   ├── Beads.Net/                  # Library (NuGet Package)
│   │   ├── BeadsClient.cs          # Einstiegspunkt
│   │   ├── BeadsConfig.cs          # Konfiguration (Prefix, DB-Pfad, …)
│   │   ├── ConfigLoader.cs         # Config-Loading aus 4 Quellen
│   │   ├── Schema/
│   │   │   ├── SchemaManager.cs    # Init, Migration, Prefix-Handling
│   │   │   └── Migrations/         # Versionierte Migrationen
│   │   ├── Models/
│   │   │   ├── Issue.cs            # Haupt-Entity (br: Issue)
│   │   │   ├── Project.cs          # Erweiterung: Projekte
│   │   │   ├── Board.cs            # Erweiterung: Boards
│   │   │   ├── Column.cs           # Erweiterung: Kanban-Spalten
│   │   │   ├── Comment.cs
│   │   │   ├── Dependency.cs
│   │   │   ├── Event.cs            # Audit-Events
│   │   │   └── EpicStatus.cs
│   │   ├── Enums/
│   │   │   ├── IssueType.cs
│   │   │   ├── IssueStatus.cs
│   │   │   ├── Priority.cs
│   │   │   └── DependencyType.cs
│   │   ├── Services/
│   │   │   ├── IssueService.cs
│   │   │   ├── ProjectService.cs   # Erweiterung
│   │   │   ├── BoardService.cs     # Erweiterung
│   │   │   ├── DependencyService.cs
│   │   │   ├── LabelService.cs
│   │   │   ├── CommentService.cs
│   │   │   ├── EpicService.cs
│   │   │   ├── EventService.cs     # Audit-Log
│   │   │   ├── QueryService.cs     # Saved Queries
│   │   │   ├── SyncService.cs
│   │   │   ├── DoctorService.cs
│   │   │   └── StatsService.cs
│   │   ├── Errors/
│   │   │   └── BeadsException.cs   # Typisierte Exceptions
│   │   └── Internal/
│   │       ├── Db.cs               # SQLite Connection + Helpers
│   │       ├── IdGenerator.cs      # Hash-basierte Short-IDs
│   │       └── ContentHash.cs      # Deterministic Content Hashing
│   │
│   └── Beads.Cli/                  # CLI (dotnet tool)
│       ├── Program.cs
│       ├── CliConfig.cs            # Config-Loading für CLI
│       └── Commands/
│           ├── InitCommand.cs
│           ├── CreateCommand.cs
│           ├── QuickCommand.cs     # br: q
│           ├── ListCommand.cs
│           ├── ShowCommand.cs
│           ├── UpdateCommand.cs
│           ├── CloseCommand.cs
│           ├── ReopenCommand.cs
│           ├── DeleteCommand.cs
│           ├── ReadyCommand.cs
│           ├── BlockedCommand.cs
│           ├── SearchCommand.cs
│           ├── CountCommand.cs
│           ├── StaleCommand.cs
│           ├── DepCommands.cs
│           ├── LabelCommands.cs
│           ├── EpicCommands.cs
│           ├── CommentCommands.cs
│           ├── DeferCommand.cs
│           ├── UnDeferCommand.cs
│           ├── OrphansCommand.cs
│           ├── QueryCommands.cs    # Saved Queries
│           ├── SyncCommand.cs
│           ├── ConfigCommand.cs
│           ├── DoctorCommand.cs
│           ├── StatsCommand.cs
│           ├── InfoCommand.cs
│           ├── WhereCommand.cs
│           ├── VersionCommand.cs
│           ├── AuditCommand.cs
│           ├── HistoryCommand.cs
│           ├── ChangelogCommand.cs
│           ├── LintCommand.cs
│           ├── GraphCommand.cs
│           ├── CompletionsCommand.cs
│           ├── ProjectCommands.cs  # Erweiterung
│           └── BoardCommands.cs    # Erweiterung
│
└── tests/
    ├── Beads.Net.Tests/            # Unit + Integration Tests
    └── Beads.Cli.Tests/            # CLI E2E Tests
```

---

## Phase 1: Grundgerüst + Schema

### Schritt 1.1 — Solution erstellen

```bash
dotnet new sln -n Beads.Net
dotnet new classlib -n Beads.Net -o src/Beads.Net --framework net8.0
dotnet new console -n Beads.Cli -o src/Beads.Cli --framework net8.0
dotnet new xunit -n Beads.Net.Tests -o tests/Beads.Net.Tests --framework net8.0
dotnet new xunit -n Beads.Cli.Tests -o tests/Beads.Cli.Tests --framework net8.0
dotnet sln add src/Beads.Net src/Beads.Cli tests/Beads.Net.Tests tests/Beads.Cli.Tests
dotnet add src/Beads.Cli reference src/Beads.Net
dotnet add tests/Beads.Net.Tests reference src/Beads.Net
dotnet add tests/Beads.Cli.Tests reference src/Beads.Net
dotnet add tests/Beads.Cli.Tests reference src/Beads.Cli
```

### Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Deterministic>true</Deterministic>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### Packages

```bash
# Library
dotnet add src/Beads.Net package Microsoft.Data.Sqlite
dotnet add src/Beads.Net package YamlDotNet

# CLI
dotnet add src/Beads.Cli package System.CommandLine

# Tests
dotnet add tests/Beads.Net.Tests package Microsoft.Data.Sqlite
dotnet add tests/Beads.Cli.Tests package Microsoft.Data.Sqlite
```

### Schritt 1.2 — BeadsConfig

```csharp
public class BeadsConfig
{
    /// Pfad zur SQLite-DB. Default: .beads/beads.db
    public string Db { get; set; } = ".beads/beads.db";

    /// Tabellen-Prefix. Default: "beads_"
    /// Wird allen Tabellen vorangestellt: {Prefix}issues, {Prefix}labels, …
    public string Prefix { get; set; } = "beads_";

    /// Issue-ID-Prefix. Default: "bd"
    /// Neue Issues: bd-abc123
    public string IdPrefix { get; set; } = "bd";

    /// Actor-Name für Audit Trail. Default: Environment.UserName
    public string Actor { get; set; } = Environment.UserName;

    /// Default-Werte für neue Issues
    public DefaultsConfig Defaults { get; set; } = new();

    /// Output-Konfiguration
    public OutputConfig Output { get; set; } = new();

    /// Sync-Konfiguration
    public SyncConfig Sync { get; set; } = new();
}

public class DefaultsConfig
{
    public int Priority { get; set; } = 2;
    public string Type { get; set; } = "task";
    public string? Assignee { get; set; }
}

public class OutputConfig
{
    public bool Color { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd";
}

public class SyncConfig
{
    public bool AutoImport { get; set; } = false;
    public bool AutoFlush { get; set; } = false;
}
```

### Schritt 1.3 — ConfigLoader

```csharp
public static class ConfigLoader
{
    /// Lädt Config aus allen 4 Quellen, merged nach Priorität.
    /// 1. CLI-Flags (höchste Priorität)
    /// 2. --config <path> (expliziter YAML-Pfad)
    /// 3. beads.yaml neben der exe
    /// 4. .beads/config.yaml (niedrigste Priorität)
    public static BeadsConfig Load(
        string? explicitConfigPath = null,
        Action<BeadsConfig>? cliOverrides = null)
    {
        var config = new BeadsConfig();

        // Schicht 4: .beads/config.yaml (falls vorhanden)
        MergeFromYaml(config, Path.Combine(".beads", "config.yaml"));

        // Schicht 3: beads.yaml neben der exe
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (exeDir != null)
            MergeFromYaml(config, Path.Combine(exeDir, "beads.yaml"));

        // Schicht 2: expliziter Config-Pfad
        if (explicitConfigPath != null)
            MergeFromYaml(config, explicitConfigPath, required: true);

        // Schicht 1: CLI-Flags
        cliOverrides?.Invoke(config);

        return config;
    }
}
```

### Schritt 1.4 — Schema mit konfigurierbarem Prefix

`SchemaManager` erstellt/migriert Tabellen. Alle Tabellennamen werden
zur Laufzeit mit dem Prefix versehen.

**Tabellen (Parity mit br, mit Prefix `{p}`):**

| Tabelle | Parity | Beschreibung |
|---------|--------|-------------|
| `{p}issues` | br | Issues (Tasks, Bugs, Epics, Features, …) |
| `{p}dependencies` | br | Issue-Dependencies mit Typen |
| `{p}labels` | br | Labels (issue_id, label) |
| `{p}comments` | br | Kommentare zu Issues |
| `{p}events` | br | Append-only Audit Trail |
| `{p}config` | br | Runtime-Config (key/value) |
| `{p}metadata` | br | Workspace-Metadata (key/value) |
| `{p}dirty_issues` | br | Dirty-Tracking für Export |
| `{p}export_hashes` | br | Content-Hashes für inkrementellen Export |
| `{p}blocked_issues_cache` | br | Materialized View: blockierte Issues |
| `{p}child_counters` | br | Hierarchische ID-Counter |
| `{p}projects` | **NEU** | Projekte (optional) |
| `{p}boards` | **NEU** | Boards pro Projekt (optional) |
| `{p}columns` | **NEU** | Kanban-Spalten pro Board (optional) |

**Logik bei `EnsureSchema()`:**

```
1. Prüfe ob {p}issues existiert
   → Nein: Erstmalige Initialisierung, alle Tabellen anlegen
   → Ja:  Schema-Kompatibilität prüfen
          Fehlende Spalten per ALTER TABLE ADD COLUMN
          Fehlende Tabellen anlegen
          Migrations ausführen
2. PRAGMA journal_mode = WAL
3. PRAGMA foreign_keys = ON
4. PRAGMA synchronous = NORMAL
5. PRAGMA temp_store = MEMORY
6. PRAGMA cache_size = -8000
7. Bestehende User-Tabellen NICHT anfassen
```

**Prefix-Handling:**

```csharp
private string T(string table) => $"{_config.Prefix}{table}";
private string Sql(string template) =>
    template.Replace("{p}", _config.Prefix);
```

### Schritt 1.5 — Schema SQL (Parity mit br)

Das Schema folgt 1:1 dem `br`-Schema für Interoperabilität:

```sql
-- Issues table (br-kompatibel)
CREATE TABLE IF NOT EXISTS {p}issues (
    id                  TEXT PRIMARY KEY,
    content_hash        TEXT,
    title               TEXT NOT NULL CHECK(length(title) <= 500),
    description         TEXT NOT NULL DEFAULT '',
    design              TEXT NOT NULL DEFAULT '',
    acceptance_criteria TEXT NOT NULL DEFAULT '',
    notes               TEXT NOT NULL DEFAULT '',
    status              TEXT NOT NULL DEFAULT 'open',
    priority            INTEGER NOT NULL DEFAULT 2 CHECK(priority >= 0 AND priority <= 4),
    issue_type          TEXT NOT NULL DEFAULT 'task',
    assignee            TEXT,
    owner               TEXT DEFAULT '',
    estimated_minutes   INTEGER,
    created_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by          TEXT DEFAULT '',
    updated_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    closed_at           DATETIME,
    close_reason        TEXT DEFAULT '',
    closed_by_session   TEXT DEFAULT '',
    due_at              DATETIME,
    defer_until         DATETIME,
    external_ref        TEXT,
    source_system       TEXT DEFAULT '',
    source_repo         TEXT NOT NULL DEFAULT '.',
    deleted_at          DATETIME,
    deleted_by          TEXT DEFAULT '',
    delete_reason       TEXT DEFAULT '',
    original_type       TEXT DEFAULT '',
    compaction_level    INTEGER DEFAULT 0,
    compacted_at        DATETIME,
    compacted_at_commit TEXT,
    original_size       INTEGER,
    sender              TEXT DEFAULT '',
    ephemeral           INTEGER NOT NULL DEFAULT 0,
    pinned              INTEGER NOT NULL DEFAULT 0,
    is_template         INTEGER NOT NULL DEFAULT 0,
    -- Erweiterung: optionale Projekt-/Board-Zuordnung
    project_id          TEXT REFERENCES {p}projects(id) ON DELETE SET NULL,
    column_id           TEXT REFERENCES {p}columns(id) ON DELETE SET NULL,
    position            INTEGER NOT NULL DEFAULT 0,
    CHECK (
        (status = 'closed' AND closed_at IS NOT NULL) OR
        (status = 'tombstone') OR
        (status NOT IN ('closed', 'tombstone') AND closed_at IS NULL)
    )
);

-- Dependencies
CREATE TABLE IF NOT EXISTS {p}dependencies (
    issue_id      TEXT NOT NULL,
    depends_on_id TEXT NOT NULL,
    type          TEXT NOT NULL DEFAULT 'blocks',
    created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by    TEXT NOT NULL DEFAULT '',
    metadata      TEXT DEFAULT '{}',
    thread_id     TEXT DEFAULT '',
    PRIMARY KEY (issue_id, depends_on_id),
    FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
);

-- Labels
CREATE TABLE IF NOT EXISTS {p}labels (
    issue_id TEXT NOT NULL,
    label    TEXT NOT NULL,
    PRIMARY KEY (issue_id, label),
    FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
);

-- Comments
CREATE TABLE IF NOT EXISTS {p}comments (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    issue_id   TEXT NOT NULL,
    author     TEXT NOT NULL DEFAULT '',
    text       TEXT NOT NULL DEFAULT '',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
);

-- Events (Audit)
CREATE TABLE IF NOT EXISTS {p}events (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    issue_id   TEXT NOT NULL,
    event_type TEXT NOT NULL DEFAULT '',
    actor      TEXT NOT NULL DEFAULT '',
    old_value  TEXT,
    new_value  TEXT,
    comment    TEXT,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
);

-- Config (Runtime key/value)
CREATE TABLE IF NOT EXISTS {p}config (
    key   TEXT NOT NULL,
    value TEXT NOT NULL
);

-- Metadata
CREATE TABLE IF NOT EXISTS {p}metadata (
    key   TEXT NOT NULL,
    value TEXT NOT NULL
);

-- Dirty Issues (für Export)
CREATE TABLE IF NOT EXISTS {p}dirty_issues (
    issue_id  TEXT PRIMARY KEY,
    marked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
);

-- Export Hashes (inkrementeller Export)
CREATE TABLE IF NOT EXISTS {p}export_hashes (
    issue_id     TEXT PRIMARY KEY,
    content_hash TEXT NOT NULL,
    exported_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
);

-- Blocked Issues Cache (Materialized View)
CREATE TABLE IF NOT EXISTS {p}blocked_issues_cache (
    issue_id   TEXT PRIMARY KEY,
    blocked_by TEXT NOT NULL,
    blocked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
);

-- Child Counters (hierarchische IDs)
CREATE TABLE IF NOT EXISTS {p}child_counters (
    parent_id  TEXT PRIMARY KEY,
    last_child INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (parent_id) REFERENCES {p}issues(id) ON DELETE CASCADE
);

-- ============================================================
-- ERWEITERUNG: Projekte + Boards (optional)
-- ============================================================

CREATE TABLE IF NOT EXISTS {p}projects (
    id          TEXT PRIMARY KEY,
    name        TEXT NOT NULL,
    description TEXT DEFAULT '',
    status      TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'archived')),
    color       TEXT,
    created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS {p}boards (
    id         TEXT PRIMARY KEY,
    project_id TEXT NOT NULL REFERENCES {p}projects(id) ON DELETE CASCADE,
    name       TEXT NOT NULL,
    position   INTEGER NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS {p}columns (
    id        TEXT PRIMARY KEY,
    board_id  TEXT NOT NULL REFERENCES {p}boards(id) ON DELETE CASCADE,
    name      TEXT NOT NULL,
    position  INTEGER NOT NULL DEFAULT 0,
    wip_limit INTEGER,
    color     TEXT,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

**Indexes (Parity mit br):**

```sql
-- Issues
CREATE INDEX IF NOT EXISTS {p}idx_issues_status ON {p}issues(status);
CREATE INDEX IF NOT EXISTS {p}idx_issues_priority ON {p}issues(priority);
CREATE INDEX IF NOT EXISTS {p}idx_issues_issue_type ON {p}issues(issue_type);
CREATE INDEX IF NOT EXISTS {p}idx_issues_assignee ON {p}issues(assignee) WHERE assignee IS NOT NULL;
CREATE INDEX IF NOT EXISTS {p}idx_issues_created_at ON {p}issues(created_at);
CREATE INDEX IF NOT EXISTS {p}idx_issues_updated_at ON {p}issues(updated_at);
CREATE INDEX IF NOT EXISTS {p}idx_issues_content_hash ON {p}issues(content_hash);
CREATE UNIQUE INDEX IF NOT EXISTS {p}idx_issues_external_ref ON {p}issues(external_ref) WHERE external_ref IS NOT NULL;
CREATE INDEX IF NOT EXISTS {p}idx_issues_ephemeral ON {p}issues(ephemeral) WHERE ephemeral = 1;
CREATE INDEX IF NOT EXISTS {p}idx_issues_pinned ON {p}issues(pinned) WHERE pinned = 1;
CREATE INDEX IF NOT EXISTS {p}idx_issues_tombstone ON {p}issues(status) WHERE status = 'tombstone';
CREATE INDEX IF NOT EXISTS {p}idx_issues_due_at ON {p}issues(due_at) WHERE due_at IS NOT NULL;
CREATE INDEX IF NOT EXISTS {p}idx_issues_defer_until ON {p}issues(defer_until) WHERE defer_until IS NOT NULL;
CREATE INDEX IF NOT EXISTS {p}idx_issues_ready ON {p}issues(status, priority, created_at)
    WHERE status = 'open' AND ephemeral = 0 AND pinned = 0 AND is_template = 0;
CREATE INDEX IF NOT EXISTS {p}idx_issues_list_active ON {p}issues(priority, created_at DESC)
    WHERE status NOT IN ('closed', 'tombstone') AND (is_template = 0 OR is_template IS NULL);

-- Dependencies
CREATE INDEX IF NOT EXISTS {p}idx_deps_issue ON {p}dependencies(issue_id);
CREATE INDEX IF NOT EXISTS {p}idx_deps_depends_on ON {p}dependencies(depends_on_id);
CREATE INDEX IF NOT EXISTS {p}idx_deps_type ON {p}dependencies(type);
CREATE INDEX IF NOT EXISTS {p}idx_deps_depends_on_type ON {p}dependencies(depends_on_id, type);
CREATE INDEX IF NOT EXISTS {p}idx_deps_thread ON {p}dependencies(thread_id) WHERE thread_id != '';
CREATE INDEX IF NOT EXISTS {p}idx_deps_blocking ON {p}dependencies(depends_on_id, issue_id)
    WHERE (type = 'blocks' OR type = 'parent-child' OR type = 'conditional-blocks' OR type = 'waits-for');

-- Labels
CREATE INDEX IF NOT EXISTS {p}idx_labels_label ON {p}labels(label);
CREATE INDEX IF NOT EXISTS {p}idx_labels_issue ON {p}labels(issue_id);

-- Comments
CREATE INDEX IF NOT EXISTS {p}idx_comments_issue ON {p}comments(issue_id);
CREATE INDEX IF NOT EXISTS {p}idx_comments_created ON {p}comments(created_at);

-- Events
CREATE INDEX IF NOT EXISTS {p}idx_events_issue ON {p}events(issue_id);
CREATE INDEX IF NOT EXISTS {p}idx_events_type ON {p}events(event_type);
CREATE INDEX IF NOT EXISTS {p}idx_events_created ON {p}events(created_at);
CREATE INDEX IF NOT EXISTS {p}idx_events_actor ON {p}events(actor) WHERE actor != '';

-- Dirty Issues
CREATE INDEX IF NOT EXISTS {p}idx_dirty_marked ON {p}dirty_issues(marked_at);

-- Blocked Cache
CREATE INDEX IF NOT EXISTS {p}idx_blocked_at ON {p}blocked_issues_cache(blocked_at);

-- Erweiterung: Projekte + Boards
CREATE INDEX IF NOT EXISTS {p}idx_issues_project ON {p}issues(project_id);
CREATE INDEX IF NOT EXISTS {p}idx_issues_column ON {p}issues(column_id);
CREATE INDEX IF NOT EXISTS {p}idx_boards_project ON {p}boards(project_id);
CREATE INDEX IF NOT EXISTS {p}idx_columns_board ON {p}columns(board_id);
```

### Schritt 1.6 — BeadsClient

```csharp
public class BeadsClient : IDisposable
{
    public BeadsClient(string dbPath, Action<BeadsConfig>? configure = null);
    public BeadsClient(BeadsConfig config);

    // Bestehende Connection mitgeben (für Integration in eigene App)
    public BeadsClient(SqliteConnection connection, Action<BeadsConfig>? configure = null);

    // br-Parity Services
    public IssueService      Issues       { get; }
    public DependencyService Dependencies { get; }
    public LabelService      Labels       { get; }
    public CommentService    Comments     { get; }
    public EpicService       Epics        { get; }
    public EventService      Events       { get; }  // Audit
    public QueryService      Queries      { get; }  // Saved Queries
    public SyncService       Sync         { get; }
    public DoctorService     Doctor       { get; }
    public StatsService      Stats        { get; }

    // Erweiterung
    public ProjectService    Projects     { get; }
    public BoardService      Boards       { get; }

    public SchemaManager     Schema       { get; }
    public BeadsConfig       Config       { get; }
}
```

### Schritt 1.7 — Tests Phase 1

- [ ] Neue DB: alle Tabellen werden erstellt (br-Tabellen + Erweiterungen)
- [ ] Bestehende leere DB: Tabellen werden hinzugefügt
- [ ] Bestehende DB mit User-Tabellen: User-Tabellen bleiben unberührt
- [ ] Custom Prefix: Tabellen heißen `custom_issues` statt `beads_issues`
- [ ] Zwei BeadsClients mit verschiedenen Prefixes in derselben DB
- [ ] Schema-Version wird korrekt geschrieben und gelesen
- [ ] PRAGMA journal_mode = WAL
- [ ] PRAGMA foreign_keys = ON
- [ ] PRAGMA synchronous = NORMAL
- [ ] Indexes existieren (alle br-Parity-Indexes)
- [ ] Closed-at CHECK-Constraint funktioniert
- [ ] Config-Loading: 4 Quellen mit korrektem Merge
- [ ] Config-Loading: CLI-Flags überschreiben YAML
- [ ] Config-Loading: Fehlende Config-Dateien werden ignoriert

---

## Phase 2: Models + Enums + ID-Generierung

### Schritt 2.1 — Enums (Parity mit br)

```csharp
/// Status: br hat Open, InProgress, Blocked, Deferred, Draft, Closed, Tombstone, Pinned + Custom
public enum IssueStatus
{
    Open,
    InProgress,
    Blocked,
    Deferred,
    Draft,
    Closed,
    Tombstone,
    Pinned
}
// + Custom-Status als string-Fallback (parsing: unbekannte Werte werden als Custom akzeptiert)

/// Priority: 0=Critical, 1=High, 2=Medium, 3=Low, 4=Backlog
/// Parsing: "P0", "p0", "0" → 0 (Critical)
public enum Priority
{
    Critical = 0,
    High     = 1,
    Medium   = 2,
    Low      = 3,
    Backlog  = 4
}

/// IssueType: Task, Bug, Feature, Epic, Chore, Docs, Question + Custom
public enum IssueType
{
    Task,
    Bug,
    Feature,
    Epic,
    Chore,
    Docs,
    Question
}

/// DependencyType: Blocking + non-blocking Beziehungen
public enum DependencyType
{
    // Blocking (affects ready work)
    Blocks,
    ParentChild,
    ConditionalBlocks,
    WaitsFor,
    // Non-blocking
    Related,
    DiscoveredFrom,
    RepliesTo,
    RelatesTo,
    Duplicates,
    Supersedes,
    CausedBy
}

/// EventType: Audit-Events
public enum EventType
{
    Created,
    Updated,
    StatusChanged,
    PriorityChanged,
    AssigneeChanged,
    Commented,
    Closed,
    Reopened,
    DependencyAdded,
    DependencyRemoved,
    LabelAdded,
    LabelRemoved,
    Compacted,
    Deleted,
    Restored
}
```

**Enum-Handling:** Unbekannte Werte (Custom-Types/Status) werden als Strings gespeichert/gelesen,
nicht als Exception. br akzeptiert Custom-Strings überall.

### Schritt 2.2 — Issue Model (Parity mit br)

```csharp
public record Issue
{
    // Core
    public string Id { get; init; } = "";
    public string? ContentHash { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Design { get; init; } = "";
    public string AcceptanceCriteria { get; init; } = "";
    public string Notes { get; init; } = "";

    // Classification
    public string Status { get; init; } = "open";           // string für Custom-Status
    public int Priority { get; init; } = 2;
    public string IssueType { get; init; } = "task";        // string für Custom-Types
    public string? Assignee { get; init; }
    public string? Owner { get; init; }
    public int? EstimatedMinutes { get; init; }

    // Timestamps
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public string? CloseReason { get; init; }
    public string? ClosedBySession { get; init; }

    // Scheduling
    public DateTime? DueAt { get; init; }
    public DateTime? DeferUntil { get; init; }

    // External
    public string? ExternalRef { get; init; }
    public string? SourceSystem { get; init; }
    public string SourceRepo { get; init; } = ".";

    // Tombstone
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
    public string? DeleteReason { get; init; }
    public string? OriginalType { get; init; }

    // Compaction
    public int CompactionLevel { get; init; } = 0;
    public DateTime? CompactedAt { get; init; }
    public string? CompactedAtCommit { get; init; }
    public int? OriginalSize { get; init; }

    // Flags
    public string? Sender { get; init; }
    public bool Ephemeral { get; init; }
    public bool Pinned { get; init; }
    public bool IsTemplate { get; init; }

    // Relations (für Export/Display, nicht direkt in issues-Tabelle)
    public List<string> Labels { get; init; } = [];
    public List<Dependency> Dependencies { get; init; } = [];
    public List<Comment> Comments { get; init; } = [];

    // Erweiterung (optional)
    public string? ProjectId { get; init; }
    public string? ColumnId { get; init; }
    public int Position { get; init; }
}
```

### Schritt 2.3 — Weitere Models

```csharp
public record Dependency
{
    public string IssueId { get; init; } = "";
    public string DependsOnId { get; init; } = "";
    public string DepType { get; init; } = "blocks";
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public string? Metadata { get; init; }
    public string? ThreadId { get; init; }
}

public record Comment
{
    public long Id { get; init; }
    public string IssueId { get; init; } = "";
    public string Author { get; init; } = "";
    public string Body { get; init; } = "";        // DB-Spalte: "text"
    public DateTime CreatedAt { get; init; }
}

public record Event
{
    public long Id { get; init; }
    public string IssueId { get; init; } = "";
    public string EventType { get; init; } = "";
    public string Actor { get; init; } = "";
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record EpicStatus
{
    public Issue Epic { get; init; } = null!;
    public int TotalChildren { get; init; }
    public int ClosedChildren { get; init; }
    public bool EligibleForClose { get; init; }
}

// Erweiterungen
public record Project
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Status { get; init; } = "active";
    public string? Color { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record Board
{
    public string Id { get; init; } = "";
    public string ProjectId { get; init; } = "";
    public string Name { get; init; } = "";
    public int Position { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record Column
{
    public string Id { get; init; } = "";
    public string BoardId { get; init; } = "";
    public string Name { get; init; } = "";
    public int Position { get; init; }
    public int? WipLimit { get; init; }
    public string? Color { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

### Schritt 2.4 — ID-Generierung (Parity mit br)

Hash-basierte Short-IDs (nicht auto-increment):

```
Format:     {id_prefix}-{hash}     z.B. "bd-a1b2c3"
Material:   title + created_at (ns) + nonce
Hash:       SHA256 → Base36, Länge 6
Kollision:  Nonce hochzählen, DB-Lookup, ggf. Länge erhöhen
```

```csharp
public static class IdGenerator
{
    public static string Generate(string prefix, string title, DateTime createdAt, Func<string, bool> exists);
}
```

### Schritt 2.5 — Content Hashing (Parity mit br)

Deterministischer Hash über semantische Felder (title, description, design, acceptance_criteria,
notes, status, priority, issue_type, assignee, external_ref, pinned, is_template).
Excludiert: id, timestamps, relations.

```csharp
public static class ContentHash
{
    public static string Compute(Issue issue);
}
```

### Schritt 2.6 — Tests Phase 2

- [ ] ID-Generierung erzeugt stabile, kollisionsfreie IDs
- [ ] ID-Format: `{prefix}-{base36hash}`
- [ ] Kollisionsauflösung funktioniert (Nonce-Inkrement)
- [ ] Content-Hash ist deterministisch (gleicher Input → gleicher Hash)
- [ ] Content-Hash ändert sich bei Titel-/Beschreibungs-/Status-/Priority-Änderung
- [ ] Content-Hash bleibt gleich bei Timestamp-Änderung
- [ ] Content-Hash bleibt gleich bei ID-Änderung
- [ ] Enum-Parsing: "P0"/"p0"/"0" → Critical
- [ ] Enum-Parsing: Unbekannte Werte → Custom akzeptiert
- [ ] Status: is_terminal() (Closed, Tombstone)
- [ ] Status: is_active() (Open, InProgress)
- [ ] DependencyType: is_blocking() / affects_ready_work()
- [ ] Models lassen sich korrekt aus SQLite-Rows mappen
- [ ] Enums werden als Strings in DB gespeichert

---

## Phase 3: Core Services — CRUD (br Parity)

### Schritt 3.1 — IssueService

```csharp
public class IssueService
{
    // CRUD
    Task<Issue> Create(string title, CreateIssueOptions? options = null);
    Task<Issue?> Get(string id);
    Task<IssueListResult> List(IssueFilter? filter = null);
    Task<Issue> Update(string id, UpdateIssueOptions options);
    Task<Issue> Close(string id, CloseOptions? options = null);
    Task<Issue> Reopen(string id);
    Task Delete(string id, DeleteOptions? options = null);    // Tombstone

    // Querying
    Task<IssueListResult> Ready(ReadyFilter? filter = null);
    Task<IssueListResult> Blocked(BlockedFilter? filter = null);
    Task<IssueListResult> Search(string query, IssueFilter? filter = null);
    Task<IssueListResult> Stale(int days = 14);
    Task<CountResult> Count(string? groupBy = null);
    Task<List<Issue>> Orphans();

    // Quick capture
    Task<string> Quick(string title, CreateIssueOptions? options = null);

    // Defer
    Task Defer(string id, DateTime? until = null);
    Task Undefer(string id);

    // Subtasks
    Task<List<Issue>> GetSubtasks(string parentId);
}
```

```csharp
public record CreateIssueOptions
{
    public string? ProjectId { get; init; }
    public string? ParentId { get; init; }
    public string? IssueType { get; init; }
    public int? Priority { get; init; }
    public string? Description { get; init; }
    public string? Design { get; init; }
    public string? AcceptanceCriteria { get; init; }
    public string? Notes { get; init; }
    public string? Assignee { get; init; }
    public string? Owner { get; init; }
    public int? EstimatedMinutes { get; init; }
    public DateTime? DueAt { get; init; }
    public DateTime? DeferUntil { get; init; }
    public string? ExternalRef { get; init; }
    public string? Status { get; init; }
    public bool Ephemeral { get; init; }
    public bool DryRun { get; init; }
    public List<string>? Labels { get; init; }
    public List<string>? DependsOn { get; init; }
}

public record IssueFilter
{
    public string? ProjectId { get; init; }
    public List<string>? Statuses { get; init; }
    public List<string>? Types { get; init; }
    public List<int>? Priorities { get; init; }
    public int? PriorityMin { get; init; }
    public int? PriorityMax { get; init; }
    public string? Assignee { get; init; }
    public bool? Unassigned { get; init; }
    public List<string>? Ids { get; init; }
    public List<string>? Labels { get; init; }       // AND-Logik
    public List<string>? LabelsAny { get; init; }    // OR-Logik
    public string? TitleContains { get; init; }
    public string? DescContains { get; init; }
    public bool IncludeClosed { get; init; }
    public bool IncludeDeferred { get; init; }
    public bool? Overdue { get; init; }
    public int Limit { get; init; } = 50;           // 0 = unlimited
    public int Offset { get; init; }
    public string? SortBy { get; init; }             // priority, created_at, updated_at, title
    public bool Reverse { get; init; }
}

public record IssueListResult
{
    public List<Issue> Issues { get; init; } = [];
    public int Total { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
    public bool HasMore { get; init; }
}

public record CloseOptions
{
    public string? Reason { get; init; }
    public bool Force { get; init; }           // Close even if blocked
    public bool SuggestNext { get; init; }     // Return newly unblocked issues
    public string? Session { get; init; }
}

public record DeleteOptions
{
    public string? Reason { get; init; }
    public bool Force { get; init; }
}
```

### Schritt 3.2 — DependencyService

```csharp
public class DependencyService
{
    Task Add(string issueId, string dependsOnId, string depType = "blocks");
    Task Remove(string issueId, string dependsOnId);
    Task<List<Dependency>> List(string issueId);
    Task<List<Issue>> GetBlockers(string issueId);
    Task<List<Issue>> GetDependents(string issueId);
    Task<DependencyTree> Tree(string issueId);
    Task<List<List<string>>> FindCycles();

    // Interne Methoden
    Task<bool> WouldCreateCycle(string issueId, string dependsOnId);
    Task RebuildBlockedCache();
}
```

### Schritt 3.3 — LabelService

```csharp
public class LabelService
{
    Task Add(string issueId, params string[] labels);
    Task Remove(string issueId, params string[] labels);
    Task<List<string>> List(string? issueId = null);
    Task<List<string>> ListAll();
    Task Rename(string oldName, string newName);
}
```

### Schritt 3.4 — CommentService

```csharp
public class CommentService
{
    Task<Comment> Add(string issueId, string body, string? author = null);
    Task<List<Comment>> List(string issueId);
}
```

### Schritt 3.5 — EpicService

```csharp
public class EpicService
{
    Task<EpicStatus> Status(string epicId);
    Task<EpicStatus> CloseEligible(string epicId);
}
```

### Schritt 3.6 — EventService (Audit)

Jede Mutation schreibt automatisch Events:

```csharp
public class EventService
{
    Task<List<Event>> List(string? issueId = null, string? eventType = null);
    Task Record(string issueId, string eventType, string? oldValue = null, string? newValue = null);
}
```

Event-Types: `created`, `updated`, `status_changed`, `priority_changed`, `assignee_changed`,
`commented`, `closed`, `reopened`, `dependency_added`, `dependency_removed`,
`label_added`, `label_removed`, `compacted`, `deleted`, `restored`

### Schritt 3.7 — QueryService (Saved Queries)

```csharp
public class QueryService
{
    Task Save(string name, IssueFilter filter);
    Task<IssueListResult> Run(string name);
    Task<List<string>> List();
    Task Delete(string name);
}
```

### Schritt 3.8 — ProjectService (Erweiterung)

```csharp
public class ProjectService
{
    Task<Project> Create(string name, string? description = null, string? color = null);
    Task<Project?> Get(string idOrName);
    Task<List<Project>> List(bool includeArchived = false);
    Task<Project> Update(string id, string? name = null, string? description = null);
    Task Archive(string id);
    Task Delete(string id);  // nur wenn keine Issues mehr
}
```

### Schritt 3.9 — BoardService (Erweiterung)

```csharp
public class BoardService
{
    Task<Board> Create(string projectId, string name);
    Task<List<Board>> List(string projectId);
    Task<Board> Update(string id, string? name = null, int? position = null);
    Task Delete(string id);

    // Columns
    Task<Column> CreateColumn(string boardId, string name, int? wipLimit = null, string? color = null);
    Task<List<Column>> ListColumns(string boardId);
    Task<Column> UpdateColumn(string id, string? name = null, int? position = null, int? wipLimit = null);
    Task DeleteColumn(string id);

    // Issue ↔ Column
    Task MoveIssue(string issueId, string columnId, int? position = null);
}
```

### Schritt 3.10 — StatsService

```csharp
public class StatsService
{
    Task<ProjectStats> GetStats();
}

public record ProjectStats
{
    public int TotalIssues { get; init; }
    public Dictionary<string, int> ByStatus { get; init; } = new();
    public Dictionary<string, int> ByType { get; init; } = new();
    public Dictionary<string, int> ByPriority { get; init; } = new();
    public Dictionary<string, int> ByAssignee { get; init; } = new();
    public int OpenCount { get; init; }
    public int ClosedCount { get; init; }
    public int BlockedCount { get; init; }
    public int OverdueCount { get; init; }
    public DateTime? LastActivity { get; init; }
}
```

### Schritt 3.11 — DoctorService

```csharp
public class DoctorService
{
    Task<DoctorReport> Run();
}

public record DoctorReport
{
    public int SchemaVersion { get; init; }
    public bool SchemaOk { get; init; }
    public List<string> OrphanedSubtasks { get; init; } = [];
    public List<string> OrphanedDependencies { get; init; } = [];
    public List<List<string>> DependencyCycles { get; init; } = [];
    public int DirtyCount { get; init; }
    public bool ForeignKeyIntegrity { get; init; }
    public string JournalMode { get; init; } = "";
    public List<string> Warnings { get; init; } = [];
}
```

### Schritt 3.12 — Tests Phase 3

**CRUD:**
- [ ] Issue erstellen mit allen Feldern
- [ ] Issue lesen (Get by ID)
- [ ] Issue aktualisieren (Title, Description, Status, Priority, Assignee, …)
- [ ] Issue schließen (mit Reason, Force, SuggestNext)
- [ ] Issue wieder öffnen
- [ ] Issue löschen (Tombstone erstellt)
- [ ] Subtask-Kaskadierung bei Delete

**Querying:**
- [ ] List mit allen Filter-Kombinationen
- [ ] Pagination (Limit, Offset, HasMore)
- [ ] Sort by priority, created_at, updated_at, title
- [ ] Ready: nur unblockierte, nicht-deferred Issues
- [ ] Blocked: nur blockierte Issues
- [ ] Search: Volltextsuche in title + description
- [ ] Stale: Issues ohne Update seit N Tagen
- [ ] Count: Gruppierung nach status, type, priority, assignee, label
- [ ] Orphans: verwaiste Subtasks

**Dependencies:**
- [ ] Add/Remove Dependencies
- [ ] Zirkuläre Dependency Detection
- [ ] Dependency Tree
- [ ] Find Cycles
- [ ] Blocked Cache wird aktualisiert
- [ ] Verschiedene DependencyTypes (blocks, parent-child, related, …)
- [ ] Blocking vs. non-blocking korrekt in Ready

**Labels:**
- [ ] Add/Remove/List/ListAll
- [ ] Rename
- [ ] AND/OR Filter-Logik

**Comments:**
- [ ] Add/List
- [ ] Author wird korrekt gesetzt

**Epics:**
- [ ] Epic Status (total/closed children)
- [ ] Close Eligible

**Events (Audit):**
- [ ] Event wird bei jeder Mutation geschrieben
- [ ] Event-Typen korrekt
- [ ] Old/New-Value korrekt bei StatusChanged, PriorityChanged

**Saved Queries:**
- [ ] Save/Run/List/Delete

**Defer:**
- [ ] Defer setzt defer_until
- [ ] Undefer entfernt defer_until
- [ ] Deferred Issues erscheinen nicht in Ready

**Quick Capture:**
- [ ] Gibt nur ID zurück

**Projekte (Erweiterung):**
- [ ] CRUD für Projects
- [ ] Archive/Unarchive
- [ ] Delete nur wenn keine Issues
- [ ] Issues mit project_id Filter

**Boards (Erweiterung):**
- [ ] Board CRUD
- [ ] Column CRUD mit WIP-Limit
- [ ] MoveIssue ordnet column_id und position zu

**Custom Prefix:**
- [ ] Alle Operations funktionieren mit custom Prefix

**Content Hash:**
- [ ] Hash wird bei Create/Update berechnet und gespeichert
- [ ] Dirty-Flag wird bei jeder Mutation gesetzt

---

## Phase 4: Sync (JSONL) — Parity mit br

### Schritt 4.1 — SyncService

SQLite ist Primary, JSONL ist Export für Git. Vollständige Parity mit br.

```csharp
public class SyncService
{
    /// Export DB → JSONL (dirty Records)
    Task<FlushResult> Flush(FlushOptions? options = null);

    /// Import JSONL → DB (Merge mit Content-Hashing)
    Task<ImportResult> Import(ImportOptions? options = null);

    /// Status: dirty count, letzer Sync, Staleness
    Task<SyncStatus> Status();

    /// History (lokale Backups)
    Task<List<HistoryEntry>> HistoryList();
    Task HistoryRestore(string backup);
}

public record FlushOptions
{
    public string? OutputPath { get; init; }
    public bool AllowExternalJsonl { get; init; }
    public bool Manifest { get; init; }
    public string ErrorPolicy { get; init; } = "strict";  // strict, best-effort, partial
}

public record ImportOptions
{
    public string? InputPath { get; init; }
    public bool AllowExternalJsonl { get; init; }
    public bool Force { get; init; }
    public string? RenamePrefix { get; init; }
    public string OrphanPolicy { get; init; } = "strict";  // strict, resurrect, skip, allow
}

public record SyncStatus
{
    public string DbPath { get; init; } = "";
    public string JsonlPath { get; init; } = "";
    public DateTime? DbModified { get; init; }
    public DateTime? JsonlModified { get; init; }
    public int DbIssueCount { get; init; }
    public int JsonlIssueCount { get; init; }
    public int DirtyCount { get; init; }
    public string Status { get; init; } = "";        // "db_newer", "jsonl_newer", "in_sync"
}
```

### Schritt 4.2 — Safety Guarantees (Parity mit br)

- **Niemals** git-Commands ausführen
- **Niemals** Dateien außerhalb `.beads/` schreiben (außer `--allow-external-jsonl`)
- Atomic writes: Temp-File → Rename
- Safety-Guards: kein Überschreiben von nicht-leerem JSONL mit leerem DB-Export

### Schritt 4.3 — Dirty Tracking

- `{p}dirty_issues` trackt geänderte Issues
- INSERT/UPDATE/DELETE am Issue → dirty_issues Eintrag
- `Flush()` schreibt dirty Records als JSONL, leert dirty_issues
- `{p}export_hashes` speichert Content-Hashes für inkrementellen Export

### Schritt 4.4 — JSONL Format

Jede Zeile ist ein vollständiges Issue-JSON (mit labels, dependencies, comments inline).
Format-Parity mit br für Cross-Kompatibilität.

### Schritt 4.5 — Tests Phase 4

- [ ] Flush: Export erzeugt valides JSONL
- [ ] Flush: Nur dirty Records werden exportiert
- [ ] Flush: dirty_issues wird geleert
- [ ] Flush: Atomic write (Temp + Rename)
- [ ] Flush: Safety-Guard bei leerem DB + nicht-leerem JSONL
- [ ] Import: Merge korrekt (kein Datenverlust)
- [ ] Import: Content-Hashing Deduplizierung
- [ ] Import: Neue Issues werden eingefügt
- [ ] Import: Bestehende Issues werden aktualisiert
- [ ] Import: Tombstones werden respektiert
- [ ] Import: --force überschreibt Safety-Guards
- [ ] Import: --rename-prefix schreibt IDs um
- [ ] Roundtrip: Export → Import → identischer State (sync_equals)
- [ ] Status: korrekte Staleness-Erkennung
- [ ] History: Backup wird bei Flush erstellt
- [ ] History: Restore funktioniert
- [ ] Kein Schreiben außerhalb .beads/ (ohne --allow-external-jsonl)

---

## Phase 5: CLI — Vollständige br-Parity + Erweiterungen

### Schritt 5.1 — Globale Argumente

```
beads [global-options] <command> [command-options]

Global Options:
  --db <path>          SQLite-Pfad (default: .beads/beads.db)
  --config <path>      Pfad zu Config-YAML
  --prefix <string>    Tabellen-Prefix (default: beads_)
  --actor <name>       Actor für Audit Trail
  --json               JSON-Output
  --no-color           Keine ANSI-Farben
  --no-auto-flush      Kein automatischer JSONL-Export nach Mutationen
  --no-auto-import     Kein automatischer Import-Check
  --allow-stale        Stale DB erlauben
  --lock-timeout <ms>  SQLite busy timeout
  --no-db              JSONL-only Modus
  -v, --verbose        Verbose Logging (-v, -vv)
  -q, --quiet          Nur Fehler ausgeben
  -h, --help           Hilfe
  -V, --version        Version
```

### Schritt 5.2 — Commands: Core (br Parity)

```
beads init [--prefix <PREFIX>] [--force]
beads create [TITLE] [options]
beads q [TITLE] [options]                    # Quick capture, gibt nur ID aus
beads list [options]
beads show <ID>...
beads update <ID>... [options]
beads close <ID>... [--reason <text>] [--force] [--suggest-next]
beads reopen <ID>...
beads delete <ID>... [--reason <text>] [--force]
```

### Schritt 5.3 — Commands: Query (br Parity)

```
beads ready [--limit N] [--assignee X] [--unassigned] [--label L] [--type T] [--priority P]
beads blocked [options]
beads search <QUERY> [filter-options]
beads count [--by status|type|priority|assignee|label]
beads stale [--days N]
```

### Schritt 5.4 — Commands: Organization (br Parity)

```
beads dep add <ID> <DEPENDS_ON> [--type blocks|parent-child|...]
beads dep remove <ID> <DEPENDS_ON>
beads dep list <ID>
beads dep tree <ID>
beads dep cycles

beads label add <ID> <LABELS>...
beads label remove <ID> <LABELS>...
beads label list [ID]
beads label list-all
beads label rename <OLD> <NEW>

beads epic status <ID>
beads epic close-eligible <ID>

beads comments add <ID> <BODY>
beads comments list <ID>
```

### Schritt 5.5 — Commands: Workflow (br Parity)

```
beads defer <ID>... [--until <DATE>]
beads undefer <ID>...
beads orphans
beads query save <NAME> <FILTER-OPTIONS>
beads query run <NAME>
beads query list
beads query delete <NAME>
```

### Schritt 5.6 — Commands: Sync & Config (br Parity)

```
beads sync --flush-only [--allow-external-jsonl] [--manifest] [--error-policy strict|best-effort]
beads sync --import-only [--force] [--allow-external-jsonl] [--rename-prefix]
beads sync --status

beads config list
beads config get <KEY>
beads config set <KEY=VALUE>
beads config delete <KEY>
beads config path
```

### Schritt 5.7 — Commands: Diagnostics (br Parity)

```
beads stats
beads doctor
beads version
beads info [--schema]
beads where
beads audit [options]
beads history list
beads history restore <BACKUP>
beads changelog [--since <DATE>] [--format markdown|json]
beads lint
beads graph <ID> [--format mermaid|text]
beads completions <SHELL>              # bash, zsh, fish, powershell
```

### Schritt 5.8 — Commands: Erweiterungen (Projekte + Boards)

```
beads project create <NAME> [--description TEXT] [--color HEX]
beads project list [--all]
beads project show <ID|NAME>
beads project archive <ID|NAME>
beads project delete <ID|NAME>

beads board create <PROJECT> <NAME>
beads board list <PROJECT>
beads board columns <BOARD>
beads board add-column <BOARD> <NAME> [--wip-limit N]
beads board move <ISSUE-ID> <COLUMN> [--position N]
```

### Schritt 5.9 — create-Command Optionen (vollständig)

```
beads create "Auth System implementieren" \
  --type epic \
  --priority P1 \
  --description "Detaillierte Beschreibung" \
  --design "Technische Design-Notes" \
  --acceptance-criteria "AC hier" \
  --notes "Zusätzliche Notizen" \
  --assignee alice \
  --owner alice@example.com \
  --estimate 120 \
  --due 2026-04-15 \
  --defer 2026-04-01 \
  --external-ref "JIRA-123" \
  --label backend \
  --label auth \
  --deps "blocks:bd-abc123" \
  --parent bd-epic1 \
  --project "Website Relaunch" \
  --ephemeral \
  --dry-run \
  --silent
```

### Schritt 5.10 — Output-Formate

**Table (default, rich in TTY):**
```
ID          TYPE     P   STATUS       TITLE
bd-ab12c3   epic     P1  open         Auth System
bd-de45f6   task     P2  in_progress  ├─ Login Form
bd-gh78i9   task     P2  done         ├─ DB Schema
bd-jk01l2   task     P2  open         └─ JWT Tokens
```

**JSON (`--json`):**
```json
{
  "issues": [],
  "total": 4,
  "limit": 50,
  "offset": 0,
  "has_more": false
}
```

**CSV (`--format csv`):**
```
id,title,status,priority,type,assignee
bd-ab12c3,Auth System,open,1,epic,
```

**Output-Mode Auto-Detection:**
- Rich: Interactive TTY mit Farben
- Plain: Piped Output oder `--no-color`
- JSON: `--json` oder `--format json`
- Quiet: `--quiet`

### Schritt 5.11 — Exit Codes (Parity mit br)

| Code | Kategorie | Beschreibung |
|------|-----------|-------------|
| 0 | Success | Erfolgreich |
| 1 | Internal | Interner Fehler |
| 2 | Database | DB-Fehler (nicht initialisiert, Schema-Mismatch) |
| 3 | Issue | Issue-Fehler (nicht gefunden, mehrdeutige ID) |
| 4 | Validation | Validierungsfehler (ungültige Eingabe) |
| 5 | Dependency | Dependency-Fehler (Zyklus, Self-Dependency) |
| 6 | Sync | Sync-Fehler (Parse-Error, Conflict-Markers) |
| 7 | Config | Konfigurationsfehler |
| 8 | I/O | I/O-Fehler (Datei nicht gefunden, Permission denied) |

### Schritt 5.12 — Error Handling

Typisierte Exceptions in der Library:

```csharp
public class BeadsException : Exception { public int ExitCode { get; } }
public class BeadsNotFoundException : BeadsException { }           // Exit 3
public class BeadsDuplicateException : BeadsException { }          // Exit 4
public class BeadsValidationException : BeadsException { }         // Exit 4
public class BeadsCyclicDependencyException : BeadsException { }   // Exit 5
public class BeadsSyncException : BeadsException { }               // Exit 6
public class BeadsConfigException : BeadsException { }             // Exit 7
public class BeadsSchemaException : BeadsException { }             // Exit 2
```

JSON-Error-Output (`--json`):
```json
{
  "error_code": 3,
  "message": "Issue not found: bd-xyz999",
  "kind": "not_found",
  "recovery_hints": ["Check the issue ID", "Use 'beads list' to find issues"]
}
```

### Schritt 5.13 — Publish als dotnet tool

```xml
<!-- Beads.Cli.csproj -->
<PackageAsTool>true</PackageAsTool>
<ToolCommandName>beads</ToolCommandName>
<PackageId>Beads.Cli</PackageId>
```

### Schritt 5.14 — Tests Phase 5

**CRUD Commands:**
- [ ] `beads init` erstellt .beads/ und DB
- [ ] `beads init --prefix myproj` setzt Prefix
- [ ] `beads init --force` überschreibt bestehende DB
- [ ] `beads create "Title"` erstellt Issue
- [ ] `beads create` mit allen Optionen (type, priority, description, assignee, due, labels, deps, parent, …)
- [ ] `beads create --dry-run` erstellt nichts
- [ ] `beads create --silent` gibt nur ID aus
- [ ] `beads create -f issues.md` Bulk-Import
- [ ] `beads q "Quick fix"` gibt nur ID aus
- [ ] `beads list` mit allen Filtern
- [ ] `beads list --format csv --fields id,title,status`
- [ ] `beads list --long` / `--pretty` (Tree)
- [ ] `beads show bd-abc123`
- [ ] `beads show bd-abc123 --json`
- [ ] `beads update bd-abc --title "New" --priority P0 --assignee bob`
- [ ] `beads update bd-abc --claim` (atomic assign + in_progress)
- [ ] `beads update bd-abc --add-label X --remove-label Y`
- [ ] `beads close bd-abc --reason "Done"`
- [ ] `beads close bd-abc --force` (close despite blockers)
- [ ] `beads close bd-abc --suggest-next --json`
- [ ] `beads reopen bd-abc`
- [ ] `beads delete bd-abc --reason "Duplicate"`

**Query Commands:**
- [ ] `beads ready` / `beads ready --json`
- [ ] `beads blocked`
- [ ] `beads search "auth"` / mit Filtern
- [ ] `beads count` / `beads count --by status`
- [ ] `beads stale` / `beads stale --days 30`

**Organization Commands:**
- [ ] `beads dep add/remove/list/tree/cycles`
- [ ] `beads label add/remove/list/list-all/rename`
- [ ] `beads epic status/close-eligible`
- [ ] `beads comments add/list`

**Workflow Commands:**
- [ ] `beads defer/undefer`
- [ ] `beads orphans`
- [ ] `beads query save/run/list/delete`

**Sync Commands:**
- [ ] `beads sync --flush-only` / `--import-only` / `--status`
- [ ] `beads config list/get/set/delete/path`

**Diagnostic Commands:**
- [ ] `beads stats` (Table + JSON)
- [ ] `beads doctor` Checks
- [ ] `beads version`
- [ ] `beads info`
- [ ] `beads where`
- [ ] `beads audit`
- [ ] `beads history list/restore`
- [ ] `beads changelog`
- [ ] `beads lint`
- [ ] `beads graph bd-abc --format mermaid`
- [ ] `beads completions powershell`

**Extension Commands:**
- [ ] `beads project create/list/show/archive/delete`
- [ ] `beads board create/list/columns/add-column/move`

**Global:**
- [ ] `--json` bei allen Commands
- [ ] `--quiet` unterdrückt Output
- [ ] `--no-color` deaktiviert ANSI
- [ ] `--db` / `--config` / `--prefix` werden durchgereicht
- [ ] Exit-Codes korrekt (0-8)
- [ ] Error-Output als JSON bei `--json`

---

## Phase 6: Polish + Docs

### Schritt 6.1 — README + Docs

- README mit Quickstart (CLI + Library)
- CLI-Reference (alle Commands mit Optionen)
- Library-API-Dokumentation
- XML-Doc-Comments auf allen öffentlichen APIs

### Schritt 6.2 — JSON Schema

```
beads schema all --format json    # Gibt JSON-Schema für alle Typen aus
beads schema issue --format json  # Schema für Issue
```

---

## Phase 7: Packaging + Release (nach ladybug-csharp)

### Schritt 7.1 — Versioning

- **Tag-driven**: `v*` Tags → Version wird aus Tag extrahiert
- **Kein hardcoded Version** in `.csproj` — wird beim Pack via `/p:PackageVersion` injected
- **Deterministic builds**: `ContinuousIntegrationBuild=true`

### Schritt 7.2 — NuGet Packages

| Package | Typ | Beschreibung |
|---------|-----|-------------|
| `Beads.Net` | Library | Core Library für Integration |
| `Beads.Cli` | dotnet tool | CLI (`beads` command) |

```xml
<!-- Beads.Net.csproj NuGet Metadata -->
<PackageId>Beads.Net</PackageId>
<Title>Beads.Net</Title>
<Description>Local-first issue tracker — .NET port of beads_rust</Description>
<Authors><!-- your name --></Authors>
<PackageTags>issue-tracker;sqlite;cli;git;local-first</PackageTags>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<PackageReadmeFile>README.md</PackageReadmeFile>
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
```

### Schritt 7.3 — GitHub Actions: CI (`ci.yml`)

```yaml
name: ci

on:
  push:
    branches: [main]
  pull_request:

permissions:
  contents: read

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build-test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        dotnet: ['8.0.x', '10.0.x']
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet }}
      - run: dotnet restore
      - run: dotnet build Beads.Net.sln -c Release --no-restore
      - run: dotnet test Beads.Net.sln -c Release --no-build --logger "trx;LogFileName=test-${{ matrix.dotnet }}.trx" --results-directory TestResults
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-${{ matrix.dotnet }}
          path: TestResults/*.trx
```

### Schritt 7.4 — GitHub Actions: Release (`release.yml`)

```yaml
name: release

on:
  push:
    tags: ["v*"]
  workflow_dispatch:
    inputs:
      version:
        description: "Version (e.g. 1.2.3)"
        required: false
        type: string

permissions:
  contents: write

concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false

jobs:
  release:
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.version.outputs.version }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Resolve version
        id: version
        shell: bash
        run: |
          if [ "${GITHUB_REF_TYPE}" = "tag" ]; then
            VERSION="${GITHUB_REF_NAME#v}"
          elif [ -n "${{ inputs.version }}" ]; then
            VERSION="${{ inputs.version }}"
          else
            echo "Version required" >&2; exit 1
          fi
          echo "version=$VERSION" >> "$GITHUB_OUTPUT"

      - run: dotnet restore
      - run: dotnet build Beads.Net.sln -c Release --no-restore
      - run: dotnet test Beads.Net.sln -c Release --no-build --logger "trx" --results-directory TestResults

      - name: Pack Beads.Net (Library)
        run: >
          dotnet pack src/Beads.Net/Beads.Net.csproj -c Release -o artifacts
          /p:PackageVersion=${{ steps.version.outputs.version }}
          /p:ContinuousIntegrationBuild=true
          /p:IncludeSymbols=true
          /p:SymbolPackageFormat=snupkg

      - name: Pack Beads.Cli (dotnet tool)
        run: >
          dotnet pack src/Beads.Cli/Beads.Cli.csproj -c Release -o artifacts
          /p:PackageVersion=${{ steps.version.outputs.version }}
          /p:ContinuousIntegrationBuild=true
          /p:IncludeSymbols=true
          /p:SymbolPackageFormat=snupkg

      - uses: actions/upload-artifact@v4
        with:
          name: release-packages
          path: |
            artifacts/*.nupkg
            artifacts/*.snupkg

      - uses: softprops/action-gh-release@v2
        with:
          tag_name: v${{ steps.version.outputs.version }}
          generate_release_notes: true
          files: |
            artifacts/*.nupkg
            artifacts/*.snupkg

      - name: Publish to NuGet
        run: |
          dotnet nuget push "artifacts/*.nupkg" --api-key "${{ secrets.NUGET_API_KEY }}" --source "https://api.nuget.org/v3/index.json" --skip-duplicate
          dotnet nuget push "artifacts/*.snupkg" --api-key "${{ secrets.NUGET_API_KEY }}" --source "https://api.nuget.org/v3/index.json" --skip-duplicate

  build-binaries:
    needs: release
    strategy:
      matrix:
        include:
          - os: ubuntu-latest
            rid: linux-x64
          - os: ubuntu-latest
            rid: linux-arm64
          - os: macos-latest
            rid: osx-arm64
          - os: windows-latest
            rid: win-x64
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - name: Publish
        run: >
          dotnet publish src/Beads.Cli/Beads.Cli.csproj -c Release
          -r ${{ matrix.rid }}
          --self-contained
          -p:PublishSingleFile=true
          -p:PublishTrimmed=true
          -p:Version=${{ needs.release.outputs.version }}
          -o dist/
      - uses: softprops/action-gh-release@v2
        with:
          tag_name: v${{ needs.release.outputs.version }}
          files: dist/*
```

### Schritt 7.5 — Tests Phase 7

- [ ] NuGet Package `Beads.Net` installierbar in frischem Projekt
- [ ] `dotnet tool install --global Beads.Cli` funktioniert
- [ ] `beads --version` gibt korrekte Version aus
- [ ] Self-contained Binary startet auf Linux, macOS, Windows
- [ ] Binary-Größe < 20 MB (mit Trimming)
- [ ] CI läuft auf .NET 8.0 und 10.0
- [ ] Release Pipeline: Tag → NuGet + GitHub Release + Binaries

---

## Test-Parity mit beads_rust

### Kategorien (100% Coverage der br-Test-Bereiche)

| br Test-Bereich | beads.net Equivalent | Scope |
|-----------------|---------------------|-------|
| `conformance.rs` | `ConformanceTests.cs` | Schema, Defaults, Constraints |
| `conformance_edge_cases.rs` | `ConformanceEdgeCaseTests.cs` | Edge Cases |
| `conformance_labels_comments.rs` | `LabelCommentConformanceTests.cs` | Labels + Comments |
| `conformance_schema.rs` | `SchemaConformanceTests.cs` | Schema-Parity |
| `conformance_workflows.rs` | `WorkflowConformanceTests.cs` | End-to-End Workflows |
| `storage_crud.rs` | `StorageCrudTests.cs` | CRUD Operations |
| `storage_deps.rs` | `DependencyTests.cs` | Dependencies + Cycles |
| `storage_ready.rs` | `ReadyTests.cs` | Ready-Work Logic |
| `storage_list_filters.rs` | `ListFilterTests.cs` | Query Filters |
| `storage_blocked_cache.rs` | `BlockedCacheTests.cs` | Blocked Cache |
| `storage_history.rs` | `HistoryTests.cs` | History Backups |
| `storage_invariants.rs` | `InvariantTests.cs` | Data Invariants |
| `storage_export_atomic.rs` | `ExportAtomicTests.cs` | Atomic Export |
| `storage_id_hash_parity.rs` | `IdHashParityTests.cs` | ID-Hash Parity |
| `jsonl_import_export.rs` | `JsonlImportExportTests.cs` | JSONL Round-Trip |
| `proptest_hash.rs` | `ContentHashPropertyTests.cs` | Hash Properties |
| `proptest_id.rs` | `IdGeneratorPropertyTests.cs` | ID Properties |
| `proptest_validation.rs` | `ValidationPropertyTests.cs` | Validation Properties |
| `e2e_basic_lifecycle.rs` | `CliLifecycleTests.cs` | CLI E2E Lifecycle |
| `e2e_comments.rs` | `CliCommentsTests.cs` | CLI Comments |
| `e2e_concurrency.rs` | `CliConcurrencyTests.cs` | CLI Concurrent Access |
| `e2e_config_precedence.rs` | `CliConfigTests.cs` | CLI Config |
| `e2e_create_output.rs` | `CliCreateOutputTests.cs` | CLI Create Output |
| `e2e_defer.rs` | `CliDeferTests.cs` | CLI Defer/Undefer |
| `e2e_dep_tree_mermaid.rs` | `CliDepTreeTests.cs` | CLI Dependency Tree |
| `e2e_epic.rs` | `CliEpicTests.cs` | CLI Epic Commands |
| `e2e_errors.rs` | `CliErrorTests.cs` | CLI Error Handling |
| `e2e_global_flags.rs` | `CliGlobalFlagsTests.cs` | CLI Global Flags |
| `e2e_graph.rs` | `CliGraphTests.cs` | CLI Graph |
| `e2e_labels.rs` | `CliLabelTests.cs` | CLI Labels |
| `e2e_lint.rs` | `CliLintTests.cs` | CLI Lint |
| `e2e_list_comprehensive.rs` | `CliListTests.cs` | CLI List |
| `e2e_orphans.rs` | `CliOrphansTests.cs` | CLI Orphans |
| `e2e_queries.rs` | `CliQueriesTests.cs` | CLI Saved Queries |
| `e2e_ready.rs` | `CliReadyTests.cs` | CLI Ready |
| `e2e_relations.rs` | `CliRelationsTests.cs` | CLI Relations |
| `e2e_search_scenarios.rs` | `CliSearchTests.cs` | CLI Search |
| `e2e_sync_artifacts.rs` | `CliSyncTests.cs` | CLI Sync |
| `model/mod.rs tests` | `ModelTests.cs` | Model Serialization + Enums |
| `schema.rs tests` | `SchemaTests.cs` | Schema Apply + Migrations |

### Model-Tests (Parity mit br `model/mod.rs`)

- [ ] Status: FromStr für alle Varianten (open, in_progress, blocked, deferred, draft, closed, tombstone, pinned)
- [ ] Status: Case-insensitive Parsing
- [ ] Status: Unbekannte Werte → Custom
- [ ] Status: Display-Format (lowercase snake_case)
- [ ] Status: is_terminal(), is_active()
- [ ] Priority: FromStr mit P-Prefix (P0-P4, p0-p4, 0-4)
- [ ] Priority: Ungültige Werte (5, -1, "high") → Error
- [ ] Priority: Display-Format (P0, P1, …)
- [ ] Priority: Ordering (Critical < High < Medium < Low < Backlog)
- [ ] Priority: Default = Medium (2)
- [ ] IssueType: FromStr für alle Varianten
- [ ] IssueType: Case-insensitive
- [ ] IssueType: Unbekannte Werte → Custom
- [ ] IssueType: Default = Task
- [ ] DependencyType: FromStr für alle 11 Varianten + Custom
- [ ] DependencyType: is_blocking() (Blocks, ParentChild, ConditionalBlocks, WaitsFor)
- [ ] DependencyType: affects_ready_work() (same as is_blocking)
- [ ] DependencyType: Display-Format (kebab-case)
- [ ] EventType: as_str() für alle Varianten
- [ ] EventType: Deserialization für alle bekannten + Custom
- [ ] Issue: Content-Hash deterministisch
- [ ] Issue: Content-Hash ändert sich bei Titel/Desc/Status/Priority
- [ ] Issue: Content-Hash bleibt gleich bei Timestamps/ID
- [ ] Issue: sync_equals() ignoriert Timestamps + Relation-Order
- [ ] Issue: sync_equals() erkennt semantische Änderungen
- [ ] Issue: sync_equals() behandelt Duplicate-Labels als equivalent
- [ ] Issue: Tombstone Expiration (is_expired_tombstone)
- [ ] Issue: Serialization Round-Trip (JSON)
- [ ] Comment: Serialization Round-Trip, "text" → body Alias
- [ ] Dependency: Serialization Round-Trip, "type" → dep_type Alias
- [ ] Event: Serialization Round-Trip
- [ ] EpicStatus: Serialization

---

## Zusammenfassung

| Phase | Was | Inhalt |
|-------|-----|--------|
| 1 | Grundgerüst + Schema | Solution, Config-Loading, Schema (br-Parity + Erweiterungen), BeadsClient |
| 2 | Models + Enums + IDs | Issue, Dependency, Comment, Event, Enums, ID-Generator, Content-Hash |
| 3 | Core Services | IssueService, DependencyService, LabelService, CommentService, EpicService, EventService, QueryService, ProjectService, BoardService, StatsService, DoctorService |
| 4 | JSONL Sync | SyncService, Dirty-Tracking, Flush/Import, History, Atomic Writes |
| 5 | CLI | Alle br-Commands + Project/Board-Erweiterungen, Output-Formate, Exit-Codes |
| 6 | Polish | README, CLI-Reference, XML-Doc-Comments, JSON-Schema |
| 7 | Release | NuGet, dotnet tool, Self-Contained Binaries, GitHub Actions CI/CD |

### Feature-Übersicht

| Feature | br | beads.net | Anmerkung |
|---------|:--:|:---------:|-----------|
| Issue CRUD | ✅ | ✅ | Volle Parity |
| Issue Status (inkl. Custom) | ✅ | ✅ | |
| Priority (P0-P4) | ✅ | ✅ | |
| Issue Types (inkl. Custom) | ✅ | ✅ | |
| Dependencies (11 Typen) | ✅ | ✅ | |
| Cycle Detection | ✅ | ✅ | |
| Dependency Tree/Graph | ✅ | ✅ | |
| Labels | ✅ | ✅ | |
| Comments | ✅ | ✅ | |
| Epics | ✅ | ✅ | |
| Audit Events | ✅ | ✅ | |
| JSONL Sync | ✅ | ✅ | |
| Dirty Tracking | ✅ | ✅ | |
| Content Hashing | ✅ | ✅ | |
| Blocked Cache | ✅ | ✅ | |
| Ready/Blocked/Stale/Count | ✅ | ✅ | |
| Defer/Undefer | ✅ | ✅ | |
| Orphans | ✅ | ✅ | |
| Saved Queries | ✅ | ✅ | |
| Doctor | ✅ | ✅ | |
| Stats | ✅ | ✅ | |
| Lint | ✅ | ✅ | |
| Changelog | ✅ | ✅ | |
| History/Restore | ✅ | ✅ | |
| Graph (Mermaid) | ✅ | ✅ | |
| Schema Export | ✅ | ✅ | |
| Shell Completions | ✅ | ✅ | |
| Config Command | ✅ | ✅ | |
| JSON/CSV/Text Output | ✅ | ✅ | |
| Self-Update | ✅ | ❌ | dotnet tool update statt self-update |
| Agents Command | ✅ | ❌ | Entfernt (nicht relevant) |
| Tabellen-Prefix | ❌ | ✅ | **Erweiterung** |
| Projekte | ❌ | ✅ | **Erweiterung** |
| Boards/Kanban | ❌ | ✅ | **Erweiterung** |
| Library-API (NuGet) | ❌ | ✅ | **Erweiterung** |
| Portable Config | ❌ | ✅ | **Erweiterung** (kein Footprint) |
