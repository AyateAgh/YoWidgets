namespace YoWidgets.Core;

public static class ArtworkSelection
{
    public static async Task<(T? Image, bool Primary)> FirstAsync<T>(Func<Task<T?>> primary, Func<Task<T?>> fallback) where T : class
    {
        try
        {
            var image = await primary();
            if (image != null)
                return (image, true);
        }
        catch (OperationCanceledException) { throw; }
        catch { }
        try
        {
            return (await fallback(), false);
        }
        catch (OperationCanceledException) { throw; }
        catch { return (null, false); }
    }
}
