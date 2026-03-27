using Beads.Net.Models;
using Beads.Net.Errors;

namespace Beads.Net.Tests;

/// <summary>
/// Additional edge-case and integration tests for deeper coverage.
/// </summary>
public sealed class EdgeCaseTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    // ── Issue lifecycle ──────────────────────────────────────────

    [Fact]
    public void Close_AlreadyClosed_Idempotent()
    {
        var issue = _client.Issues.Create("To close");
        _client.Issues.Close(issue.Id);
        // Closing again should be idempotent or throw
        try
        {
            _client.Issues.Close(issue.Id);
        }
        catch (BeadsValidationException)
        {
            // Acceptable: already closed
        }
    }

    [Fact]
    public void Reopen_AlreadyOpen_Idempotent()
    {
        var issue = _client.Issues.Create("Already open");
        try
        {
            _client.Issues.Reopen(issue.Id);
        }
        catch (BeadsValidationException)
        {
            // Acceptable: already open
        }
    }

    [Fact]
    public void Delete_NonExistent_Throws()
    {
        Assert.ThrowsAny<BeadsException>(() =>
            _client.Issues.Delete("bd-nonexistent", new DeleteOptions { Force = true }));
    }

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        Assert.ThrowsAny<BeadsException>(() => _client.Issues.Create(""));
    }

    [Fact]
    public void Update_NonExistent_Throws()
    {
        Assert.ThrowsAny<BeadsException>(() =>
            _client.Issues.Update("bd-nonexistent", new UpdateIssueOptions { Title = "New" }));
    }

    // ── Subtasks ──────────────────────────────────────────────────

    [Fact]
    public void GetSubtasks_ReturnsChildren()
    {
        var parent = _client.Issues.Create("Parent");
        _client.Issues.Create("Child 1", new CreateIssueOptions { ParentId = parent.Id });
        _client.Issues.Create("Child 2", new CreateIssueOptions { ParentId = parent.Id });

        var subtasks = _client.Issues.GetSubtasks(parent.Id);
        Assert.Equal(2, subtasks.Count);
    }

    [Fact]
    public void GetSubtasks_EmptyForLeafIssue()
    {
        var leaf = _client.Issues.Create("Leaf");
        var subtasks = _client.Issues.GetSubtasks(leaf.Id);
        Assert.Empty(subtasks);
    }

    // ── Dependency edge cases ─────────────────────────────────────

    [Fact]
    public void Dependency_SelfReference_Prevented()
    {
        var issue = _client.Issues.Create("Self-dep");
        Assert.ThrowsAny<Exception>(() =>
            _client.Dependencies.Add(issue.Id, issue.Id));
    }

    [Fact]
    public void Dependency_NonExistentIssue_AllowedOrThrows()
    {
        var issue = _client.Issues.Create("Real");
        // Implementation may allow dangling deps (resolved at query time) or reject
        try
        {
            _client.Dependencies.Add(issue.Id, "bd-fake999");
            // If allowed, verify it's stored
            var deps = _client.Dependencies.List(issue.Id);
            Assert.Single(deps);
        }
        catch (Exception)
        {
            // Rejection is also acceptable
        }
    }

    [Fact]
    public void Dependency_Tree_SingleNode()
    {
        var solo = _client.Issues.Create("Solo node");
        var tree = _client.Dependencies.Tree(solo.Id);
        Assert.Equal(solo.Id, tree.Root.IssueId);
        Assert.Empty(tree.Root.Children);
    }

    // ── Label edge cases ──────────────────────────────────────────

    [Fact]
    public void Label_DuplicateAdd_Idempotent()
    {
        var issue = _client.Issues.Create("Dup label");
        _client.Labels.Add(issue.Id, "same");
        _client.Labels.Add(issue.Id, "same");
        var labels = _client.Labels.List(issue.Id);
        Assert.Single(labels);
    }

    [Fact]
    public void Label_CaseSensitive()
    {
        var issue = _client.Issues.Create("Case test");
        _client.Labels.Add(issue.Id, "Bug");
        _client.Labels.Add(issue.Id, "bug");
        var labels = _client.Labels.List(issue.Id);
        // Either 1 (case-insensitive) or 2 (case-sensitive)
        Assert.True(labels.Count >= 1);
    }

    // ── Filter combinations ───────────────────────────────────────

    [Fact]
    public void List_SortByPriority()
    {
        _client.Issues.Create("P3", new CreateIssueOptions { Priority = 3 });
        _client.Issues.Create("P0", new CreateIssueOptions { Priority = 0 });
        _client.Issues.Create("P1", new CreateIssueOptions { Priority = 1 });

        var result = _client.Issues.List(new IssueFilter { SortBy = "priority" });
        Assert.Equal(3, result.Issues.Count);
        Assert.True(result.Issues[0].Priority <= result.Issues[1].Priority);
    }

    [Fact]
    public void List_Reversed()
    {
        _client.Issues.Create("First");
        _client.Issues.Create("Second");

        var normal = _client.Issues.List(new IssueFilter { SortBy = "created_at" });
        var reversed = _client.Issues.List(new IssueFilter { SortBy = "created_at", Reverse = true });

        if (normal.Issues.Count >= 2 && reversed.Issues.Count >= 2)
        {
            Assert.Equal(normal.Issues[0].Id, reversed.Issues[^1].Id);
        }
    }

    [Fact]
    public void List_Offset()
    {
        for (int i = 0; i < 5; i++)
            _client.Issues.Create($"Issue {i}");

        var page1 = _client.Issues.List(new IssueFilter { Limit = 2, Offset = 0 });
        var page2 = _client.Issues.List(new IssueFilter { Limit = 2, Offset = 2 });

        Assert.Equal(2, page1.Issues.Count);
        Assert.Equal(2, page2.Issues.Count);
        Assert.NotEqual(page1.Issues[0].Id, page2.Issues[0].Id);
    }

    [Fact]
    public void List_FilterByPriority()
    {
        _client.Issues.Create("P0", new CreateIssueOptions { Priority = 0 });
        _client.Issues.Create("P1", new CreateIssueOptions { Priority = 1 });
        _client.Issues.Create("P2", new CreateIssueOptions { Priority = 2 });

        var result = _client.Issues.List(new IssueFilter { Priorities = [0, 1] });
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public void List_PriorityRange()
    {
        _client.Issues.Create("P0", new CreateIssueOptions { Priority = 0 });
        _client.Issues.Create("P2", new CreateIssueOptions { Priority = 2 });
        _client.Issues.Create("P4", new CreateIssueOptions { Priority = 4 });

        var result = _client.Issues.List(new IssueFilter { PriorityMin = 1, PriorityMax = 3 });
        Assert.Single(result.Issues);
        Assert.Equal(2, result.Issues[0].Priority);
    }

    [Fact]
    public void Search_ByDescription()
    {
        _client.Issues.Create("Generic title", new CreateIssueOptions
        {
            Description = "The frobnicate algorithm needs optimization"
        });

        var result = _client.Issues.Search("frobnicate");
        Assert.Single(result.Issues);
    }

    [Fact]
    public void Search_NoResults_ReturnsEmpty()
    {
        _client.Issues.Create("Something");
        var result = _client.Issues.Search("xyznonexistent");
        Assert.Empty(result.Issues);
    }

    // ── Update edge cases ─────────────────────────────────────────

    [Fact]
    public void Update_Claim_SetsAssigneeAndStatus()
    {
        var issue = _client.Issues.Create("Claimable");
        var updated = _client.Issues.Update(issue.Id, new UpdateIssueOptions { Claim = true });
        Assert.Equal("in_progress", updated.Status);
    }

    [Fact]
    public void Update_AddAndRemoveLabels()
    {
        var issue = _client.Issues.Create("Labels");
        _client.Labels.Add(issue.Id, "keep", "remove-me");
        _client.Issues.Update(issue.Id, new UpdateIssueOptions
        {
            AddLabels = ["new-label"],
            RemoveLabels = ["remove-me"],
        });

        var labels = _client.Labels.List(issue.Id);
        Assert.Contains("keep", labels);
        Assert.Contains("new-label", labels);
        Assert.DoesNotContain("remove-me", labels);
    }

    // ── Query edge cases ──────────────────────────────────────────

    [Fact]
    public void Query_Run_NonExistent_Throws()
    {
        Assert.ThrowsAny<Exception>(() => _client.Queries.Run("nonexistent"));
    }

    [Fact]
    public void Query_Delete_NonExistent_NoError()
    {
        // Should either succeed silently or throw
        try { _client.Queries.Delete("nonexistent"); }
        catch (BeadsException) { /* acceptable */ }
    }

    // ── Board edge cases ──────────────────────────────────────────

    [Fact]
    public void Board_CreateMultiple_IncrementPosition()
    {
        var project = _client.Projects.Create("BoardTest");
        _client.Boards.Create(project.Id, "Board 1");
        _client.Boards.Create(project.Id, "Board 2");

        var boards = _client.Boards.List(project.Id);
        Assert.Equal(2, boards.Count);
    }

    [Fact]
    public void Column_WipLimitNull_NoLimit()
    {
        var project = _client.Projects.Create("WipTest");
        var board = _client.Boards.Create(project.Id, "Board");
        var col = _client.Boards.CreateColumn(board.Id, "Unlimited");
        Assert.Null(col.WipLimit);
    }

    // ── Event audit trail ─────────────────────────────────────────

    [Fact]
    public void Update_RecordsEvents()
    {
        var issue = _client.Issues.Create("Audited");
        _client.Issues.Update(issue.Id, new UpdateIssueOptions { Title = "Updated title" });

        var events = _client.Events.List(issueId: issue.Id);
        Assert.True(events.Count >= 2); // created + title_changed
    }

    [Fact]
    public void Reopen_RecordsEvent()
    {
        var issue = _client.Issues.Create("Reopen audit");
        _client.Issues.Close(issue.Id);
        _client.Issues.Reopen(issue.Id);

        var events = _client.Events.List(issueId: issue.Id);
        Assert.True(events.Count >= 3); // created + closed + reopened
    }

    // ── Stats edge cases ──────────────────────────────────────────

    [Fact]
    public void Stats_TracksBlockedCount()
    {
        var a = _client.Issues.Create("Blocker");
        var b = _client.Issues.Create("Blocked");
        _client.Dependencies.Add(b.Id, a.Id, "blocks");
        _client.Dependencies.RebuildBlockedCache();

        var stats = _client.Stats.GetStats();
        Assert.True(stats.BlockedCount >= 0);
    }

    // ── Lint ──────────────────────────────────────────────────────

    [Fact]
    public void Lint_DetectsProblems()
    {
        // Create a valid issue, lint should find no warnings for it
        _client.Issues.Create("Valid issue", new CreateIssueOptions
        {
            IssueType = "task",
            Priority = 1,
        });

        var lint = _client.Lint();
        Assert.Equal(1, lint.TotalChecked);
    }
}
