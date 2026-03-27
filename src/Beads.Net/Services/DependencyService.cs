using Beads.Net.Errors;
using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class DependencyService
{
    private readonly Db _db;
    private readonly BeadsConfig _config;
    private readonly EventService _events;

    internal DependencyService(Db db, BeadsConfig config, EventService events)
    {
        _db = db;
        _config = config;
        _events = events;
    }

    public void Add(string issueId, string dependsOnId, string depType = "blocks")
    {
        if (issueId == dependsOnId)
            throw new BeadsCyclicDependencyException("Cannot add self-dependency.");

        if (WouldCreateCycle(issueId, dependsOnId))
            throw new BeadsCyclicDependencyException($"Adding dependency {issueId} → {dependsOnId} would create a cycle.");

        _db.Execute(
            $"INSERT OR IGNORE INTO {_db.T("dependencies")} (issue_id, depends_on_id, type, created_at, created_by) VALUES (@id, @dep, @type, @now, @by)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@dep", dependsOnId);
                cmd.Parameters.AddWithValue("@type", depType);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@by", _config.Actor);
            });

        _events.Record(issueId, "dependency_added", null, $"{depType}:{dependsOnId}");
    }

    public void Remove(string issueId, string dependsOnId)
    {
        var deleted = _db.Execute(
            $"DELETE FROM {_db.T("dependencies")} WHERE issue_id = @id AND depends_on_id = @dep",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@dep", dependsOnId);
            });

        if (deleted > 0)
            _events.Record(issueId, "dependency_removed", dependsOnId);
    }

    public List<Dependency> List(string issueId)
    {
        return _db.Query(
            $"SELECT * FROM {_db.T("dependencies")} WHERE issue_id = @id OR depends_on_id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            IssueService.ReadDependency);
    }

    public List<Issue> GetBlockers(string issueId)
    {
        var d = _db.T("dependencies");
        var t = _db.T("issues");
        return _db.Query(
            $"SELECT i.* FROM {t} i INNER JOIN {d} dep ON dep.depends_on_id = i.id " +
            $"WHERE dep.issue_id = @id AND dep.type IN ('blocks','parent-child','conditional-blocks','waits-for')",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            IssueService.ReadIssue);
    }

    public List<Issue> GetDependents(string issueId)
    {
        var d = _db.T("dependencies");
        var t = _db.T("issues");
        return _db.Query(
            $"SELECT i.* FROM {t} i INNER JOIN {d} dep ON dep.issue_id = i.id WHERE dep.depends_on_id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            IssueService.ReadIssue);
    }

    public DependencyTree Tree(string issueId)
    {
        var visited = new HashSet<string>();
        var root = BuildNode(issueId, visited);
        return new DependencyTree { Root = root };
    }

    public List<List<string>> FindCycles()
    {
        var cycles = new List<List<string>>();
        var allIds = _db.Query(
            $"SELECT DISTINCT issue_id FROM {_db.T("dependencies")}",
            null, r => r.GetString(0));

        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        foreach (var id in allIds)
        {
            if (!visited.Contains(id))
            {
                var path = new List<string>();
                DetectCycleDfs(id, visited, stack, path, cycles);
            }
        }

        return cycles;
    }

    public bool WouldCreateCycle(string issueId, string dependsOnId)
    {
        var visited = new HashSet<string> { issueId };
        var queue = new Queue<string>();
        queue.Enqueue(dependsOnId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == issueId) return true;
            if (!visited.Add(current)) continue;

            var deps = _db.Query(
                $"SELECT depends_on_id FROM {_db.T("dependencies")} WHERE issue_id = @id",
                cmd => cmd.Parameters.AddWithValue("@id", current),
                r => r.GetString(0));

            foreach (var dep in deps)
                queue.Enqueue(dep);
        }

        return false;
    }

    public void RebuildBlockedCache()
    {
        var cache = _db.T("blocked_issues_cache");
        var d = _db.T("dependencies");
        var t = _db.T("issues");

        _db.Execute($"DELETE FROM {cache}");
        _db.Execute($"""
            INSERT INTO {cache} (issue_id, blocked_by, blocked_at)
            SELECT dep.issue_id, dep.depends_on_id, datetime('now')
            FROM {d} dep
            INNER JOIN {t} i ON i.id = dep.depends_on_id
            WHERE dep.type IN ('blocks','parent-child','conditional-blocks','waits-for')
            AND i.status NOT IN ('closed','tombstone')
            """);
    }

    private DependencyNode BuildNode(string issueId, HashSet<string> visited)
    {
        visited.Add(issueId);
        var deps = _db.Query(
            $"SELECT depends_on_id, type FROM {_db.T("dependencies")} WHERE issue_id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            r => (r.GetString(0), r.GetString(1)));

        var children = new List<DependencyNode>();
        foreach (var (depId, depType) in deps)
        {
            if (!visited.Contains(depId))
                children.Add(BuildNode(depId, visited));
        }

        return new DependencyNode { IssueId = issueId, Children = children };
    }

    private void DetectCycleDfs(string id, HashSet<string> visited, HashSet<string> stack, List<string> path, List<List<string>> cycles)
    {
        visited.Add(id);
        stack.Add(id);
        path.Add(id);

        var deps = _db.Query(
            $"SELECT depends_on_id FROM {_db.T("dependencies")} WHERE issue_id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", id),
            r => r.GetString(0));

        foreach (var dep in deps)
        {
            if (!visited.Contains(dep))
            {
                DetectCycleDfs(dep, visited, stack, path, cycles);
            }
            else if (stack.Contains(dep))
            {
                var cycleStart = path.IndexOf(dep);
                if (cycleStart >= 0)
                    cycles.Add(path.Skip(cycleStart).ToList());
            }
        }

        path.RemoveAt(path.Count - 1);
        stack.Remove(id);
    }
}
