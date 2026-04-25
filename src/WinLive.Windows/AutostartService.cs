using Microsoft.Win32;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class RegistryAutostartService : IAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(appName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(string appName, string executablePath, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            key.SetValue(appName, $"\"{executablePath}\"");
            return;
        }

        key.DeleteValue(appName, throwOnMissingValue: false);
    }
}
