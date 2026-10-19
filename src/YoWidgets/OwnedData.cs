using System.Text.Json;
namespace YoWidgets;

internal static class OwnedData
{
    public static void RegisterImage(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-fA-F0-9]{32}\.(png|jpg|jpeg|bmp)$"))
            throw new InvalidOperationException("Unsupported image name");
        var file = Path.Combine(App.DataRoot, "owned-images.json");
        var names = File.Exists(file) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file)) ?? [] : new List<string>();
        if (!names.Contains(name))
            names.Add(name);
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(names));
        File.Move(file + ".tmp", file, true);
    }
}
