using Beads.Net.Internal;
using Beads.Net.Models;
using Beads.Net.Schema;

namespace Beads.Net.Services;

public sealed class DoctorService
{
    private readonly Db _db;
    private readonly SchemaManager _schema;
    private readonly DependencyService _deps;

    internal DoctorService(Db db, SchemaManager schema, DependencyService deps)
    {
        _db = db;
        _schema = schema;
        _deps = deps;
    }

    public DoctorReport Run()
    {
        var warnings = new List<string>();

        var schemaVersion = _schema.GetSchemaVersion();
        var schemaOk = schemaVersion > 0;
        if (!schemaOk) warnings.Add("Schema version not found.");

        // Orphaned subtasks: children whose parent no longer exists or is tombstoned
        var t = _db.T("issues");
        var d = _db.T("dependencies");
        var orphanedSubtasks = _db.Query(
            $"SELECT dep.issue_id FROM {d} dep " +
            $"LEFT JOIN {t} parent ON parent.id = dep.depends_on_id " +
            "WHERE dep.type = 'parent-child' AND (parent.id IS NULL OR parent.status = 'tombstone')",
            null, r => r.GetString(0));
        if (orphanedSubtasks.Count > 0)
            warnings.Add($"Found {orphanedSubtasks.Count} orphaned subtask(s).");

        // Orphaned dependencies: deps pointing to non-existent issues
        var orphanedDeps = _db.Query(
            $"SELECT dep.depends_on_id FROM {d} dep " +
            $"LEFT JOIN {t} i ON i.id = dep.depends_on_id " +
            "WHERE i.id IS NULL",
            null, r => r.GetString(0));
        if (orphanedDeps.Count > 0)
            warnings.Add($"Found {orphanedDeps.Count} orphaned dependency target(s).");

        // Cycles
        var cycles = _deps.FindCycles();
        if (cycles.Count > 0)
            warnings.Add($"Found {cycles.Count} dependency cycle(s).");

        // Dirty count
        var dirtyCount = (int)(_db.QueryScalar<long>(
            $"SELECT COUNT(*) FROM {_db.T("dirty_issues")}") ?? 0);
        if (dirtyCount > 0)
            warnings.Add($"{dirtyCount} dirty issue(s) not yet flushed.");

        // Foreign key integrity
        var fkErrors = _db.Query("PRAGMA foreign_key_check", null, r =>
        {
            var table = r.GetString(0);
            var rowid = r.GetInt64(1);
            return $"{table}:{rowid}";
        });
        var fkOk = fkErrors.Count == 0;
        if (!fkOk)
            warnings.Add($"Foreign key integrity failed: {fkErrors.Count} violation(s).");

        var journalMode = _db.QueryScalarString("PRAGMA journal_mode") ?? "";

        return new DoctorReport
        {
            SchemaVersion = schemaVersion,
            SchemaOk = schemaOk,
            OrphanedSubtasks = orphanedSubtasks,
            OrphanedDependencies = orphanedDeps,
            DependencyCycles = cycles,
            DirtyCount = dirtyCount,
            ForeignKeyIntegrity = fkOk,
            JournalMode = journalMode,
            Warnings = warnings,
        };
    }
}
