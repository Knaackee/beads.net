namespace Beads.Net.Models;

public sealed class Dependency
{
    public string IssueId { get; set; } = "";
    public string DependsOnId { get; set; } = "";
    public string DepType { get; set; } = "blocks";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? Metadata { get; set; }
    public string? ThreadId { get; set; }
}
