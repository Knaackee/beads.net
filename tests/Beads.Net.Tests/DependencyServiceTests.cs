using Beads.Net.Models;
using Beads.Net.Errors;

namespace Beads.Net.Tests;

public sealed class DependencyServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Add_CreatesDependency()
    {
        var a = _client.Issues.Create("Issue A");
        var b = _client.Issues.Create("Issue B");
        _client.Dependencies.Add(a.Id, b.Id);

        var deps = _client.Dependencies.List(a.Id);
        Assert.Single(deps);
        Assert.Equal(b.Id, deps[0].DependsOnId);
    }

    [Fact]
    public void Add_WithType_SetsDepType()
    {
        var a = _client.Issues.Create("Issue A");
        var b = _client.Issues.Create("Issue B");
        _client.Dependencies.Add(a.Id, b.Id, "waits-for");

        var deps = _client.Dependencies.List(a.Id);
        Assert.Single(deps);
        Assert.Equal("waits-for", deps[0].DepType);
    }

    [Fact]
    public void Add_DuplicateDepIgnoredOrThrows()
    {
        var a = _client.Issues.Create("A");
        var b = _client.Issues.Create("B");
        _client.Dependencies.Add(a.Id, b.Id);

        // Adding the same dependency again should either be idempotent or throw
        try
        {
            _client.Dependencies.Add(a.Id, b.Id);
            // Idempotent path - still only one dependency
            var deps = _client.Dependencies.List(a.Id);
            Assert.Single(deps);
        }
        catch (BeadsException)
        {
            // Duplicate rejection path - also acceptable
        }
    }

    [Fact]
    public void Remove_DeletesDependency()
    {
        var a = _client.Issues.Create("A");
        var b = _client.Issues.Create("B");
        _client.Dependencies.Add(a.Id, b.Id);
        _client.Dependencies.Remove(a.Id, b.Id);

        var deps = _client.Dependencies.List(a.Id);
        Assert.Empty(deps);
    }

    [Fact]
    public void GetBlockers_ReturnsBlockingIssues()
    {
        var a = _client.Issues.Create("Blocked issue");
        var b = _client.Issues.Create("Blocker");
        _client.Dependencies.Add(a.Id, b.Id, "blocks");

        var blockers = _client.Dependencies.GetBlockers(a.Id);
        Assert.Single(blockers);
        Assert.Equal(b.Id, blockers[0].Id);
    }

    [Fact]
    public void GetDependents_ReturnsDependentIssues()
    {
        var a = _client.Issues.Create("Issue A");
        var b = _client.Issues.Create("Depends on A");
        _client.Dependencies.Add(b.Id, a.Id);

        var dependents = _client.Dependencies.GetDependents(a.Id);
        Assert.Single(dependents);
        Assert.Equal(b.Id, dependents[0].Id);
    }

    [Fact]
    public void Tree_ReturnsHierarchy()
    {
        var a = _client.Issues.Create("Root");
        var b = _client.Issues.Create("Child");
        var c = _client.Issues.Create("Grandchild");
        _client.Dependencies.Add(a.Id, b.Id);
        _client.Dependencies.Add(b.Id, c.Id);

        var tree = _client.Dependencies.Tree(a.Id);
        Assert.Equal(a.Id, tree.Root.IssueId);
        Assert.Single(tree.Root.Children);
        Assert.Equal(b.Id, tree.Root.Children[0].IssueId);
    }

    [Fact]
    public void FindCycles_NoCycles_ReturnsEmpty()
    {
        var a = _client.Issues.Create("A");
        var b = _client.Issues.Create("B");
        _client.Dependencies.Add(a.Id, b.Id);

        var cycles = _client.Dependencies.FindCycles();
        Assert.Empty(cycles);
    }

    [Fact]
    public void WouldCreateCycle_DetectsPotentialCycle()
    {
        var a = _client.Issues.Create("A");
        var b = _client.Issues.Create("B");
        _client.Dependencies.Add(a.Id, b.Id);

        var wouldCycle = _client.Dependencies.WouldCreateCycle(b.Id, a.Id);
        Assert.True(wouldCycle);
    }

    [Fact]
    public void WouldCreateCycle_NoCycle_ReturnsFalse()
    {
        var a = _client.Issues.Create("A");
        var b = _client.Issues.Create("B");
        var c = _client.Issues.Create("C");
        _client.Dependencies.Add(a.Id, b.Id);

        var wouldCycle = _client.Dependencies.WouldCreateCycle(a.Id, c.Id);
        Assert.False(wouldCycle);
    }

    [Fact]
    public void RebuildBlockedCache_RunsWithoutError()
    {
        var a = _client.Issues.Create("A");
        var b = _client.Issues.Create("B");
        _client.Dependencies.Add(a.Id, b.Id);
        _client.Dependencies.RebuildBlockedCache();
    }

    [Fact]
    public void List_NoIssue_ReturnsEmpty()
    {
        var a = _client.Issues.Create("Solo");
        var deps = _client.Dependencies.List(a.Id);
        Assert.Empty(deps);
    }
}
