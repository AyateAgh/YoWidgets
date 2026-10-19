using System.Runtime.InteropServices;
using System.Text.Json;
namespace YoWidgets;

internal static class BundleCache
{
    public record Entry(string Path, string[] Files);
    public static void Register()
    {
        try
        {
            var root = System.IO.Path.GetFullPath(RuntimeEnvironment.GetRuntimeDirectory()).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            var boundary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), ".net", "YoWidgets");
            if (!string.Equals(System.IO.Path.GetDirectoryName(root), boundary, StringComparison.OrdinalIgnoreCase))
                return;
            OwnedPaths.EnsureNoReparseAncestors(root);
            var files = new List<string>();
            void Collect(string directory)
            {
                foreach (var child in Directory.EnumerateFileSystemEntries(directory))
                {
                    OwnedPaths.EnsureNoReparseAncestors(child);
                    if (Directory.Exists(child))
                        Collect(child);
                    else
                        files.Add(System.IO.Path.GetRelativePath(root, child));
                }
            }
            Collect(root);
            var journal = System.IO.Path.Combine(App.DataRoot, "bundle-cache.json");
            var entries = File.Exists(journal) ? JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(journal)) ?? [] : [];
            entries.RemoveAll(e => string.Equals(e.Path, root, StringComparison.OrdinalIgnoreCase));
            entries.Add(new(root, files.ToArray()));
            File.WriteAllText(journal + ".tmp", JsonSerializer.Serialize(entries));
            File.Move(journal + ".tmp", journal, true);
            App.Trace("bundle-cache", root);
        }
        catch (Exception e) { App.Log("bundle-cache", e); }
    }
}
