namespace Lapper.Privacy;

/// <summary>
/// Project boundary (docs/02-architecture.md): application exclusion policy,
/// password control detection, secret pattern checks, screenshot
/// cropping/downscaling and the sensitive context gate. All captured screen
/// content passes through this project before leaving the machine.
/// </summary>
public static class ProjectBoundary
{
    public const string Name = "Lapper.Privacy";
}
