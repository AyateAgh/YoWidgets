namespace YoWidgets.Core;

public static class ActivityPolicy
{
    public static int Seconds(bool secondsClock, bool playingMusic, bool focus, bool snoozed) => snoozed ? 0 : secondsClock || playingMusic || focus ? 1 : 60;
}
