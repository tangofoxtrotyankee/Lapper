namespace Lapper.Privacy.Exclusions;

/// <summary>
/// Identity of a foreground window as needed for exclusion decisions.
/// Local-only data: the title never leaves the machine through this type.
/// </summary>
public sealed record WindowIdentity(
    string? ProcessImagePath,
    string ProcessName,
    int ProcessId,
    string WindowTitle,
    string ClassName);
