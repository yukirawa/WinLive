using System.Security.Cryptography;
using System.Text;
using WinLive.Core;

namespace WinLive.Windows;

public readonly record struct ProgressBarSnapshot(
    int ProcessId,
    string WindowTitle,
    string AutomationId,
    string Name,
    string ClassName,
    bool IsEnabled,
    bool IsOffscreen,
    double Minimum,
    double Maximum,
    double Value);

public static class ProgressActivityDetection
{
    public const string SourceMetadataValue = "uiAutomationProgressBar";

    public static bool TryCreateActivity(
        ProgressBarSnapshot snapshot,
        string? processName,
        out LiveActivity activity)
    {
        activity = default!;

        if (snapshot.ProcessId <= 0 ||
            snapshot.IsOffscreen ||
            !snapshot.IsEnabled ||
            string.IsNullOrWhiteSpace(snapshot.WindowTitle) ||
            !IsFinite(snapshot.Minimum) ||
            !IsFinite(snapshot.Maximum) ||
            !IsFinite(snapshot.Value) ||
            snapshot.Maximum <= snapshot.Minimum ||
            snapshot.Value < snapshot.Minimum)
        {
            return false;
        }

        var progress = Math.Clamp(
            (snapshot.Value - snapshot.Minimum) / (snapshot.Maximum - snapshot.Minimum),
            0d,
            1d);
        var type = Classify(snapshot.WindowTitle, snapshot.Name, processName);
        var title = BuildTitle(type, snapshot.Name, snapshot.WindowTitle);
        var id = BuildId(snapshot, processName);

        activity = new LiveActivity
        {
            Id = id,
            Type = type,
            State = progress >= 1 ? LiveActivityState.Completed : LiveActivityState.Active,
            Title = title,
            Subtitle = snapshot.WindowTitle,
            Progress = progress,
            Priority = type == LiveActivityType.Experimental ? 15 : 35,
            SourceApp = new LiveActivitySourceApp
            {
                Name = processName,
                ProcessId = snapshot.ProcessId
            },
            Metadata = new Dictionary<string, string>
            {
                ["source"] = SourceMetadataValue,
                ["automationId"] = snapshot.AutomationId,
                ["className"] = snapshot.ClassName
            }
        };
        return true;
    }

    public static bool IsDetectedProgressActivity(LiveActivity activity)
    {
        return activity.Metadata.TryGetValue("source", out var source) &&
            string.Equals(source, SourceMetadataValue, StringComparison.Ordinal);
    }

    public static int RemoveStaleActivities(ILiveActivityStore store, IReadOnlySet<string> seenIds)
    {
        var removed = 0;
        foreach (var activity in store.Activities.Where(IsDetectedProgressActivity))
        {
            if (!seenIds.Contains(activity.Id) &&
                store.Remove(activity.Id, LiveActivityEndReason.SourceClosed))
            {
                removed++;
            }
        }

        return removed;
    }

    private static LiveActivityType Classify(string windowTitle, string barName, string? processName)
    {
        var text = $"{windowTitle} {barName} {processName}".ToLowerInvariant();
        if (ContainsAny(text, "copy", "copying", "move", "moving", "ファイル"))
        {
            return LiveActivityType.FileCopy;
        }

        if (ContainsAny(text, "download", "downloading", "ダウンロード"))
        {
            return LiveActivityType.Download;
        }

        if (ContainsAny(text, "upload", "uploading", "アップロード"))
        {
            return LiveActivityType.Upload;
        }

        if (ContainsAny(text, "encode", "encoding", "render", "rendering", "export", "compress"))
        {
            return LiveActivityType.Encode;
        }

        if (ContainsAny(text, "install", "installing", "setup", "update", "updating"))
        {
            return LiveActivityType.Install;
        }

        if (ContainsAny(text, "timer", "remaining"))
        {
            return LiveActivityType.Timer;
        }

        return LiveActivityType.Experimental;
    }

    private static string BuildTitle(LiveActivityType type, string barName, string windowTitle)
    {
        if (!string.IsNullOrWhiteSpace(barName) &&
            !string.Equals(barName.Trim(), windowTitle.Trim(), StringComparison.CurrentCultureIgnoreCase))
        {
            return barName.Trim();
        }

        return type switch
        {
            LiveActivityType.Download => "Download progress",
            LiveActivityType.Upload => "Upload progress",
            LiveActivityType.Encode => "Encode progress",
            LiveActivityType.FileCopy => "File copy progress",
            LiveActivityType.Install => "Install progress",
            LiveActivityType.Timer => "Timer progress",
            _ => "App progress"
        };
    }

    private static string BuildId(ProgressBarSnapshot snapshot, string? processName)
    {
        var stableName = FirstNonEmpty(
            snapshot.AutomationId,
            snapshot.Name,
            snapshot.ClassName,
            processName,
            "progress");
        var key = $"{snapshot.ProcessId}:{processName}:{stableName}:{snapshot.ClassName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return $"detected:{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.Ordinal));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
