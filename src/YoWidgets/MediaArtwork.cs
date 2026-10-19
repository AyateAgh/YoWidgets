using Windows.Storage.Streams;
using Windows.ApplicationModel;
namespace YoWidgets;

internal sealed class MediaArtwork
{
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(6) };
    readonly Dictionary<string, BitmapSource?> icons = [];
    public async Task<(BitmapSource? Image, string Origin)> ResolveAsync(IRandomAccessStreamReference? thumbnail, string source, string title, string artist, string subtitle, CancellationToken ct)
    {
        var origin = "none";
        var choice = await ArtworkSelection.FirstAsync<BitmapSource>(() => thumbnail == null ? Task.FromResult<BitmapSource?>(null) : ReadAsync(thumbnail, ct), async () => { var logo = await ResolveLogoAsync(source, title, artist, subtitle, ct); origin = logo.Origin; return logo.Image; });
        return (choice.Image, choice.Image == null ? "none" : choice.Primary ? "track" : origin);
    }
    async Task<(BitmapSource? Image, string Origin)> ResolveLogoAsync(string source, string title, string artist, string subtitle, CancellationToken ct)
    {
        var site = ArtworkPolicy.WebsiteIcon(source, title, artist, subtitle);
        if (site != null)
        {
            if (!icons.TryGetValue(site, out var logo))
            {
                try
                {
                    using var response = await http.GetAsync(site, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > 512_000)
                        throw new InvalidDataException("Icon too large");
                    using var input = await response.Content.ReadAsStreamAsync(ct);
                    using var output = new MemoryStream();
                    var buffer = new byte[8192];
                    int count;
                    while ((count = await input.ReadAsync(buffer, ct)) > 0)
                    {
                        if (output.Length + count > 512_000)
                            throw new InvalidDataException("Icon too large");
                        output.Write(buffer, 0, count);
                    }
                    logo = Decode(output.ToArray());
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { logo = null; }
                if (icons.Count >= 16)
                    icons.Clear();
                icons[site] = logo;
            }
            if (logo != null)
                return (logo, "website");
            return (null, "none");
        }
        // Native players can expose their identity's logo. Browsers without a
        // website identity stay art-free rather than inventing the current site.
        if (!source.Contains("chrome", StringComparison.OrdinalIgnoreCase) && !source.Contains("msedge", StringComparison.OrdinalIgnoreCase) && !source.Contains("firefox", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var app = AppInfo.GetFromAppUserModelId(source);
                var logo = await ReadAsync(app.DisplayInfo.GetLogo(new Windows.Foundation.Size(96, 96)), ct);
                if (logo != null)
                    return (logo, "application");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { }
        }
        return (null, "none");
    }
    static async Task<BitmapSource?> ReadAsync(IRandomAccessStreamReference reference, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            using var stream = await reference.OpenReadAsync().AsTask(timeout.Token);
            if (stream.Size == 0 || stream.Size > 8_000_000)
                return null;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            var loaded = await reader.LoadAsync((uint)stream.Size).AsTask(timeout.Token);
            if (loaded != stream.Size)
                return null;
            var bytes = new byte[(int)loaded];
            reader.ReadBytes(bytes);
            return Decode(bytes);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }
    }
    static BitmapSource? Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
            return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 360;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }
    public void Clear() => icons.Clear();
}
