using Microsoft.Data.Sqlite;

namespace Beads.Net.Internal;

internal sealed class Db : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly string _prefix;
    private bool _disposed;

    public SqliteConnection Connection => _conn;
    public string Prefix => _prefix;

    public Db(string dbPath, string prefix)
    {
        _prefix = prefix;

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        SetPragmas();
    }

    public Db(SqliteConnection connection, string prefix)
    {
        _conn = connection;
        _prefix = prefix;
        if (_conn.State != System.Data.ConnectionState.Open)
            _conn.Open();
        SetPragmas();
    }

    private void SetPragmas()
    {
        Execute("PRAGMA journal_mode = WAL");
        Execute("PRAGMA foreign_keys = ON");
        Execute("PRAGMA synchronous = NORMAL");
        Execute("PRAGMA temp_store = MEMORY");
        Execute("PRAGMA cache_size = -8000");
    }

    public string T(string table) => $"{_prefix}{table}";

    public string Sql(string template) => template.Replace("{p}", _prefix);

    public int Execute(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteNonQuery();
    }

    public int Execute(string sql, Action<SqliteCommand> bindParams)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        bindParams(cmd);
        return cmd.ExecuteNonQuery();
    }

    public T? QueryScalar<T>(string sql) where T : struct
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        if (result is null or DBNull) return null;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    public T? QueryScalar<T>(string sql, Action<SqliteCommand>? bindParams) where T : struct
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        bindParams?.Invoke(cmd);
        var result = cmd.ExecuteScalar();
        if (result is null or DBNull) return null;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    public string? QueryScalarString(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        if (result is null or DBNull) return null;
        return result.ToString();
    }

    public string? QueryScalarString(string sql, Action<SqliteCommand>? bindParams)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        bindParams?.Invoke(cmd);
        var result = cmd.ExecuteScalar();
        if (result is null or DBNull) return null;
        return result.ToString();
    }

    public List<T> Query<T>(string sql, Func<SqliteDataReader, T> map)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var results = new List<T>();
        while (reader.Read())
            results.Add(map(reader));
        return results;
    }

    public List<T> Query<T>(string sql, Action<SqliteCommand>? bindParams, Func<SqliteDataReader, T> map)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        bindParams?.Invoke(cmd);
        using var reader = cmd.ExecuteReader();
        var results = new List<T>();
        while (reader.Read())
            results.Add(map(reader));
        return results;
    }

    public T? QuerySingle<T>(string sql, Action<SqliteCommand>? bindParams, Func<SqliteDataReader, T> map) where T : class
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        bindParams?.Invoke(cmd);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? map(reader) : null;
    }

    public SqliteTransaction BeginTransaction() => _conn.BeginTransaction();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _conn.Dispose();
    }
}

internal static class ReaderExtensions
{
    public static string GetStringOrEmpty(this SqliteDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? "" : r.GetString(ordinal);

    public static string? GetNullableString(this SqliteDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    public static int? GetNullableInt(this SqliteDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetInt32(ordinal);

    public static DateTime? GetNullableDateTime(this SqliteDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetDateTime(ordinal);

    public static bool GetBoolFromInt(this SqliteDataReader r, int ordinal) =>
        !r.IsDBNull(ordinal) && r.GetInt32(ordinal) != 0;

    // String-name overloads
    public static string GetStringOrEmpty(this SqliteDataReader r, string name) =>
        r.GetStringOrEmpty(r.GetOrdinal(name));

    public static string? GetNullableString(this SqliteDataReader r, string name) =>
        r.GetNullableString(r.GetOrdinal(name));

    public static int? GetNullableInt(this SqliteDataReader r, string name) =>
        r.GetNullableInt(r.GetOrdinal(name));

    public static DateTime? GetNullableDateTime(this SqliteDataReader r, string name) =>
        r.GetNullableDateTime(r.GetOrdinal(name));

    public static bool GetBoolFromInt(this SqliteDataReader r, string name) =>
        r.GetBoolFromInt(r.GetOrdinal(name));
}
