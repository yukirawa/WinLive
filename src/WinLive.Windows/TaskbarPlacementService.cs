using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class TaskbarPlacementService : ITaskbarPlacementService
{
    private const int Margin = 14;
    private const int AppBarMessageGetTaskbarPos = 0x00000005;

    public IslandBounds GetDefaultIslandBounds(IslandBounds preferred)
    {
        var desired = preferred.Width <= 0 || preferred.Height <= 0
            ? IslandBounds.Default
            : preferred;
        var taskbar = TryGetTaskbarBounds();
        if (taskbar is null)
        {
            return CorrectToVisibleArea(new IslandBounds(0, 0, desired.Width, desired.Height));
        }

        return CorrectToVisibleArea(PositionNearNotificationArea(desired, taskbar.Value));
    }

    public IslandBounds CorrectToVisibleArea(IslandBounds desired)
    {
        var screens = Screen.AllScreens.Select(ToScreenBounds).ToArray();
        var target = screens.FirstOrDefault(screen => Intersects(screen, desired));
        if (target.Width <= 0)
        {
            target = screens.FirstOrDefault(screen => screen.IsPrimary);
        }

        if (target.Width <= 0)
        {
            target = new ScreenBounds(0, 0, 1920, 1080, true);
        }

        var width = Math.Min(Math.Max(desired.Width, 260), Math.Max(260, target.Width - Margin * 2));
        var height = Math.Min(Math.Max(desired.Height, 48), Math.Max(48, target.Height - Margin * 2));
        var left = Math.Clamp(desired.Left, target.Left + Margin, target.Right - width - Margin);
        var top = Math.Clamp(desired.Top, target.Top + Margin, target.Bottom - height - Margin);
        return new IslandBounds(left, top, width, height);
    }

    private static IslandBounds PositionNearNotificationArea(
        IslandBounds desired,
        TaskbarBounds taskbar)
    {
        return taskbar.Edge switch
        {
            TaskbarEdge.Bottom => new IslandBounds(
                taskbar.Right - desired.Width - Margin,
                taskbar.Top - desired.Height - Margin,
                desired.Width,
                desired.Height),
            TaskbarEdge.Top => new IslandBounds(
                taskbar.Right - desired.Width - Margin,
                taskbar.Bottom + Margin,
                desired.Width,
                desired.Height),
            TaskbarEdge.Left => new IslandBounds(
                taskbar.Right + Margin,
                taskbar.Bottom - desired.Height - Margin,
                desired.Width,
                desired.Height),
            TaskbarEdge.Right => new IslandBounds(
                taskbar.Left - desired.Width - Margin,
                taskbar.Bottom - desired.Height - Margin,
                desired.Width,
                desired.Height),
            _ => new IslandBounds(
                taskbar.Right - desired.Width - Margin,
                taskbar.Top - desired.Height - Margin,
                desired.Width,
                desired.Height)
        };
    }

    private static ScreenBounds ToScreenBounds(Screen screen)
    {
        var area = screen.WorkingArea;
        return new ScreenBounds(area.Left, area.Top, area.Width, area.Height, screen.Primary);
    }

    private static bool Intersects(ScreenBounds screen, IslandBounds bounds)
    {
        return bounds.Left < screen.Right &&
            bounds.Left + bounds.Width > screen.Left &&
            bounds.Top < screen.Bottom &&
            bounds.Top + bounds.Height > screen.Top;
    }

    private static TaskbarBounds? TryGetTaskbarBounds()
    {
        var data = new AppBarData
        {
            cbSize = Marshal.SizeOf<AppBarData>()
        };

        var result = SHAppBarMessage(AppBarMessageGetTaskbarPos, ref data);
        if (result == IntPtr.Zero)
        {
            return null;
        }

        return new TaskbarBounds(
            data.rc.Left,
            data.rc.Top,
            data.rc.Right - data.rc.Left,
            data.rc.Bottom - data.rc.Top,
            data.uEdge switch
            {
                0 => TaskbarEdge.Left,
                1 => TaskbarEdge.Top,
                2 => TaskbarEdge.Right,
                3 => TaskbarEdge.Bottom,
                _ => TaskbarEdge.Unknown
            });
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr SHAppBarMessage(int dwMessage, ref AppBarData pData);

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public Rect rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
