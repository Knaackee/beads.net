# Beads.Net

A local-first, offline-capable issue tracker — built as a .NET library and CLI tool, backed by SQLite.

Beads.Net is a complete port of the [beads_rust](https://github.com/example/beads_rust) project management system to .NET, designed for solo developers and small teams who want fast, private issue tracking without a server.

## Projects

| Package | Description |
|---------|-------------|
| [Beads.Net](docs/library.md) | Core library — embed issue tracking into any .NET app |
| [Beads.Cli](docs/cli.md) | CLI tool — `beads` command for terminal-based issue tracking |

## Quick Start

### CLI

```bash
# Install as a global tool
dotnet tool install -g Beads.Cli

# Initialize a workspace
beads init

# Create issues
beads create "Implement authentication" --type task --priority 0
beads create "Track rollout" --metadata '{"owner":"platform"}'
beads q "Fix login bug"

# List & filter
beads list
beads list --type bug --assignee alice
beads ready
beads blocked

# Manage
beads close bd-abc123
beads dep add bd-abc123 bd-def456
beads label add bd-abc123 backend urgent
```

### Library

```csharp
using Beads.Net;

using var client = BeadsClient.Init("my-project.db");

var issue = client.Issues.Create("Fix login page", new() {
    IssueType = "bug",
    Priority = 0,
    Assignee = "alice",
    Labels = ["frontend", "urgent"],
    Metadata = "{\"owner\":\"platform\"}",
});

var project = client.Projects.Create("Portal", metadata: "{\"domain\":\"customer\"}");

client.Dependencies.Add(issue.Id, otherIssue.Id);
client.Comments.Add(issue.Id, "Investigating root cause");

var ready = client.Issues.Ready();
var stats = client.Stats.GetStats();
```

## Features

- **Local-first** — everything in a single SQLite file, works offline
- **Full issue lifecycle** — create, update, close, reopen, delete with audit trail
- **Dependencies** — blocks/waits-for with cycle detection and dependency trees
- **Labels** — attach, remove, rename across all issues
- **Epics** — parent-child hierarchy with progress tracking
- **Projects & Boards** — Kanban-style project management with WIP limits
- **Metadata fields** — attach custom JSON metadata on issues and projects
- **Saved queries** — persist common filters and rerun them
- **Sync** — JSONL flush/import for backup and collaboration
- **Doctor** — health checks, orphan detection, schema validation
- **Lint** — automated issue quality checks
- **Changelog** — generate markdown/JSON changelogs from closed issues

## Architecture

```
Beads.Net (library/NuGet)
├── Models/       Immutable records: Issue, Dependency, Comment, Event, etc.
├── Services/     12 service classes with all business logic
├── Schema/       SQLite schema management and migrations
├── Enums/        IssueStatus, Priority, IssueType, DependencyType, EventType
├── Errors/       Typed exceptions (NotFound, Validation, CyclicDependency, etc.)
└── BeadsClient   Facade exposing all services

Beads.Cli (dotnet tool)
├── Program.cs    Root command + registration
├── Commands/     Thin command handlers (~5-15 lines each)
└── Globals.cs    Shared options (--db, --json, --quiet, etc.)
```

## Building

```bash
dotnet build
```

## Testing

```bash
# Unit tests (143 tests × 2 TFMs)
dotnet test tests/Beads.Net.Tests

# CLI smoke tests (18 tests)
dotnet test tests/Beads.Cli.Tests

# Benchmarks
dotnet run --project tests/Beads.Net.Benchmarks -c Release -- --filter "*Issue*"
```

## Requirements

- .NET 8.0 or .NET 10.0
- No external services — SQLite only

## License

MIT
