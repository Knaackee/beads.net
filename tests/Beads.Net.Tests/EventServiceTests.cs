namespace Beads.Net.Tests;

public sealed class EventServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Create_RecordsEvent()
    {
        var issue = _client.Issues.Create("Audited issue");
        var events = _client.Events.List(issueId: issue.Id);
        // Creating an issue should record at least one "created" event
        Assert.NotEmpty(events);
    }

    [Fact]
    public void Close_RecordsEvent()
    {
        var issue = _client.Issues.Create("To close");
        _client.Issues.Close(issue.Id);

        var events = _client.Events.List(issueId: issue.Id);
        Assert.True(events.Count >= 2); // created + closed
    }

    [Fact]
    public void Record_CustomEvent()
    {
        var issue = _client.Issues.Create("Custom event");
        _client.Events.Record(issue.Id, "custom", oldValue: "a", newValue: "b", comment: "test");

        var events = _client.Events.List(issueId: issue.Id, eventType: "custom");
        Assert.Single(events);
        Assert.Equal("a", events[0].OldValue);
        Assert.Equal("b", events[0].NewValue);
    }

    [Fact]
    public void List_FilterByEventType()
    {
        var issue = _client.Issues.Create("Filtered");
        _client.Events.Record(issue.Id, "note", comment: "A note");
        _client.Events.Record(issue.Id, "note", comment: "Another note");
        _client.Events.Record(issue.Id, "other", comment: "Something else");

        var noteEvents = _client.Events.List(issueId: issue.Id, eventType: "note");
        Assert.Equal(2, noteEvents.Count);
    }

    [Fact]
    public void List_AllEvents_NoFilter()
    {
        var i1 = _client.Issues.Create("Issue 1");
        var i2 = _client.Issues.Create("Issue 2");

        var all = _client.Events.List();
        Assert.True(all.Count >= 2); // at least two "created" events
    }

    [Fact]
    public void List_EmptyForNonExistentIssue()
    {
        var events = _client.Events.List(issueId: "bd-nonexistent");
        Assert.Empty(events);
    }
}
