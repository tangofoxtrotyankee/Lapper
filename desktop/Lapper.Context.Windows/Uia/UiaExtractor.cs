using System.Diagnostics;
using System.Runtime.InteropServices;
using Lapper.Context.Windows.Blocks;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Lapper.Context.Windows.Uia;

public enum UiaOutcome
{
    Success,
    SensitiveBlocked,
    NothingExtracted,
}

public sealed record UiaExtraction(
    UiaOutcome Outcome,
    string? SelectedText,
    IReadOnlyList<RawBlock> Blocks,
    bool TruncatedByBudget,
    int PasswordControlsSkipped,
    bool RetriedForAccessibilityWarmup,
    bool PasswordCheckCompleted)
{
    public override string ToString() =>
        $"UiaExtraction({Outcome}, blocks={Blocks.Count}, truncated={TruncatedByBudget})";
}

/// <summary>
/// UI Automation extraction. Runs entirely on the capture thread; only
/// plain records leave. The password gate runs BEFORE any text is read.
/// </summary>
internal sealed class UiaExtractor(UiaClient client)
{
    private const int MaxRawBlocks = 200;
    private const int MaxElementTextChars = 2000;
    private const int MaxDocumentTextChars = 8000;
    private const int MaxSelectedTextChars = 4096;
    private const int SoftBudgetMs = 350;

    public UiaExtraction Extract(nint hwndRaw, bool isBrowser)
    {
        var stopwatch = Stopwatch.StartNew();
        HWND hwnd;
        unsafe
        {
            hwnd = new HWND((void*)hwndRaw);
        }

        var root = client.Automation.ElementFromHandleBuildCache(hwnd, client.BlockCache);

        // Password gate FIRST: a focused password control blocks the entire
        // capture before any text has been read. The FocusCache omits the
        // Value property so a focused password's content is never fetched.
        // If the check cannot complete, the pipeline treats password status
        // as UNKNOWN and must not fall back to pixels (fail closed).
        var focusedIsPassword = false;
        var passwordCheckCompleted = false;
        try
        {
            var focused = client.Automation.GetFocusedElementBuildCache(client.FocusCache);
            focusedIsPassword = focused.CachedIsPassword;
            passwordCheckCompleted = true;
        }
        catch (COMException)
        {
            // Could not determine focus state — leave passwordCheckCompleted false.
        }
        if (focusedIsPassword)
        {
            return new UiaExtraction(UiaOutcome.SensitiveBlocked, null, [], false, 1, false, true);
        }

        var selectedText = TryReadSelection(root);
        var (blocks, truncated, passwordsSkipped) = WalkBlocks(root, isBrowser, stopwatch);

        var retried = false;
        if (isBrowser && blocks.Count < 3 && stopwatch.ElapsedMilliseconds < SoftBudgetMs)
        {
            // Chromium builds its accessibility tree lazily on first query.
            Thread.Sleep(150);
            (blocks, truncated, passwordsSkipped) = WalkBlocks(root, isBrowser, stopwatch);
            retried = true;
        }

        var outcome = blocks.Count == 0 && selectedText is null
            ? UiaOutcome.NothingExtracted
            : UiaOutcome.Success;
        return new UiaExtraction(
            outcome, selectedText, blocks, truncated, passwordsSkipped, retried, passwordCheckCompleted);
    }

    private string? TryReadSelection(IUIAutomationElement root)
    {
        try
        {
            var focused = client.Automation.GetFocusedElement();
            var text = ReadSelectionFromElement(focused);
            if (text is not null)
            {
                return text;
            }
        }
        catch (COMException)
        {
        }

        try
        {
            return ReadSelectionFromElement(root);
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? ReadSelectionFromElement(IUIAutomationElement element)
    {
        var patternObj = TryGetPattern(element, UIA_PATTERN_ID.UIA_TextPattern2Id)
            ?? TryGetPattern(element, UIA_PATTERN_ID.UIA_TextPatternId);
        if (patternObj is not IUIAutomationTextPattern textPattern)
        {
            return null;
        }

        var selection = textPattern.GetSelection();
        if (selection.Length == 0)
        {
            return null;
        }
        var parts = new List<string>();
        var total = 0;
        for (var i = 0; i < selection.Length && total < MaxSelectedTextChars; i++)
        {
            var text = TakeBstr(selection.GetElement(i).GetText(MaxSelectedTextChars - total));
            if (text.Length > 0)
            {
                parts.Add(text);
                total += text.Length;
            }
        }
        var joined = string.Join('\n', parts).Trim();
        return joined.Length > 0 ? joined : null;
    }

    private (List<RawBlock> Blocks, bool Truncated, int PasswordsSkipped) WalkBlocks(
        IUIAutomationElement root,
        bool isBrowser,
        Stopwatch stopwatch)
    {
        var blocks = new List<RawBlock>();
        var truncated = false;
        var passwordsSkipped = 0;
        var treeOrder = 0;

        IUIAutomationElement scope = root;
        IUIAutomationElement? documentElement = null;
        try
        {
            documentElement = root.FindFirstBuildCache(
                TreeScope.TreeScope_Descendants,
                client.Automation.CreatePropertyCondition(
                    UIA_PROPERTY_ID.UIA_ControlTypePropertyId,
                    (int)UIA_CONTROLTYPE_ID.UIA_DocumentControlTypeId),
                client.BlockCache);
        }
        catch (COMException)
        {
        }
        if (documentElement is not null)
        {
            scope = documentElement;
            var documentText = TryReadDocumentText(documentElement);
            if (documentText is not null)
            {
                blocks.Add(new RawBlock(
                    ContextBlockRoles.Document,
                    documentText,
                    documentElement.CachedHasKeyboardFocus,
                    treeOrder++));
            }
        }

        IUIAutomationElementArray found;
        try
        {
            found = scope.FindAllBuildCache(
                TreeScope.TreeScope_Subtree,
                client.InterestingCondition,
                client.BlockCache);
        }
        catch (COMException)
        {
            return (blocks, truncated, passwordsSkipped);
        }

        var count = found.Length;
        for (var i = 0; i < count; i++)
        {
            if (blocks.Count >= MaxRawBlocks)
            {
                truncated = true;
                break;
            }
            if (i % 20 == 0 && stopwatch.ElapsedMilliseconds > SoftBudgetMs)
            {
                truncated = true;
                break;
            }

            var element = found.GetElement(i);
            if (element.CachedIsPassword)
            {
                passwordsSkipped++;
                continue;
            }

            var controlType = element.CachedControlType;
            var role = MapRole(controlType);
            if (role is null)
            {
                continue;
            }

            var text = TakeBstr(element.CachedName);
            if (controlType == UIA_CONTROLTYPE_ID.UIA_EditControlTypeId)
            {
                // Browser omnibox and other top-chrome edits never enter
                // context: for browsers only in-document edits are allowed,
                // and the document subtree is our scope only when found.
                if (isBrowser && documentElement is null)
                {
                    continue;
                }
                var value = TryGetCachedValueString(element);
                if (value is not null && value.Length > text.Length)
                {
                    text = value;
                }
            }

            if (text.Length == 0)
            {
                continue;
            }
            if (text.Length > MaxElementTextChars)
            {
                text = text[..MaxElementTextChars];
            }

            blocks.Add(new RawBlock(role, text, element.CachedHasKeyboardFocus, treeOrder++));
        }

        return (blocks, truncated, passwordsSkipped);
    }

    private static string? TryReadDocumentText(IUIAutomationElement documentElement)
    {
        try
        {
            if (TryGetPattern(documentElement, UIA_PATTERN_ID.UIA_TextPatternId)
                is not IUIAutomationTextPattern pattern)
            {
                return null;
            }

            // Visible ranges first (Word: whole-document reads are slow).
            var parts = new List<string>();
            var total = 0;
            try
            {
                var visible = pattern.GetVisibleRanges();
                for (var i = 0; i < visible.Length && total < MaxDocumentTextChars; i++)
                {
                    var text = TakeBstr(visible.GetElement(i).GetText(MaxDocumentTextChars - total));
                    if (text.Trim().Length > 0)
                    {
                        parts.Add(text);
                        total += text.Length;
                    }
                }
            }
            catch (COMException)
            {
            }

            if (total == 0)
            {
                var text = TakeBstr(pattern.DocumentRange.GetText(MaxDocumentTextChars));
                if (text.Trim().Length > 0)
                {
                    parts.Add(text);
                }
            }

            var joined = string.Join('\n', parts).Trim();
            return joined.Length > 0 ? joined : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? TryGetCachedValueString(IUIAutomationElement element)
    {
        try
        {
            return element.GetCachedPropertyValue(UIA_PROPERTY_ID.UIA_ValueValuePropertyId) as string;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static object? TryGetPattern(IUIAutomationElement element, UIA_PATTERN_ID patternId)
    {
        try
        {
            return element.GetCurrentPattern(patternId);
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? MapRole(UIA_CONTROLTYPE_ID controlType) => controlType switch
    {
        UIA_CONTROLTYPE_ID.UIA_DocumentControlTypeId => null, // handled via TextPattern above
        UIA_CONTROLTYPE_ID.UIA_EditControlTypeId => ContextBlockRoles.Edit,
        UIA_CONTROLTYPE_ID.UIA_TextControlTypeId => ContextBlockRoles.Text,
        UIA_CONTROLTYPE_ID.UIA_HeaderControlTypeId => ContextBlockRoles.Heading,
        UIA_CONTROLTYPE_ID.UIA_HeaderItemControlTypeId => ContextBlockRoles.Heading,
        UIA_CONTROLTYPE_ID.UIA_ListItemControlTypeId => ContextBlockRoles.ListItem,
        UIA_CONTROLTYPE_ID.UIA_HyperlinkControlTypeId => ContextBlockRoles.Link,
        UIA_CONTROLTYPE_ID.UIA_DataItemControlTypeId => ContextBlockRoles.Cell,
        UIA_CONTROLTYPE_ID.UIA_TableControlTypeId => ContextBlockRoles.Table,
        UIA_CONTROLTYPE_ID.UIA_TabItemControlTypeId => ContextBlockRoles.Tab,
        UIA_CONTROLTYPE_ID.UIA_MenuItemControlTypeId => ContextBlockRoles.MenuItem,
        UIA_CONTROLTYPE_ID.UIA_ButtonControlTypeId => ContextBlockRoles.Button,
        UIA_CONTROLTYPE_ID.UIA_CheckBoxControlTypeId => ContextBlockRoles.Option,
        _ => null,
    };

    /// <summary>Converts a BSTR to string and frees it.</summary>
    private static unsafe string TakeBstr(BSTR bstr)
    {
        if (bstr.Value is null)
        {
            return string.Empty;
        }
        try
        {
            return new string(bstr.Value, 0, bstr.Length);
        }
        finally
        {
            Marshal.FreeBSTR((nint)bstr.Value);
        }
    }
}
