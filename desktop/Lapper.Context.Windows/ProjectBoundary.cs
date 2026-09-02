namespace Lapper.Context.Windows;

/// <summary>
/// Project boundary (docs/02-architecture.md): foreground window detection,
/// UI Automation extraction, selection retrieval, local OCR and
/// active-window screenshot fallback — in that priority order.
/// Screen capture code is not written until Phase 2; nothing in this project
/// may run without an explicit user trigger.
/// </summary>
public static class ProjectBoundary
{
    public const string Name = "Lapper.Context.Windows";
}
