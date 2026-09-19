using Windows.Win32.UI.Accessibility;

namespace Lapper.Context.Windows.Uia;

/// <summary>
/// Owns the UIA COM client. Created and used ONLY on the capture thread
/// (MTA); no COM reference ever leaves that thread.
/// </summary>
internal sealed class UiaClient
{
    public IUIAutomation Automation { get; }
    public IUIAutomationCacheRequest BlockCache { get; }

    /// <summary>
    /// Minimal cache for the focused-element password check: no Value
    /// property, so a focused password control's content is never fetched
    /// across the process boundary just to ask whether it is a password.
    /// </summary>
    public IUIAutomationCacheRequest FocusCache { get; }

    public IUIAutomationCondition InterestingCondition { get; }

    public UiaClient()
    {
        Automation = (IUIAutomation)new CUIAutomation8();
        if (Automation is IUIAutomation2 automation2)
        {
            // Bound hung providers: without these a busy target app can
            // block a UIA call indefinitely.
            automation2.ConnectionTimeout = 2000;
            automation2.TransactionTimeout = 1000;
        }

        BlockCache = Automation.CreateCacheRequest();
        BlockCache.TreeScope = TreeScope.TreeScope_Element;
        // Full mode is required: None-mode elements are cache-only and
        // cannot serve FindFirst/FindAll navigation or GetCurrentPattern,
        // which the extractor uses on the root/document elements.
        BlockCache.AutomationElementMode = AutomationElementMode.AutomationElementMode_Full;
        foreach (var property in new[]
        {
            UIA_PROPERTY_ID.UIA_NamePropertyId,
            UIA_PROPERTY_ID.UIA_ControlTypePropertyId,
            UIA_PROPERTY_ID.UIA_LocalizedControlTypePropertyId,
            UIA_PROPERTY_ID.UIA_IsPasswordPropertyId,
            UIA_PROPERTY_ID.UIA_IsOffscreenPropertyId,
            UIA_PROPERTY_ID.UIA_HasKeyboardFocusPropertyId,
            UIA_PROPERTY_ID.UIA_ValueValuePropertyId,
        })
        {
            BlockCache.AddProperty(property);
        }

        var interesting = new[]
        {
            UIA_CONTROLTYPE_ID.UIA_DocumentControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_EditControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_TextControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_HeaderControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_HeaderItemControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_ListItemControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_HyperlinkControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_DataItemControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_TabItemControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_MenuItemControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_ButtonControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_CheckBoxControlTypeId,
            UIA_CONTROLTYPE_ID.UIA_TableControlTypeId,
        };
        var anyInteresting = interesting
            .Select(controlType => Automation.CreatePropertyCondition(
                UIA_PROPERTY_ID.UIA_ControlTypePropertyId,
                (int)controlType))
            .Aggregate((left, right) => Automation.CreateOrCondition(left, right));
        var notOffscreen = Automation.CreatePropertyCondition(
            UIA_PROPERTY_ID.UIA_IsOffscreenPropertyId,
            false);
        // Password elements are excluded at the QUERY level so their Value
        // property is never bulk-fetched into our cache (read-time block,
        // not filter-time); the extractor's CachedIsPassword skip remains
        // as defense in depth.
        var notPassword = Automation.CreatePropertyCondition(
            UIA_PROPERTY_ID.UIA_IsPasswordPropertyId,
            false);
        InterestingCondition = Automation.CreateAndCondition(
            Automation.CreateAndCondition(anyInteresting, notOffscreen),
            notPassword);

        FocusCache = Automation.CreateCacheRequest();
        FocusCache.TreeScope = TreeScope.TreeScope_Element;
        FocusCache.AutomationElementMode = AutomationElementMode.AutomationElementMode_Full;
        FocusCache.AddProperty(UIA_PROPERTY_ID.UIA_IsPasswordPropertyId);
        FocusCache.AddProperty(UIA_PROPERTY_ID.UIA_ControlTypePropertyId);
    }
}
