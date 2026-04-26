using System.IO;

namespace WinLive.App;

internal static class WinLiveDiagnostics
{
    private static readonly object Gate = new();
    private static readonly bool IsEnabled =
        string.Equals(
            Environment.GetEnvironmentVariable("WINLIVE_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal);

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinLive",
        "diagnostics.log");

    public static void Write(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }
}
