using System.Diagnostics;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class WindowsSourceAppLauncher : ISourceAppLauncher
{
    public bool CanLaunch(string? appUserModelId)
    {
        return !string.IsNullOrWhiteSpace(appUserModelId);
    }

    public Task<bool> LaunchAsync(
        string? appUserModelId,
        CancellationToken cancellationToken = default)
    {
        if (!CanLaunch(appUserModelId))
        {
            return Task.FromResult(false);
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{appUserModelId}",
                UseShellExecute = false
            });
            return Task.FromResult(process is not null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
