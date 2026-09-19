using Lapper.Contracts;

namespace Lapper.ApiClient;

/// <summary>
/// Typed events from the backend SSE stream. Error carries only the
/// backend's fixed error code/message — never raw content.
/// </summary>
public abstract record ApiStreamEvent
{
    public sealed record Accepted(string Model, string Route) : ApiStreamEvent;

    /// <summary>Raw model text fragment (for orient: a JSON fragment).</summary>
    public sealed record Delta(string Text) : ApiStreamEvent;

    public sealed record Completed : ApiStreamEvent;

    /// <summary>Final validated orientation (orient endpoint only).</summary>
    public sealed record OrientResult(OrientationResult Result) : ApiStreamEvent;

    /// <summary>Final plain-text result (action endpoint only).</summary>
    public sealed record TextResult(string Text) : ApiStreamEvent;

    public sealed record Usage(int InputTokens, int OutputTokens, int LatencyMs) : ApiStreamEvent;

    public sealed record Error(string Code, string Message) : ApiStreamEvent;
}
