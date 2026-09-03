using Xunit;

namespace Lapper.Shell.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"lapper-settings-test-{Guid.NewGuid():N}.sqlite");

    [Fact]
    public void ReturnsDefaultsWhenEmpty()
    {
        using var store = new SettingsStore(_databasePath);
        Assert.Null(store.GetString(SettingsKeys.ShortcutGesture));
        Assert.Null(store.GetInt(SettingsKeys.PillX));
        Assert.True(store.GetBool(SettingsKeys.PillVisible, defaultValue: true));
        Assert.False(store.GetBool(SettingsKeys.PillVisible, defaultValue: false));
    }

    [Fact]
    public void RoundTripsTypedValues()
    {
        using var store = new SettingsStore(_databasePath);
        store.SetString(SettingsKeys.ShortcutGesture, "Ctrl+Alt+L");
        store.SetInt(SettingsKeys.PillX, -120);
        store.SetBool(SettingsKeys.PillVisible, false);

        Assert.Equal("Ctrl+Alt+L", store.GetString(SettingsKeys.ShortcutGesture));
        Assert.Equal(-120, store.GetInt(SettingsKeys.PillX));
        Assert.False(store.GetBool(SettingsKeys.PillVisible, defaultValue: true));
    }

    [Fact]
    public void OverwritesExistingValue()
    {
        using var store = new SettingsStore(_databasePath);
        store.SetInt(SettingsKeys.PillY, 10);
        store.SetInt(SettingsKeys.PillY, 20);
        Assert.Equal(20, store.GetInt(SettingsKeys.PillY));
    }

    [Fact]
    public void PersistsAcrossReopen()
    {
        using (var store = new SettingsStore(_databasePath))
        {
            store.SetString(SettingsKeys.ShortcutGesture, "Win+F5");
        }

        using var reopened = new SettingsStore(_databasePath);
        Assert.Equal("Win+F5", reopened.GetString(SettingsKeys.ShortcutGesture));
    }

    [Fact]
    public void RemoveDeletesKey()
    {
        using var store = new SettingsStore(_databasePath);
        store.SetInt(SettingsKeys.PillX, 42);
        store.Remove(SettingsKeys.PillX);
        Assert.Null(store.GetInt(SettingsKeys.PillX));
    }

    public void Dispose()
    {
        SqliteTestCleanup.Delete(_databasePath);
    }
}

internal static class SqliteTestCleanup
{
    public static void Delete(string path)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
