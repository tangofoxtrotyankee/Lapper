using System.Reflection;

namespace Lapper.Contracts;

/// <summary>
/// Provides access to the shared orientation result JSON schema
/// (contracts/orientation.schema.json), embedded at build time so the
/// desktop client always validates against the same contract as the backend.
/// </summary>
public static class OrientationSchema
{
    private const string ResourceName = "Lapper.Contracts.orientation.schema.json";

    public static string GetSchemaJson()
    {
        var assembly = typeof(OrientationSchema).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded schema resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
