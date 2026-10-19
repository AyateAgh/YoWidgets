using System.Text.Json;
namespace YoWidgets.Core;

public sealed class SettingsStore(string path)
{
    public string Path { get; } = path;
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public AppSettings Load()
    {
        foreach (var file in new[] { Path, Path + ".bak" })
        {
            if (!File.Exists(file))
                continue;
            try
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file), Options) ?? new();
                s.Normalize();
                return s;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { if (file == Path) { try { File.Copy(Path, Path + ".corrupt", true); } catch (IOException) { } } }
        }
        return new();
    }
    public void Save(AppSettings s)
    {
        s.Normalize();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Options));
        File.Move(tmp, Path, true);
        File.Copy(Path, Path + ".bak", true);
    }
}
