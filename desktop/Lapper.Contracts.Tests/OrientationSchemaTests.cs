using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace Lapper.Contracts.Tests;

public class OrientationSchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromText(OrientationSchema.GetSchemaJson());

    private static readonly EvaluationOptions Options = new()
    {
        OutputFormat = OutputFormat.List,
    };

    private static string FixturesDir(string kind) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", kind);

    public static TheoryData<string> ValidFixtures() => FixtureNames("valid");

    public static TheoryData<string> InvalidFixtures() => FixtureNames("invalid");

    private static TheoryData<string> FixtureNames(string kind)
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.EnumerateFiles(FixturesDir(kind), "*.json"))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Fact]
    public void EmbeddedSchemaLoadsAndDeclaresContract()
    {
        var schema = JsonNode.Parse(OrientationSchema.GetSchemaJson())!;
        Assert.Equal("OrientationResult", schema["title"]!.GetValue<string>());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
    }

    [Fact]
    public void FixtureSetsAreNonTrivial()
    {
        Assert.True(Directory.EnumerateFiles(FixturesDir("valid"), "*.json").Count() >= 2);
        Assert.True(Directory.EnumerateFiles(FixturesDir("invalid"), "*.json").Count() >= 4);
    }

    [Theory]
    [MemberData(nameof(ValidFixtures))]
    public void ValidFixturePassesValidation(string fileName)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixturesDir("valid"), fileName)));
        var result = Schema.Evaluate(doc.RootElement, Options);
        Assert.True(result.IsValid, $"{fileName} should validate");
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void InvalidFixtureFailsValidation(string fileName)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixturesDir("invalid"), fileName)));
        var result = Schema.Evaluate(doc.RootElement, Options);
        Assert.False(result.IsValid, $"{fileName} should NOT validate");
    }

    [Fact]
    public void ValidFixtureRoundTripsThroughDtos()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir("valid"), "renewal-notice.json"));
        var dto = JsonSerializer.Deserialize<OrientationResult>(json);

        Assert.NotNull(dto);
        Assert.Equal("renewal_notice", dto.ContentType);
        Assert.False(dto.NeedsMoreContext);

        var reserialized = JsonSerializer.SerializeToElement(dto);
        var result = Schema.Evaluate(reserialized, Options);
        Assert.True(result.IsValid, "re-serialized DTO should still validate against the schema");
    }
}
