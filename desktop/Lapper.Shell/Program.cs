using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Lapper.Shell;

/// <summary>
/// Custom entry point (DISABLE_XAML_GENERATED_MAIN) so single-instance
/// enforcement runs before any window exists: a second launch redirects its
/// activation to the first instance and exits immediately.
/// </summary>
public static class Program
{
    private const string InstanceKey = "LapperMainInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            mainInstance.RedirectActivationToAsync(activationArgs).AsTask().GetAwaiter().GetResult();
            return;
        }

        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
