using YoWidgets.Core;
static class BridgeChecks
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool value) { if (!value) throw new Exception("Bridge routing regression"); }
        var state = new BrowserMediaState("connection/17", "soundcloud", "Just Staring / SOPHIE - VYZEE", "SOPHIE", 197, false, true, true, false, 0);
        test("browser bridge binds only the exact current music source", () => {
            Check(BrowserMediaPolicy.Match("Chrome", state.Title, 197.834, false, [state]) == state);
            Check(BrowserMediaPolicy.Match("MSEdge", state.Title, 197.834, false, [state]) == state);
        });
        test("browser bridge cannot hijack a native player or another track", () => {
            Check(BrowserMediaPolicy.Match("Spotify.exe", state.Title, 197.834, false, [state]) == null);
            Check(BrowserMediaPolicy.Match("Chrome", "Another track", 197.834, false, [state]) == null);
            Check(BrowserMediaPolicy.Match("Chrome", state.Title, 240, false, [state]) == null);
        });
        test("ambiguous browser tabs never receive a guessed queue command", () => {
            Check(BrowserMediaPolicy.Match("Chrome", state.Title, 197.834, false, [state, state with { Id = "other/18" }]) == null);
        });
        test("source playback state and provider are checked before binding", () => {
            Check(BrowserMediaPolicy.Match("Chrome", state.Title, 197.834, true, [state]) == null);
            Check(BrowserMediaPolicy.Match("Chrome", state.Title, 197.834, false, [state with { Provider = "unknown" }]) == null);
        });
    }
}
