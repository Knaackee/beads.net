using Beads.Net.Internal;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class LabelService
{
    private readonly Db _db;
    private readonly EventService _events;

    internal LabelService(Db db, EventService events)
    {
        _db = db;
        _events = events;
    }

    public void Add(string issueId, params string[] labels)
    {
        foreach (var label in labels)
        {
            _db.Execute(
                $"INSERT OR IGNORE INTO {_db.T("labels")} (issue_id, label) VALUES (@id, @lbl)",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@id", issueId);
                    cmd.Parameters.AddWithValue("@lbl", label.Trim());
                });
            _events.Record(issueId, "label_added", null, label.Trim());
        }
    }

    public void Remove(string issueId, params string[] labels)
    {
        foreach (var label in labels)
        {
            var deleted = _db.Execute(
                $"DELETE FROM {_db.T("labels")} WHERE issue_id = @id AND label = @lbl",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@id", issueId);
                    cmd.Parameters.AddWithValue("@lbl", label.Trim());
                });
            if (deleted > 0)
                _events.Record(issueId, "label_removed", label.Trim());
        }
    }

    public List<string> List(string? issueId = null)
    {
        if (issueId != null)
        {
            return _db.Query(
                $"SELECT label FROM {_db.T("labels")} WHERE issue_id = @id ORDER BY label",
                cmd => cmd.Parameters.AddWithValue("@id", issueId),
                r => r.GetString(0));
        }

        return ListAll();
    }

    public List<string> ListAll()
    {
        return _db.Query(
            $"SELECT DISTINCT label FROM {_db.T("labels")} ORDER BY label",
            null,
            r => r.GetString(0));
    }

    public void Rename(string oldName, string newName)
    {
        _db.Execute(
            $"UPDATE {_db.T("labels")} SET label = @new WHERE label = @old",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@old", oldName);
                cmd.Parameters.AddWithValue("@new", newName);
            });
    }
}
