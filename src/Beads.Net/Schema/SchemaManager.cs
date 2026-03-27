using Beads.Net.Internal;

namespace Beads.Net.Schema;

public sealed class SchemaManager
{
    private const int CurrentSchemaVersion = 1;
    private readonly Db _db;

    internal SchemaManager(Db db) => _db = db;

    public void EnsureSchema()
    {
        using var tx = _db.BeginTransaction();
        CreateTables();
        CreateIndexes();
        SetMetadata("schema_version", CurrentSchemaVersion.ToString());
        tx.Commit();
    }

    private void CreateTables()
    {
        _db.Execute(_db.Sql("""
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
                project_id          TEXT,
                column_id           TEXT,
                position            INTEGER NOT NULL DEFAULT 0
            )
            """));

        _db.Execute(_db.Sql("""
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
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}labels (
                issue_id TEXT NOT NULL,
                label    TEXT NOT NULL,
                PRIMARY KEY (issue_id, label),
                FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}comments (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                issue_id   TEXT NOT NULL,
                author     TEXT NOT NULL DEFAULT '',
                text       TEXT NOT NULL DEFAULT '',
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
            )
            """));

        _db.Execute(_db.Sql("""
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
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}config (
                key   TEXT NOT NULL,
                value TEXT NOT NULL
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}metadata (
                key   TEXT NOT NULL,
                value TEXT NOT NULL
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}dirty_issues (
                issue_id  TEXT PRIMARY KEY,
                marked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}export_hashes (
                issue_id     TEXT PRIMARY KEY,
                content_hash TEXT NOT NULL,
                exported_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}blocked_issues_cache (
                issue_id   TEXT PRIMARY KEY,
                blocked_by TEXT NOT NULL,
                blocked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (issue_id) REFERENCES {p}issues(id) ON DELETE CASCADE
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}child_counters (
                parent_id  TEXT PRIMARY KEY,
                last_child INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (parent_id) REFERENCES {p}issues(id) ON DELETE CASCADE
            )
            """));

        // Extension tables
        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}projects (
                id          TEXT PRIMARY KEY,
                name        TEXT NOT NULL,
                description TEXT DEFAULT '',
                status      TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'archived')),
                color       TEXT,
                created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}boards (
                id         TEXT PRIMARY KEY,
                project_id TEXT NOT NULL REFERENCES {p}projects(id) ON DELETE CASCADE,
                name       TEXT NOT NULL,
                position   INTEGER NOT NULL DEFAULT 0,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """));

        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}columns (
                id         TEXT PRIMARY KEY,
                board_id   TEXT NOT NULL REFERENCES {p}boards(id) ON DELETE CASCADE,
                name       TEXT NOT NULL,
                position   INTEGER NOT NULL DEFAULT 0,
                wip_limit  INTEGER,
                color      TEXT,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """));

        // Saved queries
        _db.Execute(_db.Sql("""
            CREATE TABLE IF NOT EXISTS {p}saved_queries (
                name       TEXT PRIMARY KEY,
                filter_json TEXT NOT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """));
    }

    private void CreateIndexes()
    {
        // Issues indexes
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_status ON {p}issues(status)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_priority ON {p}issues(priority)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_issue_type ON {p}issues(issue_type)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_assignee ON {p}issues(assignee) WHERE assignee IS NOT NULL"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_created_at ON {p}issues(created_at)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_updated_at ON {p}issues(updated_at)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_content_hash ON {p}issues(content_hash)"));
        _db.Execute(_db.Sql("CREATE UNIQUE INDEX IF NOT EXISTS {p}idx_issues_external_ref ON {p}issues(external_ref) WHERE external_ref IS NOT NULL"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_ephemeral ON {p}issues(ephemeral) WHERE ephemeral = 1"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_pinned ON {p}issues(pinned) WHERE pinned = 1"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_tombstone ON {p}issues(status) WHERE status = 'tombstone'"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_due_at ON {p}issues(due_at) WHERE due_at IS NOT NULL"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_defer_until ON {p}issues(defer_until) WHERE defer_until IS NOT NULL"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_ready ON {p}issues(status, priority, created_at) WHERE status = 'open' AND ephemeral = 0 AND pinned = 0 AND is_template = 0"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_list_active ON {p}issues(priority, created_at DESC) WHERE status NOT IN ('closed', 'tombstone') AND (is_template = 0 OR is_template IS NULL)"));

        // Dependencies indexes
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_deps_issue ON {p}dependencies(issue_id)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_deps_depends_on ON {p}dependencies(depends_on_id)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_deps_type ON {p}dependencies(type)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_deps_depends_on_type ON {p}dependencies(depends_on_id, type)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_deps_thread ON {p}dependencies(thread_id) WHERE thread_id != ''"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_deps_blocking ON {p}dependencies(depends_on_id, issue_id) WHERE (type = 'blocks' OR type = 'parent-child' OR type = 'conditional-blocks' OR type = 'waits-for')"));

        // Labels indexes
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_labels_label ON {p}labels(label)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_labels_issue ON {p}labels(issue_id)"));

        // Comments indexes
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_comments_issue ON {p}comments(issue_id)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_comments_created ON {p}comments(created_at)"));

        // Events indexes
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_events_issue ON {p}events(issue_id)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_events_type ON {p}events(event_type)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_events_created ON {p}events(created_at)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_events_actor ON {p}events(actor) WHERE actor != ''"));

        // Dirty/cache indexes
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_dirty_marked ON {p}dirty_issues(marked_at)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_blocked_at ON {p}blocked_issues_cache(blocked_at)"));

        // Extension indexes
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_project ON {p}issues(project_id)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_issues_column ON {p}issues(column_id)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_boards_project ON {p}boards(project_id)"));
        _db.Execute(_db.Sql("CREATE INDEX IF NOT EXISTS {p}idx_columns_board ON {p}columns(board_id)"));
    }

    public int GetSchemaVersion()
    {
        var val = GetMetadata("schema_version");
        return val != null && int.TryParse(val, out var v) ? v : 0;
    }

    public string? GetMetadata(string key)
    {
        var table = _db.T("metadata");
        return _db.QueryScalarString($"SELECT value FROM {table} WHERE key = '{key}'");
    }

    public void SetMetadata(string key, string value)
    {
        var table = _db.T("metadata");
        _db.Execute($"DELETE FROM {table} WHERE key = @key", cmd =>
            cmd.Parameters.AddWithValue("@key", key));
        _db.Execute($"INSERT INTO {table} (key, value) VALUES (@key, @value)", cmd =>
        {
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
        });
    }

    public bool TableExists(string tableName)
    {
        var result = _db.QueryScalar<long>($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{_db.T(tableName)}'");
        return result > 0;
    }
}
