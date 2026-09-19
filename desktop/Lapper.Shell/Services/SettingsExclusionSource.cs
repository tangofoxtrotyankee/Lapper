using Lapper.Privacy.Exclusions;

namespace Lapper.Shell.Services;

/// <summary>Backs the privacy exclusion policy with the user's settings.</summary>
public sealed class SettingsExclusionSource(SettingsService settings) : IUserExclusionSource
{
    public IReadOnlyCollection<string> GetExcludedProcessNames() => settings.UserExcludedApps;
}
