using Windows.ApplicationModel;

namespace Lapper.Shell.Services;

/// <summary>
/// "Start with Windows" via the packaged StartupTask, declared Disabled in
/// the manifest (default off during alpha). Enabling always goes through the
/// OS consent flow; Windows policy can deny it.
/// </summary>
public sealed class StartupService
{
    private const string TaskId = "LapperStartup";

    public async Task<bool> IsEnabledAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    /// <summary>Returns the resulting enabled state (the OS may refuse).</summary>
    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync(TaskId);
        if (enabled)
        {
            var state = await task.RequestEnableAsync();
            return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }

        task.Disable();
        return false;
    }
}
