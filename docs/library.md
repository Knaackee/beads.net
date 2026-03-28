# Beads.Net Library

The core library providing local-first issue tracking backed by SQLite. All business logic lives here — the CLI is a thin consumer.

## Installation

```bash
dotnet add package Beads.Net
```

## Getting Started

```csharp
using Beads.Net;

// Create or open a workspace
using var client = BeadsClient.Init("path/to/beads.db");

// Or open an existing database
using var client = new BeadsClient("path/to/beads.db");

// With configuration
using var client = new BeadsClient("beads.db", config => {
    config.Actor = "alice";
    config.Prefix = "myprefix_";
});
```

## BeadsClient

The main entry point. Exposes all services as properties:

| Property | Type | Description |
|----------|------|-------------|
| `Issues` | `IssueService` | CRUD, filtering, search, lifecycle |
| `Dependencies` | `DependencyService` | Dependency graph management |
| `Labels` | `LabelService` | Label operations |
| `Comments` | `CommentService` | Issue comments |
| `Epics` | `EpicService` | Epic progress tracking |
| `Events` | `EventService` | Audit trail |
| `Queries` | `QueryService` | Saved query filters |
| `Sync` | `SyncService` | JSONL sync operations |
| `Stats` | `StatsService` | Aggregate statistics |
| `Doctor` | `DoctorService` | Health checks |
| `Projects` | `ProjectService` | Project management |
| `Boards` | `BoardService` | Kanban boards |

### Static Methods

```csharp
// Initialize a new workspace (creates DB + schema)
var client = BeadsClient.Init(dbPath, prefix, force);
```

### Instance Methods

```csharp
client.GetInfo();     // BeadsInfo with runtime metadata
client.WhereDb();     // Absolute path to the database
client.Lint();        // LintResult checking all issues
client.Changelog();   // Markdown changelog from closed issues
client.Graph(id);     // Text or Mermaid dependency graph
```

## IssueService

### Create

```csharp
var issue = client.Issues.Create("Title", new CreateIssueOptions {
    IssueType = "bug",        // task, bug, epic, story, subtask
    Priority = 0,             // 0 (critical) to 4 (trivial)
    Description = "...",
    Assignee = "alice",
    Owner = "bob",
    EstimatedMinutes = 120,
    DueAt = DateTime.UtcNow.AddDays(7),
    Labels = ["backend", "urgent"],
    DependsOn = ["bd-other"],
    ParentId = "bd-epic1",
    ProjectId = "proj-abc",
    Metadata = "{\"source\":\"api\"}",
});

// Quick capture — returns only the ID
string id = client.Issues.Quick("Fix login");
```

### Read

```csharp
Issue? issue = client.Issues.Get("bd-abc123");     // null if not found
Issue issue = client.Issues.GetOrThrow("bd-abc");  // throws BeadsNotFoundException
```

### List & Filter

```csharp
var result = client.Issues.List(new IssueFilter {
    Types = ["bug", "task"],
    Statuses = ["open"],
    Assignee = "alice",
    Labels = ["backend"],           // all labels must match
    LabelsAny = ["urgent", "p0"],   // any label matches
    PriorityMin = 0,
    PriorityMax = 2,
    TitleContains = "login",
    IncludeClosed = false,
    IncludeDeferred = false,
    SortBy = "priority",            // priority, created_at, updated_at, title
    Reverse = true,
    Limit = 50,
    Offset = 0,
});

// result.Issues — List<Issue>
// result.Total  — total count (ignoring pagination)
// result.HasMore — whether more pages exist
```

### Specialized Queries

```csharp
var ready = client.Issues.Ready();       // open + not blocked
var blocked = client.Issues.Blocked();   // issues with unresolved blockers
var stale = client.Issues.Stale(14);     // untouched for N days
var orphans = client.Issues.Orphans();   // subtasks with missing parent
var search = client.Issues.Search("authentication");
var count = client.Issues.Count("status"); // grouped count
```

### Update

```csharp
var updated = client.Issues.Update("bd-abc", new UpdateIssueOptions {
    Title = "New title",
    Priority = 1,
    Assignee = "bob",
    AddLabels = ["new-label"],
    RemoveLabels = ["old-label"],
    Metadata = "{\"sprint\":\"42\"}",
    Claim = true,  // atomically assign + set in_progress
});
```

### Lifecycle

```csharp
client.Issues.Close("bd-abc", new CloseOptions {
    Reason = "Fixed in commit abc123",
    Force = true,      // skip blocker check
    SuggestNext = true, // suggest next issue
});

client.Issues.Reopen("bd-abc");
client.Issues.Defer("bd-abc", DateTime.UtcNow.AddDays(7));
client.Issues.Undefer("bd-abc");
client.Issues.Delete("bd-abc", new DeleteOptions { Force = true });
```

## DependencyService

```csharp
client.Dependencies.Add("bd-blocked", "bd-blocker", "blocks");
client.Dependencies.Add("bd-a", "bd-b", "waits-for");
client.Dependencies.Remove("bd-blocked", "bd-blocker");

List<Dependency> deps = client.Dependencies.List("bd-abc");
List<Issue> blockers = client.Dependencies.GetBlockers("bd-abc");
List<Issue> dependents = client.Dependencies.GetDependents("bd-abc");

DependencyTree tree = client.Dependencies.Tree("bd-abc");
List<List<string>> cycles = client.Dependencies.FindCycles();
bool wouldCycle = client.Dependencies.WouldCreateCycle("bd-a", "bd-b");
```

## LabelService

```csharp
client.Labels.Add("bd-abc", "backend", "urgent");
client.Labels.Remove("bd-abc", "urgent");
List<string> labels = client.Labels.List("bd-abc");
List<string> all = client.Labels.ListAll();
client.Labels.Rename("old-name", "new-name"); // renames across all issues
```

## CommentService

```csharp
Comment comment = client.Comments.Add("bd-abc", "body text", author: "alice");
List<Comment> comments = client.Comments.List("bd-abc");
```

## EpicService

```csharp
EpicStatus status = client.Epics.Status("bd-epic1");
// status.TotalChildren, .ClosedChildren, .ProgressPercent, .EligibleForClose
```

## QueryService

```csharp
client.Queries.Save("my-bugs", new IssueFilter { Types = ["bug"] });
IssueListResult result = client.Queries.Run("my-bugs");
List<string> names = client.Queries.List();
client.Queries.Delete("my-bugs");
```

## SyncService

```csharp
FlushResult flush = client.Sync.Flush();
ImportResult import = client.Sync.Import();
SyncStatus status = client.Sync.Status();
List<HistoryEntry> history = client.Sync.HistoryList();
client.Sync.HistoryRestore("backup-name");
```

## ProjectService

```csharp
Project project = client.Projects.Create("My Project", description: "...", color: "#FF0000", metadata: "{\"team\":\"core\"}");
Project? found = client.Projects.Get("My Project"); // by name or ID
List<Project> all = client.Projects.List(includeArchived: true);
client.Projects.Update(project.Id, name: "Renamed", metadata: "{\"team\":\"platform\"}");
client.Projects.Archive(project.Id);
client.Projects.Delete(project.Id); // fails if has active issues
```

## BoardService

```csharp
Board board = client.Boards.Create(project.Id, "Sprint 1");
Column col = client.Boards.CreateColumn(board.Id, "In Progress", wipLimit: 3);
client.Boards.MoveIssue("bd-abc", col.Id, position: 0);

List<Board> boards = client.Boards.List(project.Id);
List<Column> cols = client.Boards.ListColumns(board.Id);
```

## EventService

```csharp
List<Event> events = client.Events.List(issueId: "bd-abc", eventType: "status_changed");
client.Events.Record("bd-abc", "custom", oldValue: "a", newValue: "b");
```

## Error Handling

All exceptions derive from `BeadsException`:

| Exception | Code | When |
|-----------|------|------|
| `BeadsSchemaException` | 2 | Schema version mismatch |
| `BeadsNotFoundException` | 3 | Issue/resource not found |
| `BeadsAmbiguousIdException` | 3 | ID prefix matches multiple issues |
| `BeadsValidationException` | 4 | Invalid input or blocked action |
| `BeadsDuplicateException` | 4 | Duplicate entry |
| `BeadsCyclicDependencyException` | 5 | Dependency cycle detected |
| `BeadsSyncException` | 6 | Sync operation failure |
| `BeadsConfigException` | 7 | Configuration error |
| `BeadsIOException` | 8 | File system error |

## Models

Key types:

- **`Issue`** — sealed record with all fields (Id, Title, Status, IssueType, Priority, Metadata, etc.)
- **`Dependency`** — IssueId, DependsOnId, DepType
- **`Comment`** — IssueId, Body, Author, CreatedAt
- **`Event`** — audit trail entry
- **`Project`** — Id, Name, Description, Status, Color, Metadata
- **`Board`, `Column`** — Kanban board structure
- **`EpicStatus`** — progress tracking (TotalChildren, ClosedChildren, ProgressPercent)
