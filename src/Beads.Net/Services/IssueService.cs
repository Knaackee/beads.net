using Beads.Net.Enums;
using Beads.Net.Errors;
using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class IssueService
{
    private readonly Db _db;
    private readonly BeadsConfig _config;
    private readonly EventService _events;

    internal IssueService(Db db, BeadsConfig config, EventService events)
    {
        _db = db;
        _config = config;
        _events = events;
    }

    public Issue Create(string title, CreateIssueOptions? options = null)
    {
        options ??= new CreateIssueOptions();
        if (string.IsNullOrWhiteSpace(title))
            throw new BeadsValidationException("Title must not be empty.");
        if (title.Length > 500)
            throw new BeadsValidationException("Title must be 500 characters or fewer.");

        var now = DateTime.UtcNow;
        var id = IdGenerator.Generate(
            _config.IdPrefix, title, now,
            candidate => _db.QueryScalar<long>(
                $"SELECT COUNT(*) FROM {_db.T("issues")} WHERE id = @id",
                cmd => cmd.Parameters.AddWithValue("@id", candidate)) > 0);

        if (options.DryRun)
        {
            return new Issue
            {
                Id = id,
                Title = title,
                Status = options.Status ?? "open",
                Priority = options.Priority ?? _config.Defaults.Priority,
                IssueType = options.IssueType ?? _config.Defaults.Type,
                Metadata = options.Metadata ?? "{}",
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        var issue = new Issue
        {
            Id = id,
            Title = title,
            Description = options.Description ?? "",
            Design = options.Design ?? "",
            AcceptanceCriteria = options.AcceptanceCriteria ?? "",
            Notes = options.Notes ?? "",
            Status = options.Status ?? "open",
            Priority = options.Priority ?? _config.Defaults.Priority,
            IssueType = options.IssueType ?? _config.Defaults.Type,
            Assignee = options.Assignee ?? _config.Defaults.Assignee,
            Owner = options.Owner ?? "",
            EstimatedMinutes = options.EstimatedMinutes,
            CreatedAt = now,
            CreatedBy = _config.Actor,
            UpdatedAt = now,
            DueAt = options.DueAt,
            DeferUntil = options.DeferUntil,
            ExternalRef = options.ExternalRef,
            Ephemeral = options.Ephemeral,
            Pinned = options.Pinned,
            IsTemplate = options.IsTemplate,
            ProjectId = options.ProjectId,
            Metadata = options.Metadata ?? "{}",
        };
        issue = issue with { ContentHash = ContentHash.Compute(issue) };

        using var tx = _db.BeginTransaction();
        InsertIssue(issue);

        if (options.Labels is { Count: > 0 })
        {
            foreach (var label in options.Labels)
                InsertLabel(id, label);
        }

        if (options.DependsOn is { Count: > 0 })
        {
            foreach (var dep in options.DependsOn)
                InsertDependency(id, dep, "blocks");
        }

        if (options.ParentId != null)
            InsertDependency(id, options.ParentId, "parent-child");

        MarkDirty(id);
        _events.Record(id, "created");
        tx.Commit();
        return issue;
    }

    public string Quick(string title, CreateIssueOptions? options = null)
    {
        var issue = Create(title, options);
        return issue.Id;
    }

    public Issue? Get(string id)
    {
        var resolved = ResolveId(id);
        var issue = _db.QuerySingle<Issue>(
            $"SELECT * FROM {_db.T("issues")} WHERE id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", resolved),
            ReadIssue);
        if (issue == null) return null;

        issue = issue with
        {
            Labels = GetLabels(resolved),
            Dependencies = GetDependencies(resolved),
            Comments = GetComments(resolved),
        };
        return issue;
    }

    public Issue GetOrThrow(string id)
    {
        return Get(id) ?? throw new BeadsNotFoundException($"Issue not found: {id}");
    }

    public IssueListResult List(IssueFilter? filter = null)
    {
        filter ??= new IssueFilter();
        var (where, paramSetter) = BuildWhere(filter);
        var orderBy = BuildOrderBy(filter);
        var t = _db.T("issues");

        var countSql = $"SELECT COUNT(*) FROM {t}{where}";
        var total = (int)(_db.QueryScalar<long>(countSql, paramSetter) ?? 0);

        var limit = filter.Limit > 0 ? filter.Limit : int.MaxValue;
        var sql = $"SELECT * FROM {t}{where}{orderBy} LIMIT {limit} OFFSET {filter.Offset}";
        var issues = _db.Query(sql, paramSetter, ReadIssue);

        foreach (var issue in issues)
        {
            // Hydrate labels inline for list view (lightweight)
        }

        return new IssueListResult
        {
            Issues = issues,
            Total = total,
            Limit = filter.Limit,
            Offset = filter.Offset,
            HasMore = filter.Offset + issues.Count < total,
        };
    }

    public Issue Update(string id, UpdateIssueOptions options)
    {
        var resolved = ResolveId(id);
        var existing = GetOrThrow(resolved);
        var sets = new List<string>();
        Action<SqliteCommand> paramBuilder = cmd => cmd.Parameters.AddWithValue("@id", resolved);

        void Set(string col, object? val, string paramName)
        {
            if (val == null) return;
            sets.Add($"{col} = @{paramName}");
            var prev = paramBuilder;
            paramBuilder = cmd => { prev(cmd); cmd.Parameters.AddWithValue($"@{paramName}", val ?? DBNull.Value); };
        }

        if (options.Title != null) Set("title", options.Title, "title");
        if (options.Description != null) Set("description", options.Description, "desc");
        if (options.Design != null) Set("design", options.Design, "design");
        if (options.AcceptanceCriteria != null) Set("acceptance_criteria", options.AcceptanceCriteria, "ac");
        if (options.Notes != null) Set("notes", options.Notes, "notes");
        if (options.Priority.HasValue) Set("priority", options.Priority.Value, "pri");
        if (options.IssueType != null) Set("issue_type", options.IssueType, "type");
        if (options.Assignee != null) Set("assignee", options.Assignee == "" ? DBNull.Value : options.Assignee, "assign");
        if (options.Owner != null) Set("owner", options.Owner, "owner");
        if (options.EstimatedMinutes.HasValue) Set("estimated_minutes", options.EstimatedMinutes.Value, "est");
        if (options.DueAt.HasValue) Set("due_at", options.DueAt.Value.ToString("o"), "due");
        if (options.ExternalRef != null) Set("external_ref", options.ExternalRef == "" ? DBNull.Value : options.ExternalRef, "eref");
        if (options.Pinned.HasValue) Set("pinned", options.Pinned.Value ? 1 : 0, "pin");
        if (options.IsTemplate.HasValue) Set("is_template", options.IsTemplate.Value ? 1 : 0, "tmpl");
        if (options.ProjectId != null) Set("project_id", options.ProjectId == "" ? DBNull.Value : options.ProjectId, "proj");
        if (options.ColumnId != null) Set("column_id", options.ColumnId == "" ? DBNull.Value : options.ColumnId, "col");
        if (options.Metadata != null) Set("metadata", options.Metadata, "meta");

        if (options.Status != null)
        {
            Set("status", options.Status, "status");
            if (IssueStatusExtensions.IsTerminal(options.Status) && existing.ClosedAt == null)
            {
                Set("closed_at", DateTime.UtcNow.ToString("o"), "closedat");
            }
        }

        if (options.Claim)
        {
            Set("assignee", _config.Actor, "assign");
            if (existing.Status == "open")
                Set("status", "in_progress", "status");
        }

        if (sets.Count == 0 && options.AddLabels == null && options.RemoveLabels == null)
            return existing;

        using var tx = _db.BeginTransaction();

        if (sets.Count > 0)
        {
            sets.Add("updated_at = @now");
            var prevBuilder = paramBuilder;
            paramBuilder = cmd => { prevBuilder(cmd); cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o")); };

            var sql = $"UPDATE {_db.T("issues")} SET {string.Join(", ", sets)} WHERE id = @id";
            _db.Execute(sql, paramBuilder);
        }

        if (options.AddLabels is { Count: > 0 })
            foreach (var lbl in options.AddLabels) InsertLabel(resolved, lbl);

        if (options.RemoveLabels is { Count: > 0 })
            foreach (var lbl in options.RemoveLabels) RemoveLabel(resolved, lbl);

        MarkDirty(resolved);

        if (options.Status != null && options.Status != existing.Status)
            _events.Record(resolved, "status_changed", existing.Status, options.Status);
        if (options.Priority.HasValue && options.Priority.Value != existing.Priority)
            _events.Record(resolved, "priority_changed", existing.Priority.ToString(), options.Priority.Value.ToString());
        if (options.Assignee != null && options.Assignee != existing.Assignee)
            _events.Record(resolved, "assignee_changed", existing.Assignee, options.Assignee);
        if (options.Title != null || options.Description != null || options.Design != null)
            _events.Record(resolved, "updated");

        tx.Commit();

        // Recompute content hash
        var updated = GetOrThrow(resolved);
        var newHash = ContentHash.Compute(updated);
        _db.Execute($"UPDATE {_db.T("issues")} SET content_hash = @h WHERE id = @id", cmd =>
        {
            cmd.Parameters.AddWithValue("@h", newHash);
            cmd.Parameters.AddWithValue("@id", resolved);
        });
        return updated with { ContentHash = newHash };
    }

    public Issue Close(string id, CloseOptions? options = null)
    {
        options ??= new CloseOptions();
        var resolved = ResolveId(id);
        var existing = GetOrThrow(resolved);

        if (existing.IsTombstone)
            throw new BeadsValidationException($"Cannot close tombstone issue: {id}");
        if (existing.IsTerminal && !options.Force)
            throw new BeadsValidationException($"Issue already closed: {id}");

        if (!options.Force)
        {
            var blockers = GetBlockingDependencies(resolved);
            if (blockers.Count > 0)
                throw new BeadsValidationException(
                    $"Issue {id} is blocked by: {string.Join(", ", blockers.Select(b => b.DependsOnId))}. Use --force to override.");
        }

        var now = DateTime.UtcNow;
        using var tx = _db.BeginTransaction();
        _db.Execute($"""
            UPDATE {_db.T("issues")} SET
                status = 'closed',
                closed_at = @now,
                close_reason = @reason,
                closed_by_session = @session,
                updated_at = @now
            WHERE id = @id
            """, cmd =>
        {
            cmd.Parameters.AddWithValue("@now", now.ToString("o"));
            cmd.Parameters.AddWithValue("@reason", (object?)options.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@session", (object?)options.Session ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", resolved);
        });

        MarkDirty(resolved);
        _events.Record(resolved, "closed", existing.Status, "closed");
        tx.Commit();

        return GetOrThrow(resolved);
    }

    public Issue Reopen(string id)
    {
        var resolved = ResolveId(id);
        var existing = GetOrThrow(resolved);

        if (!existing.IsTerminal)
            throw new BeadsValidationException($"Issue is not closed: {id}");
        if (existing.IsTombstone)
            throw new BeadsValidationException($"Cannot reopen tombstone: {id}");

        using var tx = _db.BeginTransaction();
        _db.Execute($"""
            UPDATE {_db.T("issues")} SET
                status = 'open',
                closed_at = NULL,
                close_reason = NULL,
                closed_by_session = NULL,
                updated_at = @now
            WHERE id = @id
            """, cmd =>
        {
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", resolved);
        });

        MarkDirty(resolved);
        _events.Record(resolved, "reopened", "closed", "open");
        tx.Commit();

        return GetOrThrow(resolved);
    }

    public void Delete(string id, DeleteOptions? options = null)
    {
        options ??= new DeleteOptions();
        var resolved = ResolveId(id);
        var existing = GetOrThrow(resolved);

        if (existing.IsTombstone && !options.Force)
            throw new BeadsValidationException($"Issue already deleted: {id}");

        var now = DateTime.UtcNow;
        using var tx = _db.BeginTransaction();
        _db.Execute($"""
            UPDATE {_db.T("issues")} SET
                status = 'tombstone',
                deleted_at = @now,
                deleted_by = @by,
                delete_reason = @reason,
                original_type = @otype,
                updated_at = @now
            WHERE id = @id
            """, cmd =>
        {
            cmd.Parameters.AddWithValue("@now", now.ToString("o"));
            cmd.Parameters.AddWithValue("@by", _config.Actor);
            cmd.Parameters.AddWithValue("@reason", (object?)options.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@otype", existing.IssueType);
            cmd.Parameters.AddWithValue("@id", resolved);
        });

        MarkDirty(resolved);
        _events.Record(resolved, "deleted");
        tx.Commit();
    }

    public IssueListResult Ready(IssueFilter? filter = null)
    {
        filter ??= new IssueFilter();
        filter = filter with
        {
            Statuses = ["open"],
            IncludeClosed = false,
            IncludeDeferred = false,
        };

        var result = List(filter);

        // Filter out blocked issues
        var blockedIds = new HashSet<string>(
            _db.Query(
                $"SELECT DISTINCT issue_id FROM {_db.T("dependencies")} d " +
                $"INNER JOIN {_db.T("issues")} i ON i.id = d.depends_on_id " +
                "WHERE (d.type = 'blocks' OR d.type = 'parent-child' OR d.type = 'conditional-blocks' OR d.type = 'waits-for') " +
                "AND i.status NOT IN ('closed', 'tombstone')",
                null,
                r => r.GetString(0)));

        var readyIssues = result.Issues.Where(i =>
            !blockedIds.Contains(i.Id) &&
            (i.DeferUntil == null || i.DeferUntil <= DateTime.UtcNow)).ToList();

        return result with { Issues = readyIssues, Total = readyIssues.Count };
    }

    public IssueListResult Blocked(IssueFilter? filter = null)
    {
        filter ??= new IssueFilter();
        var t = _db.T("issues");
        var d = _db.T("dependencies");

        var issues = _db.Query(
            $"SELECT DISTINCT i.* FROM {t} i " +
            $"INNER JOIN {d} dep ON dep.issue_id = i.id " +
            $"INNER JOIN {t} blocker ON blocker.id = dep.depends_on_id " +
            "WHERE (dep.type IN ('blocks','parent-child','conditional-blocks','waits-for')) " +
            "AND blocker.status NOT IN ('closed','tombstone') " +
            "AND i.status NOT IN ('closed','tombstone')",
            null, ReadIssue);

        return new IssueListResult
        {
            Issues = issues,
            Total = issues.Count,
            Limit = filter.Limit,
            Offset = filter.Offset,
        };
    }

    public IssueListResult Search(string query, IssueFilter? filter = null)
    {
        filter ??= new IssueFilter();
        var t = _db.T("issues");
        var (where, paramSetter) = BuildWhere(filter);
        var searchClause = where.Length > 0
            ? $"{where} AND (title LIKE @q OR description LIKE @q)"
            : " WHERE (title LIKE @q OR description LIKE @q)";

        var pattern = $"%{query}%";
        var prevSetter = paramSetter;
        Action<SqliteCommand> fullSetter = cmd =>
        {
            prevSetter?.Invoke(cmd);
            cmd.Parameters.AddWithValue("@q", pattern);
        };

        var limit = filter.Limit > 0 ? filter.Limit : int.MaxValue;
        var countSql = $"SELECT COUNT(*) FROM {t}{searchClause}";
        var total = (int)(_db.QueryScalar<long>(countSql, fullSetter) ?? 0);

        var sql = $"SELECT * FROM {t}{searchClause} ORDER BY updated_at DESC LIMIT {limit} OFFSET {filter.Offset}";
        var issues = _db.Query(sql, fullSetter, ReadIssue);

        return new IssueListResult
        {
            Issues = issues,
            Total = total,
            Limit = filter.Limit,
            Offset = filter.Offset,
            HasMore = filter.Offset + issues.Count < total,
        };
    }

    public IssueListResult Stale(int days = 14)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var t = _db.T("issues");
        var issues = _db.Query(
            $"SELECT * FROM {t} WHERE updated_at < @cutoff AND status NOT IN ('closed','tombstone') ORDER BY updated_at ASC",
            cmd => cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("o")),
            ReadIssue);

        return new IssueListResult { Issues = issues, Total = issues.Count };
    }

    public CountResult Count(string? groupBy = null)
    {
        var t = _db.T("issues");
        if (groupBy == null)
        {
            var countAll = (int)(_db.QueryScalar<long>($"SELECT COUNT(*) FROM {t} WHERE status != 'tombstone'") ?? 0);
            return new CountResult { Total = countAll, Groups = new Dictionary<string, int> { ["all"] = countAll } };
        }

        var col = groupBy switch
        {
            "status" => "status",
            "type" => "issue_type",
            "priority" => "priority",
            "assignee" => "COALESCE(assignee, 'unassigned')",
            _ => throw new BeadsValidationException($"Invalid groupBy: {groupBy}. Use: status, type, priority, assignee"),
        };

        var groups = new Dictionary<string, int>();
        var groupTotal = 0;
        _db.Query(
            $"SELECT {col} as grp, COUNT(*) as cnt FROM {t} WHERE status != 'tombstone' GROUP BY {col} ORDER BY cnt DESC",
            null,
            r =>
            {
                var key = r.GetString(0);
                var cnt = r.GetInt32(1);
                groups[key] = cnt;
                groupTotal += cnt;
                return key;
            });

        return new CountResult { Total = groupTotal, Groups = groups };
    }

    public List<Issue> Orphans()
    {
        var t = _db.T("issues");
        var d = _db.T("dependencies");
        return _db.Query(
            $"SELECT i.* FROM {t} i " +
            $"INNER JOIN {d} dep ON dep.issue_id = i.id AND dep.type = 'parent-child' " +
            $"LEFT JOIN {t} parent ON parent.id = dep.depends_on_id " +
            "WHERE parent.id IS NULL OR parent.status = 'tombstone'",
            null, ReadIssue);
    }

    public void Defer(string id, DateTime? until = null)
    {
        var resolved = ResolveId(id);
        GetOrThrow(resolved);
        _db.Execute(
            $"UPDATE {_db.T("issues")} SET status = 'deferred', defer_until = @until, updated_at = @now WHERE id = @id",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@until", until.HasValue ? until.Value.ToString("o") : DBNull.Value);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@id", resolved);
            });
        MarkDirty(resolved);
    }

    public void Undefer(string id)
    {
        var resolved = ResolveId(id);
        GetOrThrow(resolved);
        _db.Execute(
            $"UPDATE {_db.T("issues")} SET status = 'open', defer_until = NULL, updated_at = @now WHERE id = @id",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@id", resolved);
            });
        MarkDirty(resolved);
    }

    public List<Issue> GetSubtasks(string parentId)
    {
        var resolved = ResolveId(parentId);
        var d = _db.T("dependencies");
        var t = _db.T("issues");
        return _db.Query(
            $"SELECT i.* FROM {t} i " +
            $"INNER JOIN {d} dep ON dep.issue_id = i.id AND dep.depends_on_id = @pid AND dep.type = 'parent-child'",
            cmd => cmd.Parameters.AddWithValue("@pid", resolved),
            ReadIssue);
    }

    // ── Internal helpers ──

    internal string ResolveId(string idOrPrefix)
    {
        var t = _db.T("issues");
        if (idOrPrefix.Contains('-'))
        {
            var exact = _db.QueryScalarString($"SELECT id FROM {t} WHERE id = '{idOrPrefix}'");
            if (exact != null) return exact;
        }

        var matches = _db.Query(
            $"SELECT id FROM {t} WHERE id LIKE @pat",
            cmd => cmd.Parameters.AddWithValue("@pat", $"{idOrPrefix}%"),
            r => r.GetString(0));

        return matches.Count switch
        {
            0 => throw new BeadsNotFoundException($"Issue not found: {idOrPrefix}"),
            1 => matches[0],
            _ => throw new BeadsAmbiguousIdException(
                $"Ambiguous ID '{idOrPrefix}' matches: {string.Join(", ", matches.Take(5))}",
                matches),
        };
    }

    internal void MarkDirty(string issueId)
    {
        _db.Execute(
            $"INSERT OR REPLACE INTO {_db.T("dirty_issues")} (issue_id, marked_at) VALUES (@id, @now)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            });
    }

    private void InsertIssue(Issue issue)
    {
        _db.Execute($"""
            INSERT INTO {_db.T("issues")} (
                id, content_hash, title, description, design, acceptance_criteria, notes,
                status, priority, issue_type, assignee, owner, estimated_minutes,
                created_at, created_by, updated_at, due_at, defer_until,
                external_ref, source_repo, ephemeral, pinned, is_template, project_id, column_id, position, metadata
            ) VALUES (
                @id, @hash, @title, @desc, @design, @ac, @notes,
                @status, @pri, @type, @assignee, @owner, @est,
                @cat, @cby, @uat, @due, @defer,
                @eref, @repo, @eph, @pin, @tmpl, @proj, @col, @pos, @meta
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
            cmd.Parameters.AddWithValue("@due", issue.DueAt.HasValue ? issue.DueAt.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@defer", issue.DeferUntil.HasValue ? issue.DeferUntil.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@eref", (object?)issue.ExternalRef ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@repo", issue.SourceRepo);
            cmd.Parameters.AddWithValue("@eph", issue.Ephemeral ? 1 : 0);
            cmd.Parameters.AddWithValue("@pin", issue.Pinned ? 1 : 0);
            cmd.Parameters.AddWithValue("@tmpl", issue.IsTemplate ? 1 : 0);
            cmd.Parameters.AddWithValue("@proj", (object?)issue.ProjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@col", (object?)issue.ColumnId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pos", issue.Position);
            cmd.Parameters.AddWithValue("@meta", issue.Metadata ?? "{}");
        });
    }

    private void InsertLabel(string issueId, string label)
    {
        _db.Execute(
            $"INSERT OR IGNORE INTO {_db.T("labels")} (issue_id, label) VALUES (@id, @lbl)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@lbl", label);
            });
    }

    private void RemoveLabel(string issueId, string label)
    {
        _db.Execute(
            $"DELETE FROM {_db.T("labels")} WHERE issue_id = @id AND label = @lbl",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@lbl", label);
            });
    }

    private void InsertDependency(string issueId, string dependsOnId, string type)
    {
        _db.Execute(
            $"INSERT OR IGNORE INTO {_db.T("dependencies")} (issue_id, depends_on_id, type, created_at, created_by) VALUES (@id, @dep, @type, @now, @by)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@dep", dependsOnId);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@by", _config.Actor);
            });
    }

    private List<Dependency> GetBlockingDependencies(string issueId)
    {
        var d = _db.T("dependencies");
        var t = _db.T("issues");
        return _db.Query(
            $"SELECT dep.* FROM {d} dep " +
            $"INNER JOIN {t} i ON i.id = dep.depends_on_id " +
            "WHERE dep.issue_id = @id AND (dep.type IN ('blocks','parent-child','conditional-blocks','waits-for')) " +
            "AND i.status NOT IN ('closed','tombstone')",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            ReadDependency);
    }

    private List<string> GetLabels(string issueId)
    {
        return _db.Query(
            $"SELECT label FROM {_db.T("labels")} WHERE issue_id = @id ORDER BY label",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            r => r.GetString(0));
    }

    private List<Dependency> GetDependencies(string issueId)
    {
        return _db.Query(
            $"SELECT * FROM {_db.T("dependencies")} WHERE issue_id = @id OR depends_on_id = @id",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            ReadDependency);
    }

    private List<Comment> GetComments(string issueId)
    {
        return _db.Query(
            $"SELECT * FROM {_db.T("comments")} WHERE issue_id = @id ORDER BY created_at",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            ReadComment);
    }

    private (string where, Action<SqliteCommand>? setter) BuildWhere(IssueFilter filter)
    {
        var clauses = new List<string>();
        Action<SqliteCommand>? setter = null;

        void AddParam(string name, object value)
        {
            var prev = setter;
            setter = cmd => { prev?.Invoke(cmd); cmd.Parameters.AddWithValue(name, value); };
        }

        if (!filter.IncludeClosed)
            clauses.Add("status NOT IN ('closed','tombstone')");

        if (filter.Statuses is { Count: > 0 })
        {
            var placeholders = filter.Statuses.Select((s, i) => $"@s{i}").ToList();
            clauses.Add($"status IN ({string.Join(",", placeholders)})");
            for (int i = 0; i < filter.Statuses.Count; i++)
                AddParam($"@s{i}", filter.Statuses[i]);
        }

        if (filter.Types is { Count: > 0 })
        {
            var placeholders = filter.Types.Select((t, i) => $"@t{i}").ToList();
            clauses.Add($"issue_type IN ({string.Join(",", placeholders)})");
            for (int i = 0; i < filter.Types.Count; i++)
                AddParam($"@t{i}", filter.Types[i]);
        }

        if (filter.Priorities is { Count: > 0 })
        {
            var placeholders = filter.Priorities.Select((p, i) => $"@p{i}").ToList();
            clauses.Add($"priority IN ({string.Join(",", placeholders)})");
            for (int i = 0; i < filter.Priorities.Count; i++)
                AddParam($"@p{i}", filter.Priorities[i]);
        }

        if (filter.PriorityMin.HasValue) { clauses.Add("priority >= @pmin"); AddParam("@pmin", filter.PriorityMin.Value); }
        if (filter.PriorityMax.HasValue) { clauses.Add("priority <= @pmax"); AddParam("@pmax", filter.PriorityMax.Value); }

        if (filter.Assignee != null) { clauses.Add("assignee = @assignee"); AddParam("@assignee", filter.Assignee); }
        if (filter.Unassigned == true) clauses.Add("assignee IS NULL");
        if (filter.ProjectId != null) { clauses.Add("project_id = @projid"); AddParam("@projid", filter.ProjectId); }
        if (filter.TitleContains != null) { clauses.Add("title LIKE @tc"); AddParam("@tc", $"%{filter.TitleContains}%"); }
        if (filter.DescContains != null) { clauses.Add("description LIKE @dc"); AddParam("@dc", $"%{filter.DescContains}%"); }
        if (filter.Overdue == true) clauses.Add("due_at IS NOT NULL AND due_at < datetime('now')");
        if (!filter.IncludeDeferred) clauses.Add("(defer_until IS NULL OR defer_until <= datetime('now'))");
        if (filter.ExcludeTemplates != false) clauses.Add("(is_template = 0 OR is_template IS NULL)");

        if (filter.Labels is { Count: > 0 })
        {
            // AND logic: issue must have ALL labels
            var lt = _db.T("labels");
            for (int i = 0; i < filter.Labels.Count; i++)
            {
                clauses.Add($"id IN (SELECT issue_id FROM {lt} WHERE label = @lbl{i})");
                AddParam($"@lbl{i}", filter.Labels[i]);
            }
        }

        if (filter.LabelsAny is { Count: > 0 })
        {
            var lt = _db.T("labels");
            var placeholders = filter.LabelsAny.Select((l, i) => $"@la{i}").ToList();
            clauses.Add($"id IN (SELECT issue_id FROM {lt} WHERE label IN ({string.Join(",", placeholders)}))");
            for (int i = 0; i < filter.LabelsAny.Count; i++)
                AddParam($"@la{i}", filter.LabelsAny[i]);
        }

        if (filter.Ids is { Count: > 0 })
        {
            var placeholders = filter.Ids.Select((_, i) => $"@fid{i}").ToList();
            clauses.Add($"id IN ({string.Join(",", placeholders)})");
            for (int i = 0; i < filter.Ids.Count; i++)
                AddParam($"@fid{i}", filter.Ids[i]);
        }

        var where = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : "";
        return (where, setter);
    }

    private static string BuildOrderBy(IssueFilter filter)
    {
        var col = filter.SortBy switch
        {
            "priority" => "priority",
            "created_at" or "created" => "created_at",
            "updated_at" or "updated" => "updated_at",
            "title" => "title",
            "due" or "due_at" => "due_at",
            _ => "priority ASC, created_at",
        };
        var dir = filter.Reverse ? "DESC" : "ASC";
        if (filter.SortBy == null) return $" ORDER BY priority ASC, created_at DESC";
        return $" ORDER BY {col} {dir}";
    }

    internal static Issue ReadIssue(SqliteDataReader r)
    {
        return new Issue
        {
            Id = r.GetString(r.GetOrdinal("id")),
            ContentHash = r.GetNullableString("content_hash"),
            Title = r.GetString(r.GetOrdinal("title")),
            Description = r.GetStringOrEmpty("description"),
            Design = r.GetStringOrEmpty("design"),
            AcceptanceCriteria = r.GetStringOrEmpty("acceptance_criteria"),
            Notes = r.GetStringOrEmpty("notes"),
            Status = r.GetStringOrEmpty("status"),
            Priority = r.GetInt32(r.GetOrdinal("priority")),
            IssueType = r.GetStringOrEmpty("issue_type"),
            Assignee = r.GetNullableString("assignee"),
            Owner = r.GetNullableString("owner"),
            EstimatedMinutes = r.GetNullableInt("estimated_minutes"),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
            CreatedBy = r.GetNullableString("created_by"),
            UpdatedAt = r.GetDateTime(r.GetOrdinal("updated_at")),
            ClosedAt = r.GetNullableDateTime("closed_at"),
            CloseReason = r.GetNullableString("close_reason"),
            ClosedBySession = r.GetNullableString("closed_by_session"),
            DueAt = r.GetNullableDateTime("due_at"),
            DeferUntil = r.GetNullableDateTime("defer_until"),
            ExternalRef = r.GetNullableString("external_ref"),
            SourceSystem = r.GetNullableString("source_system"),
            SourceRepo = r.GetStringOrEmpty("source_repo"),
            DeletedAt = r.GetNullableDateTime("deleted_at"),
            DeletedBy = r.GetNullableString("deleted_by"),
            DeleteReason = r.GetNullableString("delete_reason"),
            OriginalType = r.GetNullableString("original_type"),
            CompactionLevel = r.GetInt32(r.GetOrdinal("compaction_level")),
            CompactedAt = r.GetNullableDateTime("compacted_at"),
            CompactedAtCommit = r.GetNullableString("compacted_at_commit"),
            OriginalSize = r.GetNullableInt("original_size"),
            Sender = r.GetNullableString("sender"),
            Ephemeral = r.GetBoolFromInt("ephemeral"),
            Pinned = r.GetBoolFromInt("pinned"),
            IsTemplate = r.GetBoolFromInt("is_template"),
            ProjectId = r.GetNullableString("project_id"),
            ColumnId = r.GetNullableString("column_id"),
            Position = r.GetInt32(r.GetOrdinal("position")),
            Metadata = r.GetNullableString("metadata") ?? "{}",
        };
    }

    internal static Dependency ReadDependency(SqliteDataReader r)
    {
        return new Dependency
        {
            IssueId = r.GetString(r.GetOrdinal("issue_id")),
            DependsOnId = r.GetString(r.GetOrdinal("depends_on_id")),
            DepType = r.GetStringOrEmpty("type"),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
            CreatedBy = r.GetNullableString("created_by") ?? "",
            Metadata = r.GetNullableString("metadata"),
            ThreadId = r.GetNullableString("thread_id"),
        };
    }

    internal static Comment ReadComment(SqliteDataReader r)
    {
        return new Comment
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            IssueId = r.GetString(r.GetOrdinal("issue_id")),
            Author = r.GetStringOrEmpty("author"),
            Body = r.GetStringOrEmpty("text"),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        };
    }
}
