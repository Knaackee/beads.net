namespace Beads.Net.Errors;

public class BeadsException : Exception
{
    public int ExitCode { get; }
    public string Kind { get; }
    public List<string>? RecoveryHints { get; }

    public BeadsException(string message, int exitCode = 1, string kind = "internal", List<string>? hints = null)
        : base(message)
    {
        ExitCode = exitCode;
        Kind = kind;
        RecoveryHints = hints;
    }
}

public class BeadsSchemaException(string message, List<string>? hints = null)
    : BeadsException(message, 2, "schema", hints);

public class BeadsNotFoundException(string message, List<string>? hints = null)
    : BeadsException(message, 3, "not_found", hints ?? ["Check the issue ID", "Use 'beads list' to find issues"]);

public class BeadsAmbiguousIdException(string message, List<string> candidates)
    : BeadsException(message, 3, "ambiguous_id", candidates.Select(c => $"Did you mean: {c}").ToList());

public class BeadsValidationException(string message, List<string>? hints = null)
    : BeadsException(message, 4, "validation", hints);

public class BeadsDuplicateException(string message, List<string>? hints = null)
    : BeadsException(message, 4, "duplicate", hints);

public class BeadsCyclicDependencyException(string message, List<string>? hints = null)
    : BeadsException(message, 5, "cyclic_dependency", hints);

public class BeadsSyncException(string message, List<string>? hints = null)
    : BeadsException(message, 6, "sync", hints);

public class BeadsConfigException(string message, List<string>? hints = null)
    : BeadsException(message, 7, "config", hints);

public class BeadsIOException(string message, List<string>? hints = null)
    : BeadsException(message, 8, "io", hints);
