namespace Beads.Net.Models;

public sealed class Board
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Column
{
    public string Id { get; set; } = "";
    public string BoardId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public int? WipLimit { get; set; }
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
}
