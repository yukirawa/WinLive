using System.Runtime.InteropServices;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class WindowsSourceAppLauncher : ISourceAppLauncher
{
    public bool CanLaunch(string sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
        {
            return false;
        }

        return !sourceAppUserModelId.Contains('\\', StringComparison.Ordinal) &&
            !sourceAppUserModelId.Contains('/', StringComparison.Ordinal) &&
            !sourceAppUserModelId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> LaunchAsync(string sourceAppUserModelId, CancellationToken cancellationToken = default)
    {
        if (!CanLaunch(sourceAppUserModelId))
        {
            return Task.FromResult(false);
        }

        try
        {
            var activatorType = Type.GetTypeFromCLSID(ApplicationActivationManagerClsid, throwOnError: true)!;
            var activator = (IApplicationActivationManager)Activator.CreateInstance(activatorType)!;
            var hr = activator.ActivateApplication(sourceAppUserModelId, string.Empty, ActivateOptions.None, out _);
            return Task.FromResult(hr >= 0);
        }
        catch (COMException)
        {
            return Task.FromResult(false);
        }
    }

    [Flags]
    private enum ActivateOptions
    {
        None = 0
    }

    private static readonly Guid ApplicationActivationManagerClsid =
        new("45BA127D-10A8-46EA-8AB7-56EA9078943C");

    [ComImport]
    [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments,
            ActivateOptions options,
            out uint processId);
    }
}
