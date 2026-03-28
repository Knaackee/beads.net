using Microsoft.Data.Sqlite;

namespace Beads.Net.Tests;

public sealed class SyncServiceTests : IDisposable
{
    private readonly BeadsClient _client = TestFixture.CreateFresh();

    public void Dispose() => _client.Dispose();

    [Fact]
    public void Status_FreshDb_NoDirty()
    {
        var status = _client.Sync.Status();
        Assert.NotNull(status);
        Assert.Equal(0, status.DirtyCount);
    }

    [Fact]
    public void Status_AfterCreate_HasDirty()
    {
        _client.Issues.Create("New issue");
        var status = _client.Sync.Status();
        Assert.True(status.DirtyCount > 0 || status.DbIssueCount > 0);
    }

    [Fact]
    public void HistoryList_EmptyInitially()
    {
        var entries = _client.Sync.HistoryList();
        Assert.NotNull(entries);
    }

    [Fact]
    public void FlushImport_RetainsIssueMetadata()
    {
        var created = _client.Issues.Create("Sync metadata", new Models.CreateIssueOptions
        {
            Metadata = "{\"from\":\"sync\"}",
        });

        var tempDir = Path.Combine(Path.GetTempPath(), "beads_sync_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var jsonl = Path.Combine(tempDir, "issues.jsonl");

        var flush = _client.Sync.Flush(new Models.FlushOptions
        {
            OutputPath = jsonl,
            AllowExternalJsonl = true,
        });
        Assert.True(flush.ExportedCount > 0);

        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var importedClient = new BeadsClient(conn);

        var import = importedClient.Sync.Import(new Models.ImportOptions
        {
            InputPath = jsonl,
            AllowExternalJsonl = true,
        });

        Assert.True(import.ImportedCount > 0);
        var reloaded = importedClient.Issues.GetOrThrow(created.Id);
        Assert.Equal("{\"from\":\"sync\"}", reloaded.Metadata);

        Directory.Delete(tempDir, true);
    }
}
