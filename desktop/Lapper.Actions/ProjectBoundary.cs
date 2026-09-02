namespace Lapper.Actions;

/// <summary>
/// Project boundary (docs/02-architecture.md): local action dispatcher with a
/// strict allowlist (copy_text, read_aloud, draft_text, extract_facts,
/// ask_question, share_text), confirmation policy and clipboard/TTS/share
/// implementations. Model output never executes OS commands from here.
/// </summary>
public static class ProjectBoundary
{
    public const string Name = "Lapper.Actions";
}
