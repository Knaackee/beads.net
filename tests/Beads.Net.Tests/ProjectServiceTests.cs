using Beads.Net.Models;
using Beads.Net.Errors;

namespace Beads.Net.Tests;

public sealed class ProjectServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Create_ReturnsProject()
    {
        var project = _client.Projects.Create("My Project");
        Assert.NotNull(project);
        Assert.Equal("My Project", project.Name);
    }

    [Fact]
    public void Create_WithDescriptionAndColor()
    {
        var project = _client.Projects.Create("Styled", description: "A description", color: "#FF0000");
        Assert.Equal("A description", project.Description);
        Assert.Equal("#FF0000", project.Color);
    }

    [Fact]
    public void Create_DefaultMetadata_IsJsonObject()
    {
        var project = _client.Projects.Create("Metadata default");
        Assert.Equal("{}", project.Metadata);

        var loaded = _client.Projects.Get(project.Id);
        Assert.NotNull(loaded);
        Assert.Equal("{}", loaded!.Metadata);
    }

    [Fact]
    public void Create_WithMetadata_Persists()
    {
        var project = _client.Projects.Create("Metadata set", metadata: "{\"tier\":\"gold\"}");
        Assert.Equal("{\"tier\":\"gold\"}", project.Metadata);

        var loaded = _client.Projects.Get(project.Id);
        Assert.NotNull(loaded);
        Assert.Equal("{\"tier\":\"gold\"}", loaded!.Metadata);
    }

    [Fact]
    public void Get_ByName_ReturnsProject()
    {
        var created = _client.Projects.Create("FindMe");
        var found = _client.Projects.Get("FindMe");
        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public void Get_ById_ReturnsProject()
    {
        var created = _client.Projects.Create("ByIdTest");
        var found = _client.Projects.Get(created.Id);
        Assert.NotNull(found);
        Assert.Equal("ByIdTest", found.Name);
    }

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        var found = _client.Projects.Get("nonexistent");
        Assert.Null(found);
    }

    [Fact]
    public void List_ReturnsActiveProjects()
    {
        _client.Projects.Create("Project A");
        _client.Projects.Create("Project B");

        var projects = _client.Projects.List();
        Assert.Equal(2, projects.Count);
    }

    [Fact]
    public void List_ExcludesArchivedByDefault()
    {
        var p1 = _client.Projects.Create("Active");
        var p2 = _client.Projects.Create("Archived");
        _client.Projects.Archive(p2.Id);

        var projects = _client.Projects.List(includeArchived: false);
        Assert.Single(projects);
        Assert.Equal("Active", projects[0].Name);
    }

    [Fact]
    public void List_IncludesArchivedWhenRequested()
    {
        _client.Projects.Create("Active");
        var p2 = _client.Projects.Create("Archived");
        _client.Projects.Archive(p2.Id);

        var projects = _client.Projects.List(includeArchived: true);
        Assert.Equal(2, projects.Count);
    }

    [Fact]
    public void Update_ChangesFields()
    {
        var project = _client.Projects.Create("Original");
        var updated = _client.Projects.Update(project.Id, name: "Updated", description: "New desc");
        Assert.Equal("Updated", updated.Name);
        Assert.Equal("New desc", updated.Description);
    }

    [Fact]
    public void Update_Metadata_ChangesField()
    {
        var project = _client.Projects.Create("Original metadata");
        var updated = _client.Projects.Update(project.Id, metadata: "{\"area\":\"infra\"}");
        Assert.Equal("{\"area\":\"infra\"}", updated.Metadata);

        var loaded = _client.Projects.Get(project.Id);
        Assert.NotNull(loaded);
        Assert.Equal("{\"area\":\"infra\"}", loaded!.Metadata);
    }

    [Fact]
    public void Archive_MarksProjectArchived()
    {
        var project = _client.Projects.Create("To archive");
        _client.Projects.Archive(project.Id);

        var found = _client.Projects.Get(project.Id);
        Assert.NotNull(found);
        Assert.Equal("archived", found.Status);
    }

    [Fact]
    public void Delete_RemovesProject()
    {
        var project = _client.Projects.Create("To delete");
        _client.Projects.Delete(project.Id);

        var found = _client.Projects.Get(project.Id);
        Assert.Null(found);
    }

    [Fact]
    public void Delete_WithActiveIssues_Throws()
    {
        var project = _client.Projects.Create("Has issues");
        _client.Issues.Create("Issue", new CreateIssueOptions { ProjectId = project.Id });

        Assert.Throws<BeadsValidationException>(() => _client.Projects.Delete(project.Id));
    }
}
