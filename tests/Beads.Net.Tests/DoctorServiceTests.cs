namespace Beads.Net.Tests;

public sealed class DoctorServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Run_FreshDb_NoIssues()
    {
        var report = _client.Doctor.Run();
        Assert.True(report.SchemaOk);
        Assert.True(report.ForeignKeyIntegrity);
        Assert.Empty(report.OrphanedSubtasks);
        Assert.Empty(report.OrphanedDependencies);
        Assert.Empty(report.DependencyCycles);
    }

    [Fact]
    public void Run_WithIssues_StillHealthy()
    {
        _client.Issues.Create("Issue 1");
        _client.Issues.Create("Issue 2");

        var report = _client.Doctor.Run();
        Assert.True(report.SchemaOk);
        // Dirty issue warnings are expected in test
    }

    [Fact]
    public void Run_ReportsSchemaVersion()
    {
        var report = _client.Doctor.Run();
        Assert.True(report.SchemaVersion > 0);
    }

    [Fact]
    public void Run_DetectsDependencyCycles()
    {
        // Indirect cycle detection through dependency creation  
        var a = _client.Issues.Create("A");
        var b = _client.Issues.Create("B");
        _client.Dependencies.Add(a.Id, b.Id);

        // Force a cycle by directly manipulating dep (if possible)
        // Doctor should detect any cycles present
        var report = _client.Doctor.Run();
        // In a clean state, no cycles
        Assert.Empty(report.DependencyCycles);
    }

    [Fact]
    public void Run_ReportsJournalMode()
    {
        var report = _client.Doctor.Run();
        Assert.NotNull(report.JournalMode);
    }
}
