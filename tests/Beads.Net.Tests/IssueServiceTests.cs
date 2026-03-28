using Beads.Net.Models;
using Beads.Net.Errors;

namespace Beads.Net.Tests;

public sealed class IssueServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Create_ReturnsIssueWithId()
    {
        var issue = _client.Issues.Create("Test issue");
        Assert.NotNull(issue);
        Assert.StartsWith("bd-", issue.Id);
        Assert.Equal("Test issue", issue.Title);
        Assert.Equal("open", issue.Status);
    }

    [Fact]
    public void Create_WithAllOptions()
    {
        var issue = _client.Issues.Create("Full issue", new CreateIssueOptions
        {
            IssueType = "bug",
            Priority = 0,
            Description = "A description",
            Design = "Design notes",
            AcceptanceCriteria = "AC here",
            Notes = "Some notes",
            Assignee = "alice",
            Owner = "bob",
            EstimatedMinutes = 120,
            DueAt = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            ExternalRef = "JIRA-123",
        });

        Assert.Equal("bug", issue.IssueType);
        Assert.Equal(0, issue.Priority);
        Assert.Equal("A description", issue.Description);
        Assert.Equal("Design notes", issue.Design);
        Assert.Equal("AC here", issue.AcceptanceCriteria);
        Assert.Equal("Some notes", issue.Notes);
        Assert.Equal("alice", issue.Assignee);
        Assert.Equal("bob", issue.Owner);
        Assert.Equal(120, issue.EstimatedMinutes);
        Assert.NotNull(issue.DueAt);
        Assert.Equal("JIRA-123", issue.ExternalRef);
    }

    [Fact]
    public void CreateIssueOptions_Metadata_CanBeSet()
    {
        var options = new CreateIssueOptions
        {
            Metadata = "{\"source\":\"api\"}",
        };

        Assert.Equal("{\"source\":\"api\"}", options.Metadata);
    }

    [Fact]
    public void UpdateIssueOptions_Metadata_CanBeSet()
    {
        var options = new UpdateIssueOptions
        {
            Metadata = "{\"sprint\":\"42\"}",
        };

        Assert.Equal("{\"sprint\":\"42\"}", options.Metadata);
    }

    [Fact]
    public void Create_WithLabels()
    {
        var issue = _client.Issues.Create("Labeled", new CreateIssueOptions
        {
            Labels = ["backend", "urgent"],
        });
        var labels = _client.Labels.List(issue.Id);
        Assert.Contains("backend", labels);
        Assert.Contains("urgent", labels);
    }

    [Fact]
    public void Quick_ReturnsOnlyId()
    {
        var id = _client.Issues.Quick("Quick one");
        Assert.StartsWith("bd-", id);
        var issue = _client.Issues.GetOrThrow(id);
        Assert.Equal("Quick one", issue.Title);
    }

    [Fact]
    public void Get_NonExistent_Throws()
    {
        Assert.Throws<BeadsNotFoundException>(() => _client.Issues.Get("bd-nonexistent"));
    }

    [Fact]
    public void GetOrThrow_NonExistent_Throws()
    {
        Assert.Throws<BeadsNotFoundException>(() => _client.Issues.GetOrThrow("bd-nonexistent"));
    }

    [Fact]
    public void List_Empty_ReturnsEmpty()
    {
        var result = _client.Issues.List();
        Assert.Empty(result.Issues);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public void List_WithIssues_ReturnsPaginated()
    {
        for (int i = 0; i < 5; i++)
            _client.Issues.Create($"Issue {i}");

        var result = _client.Issues.List(new IssueFilter { Limit = 3 });
        Assert.Equal(3, result.Issues.Count);
        Assert.Equal(5, result.Total);
        Assert.True(result.HasMore);
    }

    [Fact]
    public void List_FilterByStatus()
    {
        var i1 = _client.Issues.Create("Open one");
        var i2 = _client.Issues.Create("Closed one");
        _client.Issues.Close(i2.Id);

        var result = _client.Issues.List(new IssueFilter { Statuses = ["closed"], IncludeClosed = true });
        Assert.Single(result.Issues);
        Assert.Equal(i2.Id, result.Issues[0].Id);
    }

    [Fact]
    public void List_FilterByType()
    {
        _client.Issues.Create("Bug", new CreateIssueOptions { IssueType = "bug" });
        _client.Issues.Create("Task", new CreateIssueOptions { IssueType = "task" });

        var result = _client.Issues.List(new IssueFilter { Types = ["bug"] });
        Assert.Single(result.Issues);
        Assert.Equal("bug", result.Issues[0].IssueType);
    }

    [Fact]
    public void List_FilterByAssignee()
    {
        _client.Issues.Create("Alice's", new CreateIssueOptions { Assignee = "alice" });
        _client.Issues.Create("Unassigned");

        var result = _client.Issues.List(new IssueFilter { Assignee = "alice" });
        Assert.Single(result.Issues);

        var unassigned = _client.Issues.List(new IssueFilter { Unassigned = true });
        Assert.Single(unassigned.Issues);
    }

    [Fact]
    public void List_FilterByLabel()
    {
        var i1 = _client.Issues.Create("Labeled", new CreateIssueOptions { Labels = ["backend"] });
        _client.Issues.Create("No label");

        var result = _client.Issues.List(new IssueFilter { Labels = ["backend"] });
        Assert.Single(result.Issues);
        Assert.Equal(i1.Id, result.Issues[0].Id);
    }

    [Fact]
    public void Update_ChangesFields()
    {
        var issue = _client.Issues.Create("Original title");
        var updated = _client.Issues.Update(issue.Id, new UpdateIssueOptions
        {
            Title = "New title",
            Priority = 2,
            Assignee = "bob",
        });

        Assert.Equal("New title", updated.Title);
        Assert.Equal(2, updated.Priority);
        Assert.Equal("bob", updated.Assignee);
    }

    [Fact]
    public void Close_SetsClosedStatus()
    {
        var issue = _client.Issues.Create("To close");
        var closed = _client.Issues.Close(issue.Id);
        Assert.Equal("closed", closed.Status);
        Assert.NotNull(closed.ClosedAt);
    }

    [Fact]
    public void Close_WithReason()
    {
        var issue = _client.Issues.Create("To close");
        var closed = _client.Issues.Close(issue.Id, new CloseOptions { Reason = "Done!" });
        Assert.Equal("Done!", closed.CloseReason);
    }

    [Fact]
    public void Reopen_SetsOpenStatus()
    {
        var issue = _client.Issues.Create("To reopen");
        _client.Issues.Close(issue.Id);
        var reopened = _client.Issues.Reopen(issue.Id);
        Assert.Equal("open", reopened.Status);
    }

    [Fact]
    public void Delete_RemovesIssue()
    {
        var issue = _client.Issues.Create("To delete");
        _client.Issues.Delete(issue.Id, new DeleteOptions { Force = true });
        var result = _client.Issues.Get(issue.Id);
        // Deleted issues become tombstones or null
        Assert.True(result == null || result.Status == "tombstone");
    }

    [Fact]
    public void Search_FindsByTitle()
    {
        _client.Issues.Create("Authentication module");
        _client.Issues.Create("Database migration");

        var result = _client.Issues.Search("auth");
        Assert.Single(result.Issues);
        Assert.Contains("Authentication", result.Issues[0].Title);
    }

    [Fact]
    public void Ready_ReturnsNonBlockedOpenIssues()
    {
        _client.Issues.Create("Ready task");
        var blocked = _client.Issues.Create("Blocked task");
        _client.Issues.Update(blocked.Id, new UpdateIssueOptions { Status = "blocked" });

        var result = _client.Issues.Ready();
        Assert.All(result.Issues, i => Assert.NotEqual("blocked", i.Status));
    }

    [Fact]
    public void Stale_ReturnsOldIssues()
    {
        _client.Issues.Create("Fresh issue");
        // Stale with 0 days should return all non-terminal issues
        var result = _client.Issues.Stale(0);
        Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public void Count_ReturnsTotal()
    {
        _client.Issues.Create("One");
        _client.Issues.Create("Two");
        var count = _client.Issues.Count();
        Assert.Equal(2, count.Total);
    }

    [Fact]
    public void Count_GroupByStatus()
    {
        _client.Issues.Create("Open");
        var i2 = _client.Issues.Create("Closed");
        _client.Issues.Close(i2.Id);

        var count = _client.Issues.Count("status");
        Assert.True(count.Groups.ContainsKey("open"));
        Assert.True(count.Groups.ContainsKey("closed"));
    }

    [Fact]
    public void Defer_SetsStatus()
    {
        var issue = _client.Issues.Create("To defer");
        _client.Issues.Defer(issue.Id, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var updated = _client.Issues.GetOrThrow(issue.Id);
        Assert.Equal("deferred", updated.Status);
        Assert.NotNull(updated.DeferUntil);
    }

    [Fact]
    public void Undefer_ResetsStatus()
    {
        var issue = _client.Issues.Create("To defer");
        _client.Issues.Defer(issue.Id);
        _client.Issues.Undefer(issue.Id);
        var updated = _client.Issues.GetOrThrow(issue.Id);
        Assert.Equal("open", updated.Status);
    }

    [Fact]
    public void Orphans_ReturnsSubtasksWithoutParent()
    {
        _client.Issues.Create("Orphan subtask");
        var orphans = _client.Issues.Orphans();
        // Orphans are subtasks whose parent doesn't exist
        Assert.NotNull(orphans);
    }
}
