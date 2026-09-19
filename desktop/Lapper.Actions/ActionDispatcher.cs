namespace Lapper.Actions;

/// <summary>
/// Executes LOCAL actions only, re-validating against the allowlist. It
/// never executes shell commands, never navigates URLs, never touches
/// files — by construction there is no code path for any of that.
/// </summary>
public sealed class ActionDispatcher(SpeechService speech)
{
    /// <summary>
    /// Runs a local action against already-on-screen text. Returns false
    /// for anything not allowlisted as local.
    /// Clipboard actions require the caller to be on the UI thread.
    /// </summary>
    public async Task<bool> ExecuteLocalAsync(string actionType, string text)
    {
        if (ActionPolicy.KindOf(actionType) != ActionExecutionKind.Local)
        {
            return false;
        }

        switch (actionType)
        {
            case "copy_text":
            case "share_text": // MVP: share via clipboard (documented; no Share sheet)
                return ClipboardService.TrySetText(text);
            case "read_aloud":
                await speech.SpeakAsync(text);
                return true;
            default:
                return false;
        }
    }

    public void StopSpeech() => speech.Stop();
}
