using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class IssueBenchmarks
{
    private BeadsClient _client = null!;
    private SqliteConnection _conn = null!;
    private string _existingId = "";

    [GlobalSetup]
    public void Setup()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _client = new BeadsClient(_conn);

        // Seed some issues for benchmarks
        for (int i = 0; i < 100; i++)
        {
            var issue = _client.Issues.Create($"Issue {i}", new Models.CreateIssueOptions
            {
                IssueType = i % 2 == 0 ? "task" : "bug",
                Priority = i % 5,
                Assignee = $"user-{i % 5}",
            });
            if (i == 0) _existingId = issue.Id;
            _client.Labels.Add(issue.Id, $"label-{i % 3}");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _conn.Dispose();
    }

    [Benchmark]
    public object Create()
    {
        return _client.Issues.Create("Bench issue");
    }

    [Benchmark]
    public string Quick()
    {
        return _client.Issues.Quick("Bench quick");
    }

    [Benchmark]
    public object GetById()
    {
        return _client.Issues.GetOrThrow(_existingId);
    }

    [Benchmark]
    public object ListDefault()
    {
        return _client.Issues.List();
    }

    [Benchmark]
    public object ListFiltered()
    {
        return _client.Issues.List(new Models.IssueFilter
        {
            Types = ["bug"],
            Assignee = "user-1",
            Limit = 10,
        });
    }

    [Benchmark]
    public object Search()
    {
        return _client.Issues.Search("Issue 5");
    }

    [Benchmark]
    public object Count()
    {
        return _client.Issues.Count();
    }

    [Benchmark]
    public object CountGroupByStatus()
    {
        return _client.Issues.Count("status");
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class DependencyBenchmarks
{
    private BeadsClient _client = null!;
    private SqliteConnection _conn = null!;
    private string _rootId = "";
    private readonly List<string> _ids = [];

    [GlobalSetup]
    public void Setup()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _client = new BeadsClient(_conn);

        // Create a chain of 20 issues with deps
        for (int i = 0; i < 20; i++)
        {
            var issue = _client.Issues.Create($"Chain {i}");
            _ids.Add(issue.Id);
            if (i == 0) _rootId = issue.Id;
            if (i > 0) _client.Dependencies.Add(_ids[i], _ids[i - 1]);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _conn.Dispose();
    }

    [Benchmark]
    public object Tree()
    {
        return _client.Dependencies.Tree(_rootId);
    }

    [Benchmark]
    public object FindCycles()
    {
        return _client.Dependencies.FindCycles();
    }

    [Benchmark]
    public object ListDeps()
    {
        return _client.Dependencies.List(_ids[10]);
    }

    [Benchmark]
    public object GetBlockers()
    {
        return _client.Dependencies.GetBlockers(_ids[10]);
    }

    [Benchmark]
    public bool WouldCreateCycle()
    {
        return _client.Dependencies.WouldCreateCycle(_ids[0], _ids[19]);
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class LabelBenchmarks
{
    private BeadsClient _client = null!;
    private SqliteConnection _conn = null!;
    private string _issueId = "";

    [GlobalSetup]
    public void Setup()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _client = new BeadsClient(_conn);

        for (int i = 0; i < 50; i++)
        {
            var issue = _client.Issues.Create($"Label issue {i}");
            if (i == 0) _issueId = issue.Id;
            for (int j = 0; j < 5; j++)
                _client.Labels.Add(issue.Id, $"label-{j}");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _conn.Dispose();
    }

    [Benchmark]
    public object ListAll()
    {
        return _client.Labels.ListAll();
    }

    [Benchmark]
    public object ListForIssue()
    {
        return _client.Labels.List(_issueId);
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class StatsBenchmarks
{
    private BeadsClient _client = null!;
    private SqliteConnection _conn = null!;

    [GlobalSetup]
    public void Setup()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _client = new BeadsClient(_conn);

        for (int i = 0; i < 200; i++)
        {
            var issue = _client.Issues.Create($"Stats issue {i}", new Models.CreateIssueOptions
            {
                IssueType = i % 3 == 0 ? "bug" : "task",
                Priority = i % 5,
            });
            if (i % 4 == 0) _client.Issues.Close(issue.Id);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _conn.Dispose();
    }

    [Benchmark]
    public object GetStats()
    {
        return _client.Stats.GetStats();
    }

    [Benchmark]
    public object Doctor()
    {
        return _client.Doctor.Run();
    }

    [Benchmark]
    public object Lint()
    {
        return _client.Lint();
    }
}
