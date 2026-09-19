namespace Lapper.Context.Windows;

/// <summary>
/// Maps a process exe name to the wire category enum used by the API
/// contract (email, browser, document, code, chat, pdf, terminal, files,
/// other). Pure and unit-testable.
/// </summary>
public static class AppCategoryMap
{
    private static readonly Dictionary<string, string> ByProcess = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge.exe"] = "browser",
        ["chrome.exe"] = "browser",
        ["firefox.exe"] = "browser",
        ["brave.exe"] = "browser",
        ["opera.exe"] = "browser",
        ["vivaldi.exe"] = "browser",
        ["outlook.exe"] = "email",
        ["olk.exe"] = "email",
        ["thunderbird.exe"] = "email",
        ["winword.exe"] = "document",
        ["excel.exe"] = "document",
        ["powerpnt.exe"] = "document",
        ["notepad.exe"] = "document",
        ["notepad++.exe"] = "document",
        ["wordpad.exe"] = "document",
        ["acrobat.exe"] = "pdf",
        ["acrord32.exe"] = "pdf",
        ["sumatrapdf.exe"] = "pdf",
        ["devenv.exe"] = "code",
        ["code.exe"] = "code",
        ["rider64.exe"] = "code",
        ["teams.exe"] = "chat",
        ["ms-teams.exe"] = "chat",
        ["slack.exe"] = "chat",
        ["discord.exe"] = "chat",
        ["whatsapp.exe"] = "chat",
        ["windowsterminal.exe"] = "terminal",
        ["wt.exe"] = "terminal",
        ["cmd.exe"] = "terminal",
        ["powershell.exe"] = "terminal",
        ["pwsh.exe"] = "terminal",
        ["conhost.exe"] = "terminal",
        ["explorer.exe"] = "files",
    };

    public static string Map(string processName) =>
        ByProcess.TryGetValue(processName, out var category) ? category : "other";
}
