namespace Beads.Net.Models;

public sealed class Comment
{
    public long Id { get; set; }
    public string IssueId { get; set; } = "";
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
