using Beads.Net;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Tests;

/// <summary>Creates an in-memory BeadsClient for each test, automatically disposed.</summary>
public sealed class TestFixture : IDisposable
{
    public BeadsClient Client { get; }

    public TestFixture()
    {
        // Shared in-memory DB via named connection
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        Client = new BeadsClient(conn);
    }

    public void Dispose() => Client.Dispose();

    /// <summary>Create a fresh client for tests that need isolation.</summary>
    public static BeadsClient CreateFresh()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return new BeadsClient(conn);
    }
}
