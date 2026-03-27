using System.Text.Json;
using Beads.Net.Errors;
using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class QueryService
{
    private readonly Db _db;
    private readonly IssueService _issues;

    internal QueryService(Db db, IssueService issues)
    {
        _db = db;
        _issues = issues;
    }

    public void Save(string name, IssueFilter filter)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BeadsValidationException("Query name must not be empty.");

        var json = JsonSerializer.Serialize(filter);
        _db.Execute(
            $"INSERT OR REPLACE INTO {_db.T("saved_queries")} (name, filter_json, created_at) VALUES (@name, @json, @now)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@json", json);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            });
    }

    public IssueListResult Run(string name)
    {
        var json = _db.QueryScalarString(
            $"SELECT filter_json FROM {_db.T("saved_queries")} WHERE name = @name",
            cmd => cmd.Parameters.AddWithValue("@name", name))
            ?? throw new BeadsNotFoundException($"Saved query not found: {name}");

        var filter = JsonSerializer.Deserialize<IssueFilter>(json) ?? new IssueFilter();
        return _issues.List(filter);
    }

    public List<string> List()
    {
        return _db.Query(
            $"SELECT name FROM {_db.T("saved_queries")} ORDER BY name",
            null,
            r => r.GetString(0));
    }

    public void Delete(string name)
    {
        _db.Execute(
            $"DELETE FROM {_db.T("saved_queries")} WHERE name = @name",
            cmd => cmd.Parameters.AddWithValue("@name", name));
    }
}
