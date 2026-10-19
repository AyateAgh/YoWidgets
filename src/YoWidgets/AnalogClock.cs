namespace YoWidgets;

internal sealed class AnalogClock : FrameworkElement
{
    public DateTime Time { get; set; } = DateTime.Now; public bool Seconds
    {
        get; set;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var r = Math.Max(1, Math.Min(ActualWidth, ActualHeight) / 2 - 4);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1), c, r, r);
        for (int i = 0; i < 60; i++)
        {
            var a = i * Math.PI / 30 - Math.PI / 2;
            var inner = r - (i % 5 == 0 ? 10 : 4);
            dc.DrawLine(new Pen(Brushes.White, i % 5 == 0 ? 2 : 1), new(c.X + Math.Cos(a) * inner, c.Y + Math.Sin(a) * inner), new(c.X + Math.Cos(a) * (r - 3), c.Y + Math.Sin(a) * (r - 3)));
        }
        void Hand(double value, double length, Brush brush, double width)
        {
            var angle = value * Math.PI * 2 - Math.PI / 2;
            dc.DrawLine(new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, c, new(c.X + Math.Cos(angle) * length, c.Y + Math.Sin(angle) * length));
        }
        Hand((Time.Hour % 12 + Time.Minute / 60d) / 12, r * .47, Brushes.White, 4);
        Hand((Time.Minute + Time.Second / 60d) / 60, r * .72, Brushes.White, 2.5);
        if (Seconds)
            Hand(Time.Second / 60d, r * .8, Ui.Brush("Accent"), 1);
        dc.DrawEllipse(Brushes.White, null, c, 3, 3);
    }
}
