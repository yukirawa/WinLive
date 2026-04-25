namespace WinLive.Core;

public readonly record struct IslandBounds(double Left, double Top, double Width, double Height)
{
    public static IslandBounds Default => new(80, 80, 360, 96);

    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public IslandBounds WithPosition(double left, double top) => this with { Left = left, Top = top };

    public IslandBounds WithSize(double width, double height) => this with { Width = width, Height = height };
}

public readonly record struct ScreenBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public bool Intersects(IslandBounds bounds)
    {
        return bounds.Right > Left &&
            bounds.Left < Right &&
            bounds.Bottom > Top &&
            bounds.Top < Bottom;
    }
}
