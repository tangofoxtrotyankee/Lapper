namespace Lapper.Context.Windows.Blocks;

/// <summary>
/// One extracted text region before normalisation/ranking. Plain data —
/// no COM references ever escape the capture thread.
/// ToString is deliberately content-free so accidental logging cannot leak
/// captured text.
/// </summary>
public sealed record RawBlock(string Role, string Text, bool HasFocus, int TreeOrder)
{
    public override string ToString() => $"RawBlock(role={Role}, chars={Text.Length})";
}

/// <summary>Closed local role vocabulary; the wire keeps them as strings.</summary>
public static class ContextBlockRoles
{
    public const string Document = "document";
    public const string Edit = "edit";
    public const string Text = "text";
    public const string Heading = "heading";
    public const string ListItem = "list_item";
    public const string Link = "link";
    public const string Button = "button";
    public const string MenuItem = "menu_item";
    public const string Tab = "tab";
    public const string Cell = "cell";
    public const string Table = "table";
    public const string Section = "section";
    public const string Option = "option";
    public const string OcrParagraph = "ocr_paragraph";
}
