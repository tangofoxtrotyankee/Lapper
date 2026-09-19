using Lapper.Contracts;

namespace Lapper.Actions;

public enum ActionExecutionKind
{
    /// <summary>Runs entirely in-process (clipboard/TTS). Class A: local/read-only.</summary>
    Local,
    /// <summary>An AI text transformation via POST /v1/context/action — never an OS action.</summary>
    Cloud,
}

/// <summary>
/// The action allowlist (CLAUDE.md security rules). Anything not listed —
/// whatever the model proposes — is rejected. requiresConfirmation from the
/// model can only ADD friction, never remove it.
/// </summary>
public static class ActionPolicy
{
    private static readonly Dictionary<string, ActionExecutionKind> Allowlist = new(StringComparer.Ordinal)
    {
        ["copy_text"] = ActionExecutionKind.Local,
        ["read_aloud"] = ActionExecutionKind.Local,
        ["share_text"] = ActionExecutionKind.Local,
        ["draft_text"] = ActionExecutionKind.Cloud,
        ["extract_facts"] = ActionExecutionKind.Cloud,
        ["ask_question"] = ActionExecutionKind.Cloud,
    };

    public static bool IsAllowed(string actionType) => Allowlist.ContainsKey(actionType);

    public static ActionExecutionKind? KindOf(string actionType) =>
        Allowlist.TryGetValue(actionType, out var kind) ? kind : null;

    /// <summary>Filters a model-proposed action list down to allowlisted types.</summary>
    public static IReadOnlyList<SuggestedAction> FilterAllowed(IEnumerable<SuggestedAction> proposed) =>
        [.. proposed.Where(action => IsAllowed(action.Type))];

    /// <summary>MVP actions are all Class A; the model can still request extra friction.</summary>
    public static bool NeedsConfirmation(SuggestedAction action) => action.RequiresConfirmation;
}
