using System.Text.Json;
using System.Text.RegularExpressions;

namespace YoWidgets.Core;

public static class CatalogJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true, MaxDepth = 32 };
}

public sealed record CatalogDocument
{
    public int SchemaVersion { get; init; } = 1;
    public string Version { get; init; } = "1.0.0";
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<CatalogItem> Items { get; init; } = [];
    public List<ContentPackage> Packages { get; init; } = [];
}

public sealed record CatalogItem
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "widgets";
    public string Type { get; init; } = "widget";
    public string Kind { get; init; } = "clock";
    public string Pack { get; init; } = "glass";
    public string CollectionName { get; init; } = "";
    public string Author { get; init; } = "Yo";
    public string Version { get; init; } = "1.0.0";
    public string[] Tags { get; init; } = [];
    public string[] Packages { get; init; } = [];
    public string[] Includes { get; init; } = [];
    public string[] Permissions { get; init; } = [];
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ContentPackage
{
    public string Id { get; init; } = "";
    public string Version { get; init; } = "1.0.0";
    public string Url { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public long Bytes { get; init; }
    public string[] Dependencies { get; init; } = [];
}

public sealed record ContentReference(string Id, string Version, string Sha256);
public sealed record WidgetDefinition(string Kind,string Pack,string DefaultSize);
public sealed record InstalledContent(CatalogItem Item, List<ContentReference> Packages, DateTimeOffset InstalledAt);
public sealed record ContentResolution(List<CatalogItem> Items, List<ContentPackage> Packages);
public sealed record InstallProgress(string ItemId, string Stage, long Downloaded, long Total, string PackageId = "");

public static partial class CatalogRules
{
    public static readonly string[] WidgetKinds = ["clock", "calendar", "weather", "music", "photo", "focus", "quick", "notifications", "assistant"];
    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();
    public static bool ValidId(string? value) => value is not null && Identifier().IsMatch(value) && !value.Contains("..");
    public static bool ValidVersion(string? value) => value is not null && Version.TryParse(value, out _) && value.Length <= 32;

    public static void Validate(CatalogDocument document)
    {
        if (document is null || document.SchemaVersion != 1 || document.Items is null || document.Packages is null || document.Items.Count > 4000 || document.Packages.Count > 8000)
            throw new InvalidDataException("This catalog format is not supported. Update YoWidgets.");
        if(document.Items.Any(i=>i is null)||document.Packages.Any(p=>p is null))throw new InvalidDataException("The catalog contains an empty entry.");
        if (document.Items.Select(i => i.Id).Distinct().Count() != document.Items.Count || document.Packages.Select(i => i.Id).Distinct().Count() != document.Packages.Count)
            throw new InvalidDataException("The catalog contains duplicate IDs.");
        foreach (var item in document.Items)
            if (!ValidId(item.Id) || !ValidVersion(item.Version) || string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 100 || item.Description is null || item.Description.Length > 1200 || item.Author is null || item.Author.Length > 80 || item.CollectionName is null || item.CollectionName.Length>100 || item.Type is not ("widget" or "pack") || item.Category != "widgets" || !ValidId(item.Pack) || !WidgetKinds.Contains(item.Kind) || item.Packages is null || item.Includes is null || item.Tags is null || item.Permissions is null || item.Tags.Length > 20 || item.Permissions.Length > 12 || item.Includes.Length>256 || item.Packages.Length>128 || item.Includes.Any(id=>!ValidId(id)) || item.Packages.Any(id=>!ValidId(id)) || item.Tags.Any(t=>t is null||t.Length>80) || item.Permissions.Any(t=>t is null||t.Length>500))
                throw new InvalidDataException("The catalog contains an unsupported item.");
        foreach (var package in document.Packages)
            if (!ValidId(package.Id) || !ValidVersion(package.Version) || package.Bytes is <= 0 or > 64 * 1024 * 1024 || package.Sha256 is null || package.Sha256.Length != 64 || !package.Sha256.All(Uri.IsHexDigit) || package.Dependencies is null || package.Dependencies.Length>128 || package.Dependencies.Any(id=>!ValidId(id)) || !Uri.TryCreate(package.Url, UriKind.Absolute, out var uri) || !AllowedSource(uri))
                throw new InvalidDataException("The catalog contains an invalid package.");
    }

    public static bool AllowedSource(Uri uri) => uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) || uri.Scheme == "http" && uri.IsLoopback && string.IsNullOrEmpty(uri.UserInfo);

    public static ContentResolution Resolve(CatalogDocument document, string itemId)
    {
        Validate(document);
        var items = document.Items.ToDictionary(i => i.Id);
        var packages = document.Packages.ToDictionary(p => p.Id);
        var selected = new Dictionary<string, CatalogItem>();
        var required = new Dictionary<string, ContentPackage>();
        var visiting = new HashSet<string>();
        void Package(string id)
        {
            if(visiting.Count>128)throw new InvalidDataException("Package dependencies are too deeply nested.");
            if (required.ContainsKey(id)) return;
            if (!packages.TryGetValue(id, out var package) || !visiting.Add("p:" + id)) throw new InvalidDataException("Missing or cyclic package dependency: " + id);
            foreach (var dependency in package.Dependencies) Package(dependency);
            visiting.Remove("p:" + id); required.Add(id, package);
        }
        void Item(string id)
        {
            if(visiting.Count>128)throw new InvalidDataException("Catalog collections are too deeply nested.");
            if (selected.ContainsKey(id)) return;
            if (!items.TryGetValue(id, out var item) || !visiting.Add("i:" + id)) throw new InvalidDataException("Missing or cyclic catalog item: " + id);
            if (item.Type == "pack")
            {
                if (item.Includes.Length == 0) throw new InvalidDataException("The pack is empty.");
                foreach (var included in item.Includes) Item(included);
            }
            else
            {
                if (item.Packages.Length == 0) throw new InvalidDataException("This item has no installable content.");
                foreach (var id2 in item.Packages) Package(id2);
                if(!required.ContainsKey(item.Pack+"."+item.Kind)||!required.ContainsKey("style."+item.Pack))throw new InvalidDataException("The widget or collection resources are missing.");
                selected.Add(id, item);
            }
            visiting.Remove("i:" + id);
        }
        Item(itemId);
        return new(selected.Values.ToList(), required.Values.ToList());
    }
}

public sealed class ShellLifetime
{
    public int Generation { get; private set; }
    public DateTimeOffset? HiddenAt { get; private set; }
    public int Hide(DateTimeOffset at) { HiddenAt = at; return ++Generation; }
    public void Show() { HiddenAt = null; Generation++; }
    public bool CanSuspend(int generation) => generation == Generation && HiddenAt is not null;
    public bool CanEvict(bool visible,bool dirty) => HiddenAt is not null && !visible && !dirty;
    public bool ShouldExit(DateTimeOffset now) => HiddenAt is { } at && now - at >= TimeSpan.FromMinutes(15);
}
