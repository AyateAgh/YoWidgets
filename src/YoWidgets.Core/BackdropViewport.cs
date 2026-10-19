namespace YoWidgets.Core;

public static class BackdropViewport
{
    public static RectD For(RectD window, RectD snapshot)
    {
        if (snapshot.Width <= 0 || snapshot.Height <= 0)
            return new(0, 0, 1, 1);
        return new((window.X - snapshot.X) / snapshot.Width, (window.Y - snapshot.Y) / snapshot.Height, window.Width / snapshot.Width, window.Height / snapshot.Height);
    }
}
