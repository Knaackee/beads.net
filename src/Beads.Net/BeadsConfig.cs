using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Beads.Net;

public sealed class BeadsConfig
{
    public string Db { get; set; } = ".beads/beads.db";
    public string Prefix { get; set; } = "beads_";
    public string IdPrefix { get; set; } = "bd";
    public string Actor { get; set; } = Environment.UserName;
    public DefaultsConfig Defaults { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public SyncConfig Sync { get; set; } = new();
}

public sealed class DefaultsConfig
{
    public int Priority { get; set; } = 2;
    public string Type { get; set; } = "task";
    public string? Assignee { get; set; }
}

public sealed class OutputConfig
{
    public bool Color { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd";
}

public sealed class SyncConfig
{
    public bool AutoImport { get; set; }
    public bool AutoFlush { get; set; }
}

public static class ConfigLoader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static BeadsConfig Load(string? explicitConfigPath = null, Action<BeadsConfig>? cliOverrides = null)
    {
        var config = new BeadsConfig();

        // Layer 4: .beads/config.yaml (lowest priority)
        MergeFromYaml(config, Path.Combine(".beads", "config.yaml"));

        // Layer 3: beads.yaml next to exe
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (exeDir != null)
            MergeFromYaml(config, Path.Combine(exeDir, "beads.yaml"));

        // Layer 2: explicit --config path
        if (explicitConfigPath != null)
        {
            if (!File.Exists(explicitConfigPath))
                throw new Errors.BeadsConfigException($"Config file not found: {explicitConfigPath}");
            MergeFromYaml(config, explicitConfigPath);
        }

        // Layer 1: CLI flags (highest priority)
        cliOverrides?.Invoke(config);

        return config;
    }

    private static void MergeFromYaml(BeadsConfig target, string path)
    {
        if (!File.Exists(path)) return;

        var yaml = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(yaml)) return;

        var source = YamlDeserializer.Deserialize<BeadsConfig>(yaml);
        if (source is null) return;

        if (source.Db != new BeadsConfig().Db) target.Db = source.Db;
        if (source.Prefix != "beads_") target.Prefix = source.Prefix;
        if (source.IdPrefix != "bd") target.IdPrefix = source.IdPrefix;
        if (source.Actor != Environment.UserName) target.Actor = source.Actor;
        if (source.Defaults.Priority != 2) target.Defaults.Priority = source.Defaults.Priority;
        if (source.Defaults.Type != "task") target.Defaults.Type = source.Defaults.Type;
        if (source.Defaults.Assignee != null) target.Defaults.Assignee = source.Defaults.Assignee;
        if (!source.Output.Color) target.Output.Color = false;
        if (source.Output.DateFormat != "yyyy-MM-dd") target.Output.DateFormat = source.Output.DateFormat;
        if (source.Sync.AutoImport) target.Sync.AutoImport = true;
        if (source.Sync.AutoFlush) target.Sync.AutoFlush = true;
    }
}
