using Lapper.Shell.Core;
using Windows.Storage;

namespace Lapper.Shell.Services;

/// <summary>
/// Typed access to persisted shell preferences, stored in SQLite in the
/// packaged app's local data folder (docs/02-architecture.md). Preferences
/// only — no credentials or captured content.
/// </summary>
public sealed class SettingsService : IDisposable
{
    private readonly SettingsStore _store;

    public SettingsService()
        : this(Path.Combine(ApplicationData.Current.LocalFolder.Path, "settings.sqlite"))
    {
    }

    public SettingsService(string databasePath)
    {
        _store = new SettingsStore(databasePath);
    }

    public bool PillVisible
    {
        get => _store.GetBool(SettingsKeys.PillVisible, defaultValue: true);
        set => _store.SetBool(SettingsKeys.PillVisible, value);
    }

    public PixelPoint? PillPosition
    {
        get
        {
            var x = _store.GetInt(SettingsKeys.PillX);
            var y = _store.GetInt(SettingsKeys.PillY);
            return x is not null && y is not null ? new PixelPoint(x.Value, y.Value) : null;
        }
        set
        {
            if (value is { } position)
            {
                _store.SetInt(SettingsKeys.PillX, position.X);
                _store.SetInt(SettingsKeys.PillY, position.Y);
            }
            else
            {
                _store.Remove(SettingsKeys.PillX);
                _store.Remove(SettingsKeys.PillY);
            }
        }
    }

    public ShortcutGesture Gesture
    {
        get => ShortcutGesture.TryParse(_store.GetString(SettingsKeys.ShortcutGesture), out var gesture)
            ? gesture!
            : ShortcutGesture.Default;
        set => _store.SetString(SettingsKeys.ShortcutGesture, value.Format());
    }

    public void Dispose() => _store.Dispose();
}
