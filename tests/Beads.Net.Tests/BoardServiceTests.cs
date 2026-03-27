using Beads.Net.Models;

namespace Beads.Net.Tests;

public sealed class BoardServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    private Models.Project CreateProject(string name = "Test Project") =>
        _client.Projects.Create(name);

    [Fact]
    public void Create_ReturnsBoard()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "Sprint 1");
        Assert.NotNull(board);
        Assert.Equal("Sprint 1", board.Name);
    }

    [Fact]
    public void List_ReturnsBoardsForProject()
    {
        var project = CreateProject();
        _client.Boards.Create(project.Id, "Board A");
        _client.Boards.Create(project.Id, "Board B");

        var boards = _client.Boards.List(project.Id);
        Assert.Equal(2, boards.Count);
    }

    [Fact]
    public void Update_ChangesName()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "Old Name");
        var updated = _client.Boards.Update(board.Id, name: "New Name");
        Assert.Equal("New Name", updated.Name);
    }

    [Fact]
    public void Delete_RemovesBoard()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "To Delete");
        _client.Boards.Delete(board.Id);

        var boards = _client.Boards.List(project.Id);
        Assert.Empty(boards);
    }

    [Fact]
    public void CreateColumn_AddsToBoard()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "Kanban");
        var column = _client.Boards.CreateColumn(board.Id, "To Do");
        Assert.Equal("To Do", column.Name);
    }

    [Fact]
    public void CreateColumn_WithWipLimit()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "Kanban");
        var column = _client.Boards.CreateColumn(board.Id, "In Progress", wipLimit: 3);
        Assert.Equal(3, column.WipLimit);
    }

    [Fact]
    public void ListColumns_ReturnsOrdered()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "Board");
        _client.Boards.CreateColumn(board.Id, "Todo");
        _client.Boards.CreateColumn(board.Id, "In Progress");
        _client.Boards.CreateColumn(board.Id, "Done");

        var columns = _client.Boards.ListColumns(board.Id);
        Assert.Equal(3, columns.Count);
        Assert.Equal("Todo", columns[0].Name);
        Assert.Equal("Done", columns[2].Name);
    }

    [Fact]
    public void UpdateColumn_ChangesFields()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "Board");
        var column = _client.Boards.CreateColumn(board.Id, "Old");
        var updated = _client.Boards.UpdateColumn(column.Id, name: "New", wipLimit: 5);
        Assert.Equal("New", updated.Name);
        Assert.Equal(5, updated.WipLimit);
    }

    [Fact]
    public void DeleteColumn_RemovesColumn()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "Board");
        var column = _client.Boards.CreateColumn(board.Id, "Temporary");
        _client.Boards.DeleteColumn(column.Id);

        var columns = _client.Boards.ListColumns(board.Id);
        Assert.Empty(columns);
    }

    [Fact]
    public void MoveIssue_AssignsToColumn()
    {
        var project = CreateProject();
        var board = _client.Boards.Create(project.Id, "Board");
        var column = _client.Boards.CreateColumn(board.Id, "In Progress");
        var issue = _client.Issues.Create("Movable", new CreateIssueOptions { ProjectId = project.Id });

        _client.Boards.MoveIssue(issue.Id, column.Id);

        // Verify the issue is now in the column  
        var updated = _client.Issues.GetOrThrow(issue.Id);
        Assert.Equal(column.Id, updated.ColumnId);
    }
}
