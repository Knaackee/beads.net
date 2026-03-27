namespace Beads.Net.Tests;

public sealed class StatsServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void GetStats_EmptyDb_ReturnsZeros()
    {
        var stats = _client.Stats.GetStats();
        Assert.Equal(0, stats.TotalIssues);
        Assert.Equal(0, stats.OpenCount);
        Assert.Equal(0, stats.ClosedCount);
    }

    [Fact]
    public void GetStats_IncludesTotalIssues()
    {
        _client.Issues.Create("Issue 1");
        _client.Issues.Create("Issue 2");
        var stats = _client.Stats.GetStats();
        Assert.Equal(2, stats.TotalIssues);
    }

    [Fact]
    public void GetStats_TracksOpenAndClosed()
    {
        _client.Issues.Create("Open");
        var closed = _client.Issues.Create("Closed");
        _client.Issues.Close(closed.Id);

        var stats = _client.Stats.GetStats();
        Assert.Equal(1, stats.OpenCount);
        Assert.Equal(1, stats.ClosedCount);
    }

    [Fact]
    public void GetStats_ByStatusBreakdown()
    {
        _client.Issues.Create("Open 1");
        _client.Issues.Create("Open 2");
        var closed = _client.Issues.Create("Closed");
        _client.Issues.Close(closed.Id);

        var stats = _client.Stats.GetStats();
        Assert.True(stats.ByStatus.ContainsKey("open"));
        Assert.Equal(2, stats.ByStatus["open"]);
    }

    [Fact]
    public void GetStats_ByTypeBreakdown()
    {
        _client.Issues.Create("Bug", new Models.CreateIssueOptions { IssueType = "bug" });
        _client.Issues.Create("Task", new Models.CreateIssueOptions { IssueType = "task" });

        var stats = _client.Stats.GetStats();
        Assert.True(stats.ByType.ContainsKey("bug"));
        Assert.True(stats.ByType.ContainsKey("task"));
    }

    [Fact]
    public void GetStats_ByAssigneeBreakdown()
    {
        _client.Issues.Create("Alice's", new Models.CreateIssueOptions { Assignee = "alice" });
        _client.Issues.Create("Bob's", new Models.CreateIssueOptions { Assignee = "bob" });

        var stats = _client.Stats.GetStats();
        Assert.True(stats.ByAssignee.ContainsKey("alice"));
        Assert.True(stats.ByAssignee.ContainsKey("bob"));
    }
}
