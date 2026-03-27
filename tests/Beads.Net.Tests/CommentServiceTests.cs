namespace Beads.Net.Tests;

public sealed class CommentServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Add_CreatesComment()
    {
        var issue = _client.Issues.Create("Commentable");
        var comment = _client.Comments.Add(issue.Id, "This is a comment");

        Assert.NotNull(comment);
        Assert.Equal("This is a comment", comment.Body);
        Assert.Equal(issue.Id, comment.IssueId);
    }

    [Fact]
    public void Add_WithAuthor()
    {
        var issue = _client.Issues.Create("With author");
        var comment = _client.Comments.Add(issue.Id, "By Alice", "alice");
        Assert.Equal("alice", comment.Author);
    }

    [Fact]
    public void List_ReturnsComments()
    {
        var issue = _client.Issues.Create("Two comments");
        _client.Comments.Add(issue.Id, "First");
        _client.Comments.Add(issue.Id, "Second");

        var comments = _client.Comments.List(issue.Id);
        Assert.Equal(2, comments.Count);
    }

    [Fact]
    public void List_EmptyForNewIssue()
    {
        var issue = _client.Issues.Create("No comments");
        var comments = _client.Comments.List(issue.Id);
        Assert.Empty(comments);
    }

    [Fact]
    public void List_OrderedChronologically()
    {
        var issue = _client.Issues.Create("Ordered");
        _client.Comments.Add(issue.Id, "First");
        _client.Comments.Add(issue.Id, "Second");
        _client.Comments.Add(issue.Id, "Third");

        var comments = _client.Comments.List(issue.Id);
        Assert.Equal(3, comments.Count);
        Assert.Equal("First", comments[0].Body);
        Assert.Equal("Third", comments[2].Body);
    }

    [Fact]
    public void Add_DefaultAuthorFromConfig()
    {
        var issue = _client.Issues.Create("Default author");
        var comment = _client.Comments.Add(issue.Id, "No author");
        // Author should default to config.Actor or empty string
        Assert.NotNull(comment.Author);
    }
}
