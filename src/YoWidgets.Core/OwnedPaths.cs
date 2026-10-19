namespace YoWidgets.Core;

public static class OwnedPaths
{
    public static void EnsureNoReparseAncestors(string path)
    {
        var cursor = System.IO.Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(cursor))
        {
            if ((Directory.Exists(cursor) || File.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse ancestor rejected");
            cursor = System.IO.Path.GetDirectoryName(cursor);
        }
    }
    public static bool IsInside(string root, string path)
    {
        try
        {
            var r = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            var p = System.IO.Path.GetFullPath(path);
            return p.StartsWith(r + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !HasReparseAncestor(p, r);
        }
        catch { return false; }
    }
    private static bool HasReparseAncestor(string path, string root)
    {
        var p = path;
        while (p.Length >= root.Length)
        {
            if ((Directory.Exists(p) || File.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                return true;
            var parent = System.IO.Path.GetDirectoryName(p);
            if (parent == null || parent == p)
                break;
            p = parent;
        }
        return false;
    }
}
