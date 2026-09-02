using System.Text.Json.Serialization;

namespace Lapper.Contracts;

/// <summary>
/// The orientation result returned by POST /v1/context/orient.
/// Mirrors contracts/orientation.schema.json; the schema is authoritative.
/// </summary>
public sealed record OrientationResult
{
    [JsonPropertyName("contentType")]
    public required string ContentType { get; init; }

    [JsonPropertyName("orientation")]
    public required string Orientation { get; init; }

    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("facts")]
    public required IReadOnlyList<OrientationFact> Facts { get; init; }

    [JsonPropertyName("suggestedActions")]
    public required IReadOnlyList<SuggestedAction> SuggestedActions { get; init; }

    [JsonPropertyName("warnings")]
    public required IReadOnlyList<string> Warnings { get; init; }

    [JsonPropertyName("needsMoreContext")]
    public required bool NeedsMoreContext { get; init; }
}

public sealed record OrientationFact
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("sourceIds")]
    public required IReadOnlyList<string> SourceIds { get; init; }

    /// <summary>One of "low", "medium", "high" (schema-enforced enum).</summary>
    [JsonPropertyName("uncertainty")]
    public required string Uncertainty { get; init; }
}

public sealed record SuggestedAction
{
    /// <summary>
    /// One of the allowlisted MVP action types (schema-enforced enum):
    /// copy_text, read_aloud, draft_text, extract_facts, ask_question, share_text.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("requiresConfirmation")]
    public required bool RequiresConfirmation { get; init; }
}
