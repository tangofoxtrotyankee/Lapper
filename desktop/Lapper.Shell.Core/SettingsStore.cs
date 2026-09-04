using Microsoft.Data.Sqlite;

namespace Lapper.Shell.Core;

/// <summary>
/// Local user settings persisted in SQLite (docs/02-architecture.md).
/// Stores preferences only — credentials and tokens must never be written
/// here; they belong in the Windows Credential Locker.
/// </summary>
public sealed class SettingsStore : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Lock _gate = new();

    public SettingsStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        _connection.Open();

        using var command = _connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL)";
        command.ExecuteNonQuery();
    }

    public string? GetString(string key)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT value FROM settings WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() as string;
        }
    }

    public void SetString(string key, string value)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                "INSERT INTO settings (key, value) VALUES ($key, $value) " +
                "ON CONFLICT(key) DO UPDATE SET value = $value";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
    }

    public bool GetBool(string key, bool defaultValue) =>
        GetString(key) switch
        {
            "true" => true,
            "false" => false,
            _ => defaultValue,
        };

    public void SetBool(string key, bool value) => SetString(key, value ? "true" : "false");

    public int? GetInt(string key) =>
        int.TryParse(GetString(key), out var value) ? value : null;

    public void SetInt(string key, int value) => SetString(key, value.ToString());

    public void Remove(string key)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM settings WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            command.ExecuteNonQuery();
        }
    }

    public void Dispose() => _connection.Dispose();
}
