using Windows.ApplicationModel.DataTransfer;

namespace Lapper.Actions;

/// <summary>
/// Clipboard copy. MUST be called on the UI thread (WinRT clipboard
/// requirement); Flush failures under contention are tolerated — the data
/// is still on the clipboard for as long as the app lives.
/// </summary>
public static class ClipboardService
{
    public static bool TrySetText(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            try
            {
                Clipboard.Flush();
            }
            catch
            {
                // CLIPBRD_E_CANT_OPEN under contention: non-fatal.
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
