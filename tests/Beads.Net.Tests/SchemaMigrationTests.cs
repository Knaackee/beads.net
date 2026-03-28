using Beads.Net;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Tests;

public sealed class SchemaMigrationTests
{
    [Fact]
    public void EnsureSchema_MigratesV1AndAddsMetadataColumns()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        // Simulate a v1 database with projects/issues tables that predate metadata.
        Execute(conn, "CREATE TABLE beads_metadata (key TEXT NOT NULL, value TEXT NOT NULL)");
        Execute(conn, "INSERT INTO beads_metadata (key, value) VALUES ('schema_version', '1')");

        Execute(conn, """
            CREATE TABLE beads_projects (
                id          TEXT PRIMARY KEY,
                name        TEXT NOT NULL,
                description TEXT DEFAULT '',
                status      TEXT NOT NULL DEFAULT 'active',
                color       TEXT,
                created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);

        Execute(conn, """
            CREATE TABLE beads_issues (
                id                  TEXT PRIMARY KEY,
                content_hash        TEXT,
                title               TEXT NOT NULL,
                description         TEXT NOT NULL DEFAULT '',
                design              TEXT NOT NULL DEFAULT '',
                acceptance_criteria TEXT NOT NULL DEFAULT '',
                notes               TEXT NOT NULL DEFAULT '',
                status              TEXT NOT NULL DEFAULT 'open',
                priority            INTEGER NOT NULL DEFAULT 2,
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
            """);

        Execute(conn, """
            INSERT INTO beads_projects (id, name, description, status, color, created_at, updated_at)
            VALUES ('prj-old', 'Legacy', '', 'active', NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """);

        using var client = new BeadsClient(conn);

        Assert.Equal(2, client.Schema.GetSchemaVersion());
        Assert.True(ColumnExists(conn, "beads_projects", "metadata"));
        Assert.True(ColumnExists(conn, "beads_issues", "metadata"));

        var project = client.Projects.Get("prj-old");
        Assert.NotNull(project);
        Assert.Equal("{}", project!.Metadata);
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection conn, string tableName, string columnName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var name = reader.GetString(reader.GetOrdinal("name"));
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
