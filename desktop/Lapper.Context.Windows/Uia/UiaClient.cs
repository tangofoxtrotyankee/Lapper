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
        BlockCache.AutomationElementMode = AutomationElementMode.AutomationElementMode_None;
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
        InterestingCondition = Automation.CreateAndCondition(anyInteresting, notOffscreen);
    }
}
