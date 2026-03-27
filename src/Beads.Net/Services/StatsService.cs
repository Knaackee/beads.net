using Beads.Net.Internal;
using Beads.Net.Models;

namespace Beads.Net.Services;

public sealed class StatsService
{
    private readonly Db _db;

    internal StatsService(Db db) => _db = db;

    public ProjectStats GetStats()
    {
        var t = _db.T("issues");

        var total = (int)(_db.QueryScalar<long>($"SELECT COUNT(*) FROM {t} WHERE status != 'tombstone'") ?? 0);
        var open = (int)(_db.QueryScalar<long>($"SELECT COUNT(*) FROM {t} WHERE status = 'open'") ?? 0);
        var closed = (int)(_db.QueryScalar<long>($"SELECT COUNT(*) FROM {t} WHERE status = 'closed'") ?? 0);
        var blocked = (int)(_db.QueryScalar<long>($"SELECT COUNT(*) FROM {t} WHERE status = 'blocked'") ?? 0);
        var overdue = (int)(_db.QueryScalar<long>($"SELECT COUNT(*) FROM {t} WHERE due_at IS NOT NULL AND due_at < datetime('now') AND status NOT IN ('closed','tombstone')") ?? 0);

        var byStatus = GroupBy(t, "status");
        var byType = GroupBy(t, "issue_type");
        var byPriority = GroupBy(t, "priority");
        var byAssignee = GroupBy(t, "COALESCE(assignee, 'unassigned')");

        var lastActivity = _db.QueryScalarString($"SELECT MAX(updated_at) FROM {t}");
        DateTime? lastActivityDt = lastActivity != null ? DateTime.TryParse(lastActivity, out var dt) ? dt : null : null;

        return new ProjectStats
        {
            TotalIssues = total,
            OpenCount = open,
            ClosedCount = closed,
            BlockedCount = blocked,
            OverdueCount = overdue,
            ByStatus = byStatus,
            ByType = byType,
            ByPriority = byPriority,
            ByAssignee = byAssignee,
            LastActivity = lastActivityDt,
        };
    }

    private Dictionary<string, int> GroupBy(string table, string column)
    {
        var groups = new Dictionary<string, int>();
        _db.Query(
            $"SELECT {column} as grp, COUNT(*) as cnt FROM {table} WHERE status != 'tombstone' GROUP BY {column} ORDER BY cnt DESC",
            null,
            r =>
            {
                groups[r.GetString(0)] = r.GetInt32(1);
                return "";
            });
        return groups;
    }
}
