using System.Text.Json;
using Beads.Net.Errors;
using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class SyncService
{
    private readonly Db _db;
    private readonly BeadsConfig _config;

    internal SyncService(Db db, BeadsConfig config)
    {
        _db = db;
        _config = config;
    }

    public FlushResult Flush(FlushOptions? options = null)
    {
        options ??= new FlushOptions();
        var outputPath = options.OutputPath ?? GetDefaultJsonlPath();

        if (!options.AllowExternalJsonl && !IsInsideBeadsDir(outputPath))
            throw new BeadsSyncException("Output path is outside .beads/ directory. Use --allow-external-jsonl to override.");

        var dirtyIds = _db.Query(
            $"SELECT issue_id FROM {_db.T("dirty_issues")}",
            null, r => r.GetString(0));

        if (dirtyIds.Count == 0)
            return new FlushResult { ExportedCount = 0, OutputPath = outputPath };

        // Read all dirty issues with their relations
        var issues = new List<Issue>();
        foreach (var id in dirtyIds)
        {
            var issue = ReadFullIssue(id);
            if (issue != null)
                issues.Add(issue);
        }

        // Safety guard: don't overwrite non-empty JSONL with empty export
        if (issues.Count == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
        {
            if (options.ErrorPolicy == "strict")
                throw new BeadsSyncException("Cannot overwrite non-empty JSONL with empty export.");
            return new FlushResult { ExportedCount = 0, OutputPath = outputPath, Skipped = dirtyIds.Count };
        }

        // Ensure directory exists
        var dir = Path.GetDirectoryName(outputPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Create backup
        if (File.Exists(outputPath))
        {
            var backupDir = Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", "history");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);
            var backupName = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jsonl";
            File.Copy(outputPath, Path.Combine(backupDir, backupName));
        }

        // Atomic write: temp file → rename
        var tempPath = outputPath + ".tmp";
        using (var writer = new StreamWriter(tempPath))
        {
            // Read existing non-dirty entries if file exists
            if (File.Exists(outputPath))
            {
                var existingDirtySet = new HashSet<string>(dirtyIds);
                foreach (var line in File.ReadLines(outputPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var existingId = doc.RootElement.GetProperty("id").GetString();
                        if (existingId != null && !existingDirtySet.Contains(existingId))
                            writer.WriteLine(line);
                    }
                    catch { /* skip malformed lines */ }
                }
            }

            foreach (var issue in issues)
            {
                var json = JsonSerializer.Serialize(issue, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });
                writer.WriteLine(json);
            }
        }

        File.Move(tempPath, outputPath, overwrite: true);

        // Update export hashes and clear dirty flags
        using var tx = _db.BeginTransaction();
        foreach (var issue in issues)
        {
            if (issue.ContentHash != null)
            {
                _db.Execute(
                    $"INSERT OR REPLACE INTO {_db.T("export_hashes")} (issue_id, content_hash, exported_at) VALUES (@id, @hash, @now)",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@id", issue.Id);
                        cmd.Parameters.AddWithValue("@hash", issue.ContentHash);
                        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    });
            }
        }
        _db.Execute($"DELETE FROM {_db.T("dirty_issues")}");
        tx.Commit();

        return new FlushResult
        {
            ExportedCount = issues.Count,
            OutputPath = outputPath,
        };
    }

    public ImportResult Import(ImportOptions? options = null)
    {
        options ??= new ImportOptions();
        var inputPath = options.InputPath ?? GetDefaultJsonlPath();

        if (!File.Exists(inputPath))
            throw new BeadsSyncException($"JSONL file not found: {inputPath}");

        if (!options.AllowExternalJsonl && !IsInsideBeadsDir(inputPath))
            throw new BeadsSyncException("Input path is outside .beads/ directory. Use --allow-external-jsonl to override.");

        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var line in File.ReadLines(inputPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var issue = JsonSerializer.Deserialize<Issue>(line, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                });

                if (issue == null) { skipped++; continue; }

                var id = issue.Id;
                if (options.RenamePrefix != null && id.Contains('-'))
                {
                    var parts = id.Split('-', 2);
                    id = $"{options.RenamePrefix}-{parts[1]}";
                    issue = issue with { Id = id };
                }

                // Check if exists
                var existingHash = _db.QueryScalarString(
                    $"SELECT content_hash FROM {_db.T("issues")} WHERE id = @id",
                    cmd => cmd.Parameters.AddWithValue("@id", id));

                if (existingHash != null)
                {
                    if (existingHash == issue.ContentHash && !options.Force)
                    {
                        skipped++;
                        continue;
                    }
                    UpdateIssueFromImport(issue);
                    updated++;
                }
                else
                {
                    InsertIssueFromImport(issue);
                    imported++;
                }
            }
            catch (JsonException ex)
            {
                errors.Add($"Parse error: {ex.Message}");
                if (options.OrphanPolicy == "strict")
                    throw new BeadsSyncException($"JSONL parse error: {ex.Message}");
            }
        }

        return new ImportResult
        {
            ImportedCount = imported,
            UpdatedCount = updated,
            SkippedCount = skipped,
            Errors = errors,
        };
    }

    public SyncStatus Status()
    {
        var dirtyCount = (int)(_db.QueryScalar<long>(
            $"SELECT COUNT(*) FROM {_db.T("dirty_issues")}") ?? 0);
        var dbCount = (int)(_db.QueryScalar<long>(
            $"SELECT COUNT(*) FROM {_db.T("issues")} WHERE status != 'tombstone'") ?? 0);

        var jsonlPath = GetDefaultJsonlPath();
        var jsonlCount = 0;
        DateTime? jsonlModified = null;
        if (File.Exists(jsonlPath))
        {
            jsonlCount = File.ReadLines(jsonlPath).Count(l => !string.IsNullOrWhiteSpace(l));
            jsonlModified = File.GetLastWriteTimeUtc(jsonlPath);
        }

        DateTime? dbModified = null;
        if (File.Exists(_config.Db))
            dbModified = File.GetLastWriteTimeUtc(_config.Db);

        var status = "in_sync";
        if (dirtyCount > 0) status = "db_newer";
        else if (jsonlModified > dbModified) status = "jsonl_newer";

        return new SyncStatus
        {
            DbPath = _config.Db,
            JsonlPath = jsonlPath,
            DbModified = dbModified,
            JsonlModified = jsonlModified,
            DbIssueCount = dbCount,
            JsonlIssueCount = jsonlCount,
            DirtyCount = dirtyCount,
            Status = status,
        };
    }

    public List<HistoryEntry> HistoryList()
    {
        var historyDir = Path.Combine(Path.GetDirectoryName(GetDefaultJsonlPath()) ?? ".", "history");
        if (!Directory.Exists(historyDir))
            return [];

        return Directory.GetFiles(historyDir, "*.jsonl")
            .Select(f => new HistoryEntry
            {
                Path = f,
                Name = Path.GetFileName(f),
                CreatedAt = File.GetCreationTimeUtc(f),
                Size = new FileInfo(f).Length,
            })
            .OrderByDescending(h => h.CreatedAt)
            .ToList();
    }

    public void HistoryRestore(string backup)
    {
        var historyDir = Path.Combine(Path.GetDirectoryName(GetDefaultJsonlPath()) ?? ".", "history");
        var path = Path.Combine(historyDir, backup);
        if (!File.Exists(path))
            throw new BeadsSyncException($"Backup not found: {backup}");

        var target = GetDefaultJsonlPath();
        File.Copy(path, target, overwrite: true);
    }

    // ── Helpers ──

    private string GetDefaultJsonlPath()
    {
        var dir = Path.GetDirectoryName(_config.Db) ?? ".beads";
        return Path.Combine(dir, "issues.jsonl");
    }

    private static bool IsInsideBeadsDir(string path)
    {
        var full = Path.GetFullPath(path);
        return full.Contains(".beads", StringComparison.OrdinalIgnoreCase);
    }

    private Issue? ReadFullIssue(string id)
    {
        var issue = _db.QuerySingle(
            $"SELECT * FROM {_db.T("issues")} WHERE id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", id),
            IssueService.ReadIssue);

        if (issue == null) return null;

        var labels = _db.Query(
            $"SELECT label FROM {_db.T("labels")} WHERE issue_id = @id ORDER BY label",
            cmd => cmd.Parameters.AddWithValue("@id", id),
            r => r.GetString(0));

        var deps = _db.Query(
            $"SELECT * FROM {_db.T("dependencies")} WHERE issue_id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", id),
            IssueService.ReadDependency);

        var comments = _db.Query(
            $"SELECT * FROM {_db.T("comments")} WHERE issue_id = @id ORDER BY created_at",
            cmd => cmd.Parameters.AddWithValue("@id", id),
            IssueService.ReadComment);

        return issue with { Labels = labels, Dependencies = deps, Comments = comments };
    }

    private void InsertIssueFromImport(Issue issue)
    {
        _db.Execute($"""
            INSERT INTO {_db.T("issues")} (
                id, content_hash, title, description, design, acceptance_criteria, notes,
                status, priority, issue_type, assignee, owner, estimated_minutes,
                created_at, created_by, updated_at, closed_at, close_reason,
                due_at, defer_until, external_ref, source_repo,
                ephemeral, pinned, is_template, project_id, position
            ) VALUES (
                @id, @hash, @title, @desc, @design, @ac, @notes,
                @status, @pri, @type, @assignee, @owner, @est,
                @cat, @cby, @uat, @closedat, @closereason,
                @due, @defer, @eref, @repo,
                @eph, @pin, @tmpl, @proj, @pos
            )
            """, cmd =>
        {
            cmd.Parameters.AddWithValue("@id", issue.Id);
            cmd.Parameters.AddWithValue("@hash", (object?)issue.ContentHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@title", issue.Title);
            cmd.Parameters.AddWithValue("@desc", issue.Description);
            cmd.Parameters.AddWithValue("@design", issue.Design);
            cmd.Parameters.AddWithValue("@ac", issue.AcceptanceCriteria);
            cmd.Parameters.AddWithValue("@notes", issue.Notes);
            cmd.Parameters.AddWithValue("@status", issue.Status);
            cmd.Parameters.AddWithValue("@pri", issue.Priority);
            cmd.Parameters.AddWithValue("@type", issue.IssueType);
            cmd.Parameters.AddWithValue("@assignee", (object?)issue.Assignee ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@owner", issue.Owner ?? "");
            cmd.Parameters.AddWithValue("@est", issue.EstimatedMinutes.HasValue ? issue.EstimatedMinutes.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@cat", issue.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@cby", issue.CreatedBy ?? "");
            cmd.Parameters.AddWithValue("@uat", issue.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@closedat", issue.ClosedAt.HasValue ? issue.ClosedAt.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@closereason", (object?)issue.CloseReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@due", issue.DueAt.HasValue ? issue.DueAt.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@defer", issue.DeferUntil.HasValue ? issue.DeferUntil.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@eref", (object?)issue.ExternalRef ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@repo", issue.SourceRepo);
            cmd.Parameters.AddWithValue("@eph", issue.Ephemeral ? 1 : 0);
            cmd.Parameters.AddWithValue("@pin", issue.Pinned ? 1 : 0);
            cmd.Parameters.AddWithValue("@tmpl", issue.IsTemplate ? 1 : 0);
            cmd.Parameters.AddWithValue("@proj", (object?)issue.ProjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pos", issue.Position);
        });

        // Import labels
        if (issue.Labels is { Count: > 0 })
        {
            foreach (var label in issue.Labels)
            {
                _db.Execute(
                    $"INSERT OR IGNORE INTO {_db.T("labels")} (issue_id, label) VALUES (@id, @lbl)",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@id", issue.Id);
                        cmd.Parameters.AddWithValue("@lbl", label);
                    });
            }
        }

        // Import dependencies
        if (issue.Dependencies is { Count: > 0 })
        {
            foreach (var dep in issue.Dependencies)
            {
                _db.Execute(
                    $"INSERT OR IGNORE INTO {_db.T("dependencies")} (issue_id, depends_on_id, type, created_at, created_by) VALUES (@id, @dep, @type, @now, @by)",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@id", dep.IssueId);
                        cmd.Parameters.AddWithValue("@dep", dep.DependsOnId);
                        cmd.Parameters.AddWithValue("@type", dep.DepType);
                        cmd.Parameters.AddWithValue("@now", dep.CreatedAt.ToString("o"));
                        cmd.Parameters.AddWithValue("@by", dep.CreatedBy ?? "");
                    });
            }
        }
    }

    private void UpdateIssueFromImport(Issue issue)
    {
        _db.Execute($"""
            UPDATE {_db.T("issues")} SET
                content_hash = @hash, title = @title, description = @desc,
                design = @design, acceptance_criteria = @ac, notes = @notes,
                status = @status, priority = @pri, issue_type = @type,
                assignee = @assignee, owner = @owner, estimated_minutes = @est,
                updated_at = @uat, closed_at = @closedat, close_reason = @closereason,
                due_at = @due, defer_until = @defer, external_ref = @eref,
                ephemeral = @eph, pinned = @pin, is_template = @tmpl
            WHERE id = @id
            """, cmd =>
        {
            cmd.Parameters.AddWithValue("@id", issue.Id);
            cmd.Parameters.AddWithValue("@hash", (object?)issue.ContentHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@title", issue.Title);
            cmd.Parameters.AddWithValue("@desc", issue.Description);
            cmd.Parameters.AddWithValue("@design", issue.Design);
            cmd.Parameters.AddWithValue("@ac", issue.AcceptanceCriteria);
            cmd.Parameters.AddWithValue("@notes", issue.Notes);
            cmd.Parameters.AddWithValue("@status", issue.Status);
            cmd.Parameters.AddWithValue("@pri", issue.Priority);
            cmd.Parameters.AddWithValue("@type", issue.IssueType);
            cmd.Parameters.AddWithValue("@assignee", (object?)issue.Assignee ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@owner", issue.Owner ?? "");
            cmd.Parameters.AddWithValue("@est", issue.EstimatedMinutes.HasValue ? issue.EstimatedMinutes.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@uat", issue.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@closedat", issue.ClosedAt.HasValue ? issue.ClosedAt.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@closereason", (object?)issue.CloseReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@due", issue.DueAt.HasValue ? issue.DueAt.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@defer", issue.DeferUntil.HasValue ? issue.DeferUntil.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@eref", (object?)issue.ExternalRef ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@eph", issue.Ephemeral ? 1 : 0);
            cmd.Parameters.AddWithValue("@pin", issue.Pinned ? 1 : 0);
            cmd.Parameters.AddWithValue("@tmpl", issue.IsTemplate ? 1 : 0);
        });
    }
}
