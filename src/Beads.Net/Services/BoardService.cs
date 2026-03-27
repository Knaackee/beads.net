using Beads.Net.Errors;
using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class BoardService
{
    private readonly Db _db;

    internal BoardService(Db db) => _db = db;

    // ── Boards ──

    public Board Create(string projectId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BeadsValidationException("Board name must not be empty.");

        var id = IdGenerator.Generate("brd", name, DateTime.UtcNow,
            candidate => _db.QueryScalar<long>(
                $"SELECT COUNT(*) FROM {_db.T("boards")} WHERE id = @id",
                cmd => cmd.Parameters.AddWithValue("@id", candidate)) > 0);

        var maxPos = (int)(_db.QueryScalar<long>(
            $"SELECT COALESCE(MAX(position), -1) FROM {_db.T("boards")} WHERE project_id = @pid",
            cmd => cmd.Parameters.AddWithValue("@pid", projectId)) ?? 0);

        var now = DateTime.UtcNow;
        _db.Execute(
            $"INSERT INTO {_db.T("boards")} (id, project_id, name, position, created_at) VALUES (@id, @pid, @name, @pos, @now)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@pid", projectId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@pos", maxPos + 1);
                cmd.Parameters.AddWithValue("@now", now.ToString("o"));
            });

        return new Board { Id = id, ProjectId = projectId, Name = name, Position = maxPos + 1, CreatedAt = now };
    }

    public List<Board> List(string projectId)
    {
        return _db.Query(
            $"SELECT * FROM {_db.T("boards")} WHERE project_id = @pid ORDER BY position",
            cmd => cmd.Parameters.AddWithValue("@pid", projectId),
            ReadBoard);
    }

    public Board Update(string id, string? name = null, int? position = null)
    {
        var sets = new List<string>();
        Action<SqliteCommand> setter = cmd => cmd.Parameters.AddWithValue("@id", id);

        if (name != null) { sets.Add("name = @name"); var p = setter; setter = c => { p(c); c.Parameters.AddWithValue("@name", name); }; }
        if (position.HasValue) { sets.Add("position = @pos"); var p = setter; setter = c => { p(c); c.Parameters.AddWithValue("@pos", position.Value); }; }

        if (sets.Count > 0)
            _db.Execute($"UPDATE {_db.T("boards")} SET {string.Join(", ", sets)} WHERE id = @id", setter);

        return _db.QuerySingle(
            $"SELECT * FROM {_db.T("boards")} WHERE id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", id),
            ReadBoard) ?? throw new BeadsNotFoundException($"Board not found: {id}");
    }

    public void Delete(string id)
    {
        _db.Execute(
            $"DELETE FROM {_db.T("boards")} WHERE id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", id));
    }

    // ── Columns ──

    public Column CreateColumn(string boardId, string name, int? wipLimit = null, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BeadsValidationException("Column name must not be empty.");

        var id = IdGenerator.Generate("col", name, DateTime.UtcNow,
            candidate => _db.QueryScalar<long>(
                $"SELECT COUNT(*) FROM {_db.T("columns")} WHERE id = @id",
                cmd => cmd.Parameters.AddWithValue("@id", candidate)) > 0);

        var maxPos = (int)(_db.QueryScalar<long>(
            $"SELECT COALESCE(MAX(position), -1) FROM {_db.T("columns")} WHERE board_id = @bid",
            cmd => cmd.Parameters.AddWithValue("@bid", boardId)) ?? 0);

        var now = DateTime.UtcNow;
        _db.Execute(
            $"INSERT INTO {_db.T("columns")} (id, board_id, name, position, wip_limit, color, created_at) VALUES (@id, @bid, @name, @pos, @wip, @color, @now)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@bid", boardId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@pos", maxPos + 1);
                cmd.Parameters.AddWithValue("@wip", wipLimit.HasValue ? wipLimit.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@color", (object?)color ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@now", now.ToString("o"));
            });

        return new Column { Id = id, BoardId = boardId, Name = name, Position = maxPos + 1, WipLimit = wipLimit, Color = color, CreatedAt = now };
    }

    public List<Column> ListColumns(string boardId)
    {
        return _db.Query(
            $"SELECT * FROM {_db.T("columns")} WHERE board_id = @bid ORDER BY position",
            cmd => cmd.Parameters.AddWithValue("@bid", boardId),
            ReadColumn);
    }

    public Column UpdateColumn(string id, string? name = null, int? position = null, int? wipLimit = null)
    {
        var sets = new List<string>();
        Action<SqliteCommand> setter = cmd => cmd.Parameters.AddWithValue("@id", id);

        if (name != null) { sets.Add("name = @name"); var p = setter; setter = c => { p(c); c.Parameters.AddWithValue("@name", name); }; }
        if (position.HasValue) { sets.Add("position = @pos"); var p = setter; setter = c => { p(c); c.Parameters.AddWithValue("@pos", position.Value); }; }
        if (wipLimit.HasValue) { sets.Add("wip_limit = @wip"); var p = setter; setter = c => { p(c); c.Parameters.AddWithValue("@wip", wipLimit.Value); }; }

        if (sets.Count > 0)
            _db.Execute($"UPDATE {_db.T("columns")} SET {string.Join(", ", sets)} WHERE id = @id", setter);

        return _db.QuerySingle(
            $"SELECT * FROM {_db.T("columns")} WHERE id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", id),
            ReadColumn) ?? throw new BeadsNotFoundException($"Column not found: {id}");
    }

    public void DeleteColumn(string id)
    {
        // Unassign issues from this column first
        _db.Execute(
            $"UPDATE {_db.T("issues")} SET column_id = NULL WHERE column_id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", id));
        _db.Execute(
            $"DELETE FROM {_db.T("columns")} WHERE id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", id));
    }

    // ── Issue ↔ Column ──

    public void MoveIssue(string issueId, string columnId, int? position = null)
    {
        var pos = position ?? (int)(_db.QueryScalar<long>(
            $"SELECT COALESCE(MAX(position), -1) + 1 FROM {_db.T("issues")} WHERE column_id = @cid",
            cmd => cmd.Parameters.AddWithValue("@cid", columnId)) ?? 0);

        _db.Execute(
            $"UPDATE {_db.T("issues")} SET column_id = @cid, position = @pos, updated_at = @now WHERE id = @id",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@cid", columnId);
                cmd.Parameters.AddWithValue("@pos", pos);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@id", issueId);
            });
    }

    private static Board ReadBoard(SqliteDataReader r)
    {
        return new Board
        {
            Id = r.GetString(r.GetOrdinal("id")),
            ProjectId = r.GetString(r.GetOrdinal("project_id")),
            Name = r.GetString(r.GetOrdinal("name")),
            Position = r.GetInt32(r.GetOrdinal("position")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        };
    }

    private static Column ReadColumn(SqliteDataReader r)
    {
        return new Column
        {
            Id = r.GetString(r.GetOrdinal("id")),
            BoardId = r.GetString(r.GetOrdinal("board_id")),
            Name = r.GetString(r.GetOrdinal("name")),
            Position = r.GetInt32(r.GetOrdinal("position")),
            WipLimit = r.GetNullableInt("wip_limit"),
            Color = r.GetNullableString("color"),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        };
    }
}
