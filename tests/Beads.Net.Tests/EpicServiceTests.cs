using Beads.Net.Models;

namespace Beads.Net.Tests;

public sealed class EpicServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Status_ReturnsEpicProgress()
    {
        var epic = _client.Issues.Create("Epic", new CreateIssueOptions { IssueType = "epic" });
        var child1 = _client.Issues.Create("Task 1", new CreateIssueOptions { ParentId = epic.Id });
        var child2 = _client.Issues.Create("Task 2", new CreateIssueOptions { ParentId = epic.Id });
        _client.Issues.Close(child1.Id, new CloseOptions { Force = true });

        var status = _client.Epics.Status(epic.Id);
        Assert.Equal(2, status.TotalChildren);
        Assert.Equal(1, status.ClosedChildren);
        Assert.Equal(50, status.ProgressPercent);
    }

    [Fact]
    public void Status_EmptyEpic_ZeroProgress()
    {
        var epic = _client.Issues.Create("Empty epic", new CreateIssueOptions { IssueType = "epic" });
        var status = _client.Epics.Status(epic.Id);
        Assert.Equal(0, status.TotalChildren);
    }

    [Fact]
    public void CloseEligible_AllChildrenClosed()
    {
        var epic = _client.Issues.Create("Closeable epic", new CreateIssueOptions { IssueType = "epic" });
        var child = _client.Issues.Create("Task", new CreateIssueOptions { ParentId = epic.Id });
        _client.Issues.Close(child.Id, new CloseOptions { Force = true });

        var status = _client.Epics.CloseEligible(epic.Id);
        Assert.Equal(100, status.ProgressPercent);
    }

    [Fact]
    public void CloseEligible_NotAllClosed()
    {
        var epic = _client.Issues.Create("Not closeable", new CreateIssueOptions { IssueType = "epic" });
        _client.Issues.Create("Open task", new CreateIssueOptions { ParentId = epic.Id });

        var status = _client.Epics.CloseEligible(epic.Id);
        Assert.Equal(0, status.ProgressPercent);
    }
}
