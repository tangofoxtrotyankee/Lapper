namespace Lapper.ApiClient;

/// <summary>
/// Project boundary (docs/02-architecture.md): authentication, REST/SSE calls
/// to the Lapper backend, retry/cancellation and error mapping. The desktop
/// client holds no provider secrets; tokens live in the Credential Locker,
/// never in local databases or logs.
/// </summary>
public static class ProjectBoundary
{
    public const string Name = "Lapper.ApiClient";
}
