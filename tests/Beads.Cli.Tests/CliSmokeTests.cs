using System.Diagnostics;
using System.Text.Json;

namespace Beads.Cli.Tests;

/// <summary>
/// Smoke tests that launch the CLI as a child process and verify exit codes and output.
/// </summary>
public sealed class CliSmokeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public CliSmokeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "beads_cli_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "beads.db");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); }
        catch { /* locked files on Windows */ }
    }

    private (int ExitCode, string Output, string Error) RunCli(string args)
    {
        // Find the CLI executable from the build output
        var cliProject = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "Beads.Cli"));

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{cliProject}\" --framework net8.0 --no-build -- --db \"{_dbPath}\" {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _tempDir,
        };

        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(30_000);
        return (process.ExitCode, output, error);
    }

    private static string ExtractId(string createOutput)
    {
        // Output format: "Created bd-xxxxx: title"
        foreach (var line in createOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            var idx = trimmed.IndexOf("bd-", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var end = trimmed.IndexOfAny([':', ' '], idx + 3);
                return end > idx ? trimmed[idx..end] : trimmed[idx..];
            }
        }
        throw new InvalidOperationException($"No bd- ID found in: {createOutput}");
    }

    [Fact]
    public void Help_ReturnsZeroAndContent()
    {
        var (exit, output, _) = RunCli("--help");
        Assert.Equal(0, exit);
        Assert.Contains("beads", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Version_ReturnsZero()
    {
        // init first, then version
        RunCli("init");
        var (exit, output, _) = RunCli("version");
        Assert.Equal(0, exit);
        Assert.NotEmpty(output.Trim());
    }

    [Fact]
    public void Init_CreatesDatabase()
    {
        var (exit, _, _) = RunCli("init");
        Assert.Equal(0, exit);
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public void Create_ReturnsIssueId()
    {
        RunCli("init");
        var (exit, output, _) = RunCli("create \"Test issue\" --type task --priority 1");
        Assert.Equal(0, exit);
        Assert.Contains("bd-", output);
    }

    [Fact]
    public void Quick_ReturnsId()
    {
        RunCli("init");
        var (exit, output, _) = RunCli("q \"Quick task\"");
        Assert.Equal(0, exit);
        Assert.Contains("bd-", output);
    }

    [Fact]
    public void List_EmptyDb_ReturnsZero()
    {
        RunCli("init");
        var (exit, _, _) = RunCli("list");
        Assert.Equal(0, exit);
    }

    [Fact]
    public void List_Json_ReturnsValidJson()
    {
        RunCli("init");
        RunCli("create \"JSON test\"");
        var (exit, output, _) = RunCli("list --json");
        Assert.Equal(0, exit);
        // Verify it's valid JSON
        var doc = JsonDocument.Parse(output);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Show_ExistingIssue_ReturnsDetails()
    {
        RunCli("init");
        var (_, createOut, _) = RunCli("create \"Show test\"");
        var id = ExtractId(createOut);

        var (exit, output, _) = RunCli($"show {id}");
        Assert.Equal(0, exit);
        Assert.Contains("Show test", output);
    }

    [Fact]
    public void Stats_ReturnsZero()
    {
        RunCli("init");
        RunCli("create \"Stat issue\"");
        var (exit, _, _) = RunCli("stats");
        Assert.Equal(0, exit);
    }

    [Fact]
    public void Doctor_ReturnsZero()
    {
        RunCli("init");
        var (exit, _, _) = RunCli("doctor");
        Assert.Equal(0, exit);
    }

    [Fact]
    public void Where_ReturnsDbPath()
    {
        RunCli("init");
        var (exit, output, _) = RunCli("where");
        Assert.Equal(0, exit);
        Assert.Contains("beads.db", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Label_AddAndList()
    {
        RunCli("init");
        var (_, createOut, _) = RunCli("create \"Label test\"");
        var id = ExtractId(createOut);

        RunCli($"label add {id} backend");
        var (exit, output, _) = RunCli($"label list {id}");
        Assert.Equal(0, exit);
        Assert.Contains("backend", output);
    }

    [Fact]
    public void Comment_AddAndList()
    {
        RunCli("init");
        var (_, createOut, _) = RunCli("create \"Comment test\"");
        var id = ExtractId(createOut);

        RunCli($"comments add {id} \"Hello world\"");
        var (exit, output, _) = RunCli($"comments list {id}");
        Assert.Equal(0, exit);
        Assert.Contains("Hello world", output);
    }

    [Fact]
    public void Close_And_Reopen()
    {
        RunCli("init");
        var (_, createOut, _) = RunCli("create \"Close test\"");
        var id = ExtractId(createOut);

        var (closeExit, _, _) = RunCli($"close {id}");
        Assert.Equal(0, closeExit);

        var (reopenExit, _, _) = RunCli($"reopen {id}");
        Assert.Equal(0, reopenExit);
    }

    [Fact]
    public void Count_ReturnsZero()
    {
        RunCli("init");
        RunCli("create \"Count test\"");
        var (exit, output, _) = RunCli("count");
        Assert.Equal(0, exit);
    }

    [Fact]
    public void Lint_ReturnsZero()
    {
        RunCli("init");
        var (exit, _, _) = RunCli("lint");
        Assert.Equal(0, exit);
    }

    [Fact]
    public void Config_List_ReturnsZero()
    {
        RunCli("init");
        var (exit, _, _) = RunCli("config list");
        Assert.Equal(0, exit);
    }

    [Fact]
    public void Config_Path_ReturnsZero()
    {
        RunCli("init");
        var (exit, _, _) = RunCli("config path");
        Assert.Equal(0, exit);
    }
}
