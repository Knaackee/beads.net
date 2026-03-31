# Beads CLI

A terminal-based issue tracker powered by Beads.Net. Fast, local-first, works offline.

## Installation

```bash
dotnet tool install -g Beads.Cli
```

Or install the latest standalone release binary:

```powershell
# Windows (PowerShell)
Invoke-WebRequest -Uri https://github.com/Knaackee/beads.net/releases/latest/download/beads-win-x64.exe -OutFile beads.exe; Move-Item beads.exe "$env:LOCALAPPDATA\Microsoft\WindowsApps\beads.exe" -Force
```

```bash
# Linux
curl -Lo beads https://github.com/Knaackee/beads.net/releases/latest/download/beads-linux-x64 && chmod +x beads && sudo mv beads /usr/local/bin/

# macOS (Apple Silicon)
curl -Lo beads https://github.com/Knaackee/beads.net/releases/latest/download/beads-osx-arm64 && chmod +x beads && sudo mv beads /usr/local/bin/
```

## Global Options

| Option | Description | Default |
|--------|-------------|---------|
| `--db <path>` | SQLite database path | `.beads/beads.db` |
| `--prefix <str>` | Table prefix | `beads_` |
| `--actor <name>` | Actor for audit trail | |
| `--json` | JSON output | |
| `-q, --quiet` | Only show errors | |
| `-v, --verbose` | Verbose logging | |

## Commands

### Initialization

```bash
beads init                            # Initialize workspace at .beads/beads.db
beads init --db path/to/beads.db      # Custom path
```

### Creating Issues

```bash
beads create "Title"
beads create "Fix login" --type bug --priority 0 --assignee alice
beads create "Fix login" --metadata '{"source":"cli"}'
beads create "Epic task" --type epic --desc "Description" --design "Design notes"
beads create "Child" --parent bd-epic1 --project proj-abc
beads create "Labeled" --label backend --label urgent
beads create "Dependent" --depends-on bd-abc123

# Quick capture — prints only the ID
beads q "Quick note"
```

### Viewing Issues

```bash
beads show bd-abc123              # Detailed view
beads show bd-abc123 --json       # JSON output
```

### Listing & Filtering

```bash
beads list                        # All open issues
beads list --type bug             # Filter by type
beads list --assignee alice       # Filter by assignee
beads list --unassigned           # Unassigned only
beads list --label backend        # Filter by label
beads list --priority 0           # Filter by priority
beads list --sort priority        # Sort by field
beads list --reverse              # Reverse sort order
beads list --limit 10 --offset 20 # Pagination
beads list --include-closed       # Include closed issues
beads list --json                 # JSON output
```

### Specialized Queries

```bash
beads ready                       # Open + not blocked
beads blocked                     # Issues with unresolved blockers
beads search "authentication"     # Full-text search
beads count                       # Total count
beads count --by status           # Grouped count
beads stale                       # Untouched for 14 days (default)
beads stale --days 7              # Custom threshold
beads orphans                     # Subtasks with missing parent
```

### Updating Issues

```bash
beads update bd-abc --title "New title"
beads update bd-abc --priority 0 --assignee bob
beads update bd-abc --type bug
beads update bd-abc --metadata '{"sprint":"42"}'
beads update bd-abc --add-label urgent --remove-label wontfix
beads update bd-abc --claim       # Assign to self + set in_progress
```

### Lifecycle

```bash
beads close bd-abc                # Close issue
beads close bd-abc --reason "Fixed in PR #42"
beads close bd-abc --force        # Skip blocker check
beads close bd-abc --suggest-next # Suggest next issue to work on

beads reopen bd-abc               # Reopen closed issue

beads defer bd-abc                # Defer indefinitely
beads defer bd-abc --until 2025-06-01  # Defer until date
beads undefer bd-abc              # Remove deferral

beads delete bd-abc               # Delete (requires --force for safety)
beads delete bd-abc --force --reason "Duplicate"
```

### Dependencies

```bash
beads dep add bd-a bd-b           # bd-a is blocked by bd-b
beads dep add bd-a bd-b --type waits-for
beads dep remove bd-a bd-b
beads dep list bd-a               # Show dependencies for issue
beads dep tree bd-a               # Show dependency tree
beads dep cycles                  # Detect circular dependencies
```

### Labels

```bash
beads label add bd-abc backend urgent
beads label remove bd-abc urgent
beads label list bd-abc           # Labels for issue
beads label list-all              # All unique labels
beads label rename old-name new-name
```

### Comments

```bash
beads comments add bd-abc "Comment body"
beads comments add bd-abc "By Alice" --author alice
beads comments list bd-abc
```

### Epics

```bash
beads epic status bd-epic1        # Progress: X/Y (Z%)
beads epic close-eligible bd-epic1
```

### Saved Queries

```bash
beads query save my-bugs --type bug --assignee alice
beads query run my-bugs
beads query list
beads query delete my-bugs
```

### Workflow

```bash
beads defer bd-abc --until 2025-12-31
beads undefer bd-abc
```

### Sync

```bash
beads sync flush                  # Export dirty issues to JSONL
beads sync import                 # Import from JSONL
beads sync status                 # Show sync state
```

### Configuration

```bash
beads config list                 # Show all config
beads config get actor            # Get config value
beads config set actor alice      # Set config value
beads config delete actor         # Remove config value
beads config path                 # Show config file location
```

### Diagnostics

```bash
beads stats                       # Project statistics
beads doctor                      # Health check
beads version                     # Version info
beads info                        # Workspace metadata
beads where                       # Database path
beads lint                        # Check issues for problems
beads changelog                   # Generate changelog
beads changelog --since 2025-01-01
beads changelog --format json
beads graph bd-abc                # Text dependency graph
beads graph bd-abc --format mermaid
```

### Audit Trail

```bash
beads audit                       # All events
beads audit --issue bd-abc        # Events for issue
beads audit --type status_changed # Filter by type
```

### History

```bash
beads history list                # Backup snapshots
beads history restore <backup>    # Restore from backup
```

### Projects

```bash
beads project create "My Project" --desc "..." --color "#FF0000" --metadata '{"team":"core"}'
beads project list
beads project list --include-archived
beads project show proj-abc
beads project update proj-abc --name "Renamed" --metadata '{"team":"platform"}'
beads project archive proj-abc
beads project delete proj-abc
```

### Boards

```bash
beads board create proj-abc "Sprint 1"
beads board list proj-abc
beads board columns board-id
beads board add-column board-id "In Progress" --wip-limit 3
beads board move bd-abc col-id
```

## JSON Output

All list/show commands support `--json` for machine-readable output:

```bash
beads list --json | jq '.issues[].title'
beads show bd-abc --json | jq '.status'
beads stats --json
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | General error |
| 2 | Schema error |
| 3 | Not found / Ambiguous ID |
| 4 | Validation / Duplicate |
| 5 | Cyclic dependency |
| 6 | Sync error |
| 7 | Config error |
| 8 | I/O error |
