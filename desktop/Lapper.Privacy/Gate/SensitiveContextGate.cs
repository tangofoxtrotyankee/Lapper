namespace Lapper.Privacy.Gate;

public enum GateDecision
{
    Proceed,
    Block,
}

/// <summary>
/// Central sensitive-context decision so policy tightening is a single edit
/// with tests. A focused password control blocks the ENTIRE capture — no
/// text and no pixels leave that window. A private-key match, or a screen
/// saturated with secret-shaped content, blocks the cloud request outright:
/// span-redaction is not enough when the surrounding context itself (names,
/// hosts, partial values) is sensitive.
/// </summary>
public static class SensitiveContextGate
{
    /// <summary>Redaction count at which the whole capture is blocked.</summary>
    public const int BlockingRedactionCount = 3;

    /// <summary>Pattern whose presence alone blocks the capture.</summary>
    public const string BlockingPatternName = "private_key_block";

    public static GateDecision Evaluate(
        bool focusedElementIsPassword,
        int redactionCount,
        IReadOnlyCollection<string> matchedPatternNames)
    {
        if (focusedElementIsPassword)
        {
            return GateDecision.Block;
        }
        if (matchedPatternNames.Contains(BlockingPatternName))
        {
            return GateDecision.Block;
        }
        if (redactionCount >= BlockingRedactionCount)
        {
            return GateDecision.Block;
        }
        return GateDecision.Proceed;
    }
}
