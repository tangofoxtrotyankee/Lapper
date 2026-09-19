using System.Text.Json.Serialization;

namespace Lapper.Contracts;

/// <summary>
/// Request DTOs mirroring contracts/orient-request.schema.json and
/// action-request.schema.json. The JSON Schemas are authoritative; the
/// backend re-validates every request against them.
/// </summary>
public sealed record OrientRequest
{
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("application")]
    public required ApplicationInfo Application { get; init; }

    [JsonPropertyName("context")]
    public required ScreenContextPayload Context { get; init; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestOptions? Options { get; init; }

    [JsonPropertyName("client")]
    public required ClientInfo Client { get; init; }
}

public sealed record ActionRequest
{
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("action")]
    public required ActionSpec Action { get; init; }

    [JsonPropertyName("application")]
    public required ApplicationInfo Application { get; init; }

    [JsonPropertyName("context")]
    public required ScreenContextPayload Context { get; init; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestOptions? Options { get; init; }

    [JsonPropertyName("client")]
    public required ClientInfo Client { get; init; }
}

public sealed record ActionSpec
{
    /// <summary>One of: draft_text, extract_facts, ask_question.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("question")]
    public string? Question { get; init; }
}

public sealed record ApplicationInfo
{
    [JsonPropertyName("processName")]
    public required string ProcessName { get; init; }

    /// <summary>Optional and off by default: window titles stay local.</summary>
    [JsonPropertyName("windowTitle")]
    public string? WindowTitle { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }
}

public sealed record ScreenContextPayload
{
    [JsonPropertyName("selectedText")]
    public string? SelectedText { get; init; }

    [JsonPropertyName("blocks")]
    public required IReadOnlyList<ContextBlock> Blocks { get; init; }

    [JsonPropertyName("ocrText")]
    public string? OcrText { get; init; }

    /// <summary>Always false in MVP: screenshots never leave the machine.</summary>
    [JsonPropertyName("imageIncluded")]
    public required bool ImageIncluded { get; init; }
}

public sealed record RequestOptions
{
    [JsonPropertyName("deep")]
    public bool Deep { get; init; }
}

public sealed record ClientInfo
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("capabilities")]
    public required IReadOnlyList<string> Capabilities { get; init; }
}
