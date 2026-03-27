using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class EventService
{
    private readonly Db _db;
    private readonly BeadsConfig _config;

    internal EventService(Db db, BeadsConfig config)
    {
        _db = db;
        _config = config;
    }

    public List<Event> List(string? issueId = null, string? eventType = null)
    {
        var t = _db.T("events");
        var clauses = new List<string>();
        Action<SqliteCommand>? setter = null;

        if (issueId != null)
        {
            clauses.Add("issue_id = @id");
            var prev = setter;
            setter = cmd => { prev?.Invoke(cmd); cmd.Parameters.AddWithValue("@id", issueId); };
        }

        if (eventType != null)
        {
            clauses.Add("event_type = @et");
            var prev = setter;
            setter = cmd => { prev?.Invoke(cmd); cmd.Parameters.AddWithValue("@et", eventType); };
        }

        var where = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : "";
        return _db.Query(
            $"SELECT * FROM {t}{where} ORDER BY created_at DESC",
            setter,
            ReadEvent);
    }

    public void Record(string issueId, string eventType, string? oldValue = null, string? newValue = null, string? comment = null)
    {
        _db.Execute(
            $"INSERT INTO {_db.T("events")} (issue_id, event_type, actor, old_value, new_value, comment, created_at) VALUES (@id, @et, @actor, @old, @new, @comment, @now)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@et", eventType);
                cmd.Parameters.AddWithValue("@actor", _config.Actor);
                cmd.Parameters.AddWithValue("@old", (object?)oldValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@new", (object?)newValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@comment", (object?)comment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            });
    }

    private static Event ReadEvent(SqliteDataReader r)
    {
        return new Event
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            IssueId = r.GetString(r.GetOrdinal("issue_id")),
            EventType = r.GetStringOrEmpty("event_type"),
            Actor = r.GetStringOrEmpty("actor"),
            OldValue = r.GetNullableString("old_value"),
            NewValue = r.GetNullableString("new_value"),
            Comment = r.GetNullableString("comment"),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        };
    }
}
