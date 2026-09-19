using System.Text.Json.Serialization;

namespace Lapper.Contracts;

/// <summary>
/// One extracted screen-content block (docs/04-api-contract.md). Ids are
/// stable within a snapshot ("b1", "b2", ...) so facts can cite sources.
/// </summary>
public sealed record ContextBlock
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>Content-free: block text must never reach logs or output.</summary>
    public override string ToString() => $"ContextBlock({Id}, {Role}, {Text.Length} chars)";
}
