using System.Runtime.InteropServices;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class FullScreenDetector : IFullScreenDetector
{
    private const int ShowMaximized = 3;

    public bool IsForegroundFullScreen()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || IsIconic(foreground))
        {
            return false;
        }

        if (IsZoomed(foreground) || IsShowMaximized(foreground))
        {
            return false;
        }

        if (!GetWindowRect(foreground, out var windowRect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo
        {
            cbSize = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        var monitorRect = monitorInfo.rcMonitor;
        return IsCoveringMonitor(windowRect, monitorRect);
    }

    internal static bool IsCoveringMonitor(Rect windowRect, Rect monitorRect)
    {
        return windowRect.Left <= monitorRect.Left &&
            windowRect.Top <= monitorRect.Top &&
            windowRect.Right >= monitorRect.Right &&
            windowRect.Bottom >= monitorRect.Bottom;
    }

    private static bool IsShowMaximized(IntPtr hWnd)
    {
        var placement = new WindowPlacement
        {
            length = Marshal.SizeOf<WindowPlacement>()
        };

        return GetWindowPlacement(hWnd, ref placement) &&
            placement.showCmd == ShowMaximized;
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public Rect rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
