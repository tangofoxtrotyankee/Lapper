namespace Lapper.Privacy.Exclusions;

public enum ExclusionReason
{
    BuiltInSensitiveApp,
    SystemSecuritySurface,
    UserExcludedApp,
    UnknownProcess,
    SelfCapture,
}

/// <summary>
/// The decision record is metadata-safe: MatchedRule is a rule name, never
/// captured content.
/// </summary>
public sealed record ExclusionDecision(bool IsBlocked, ExclusionReason? Reason, string? MatchedRule)
{
    public static readonly ExclusionDecision Allowed = new(false, null, null);
}

public interface IUserExclusionSource
{
    /// <summary>Lower/upper case irrelevant; matched case-insensitively on exe name.</summary>
    IReadOnlyCollection<string> GetExcludedProcessNames();
}

public interface IExclusionPolicy
{
    ExclusionDecision Evaluate(WindowIdentity window);
}

/// <summary>
/// Evaluated BEFORE any capture (UIA, OCR or pixels). Fail-closed: a window
/// whose process cannot be identified is blocked.
/// </summary>
public sealed class ExclusionPolicy(IUserExclusionSource userExclusions) : IExclusionPolicy
{
    public ExclusionDecision Evaluate(WindowIdentity window)
    {
        if (window.ProcessImagePath is null || window.ProcessName.Length == 0)
        {
            return new ExclusionDecision(true, ExclusionReason.UnknownProcess, "unknown-process");
        }

        if (window.ProcessId == Environment.ProcessId)
        {
            return new ExclusionDecision(true, ExclusionReason.SelfCapture, "self-capture");
        }

        if (BuiltInExclusions.SensitiveApps.Contains(window.ProcessName))
        {
            return new ExclusionDecision(true, ExclusionReason.BuiltInSensitiveApp, "built-in-sensitive");
        }

        if (BuiltInExclusions.SystemSurfaces.Contains(window.ProcessName))
        {
            return new ExclusionDecision(true, ExclusionReason.SystemSecuritySurface, "system-surface");
        }

        foreach (var excluded in userExclusions.GetExcludedProcessNames())
        {
            if (string.Equals(NormalizeExeName(excluded), window.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return new ExclusionDecision(true, ExclusionReason.UserExcludedApp, "user-excluded");
            }
        }

        return ExclusionDecision.Allowed;
    }

    private static string NormalizeExeName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + ".exe";
    }
}
