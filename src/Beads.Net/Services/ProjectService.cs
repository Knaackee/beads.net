using Beads.Net.Errors;
using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class ProjectService
{
    private readonly Db _db;

    internal ProjectService(Db db) => _db = db;

    public Project Create(string name, string? description = null, string? color = null, string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BeadsValidationException("Project name must not be empty.");

        var id = IdGenerator.Generate("prj", name, DateTime.UtcNow,
            candidate => _db.QueryScalar<long>(
                $"SELECT COUNT(*) FROM {_db.T("projects")} WHERE id = @id",
                cmd => cmd.Parameters.AddWithValue("@id", candidate)) > 0);

        var now = DateTime.UtcNow;
        _db.Execute(
            $"INSERT INTO {_db.T("projects")} (id, name, description, status, color, metadata, created_at, updated_at) VALUES (@id, @name, @desc, 'active', @color, @meta, @now, @now)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", description ?? "");
                cmd.Parameters.AddWithValue("@color", (object?)color ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@meta", metadata ?? "{}");
                cmd.Parameters.AddWithValue("@now", now.ToString("o"));
            });

        return new Project
        {
            Id = id,
            Name = name,
            Description = description ?? "",
            Status = "active",
            Color = color,
            Metadata = metadata ?? "{}",
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public Project? Get(string idOrName)
    {
        var t = _db.T("projects");
        return _db.QuerySingle(
            $"SELECT * FROM {t} WHERE id = @v OR name = @v",
            cmd => cmd.Parameters.AddWithValue("@v", idOrName),
            ReadProject);
    }

    public List<Project> List(bool includeArchived = false)
    {
        var t = _db.T("projects");
        var where = includeArchived ? "" : " WHERE status = 'active'";
        return _db.Query($"SELECT * FROM {t}{where} ORDER BY name", null, ReadProject);
    }

    public Project Update(string id, string? name = null, string? description = null, string? metadata = null)
    {
        var existing = Get(id) ?? throw new BeadsNotFoundException($"Project not found: {id}");
        var sets = new List<string> { "updated_at = @now" };
        Action<SqliteCommand> setter = cmd =>
        {
            cmd.Parameters.AddWithValue("@id", existing.Id);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        };

        if (name != null)
        {
            sets.Add("name = @name");
            var prev = setter;
            setter = cmd => { prev(cmd); cmd.Parameters.AddWithValue("@name", name); };
        }
        if (description != null)
        {
            sets.Add("description = @desc");
            var prev = setter;
            setter = cmd => { prev(cmd); cmd.Parameters.AddWithValue("@desc", description); };
        }
        if (metadata != null)
        {
            sets.Add("metadata = @meta");
            var prev = setter;
            setter = cmd => { prev(cmd); cmd.Parameters.AddWithValue("@meta", metadata); };
        }

        _db.Execute($"UPDATE {_db.T("projects")} SET {string.Join(", ", sets)} WHERE id = @id", setter);
        return Get(existing.Id)!;
    }

    public void Archive(string id)
    {
        var existing = Get(id) ?? throw new BeadsNotFoundException($"Project not found: {id}");
        _db.Execute(
            $"UPDATE {_db.T("projects")} SET status = 'archived', updated_at = @now WHERE id = @id",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", existing.Id);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            });
    }

    public void Delete(string id)
    {
        var existing = Get(id) ?? throw new BeadsNotFoundException($"Project not found: {id}");
        var issueCount = (int)(_db.QueryScalar<long>(
            $"SELECT COUNT(*) FROM {_db.T("issues")} WHERE project_id = @id AND status != 'tombstone'",
            cmd => cmd.Parameters.AddWithValue("@id", existing.Id)) ?? 0);

        if (issueCount > 0)
            throw new BeadsValidationException($"Cannot delete project with {issueCount} active issue(s). Move or close them first.");

        _db.Execute(
            $"DELETE FROM {_db.T("projects")} WHERE id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", existing.Id));
    }

    private static Project ReadProject(SqliteDataReader r)
    {
        return new Project
        {
            Id = r.GetString(r.GetOrdinal("id")),
            Name = r.GetString(r.GetOrdinal("name")),
            Description = r.GetStringOrEmpty("description"),
            Status = r.GetStringOrEmpty("status"),
            Color = r.GetNullableString("color"),
            Metadata = r.GetNullableString("metadata") ?? "{}",
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
            UpdatedAt = r.GetDateTime(r.GetOrdinal("updated_at")),
        };
    }
}
