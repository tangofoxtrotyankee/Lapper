namespace Lapper.Privacy.Exclusions;

/// <summary>
/// Applications Lapper never captures, regardless of user settings.
/// A starting list per the threat model; user exclusions extend it and can
/// never shrink it.
/// </summary>
public static class BuiltInExclusions
{
    /// <summary>Password managers and similar credential surfaces (lower-case exe names).</summary>
    public static readonly IReadOnlySet<string> SensitiveApps = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "keepass.exe",
        "keepassxc.exe",
        "1password.exe",
        "bitwarden.exe",
        "lastpass.exe",
        "dashlane.exe",
        "protonpass.exe",
        "enpass.exe",
        "roboform.exe",
    };

    /// <summary>Windows security surfaces (UAC, credential prompts, lock screen).</summary>
    public static readonly IReadOnlySet<string> SystemSurfaces = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "consent.exe",
        "credentialuibroker.exe",
        "lockapp.exe",
        "logonui.exe",
        "sechealthui.exe",
        "credprovhost.exe",
    };
}
