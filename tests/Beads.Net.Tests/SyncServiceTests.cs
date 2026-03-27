namespace Beads.Net.Tests;

public sealed class SyncServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Status_FreshDb_NoDirty()
    {
        var status = _client.Sync.Status();
        Assert.NotNull(status);
        Assert.Equal(0, status.DirtyCount);
    }

    [Fact]
    public void Status_AfterCreate_HasDirty()
    {
        _client.Issues.Create("New issue");
        var status = _client.Sync.Status();
        Assert.True(status.DirtyCount > 0 || status.DbIssueCount > 0);
    }

    [Fact]
    public void HistoryList_EmptyInitially()
    {
        var entries = _client.Sync.HistoryList();
        Assert.NotNull(entries);
    }
}
