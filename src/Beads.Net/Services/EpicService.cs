using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class EpicService
{
    private readonly Db _db;

    internal EpicService(Db db) => _db = db;

    public EpicStatus Status(string epicId)
    {
        var d = _db.T("dependencies");
        var t = _db.T("issues");

        var epic = _db.QuerySingle(
            $"SELECT * FROM {t} WHERE id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", epicId),
            IssueService.ReadIssue)
            ?? throw new Errors.BeadsNotFoundException($"Epic not found: {epicId}");

        var total = (int)(_db.QueryScalar<long>(
            $"SELECT COUNT(*) FROM {d} dep INNER JOIN {t} i ON i.id = dep.issue_id WHERE dep.depends_on_id = @id AND dep.type = 'parent-child'",
            cmd => cmd.Parameters.AddWithValue("@id", epicId)) ?? 0);

        var closed = (int)(_db.QueryScalar<long>(
            $"SELECT COUNT(*) FROM {d} dep INNER JOIN {t} i ON i.id = dep.issue_id WHERE dep.depends_on_id = @id AND dep.type = 'parent-child' AND i.status IN ('closed','tombstone')",
            cmd => cmd.Parameters.AddWithValue("@id", epicId)) ?? 0);

        return new EpicStatus
        {
            Epic = epic,
            TotalChildren = total,
            ClosedChildren = closed,
        };
    }

    public EpicStatus CloseEligible(string epicId) => Status(epicId);
}
