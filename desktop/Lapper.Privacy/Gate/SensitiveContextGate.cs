namespace Lapper.Privacy.Gate;

public enum GateDecision
{
    Proceed,
    Block,
}

/// <summary>
/// Central sensitive-context decision so policy tightening is a single edit
/// with tests. A focused password control blocks the ENTIRE capture — no
/// text and no pixels leave that window.
/// </summary>
public static class SensitiveContextGate
{
    public static GateDecision Evaluate(
        bool focusedElementIsPassword,
        int passwordControlsSkipped,
        int redactionCount)
    {
        _ = passwordControlsSkipped;
        _ = redactionCount;
        return focusedElementIsPassword ? GateDecision.Block : GateDecision.Proceed;
    }
}
