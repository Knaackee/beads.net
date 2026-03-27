namespace Beads.Net.Tests;

public sealed class BeadsClientTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void ServiceProperties_NotNull()
    {
        Assert.NotNull(_client.Issues);
        Assert.NotNull(_client.Dependencies);
        Assert.NotNull(_client.Labels);
        Assert.NotNull(_client.Comments);
        Assert.NotNull(_client.Epics);
        Assert.NotNull(_client.Events);
        Assert.NotNull(_client.Queries);
        Assert.NotNull(_client.Stats);
        Assert.NotNull(_client.Doctor);
        Assert.NotNull(_client.Projects);
        Assert.NotNull(_client.Boards);
        Assert.NotNull(_client.Schema);
    }

    [Fact]
    public void WhereDb_ReturnsNonEmptyPath()
    {
        var path = _client.WhereDb();
        Assert.NotNull(path);
    }

    [Fact]
    public void GetInfo_ReturnsMetadata()
    {
        var info = _client.GetInfo();
        Assert.NotNull(info);
    }

    [Fact]
    public void Lint_EmptyDb_IsClean()
    {
        var lint = _client.Lint();
        Assert.True(lint.IsClean);
        Assert.Equal(0, lint.TotalChecked);
        Assert.Empty(lint.Warnings);
    }

    [Fact]
    public void Lint_WithIssues_ChecksAll()
    {
        _client.Issues.Create("Issue 1");
        _client.Issues.Create("Issue 2");

        var lint = _client.Lint();
        Assert.Equal(2, lint.TotalChecked);
    }

    [Fact]
    public void Changelog_ReturnsMarkdown()
    {
        var issue = _client.Issues.Create("Feature");
        _client.Issues.Close(issue.Id);

        var log = _client.Changelog(format: "markdown");
        Assert.NotNull(log);
    }

    [Fact]
    public void Changelog_ReturnsJson()
    {
        var issue = _client.Issues.Create("Feature");
        _client.Issues.Close(issue.Id);

        var log = _client.Changelog(format: "json");
        Assert.NotNull(log);
    }

    [Fact]
    public void Graph_ReturnsTextGraph()
    {
        var a = _client.Issues.Create("Root");
        var b = _client.Issues.Create("Child");
        _client.Dependencies.Add(a.Id, b.Id);

        var graph = _client.Graph(a.Id, format: "text");
        Assert.NotNull(graph);
        Assert.Contains(a.Id, graph);
    }

    [Fact]
    public void Graph_ReturnsMermaidGraph()
    {
        var a = _client.Issues.Create("Root");
        var b = _client.Issues.Create("Child");
        _client.Dependencies.Add(a.Id, b.Id);

        var graph = _client.Graph(a.Id, format: "mermaid");
        Assert.NotNull(graph);
        Assert.Contains("graph", graph);
    }

    [Fact]
    public void Init_FileBased_CreatesClient()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "beads_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "beads.db");
            var client = BeadsClient.Init(dbPath);
            Assert.NotNull(client);
            Assert.True(File.Exists(dbPath));
            client.Dispose();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* locked files on Windows */ }
        }
    }
}
