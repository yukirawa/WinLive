using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Automation;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class ExperimentalProgressActivitySource : ILiveActivitySource
{
    private readonly ILiveActivityStore _store;
    private readonly WinLiveSettings _settings;
    private CancellationTokenSource? _cancellation;
    private Task? _pollLoop;

    public ExperimentalProgressActivitySource(ILiveActivityStore store, WinLiveSettings settings)
    {
        _store = store;
        _settings = settings;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _settings.Normalize();
        if (!_settings.ExperimentalProgress.Enabled || _pollLoop is not null)
        {
            return Task.CompletedTask;
        }

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollLoop = Task.Run(() => PollLoopAsync(_cancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cancellation is not null)
        {
            await _cancellation.CancelAsync().ConfigureAwait(false);
        }

        if (_pollLoop is not null)
        {
            try
            {
                await _pollLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cancellation?.Dispose();
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                PollOnce();
            }
            catch
            {
                // UI Automation crosses process boundaries; failures are expected.
            }

            await Task.Delay(
                _settings.ExperimentalProgress.PollIntervalMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private void PollOnce()
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);

        foreach (AutomationElement window in windows)
        {
            var windowName = SafeGet(() => window.Current.Name);
            var processId = SafeGet(() => window.Current.ProcessId);
            if (string.IsNullOrWhiteSpace(windowName) || processId <= 0)
            {
                continue;
            }

            var bars = window.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ProgressBar));

            foreach (AutomationElement bar in bars)
            {
                if (!TryReadProgress(bar, out var progress))
                {
                    continue;
                }

                var barName = SafeGet(() => bar.Current.Name);
                var automationId = SafeGet(() => bar.Current.AutomationId);
                var id = BuildId(processId, windowName, automationId, barName);
                seenIds.Add(id);

                _store.Upsert(new LiveActivity
                {
                    Id = id,
                    Type = LiveActivityType.Experimental,
                    State = progress >= 1 ? LiveActivityState.Completed : LiveActivityState.Active,
                    Title = string.IsNullOrWhiteSpace(barName) ? windowName : barName,
                    Subtitle = windowName,
                    Progress = progress,
                    Priority = 10,
                    SourceApp = new LiveActivitySourceApp
                    {
                        Name = TryGetProcessName(processId),
                        ProcessId = processId
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        ["experimental"] = "uiAutomationProgressBar"
                    }
                });
            }
        }

        foreach (var activity in _store.Activities.Where(item => item.Type == LiveActivityType.Experimental))
        {
            if (!seenIds.Contains(activity.Id))
            {
                _store.Remove(activity.Id, LiveActivityEndReason.SourceClosed);
            }
        }
    }

    private static bool TryReadProgress(AutomationElement element, out double progress)
    {
        progress = 0;
        try
        {
            if (!element.TryGetCurrentPattern(RangeValuePattern.Pattern, out var rawPattern))
            {
                return false;
            }

            var pattern = (RangeValuePattern)rawPattern;
            var range = pattern.Current.Maximum - pattern.Current.Minimum;
            if (range <= 0 || pattern.Current.Value < pattern.Current.Minimum)
            {
                return false;
            }

            progress = (pattern.Current.Value - pattern.Current.Minimum) / range;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildId(int processId, string windowName, string automationId, string barName)
    {
        var key = $"{processId}:{windowName}:{automationId}:{barName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return $"experimental:{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }

    private static string? TryGetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static T SafeGet<T>(Func<T> valueFactory)
    {
        try
        {
            return valueFactory();
        }
        catch
        {
            return default!;
        }
    }
}
