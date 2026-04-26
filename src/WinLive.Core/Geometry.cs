namespace WinLive.Core;

public readonly record struct IslandBounds(double Left, double Top, double Width, double Height)
{
    public static IslandBounds Default => new(0, 0, 360, 74);
}

public readonly record struct ScreenBounds(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary = false)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

public readonly record struct TaskbarBounds(
    double Left,
    double Top,
    double Width,
    double Height,
    TaskbarEdge Edge)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

public enum TaskbarEdge
{
    Unknown,
    Left,
    Top,
    Right,
    Bottom
}
