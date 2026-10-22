namespace YoWidgets.Core;

public static class ActivityPolicy
{
    public static int Seconds(bool secondsClock, bool playingMusic, bool focus, bool snoozed) => snoozed ? 0 : secondsClock || playingMusic || focus ? 1 : 60;
    public static double NextDelay(int activitySeconds, bool minuteClock, DateTime now) => minuteClock ? Math.Min(activitySeconds, (60_000 - now.TimeOfDay.TotalMilliseconds % 60_000 + 20) / 1000) : activitySeconds;
}
