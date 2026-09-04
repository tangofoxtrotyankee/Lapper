namespace Lapper.Shell.Core;

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    // Values match the Win32 RegisterHotKey fsModifiers flags.
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// A global keyboard shortcut, stored in settings as text (e.g. "Ctrl+Alt+L")
/// and translated to Win32 RegisterHotKey values by the desktop shell.
/// Parsing lives here so it is unit-testable on any OS.
/// </summary>
public sealed record ShortcutGesture(ShortcutModifiers Modifiers, string Key)
{
    /// <summary>Default gesture: Ctrl+Alt+L ("L" for Lapper).</summary>
    public static ShortcutGesture Default { get; } =
        new(ShortcutModifiers.Control | ShortcutModifiers.Alt, "L");

    /// <summary>Win32 virtual-key code for <see cref="Key"/>.</summary>
    public int VirtualKeyCode => KeyToVirtualKey(Key);

    public static bool TryParse(string? text, out ShortcutGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = ShortcutModifiers.None;
        string? key = null;

        foreach (var rawPart in text.Split('+'))
        {
            var part = rawPart.Trim();
            switch (part.ToUpperInvariant())
            {
                case "":
                    return false;
                case "CTRL" or "CONTROL":
                    modifiers |= ShortcutModifiers.Control;
                    break;
                case "ALT":
                    modifiers |= ShortcutModifiers.Alt;
                    break;
                case "SHIFT":
                    modifiers |= ShortcutModifiers.Shift;
                    break;
                case "WIN" or "WINDOWS":
                    modifiers |= ShortcutModifiers.Win;
                    break;
                default:
                    if (key is not null)
                    {
                        return false; // two non-modifier keys
                    }
                    key = part.ToUpperInvariant();
                    break;
            }
        }

        // A global hotkey must have at least one modifier and a supported key,
        // otherwise it would swallow ordinary typing system-wide.
        if (key is null || modifiers == ShortcutModifiers.None || KeyToVirtualKey(key) == 0)
        {
            return false;
        }

        gesture = new ShortcutGesture(modifiers, key);
        return true;
    }

    public string Format()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(ShortcutModifiers.Control))
        {
            parts.Add("Ctrl");
        }
        if (Modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            parts.Add("Alt");
        }
        if (Modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            parts.Add("Shift");
        }
        if (Modifiers.HasFlag(ShortcutModifiers.Win))
        {
            parts.Add("Win");
        }
        parts.Add(Key);
        return string.Join('+', parts);
    }

    /// <summary>
    /// Supported keys: A-Z, 0-9, F1-F24, Space. Returns 0 for anything else.
    /// Codes are the standard Win32 virtual-key values.
    /// </summary>
    private static int KeyToVirtualKey(string key)
    {
        if (key.Length == 1)
        {
            var c = key[0];
            if (c is >= 'A' and <= 'Z')
            {
                return c; // VK_A..VK_Z equal their ASCII codes
            }
            if (c is >= '0' and <= '9')
            {
                return c; // VK_0..VK_9 equal their ASCII codes
            }
        }

        if (key == "SPACE")
        {
            return 0x20;
        }

        if (key.Length is 2 or 3 && key[0] == 'F'
            && int.TryParse(key.AsSpan(1), out var f) && f is >= 1 and <= 24)
        {
            return 0x70 + f - 1; // VK_F1 = 0x70
        }

        return 0;
    }
}
