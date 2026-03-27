using Beads.Net.Models;

namespace Beads.Net.Tests;

public sealed class QueryServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Save_PersistsQuery()
    {
        _client.Queries.Save("my-bugs", new IssueFilter { Types = ["bug"] });
        var queries = _client.Queries.List();
        Assert.Contains("my-bugs", queries);
    }

    [Fact]
    public void Run_ExecutesSavedQuery()
    {
        _client.Issues.Create("A bug", new CreateIssueOptions { IssueType = "bug" });
        _client.Issues.Create("A task", new CreateIssueOptions { IssueType = "task" });

        _client.Queries.Save("bugs-only", new IssueFilter { Types = ["bug"] });
        var result = _client.Queries.Run("bugs-only");
        Assert.Single(result.Issues);
        Assert.Equal("bug", result.Issues[0].IssueType);
    }

    [Fact]
    public void List_ReturnsAllQueryNames()
    {
        _client.Queries.Save("q1", new IssueFilter { Types = ["bug"] });
        _client.Queries.Save("q2", new IssueFilter { Assignee = "alice" });

        var names = _client.Queries.List();
        Assert.Equal(2, names.Count);
        Assert.Contains("q1", names);
        Assert.Contains("q2", names);
    }

    [Fact]
    public void Delete_RemovesQuery()
    {
        _client.Queries.Save("temp", new IssueFilter());
        _client.Queries.Delete("temp");

        var names = _client.Queries.List();
        Assert.DoesNotContain("temp", names);
    }

    [Fact]
    public void List_EmptyByDefault()
    {
        var names = _client.Queries.List();
        Assert.Empty(names);
    }

    [Fact]
    public void Save_OverwritesExisting()
    {
        _client.Queries.Save("q1", new IssueFilter { Types = ["bug"] });
        _client.Queries.Save("q1", new IssueFilter { Types = ["task"] });

        _client.Issues.Create("Task", new CreateIssueOptions { IssueType = "task" });

        var result = _client.Queries.Run("q1");
        Assert.Single(result.Issues);
        Assert.Equal("task", result.Issues[0].IssueType);
    }
}
