using System.Diagnostics;
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
            var windowIsOffscreen = SafeGet(() => window.Current.IsOffscreen);
            if (string.IsNullOrWhiteSpace(windowName) || processId <= 0 || windowIsOffscreen)
            {
                continue;
            }

            var processName = TryGetProcessName(processId);
            var bars = window.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ProgressBar));

            foreach (AutomationElement bar in bars)
            {
                if (!TryReadRange(bar, out var minimum, out var maximum, out var value))
                {
                    continue;
                }

                var snapshot = new ProgressBarSnapshot(
                    processId,
                    windowName,
                    SafeGet(() => bar.Current.AutomationId),
                    SafeGet(() => bar.Current.Name),
                    SafeGet(() => bar.Current.ClassName),
                    SafeGet(() => bar.Current.IsEnabled),
                    SafeGet(() => bar.Current.IsOffscreen),
                    minimum,
                    maximum,
                    value);
                if (!ProgressActivityDetection.TryCreateActivity(snapshot, processName, out var activity))
                {
                    continue;
                }

                seenIds.Add(activity.Id);
                _store.Upsert(activity);
            }
        }

        ProgressActivityDetection.RemoveStaleActivities(_store, seenIds);
    }

    private static bool TryReadRange(
        AutomationElement element,
        out double minimum,
        out double maximum,
        out double value)
    {
        minimum = 0;
        maximum = 0;
        value = 0;
        try
        {
            if (!element.TryGetCurrentPattern(RangeValuePattern.Pattern, out var rawPattern))
            {
                return false;
            }

            var pattern = (RangeValuePattern)rawPattern;
            minimum = pattern.Current.Minimum;
            maximum = pattern.Current.Maximum;
            value = pattern.Current.Value;
            return true;
        }
        catch
        {
            return false;
        }
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
