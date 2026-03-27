namespace Beads.Net.Tests;

public sealed class LabelServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Add_AttachesLabel()
    {
        var issue = _client.Issues.Create("Labeled");
        _client.Labels.Add(issue.Id, "backend");

        var labels = _client.Labels.List(issue.Id);
        Assert.Single(labels);
        Assert.Equal("backend", labels[0]);
    }

    [Fact]
    public void Add_MultipleLabels()
    {
        var issue = _client.Issues.Create("Multi-label");
        _client.Labels.Add(issue.Id, "backend", "urgent", "v2");

        var labels = _client.Labels.List(issue.Id);
        Assert.Equal(3, labels.Count);
        Assert.Contains("backend", labels);
        Assert.Contains("urgent", labels);
        Assert.Contains("v2", labels);
    }

    [Fact]
    public void Remove_DetachesLabel()
    {
        var issue = _client.Issues.Create("Labeled");
        _client.Labels.Add(issue.Id, "backend", "frontend");
        _client.Labels.Remove(issue.Id, "backend");

        var labels = _client.Labels.List(issue.Id);
        Assert.Single(labels);
        Assert.Equal("frontend", labels[0]);
    }

    [Fact]
    public void Remove_NonExistentLabel_NoError()
    {
        var issue = _client.Issues.Create("Labeled");
        _client.Labels.Remove(issue.Id, "nonexistent"); // should not throw
    }

    [Fact]
    public void List_EmptyForNewIssue()
    {
        var issue = _client.Issues.Create("No labels");
        var labels = _client.Labels.List(issue.Id);
        Assert.Empty(labels);
    }

    [Fact]
    public void ListAll_ReturnsUniqueLabels()
    {
        var i1 = _client.Issues.Create("Issue 1");
        var i2 = _client.Issues.Create("Issue 2");
        _client.Labels.Add(i1.Id, "backend", "urgent");
        _client.Labels.Add(i2.Id, "backend", "frontend");

        var all = _client.Labels.ListAll();
        Assert.Equal(3, all.Count);
        Assert.Contains("backend", all);
        Assert.Contains("urgent", all);
        Assert.Contains("frontend", all);
    }

    [Fact]
    public void ListAll_EmptyWhenNoLabels()
    {
        var all = _client.Labels.ListAll();
        Assert.Empty(all);
    }

    [Fact]
    public void Rename_UpdatesAcrossAllIssues()
    {
        var i1 = _client.Issues.Create("Issue 1");
        var i2 = _client.Issues.Create("Issue 2");
        _client.Labels.Add(i1.Id, "old-name");
        _client.Labels.Add(i2.Id, "old-name");

        _client.Labels.Rename("old-name", "new-name");

        Assert.Contains("new-name", _client.Labels.List(i1.Id));
        Assert.Contains("new-name", _client.Labels.List(i2.Id));
        Assert.DoesNotContain("old-name", _client.Labels.ListAll());
    }

    [Fact]
    public void List_NullId_ReturnsListAll()
    {
        var issue = _client.Issues.Create("Issue");
        _client.Labels.Add(issue.Id, "backend");

        var all = _client.Labels.List(null);
        Assert.Single(all);
        Assert.Equal("backend", all[0]);
    }
}
