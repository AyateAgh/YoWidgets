using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace YoWidgets.Core;

public sealed class ContentStore
{
    readonly string root;
    readonly string statePath;
    readonly SemaphoreSlim operations = new(1, 1);
    readonly object stateLock = new();
    List<InstalledContent> installed = [];
    public string RecoveryMessage { get; private set; } = "";
    public IReadOnlyList<InstalledContent> Installed { get { lock (stateLock) return installed.ToArray(); } }
    public ContentStore(string directory)
    {
        root = Path.GetFullPath(directory); Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("The content directory must not be a link.");
        statePath = Path.Combine(root, "installed.json");
        foreach (var file in new[] { statePath, statePath + ".bak" })
        {
            if (!File.Exists(file)) continue;
            try
            {
                var value = JsonSerializer.Deserialize<List<InstalledContent>>(File.ReadAllText(file), CatalogJson.Options) ?? throw new InvalidDataException("Invalid installed content registry.");
                foreach (var record in value)
                    if (record is null || record.Item is null || !CatalogRules.ValidId(record.Item.Id) || record.Packages is null || record.Packages.Any(p => p is null || !CatalogRules.ValidId(p.Id) || !CatalogRules.ValidVersion(p.Version) || p.Sha256 is null))
                        throw new InvalidDataException("Invalid installed content registry.");
                CatalogRules.Validate(new CatalogDocument{Items=value.Select(r=>r.Item).ToList()});
                installed = value.DistinctBy(i => i.Item.Id).ToList(); break;
            }
            catch (Exception e) when (e is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
            { RecoveryMessage = "The content registry could not be read. A backup was checked; reinstall missing items."; }
        }
    }
    public bool IsInstalled(string id) { lock (stateLock) return installed.Any(i => i.Item.Id == id); }
    public WidgetDefinition ReadWidget(string id)
    {
        var record=Installed.FirstOrDefault(i=>i.Item.Id==id)??throw new InvalidOperationException("Install this widget first.");
        foreach(var reference in record.Packages)ValidatePayload(Path.GetDirectoryName(PackageFile(reference.Id,reference.Version,"widget.json"))!,new(){Id=reference.Id});
        var package=record.Packages.FirstOrDefault(p=>p.Id==record.Item.Pack+"."+record.Item.Kind)??throw new InvalidDataException("The widget definition is missing.");
        using var document=JsonDocument.Parse(File.ReadAllText(PackageFile(package.Id,package.Version,"widget.json")));var value=document.RootElement;
        var kind=value.GetProperty("kind").GetString();var pack=value.GetProperty("pack").GetString();var size=value.TryGetProperty("defaultSize",out var requested)?requested.GetString()??"M":"M";
        if(kind!=record.Item.Kind||pack!=record.Item.Pack||size is not("S" or "M" or "L"))throw new InvalidDataException("The widget resources do not match their catalog entry. Reinstall this widget.");
        return new(kind!,pack!,size);
    }
    public bool IsReady(string id)
    {
        try{ReadWidget(id);return true;}catch(Exception e)when(e is IOException or InvalidDataException or InvalidOperationException or JsonException or KeyNotFoundException or ArgumentException){return false;}
    }
    public void ImportLegacy(CatalogDocument catalog)
    {
        if (File.Exists(statePath)) return;
        CatalogRules.Validate(catalog);
        var next = new List<InstalledContent>();
        foreach (var item in catalog.Items.Where(i => i.Type == "widget" && i.Pack is "glass" or "liquid"))
        {
            var references = new List<ContentReference>();
            foreach (var package in CatalogRules.Resolve(catalog, item.Id).Packages)
            {
                var directory = Inside(Path.Combine("packages", package.Id, "0.5.2")); Directory.CreateDirectory(directory);
                if (package.Id.StartsWith("style."))
                {
                    var style = WidgetPacks.Get(item.Pack);
                    File.WriteAllText(Path.Combine(directory, "style.json"), JsonSerializer.Serialize(new { id = item.Pack, blur = style.Blur, interactionBlur = style.InteractionBlur, tint = style.Tint, liquid = style.Liquid }, CatalogJson.Options));
                }
                else File.WriteAllText(Path.Combine(directory, "widget.json"), JsonSerializer.Serialize(new { kind = item.Kind, pack = item.Pack }, CatalogJson.Options));
                File.WriteAllText(Path.Combine(directory, ".sha256"), "migrated-from-0.5.2");
                references.Add(new(package.Id, "0.5.2", ""));
            }
            next.Add(new(item with { Version = "0.5.2" }, references, DateTimeOffset.UtcNow));
        }
        Save(next);
    }
    public string PackageFile(string id, string version, string file)
    {
        if (!CatalogRules.ValidId(id) || !CatalogRules.ValidVersion(version) || file != Path.GetFileName(file)) throw new ArgumentException("Invalid package path.");
        return Inside(Path.Combine("packages", id, version, file));
    }
    string Inside(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid content path.");
        var current = path;
        while (current.Length > root.Length)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked content paths are not supported.");
            current = Path.GetDirectoryName(current)!;
        }
        return path;
    }
    void Save(List<InstalledContent> next)
    {
        var temp = Inside("installed.json.tmp");
        File.WriteAllText(temp, JsonSerializer.Serialize(next, CatalogJson.Options));
        if (File.Exists(statePath)) File.Copy(statePath, statePath + ".bak", true);
        File.Move(temp, statePath, true);
        lock (stateLock) installed = next;
    }
    bool Present(ContentPackage package)
    {
        try{var marker = PackageFile(package.Id, package.Version, ".sha256");if(!File.Exists(marker)||!File.ReadAllText(marker).Trim().Equals(package.Sha256,StringComparison.OrdinalIgnoreCase))return false;ValidatePayload(Path.GetDirectoryName(marker)!,package);return true;}
        catch(Exception e)when(e is IOException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException){return false;}
    }
    public async Task InstallAsync(CatalogDocument catalog, string itemId, Func<ContentPackage, CancellationToken, Task<Stream>> fetch, Action<InstallProgress>? progress, CancellationToken token)
    {
        var resolution = CatalogRules.Resolve(catalog, itemId);
        await operations.WaitAsync(token).ConfigureAwait(false);
        var staging = Inside(Path.Combine("staging", Guid.NewGuid().ToString("N")));
        try
        {
            token.ThrowIfCancellationRequested();
            var needed = resolution.Packages.Where(p => !Present(p)).ToArray();
            var total = needed.Sum(p => p.Bytes); long done = 0;
            Directory.CreateDirectory(staging);
            foreach (var package in needed)
            {
                token.ThrowIfCancellationRequested();
                var archivePath = Path.Combine(staging, package.Id + ".zip");
                await using (var input = await fetch(package, token).ConfigureAwait(false))
                await using (var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    var buffer = new byte[81920]; long bytes = 0; int read;
                    while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                    {
                        bytes += read;
                        if (bytes > package.Bytes) throw new InvalidDataException("The download exceeds its declared size.");
                        hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                        progress?.Invoke(new(itemId, "downloading", done + bytes, total, package.Id));
                    }
                    if (bytes != package.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package verification failed. Retry the download.");
                }
                progress?.Invoke(new(itemId, "verifying", done + package.Bytes, total, package.Id));
                var unpack = Path.Combine(staging, package.Id); Directory.CreateDirectory(unpack);
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    if (archive.Entries.Count is < 1 or > 128) throw new InvalidDataException("The package contains too many files.");
                    long expanded = 0;
                    foreach (var entry in archive.Entries)
                    {
                        token.ThrowIfCancellationRequested();
                        var name = entry.FullName;
                        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || name.Contains(':') || name.StartsWith('/') || name.TrimEnd('/').Split('/').Any(p => p is "." or ".." or "") || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                            throw new InvalidDataException("The package contains an unsafe path.");
                        if(name.EndsWith('/')){Directory.CreateDirectory(Path.Combine(unpack,name));continue;}
                        if (!new[] { ".json", ".png", ".jpg", ".webp", ".svg" }.Contains(Path.GetExtension(name).ToLowerInvariant())) throw new InvalidDataException("Executable package content is not supported.");
                        expanded += entry.Length;
                        if (entry.Length > 16 * 1024 * 1024 || expanded > 64 * 1024 * 1024) throw new InvalidDataException("Expanded package is too large.");
                        var target = Path.GetFullPath(Path.Combine(unpack, name));
                        if (!target.StartsWith(unpack + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid package path.");
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        await using var input=entry.Open();await using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,32768,true);var buffer=new byte[32768];long actual=0;int read;
                        while((read=await input.ReadAsync(buffer,token).ConfigureAwait(false))>0){actual+=read;if(actual>entry.Length||actual>16*1024*1024)throw new InvalidDataException("Expanded package exceeds its declared size.");await output.WriteAsync(buffer.AsMemory(0,read),token).ConfigureAwait(false);}
                        if(actual!=entry.Length)throw new InvalidDataException("Expanded package size is invalid.");
                    }
                }
                ValidatePayload(unpack, package);
                File.WriteAllText(Path.Combine(unpack, ".sha256"), package.Sha256);
                done += package.Bytes;
            }
            token.ThrowIfCancellationRequested();
            progress?.Invoke(new(itemId, "installing", total, total));
            foreach (var package in needed)
            {
                var destination = Inside(Path.Combine("packages", package.Id, package.Version));
                if (Directory.Exists(destination))
                {
                    if(!Present(package))
                    {
                        var marker=PackageFile(package.Id,package.Version,".sha256");if(!File.Exists(marker)||!File.ReadAllText(marker).Trim().Equals(package.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("An existing package version has different contents. Publish a new version.");
                        var backup=Path.Combine(staging,"repair-"+Guid.NewGuid().ToString("N"));Directory.Move(destination,backup);
                        try{Directory.Move(Path.Combine(staging,package.Id),destination);}catch{Directory.Move(backup,destination);throw;}
                    }
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Directory.Move(Path.Combine(staging, package.Id), destination);
            }
            var next = Installed.Where(i => !resolution.Items.Any(n => n.Id == i.Item.Id)).ToList();
            foreach (var item in resolution.Items)
            {
                var refs = CatalogRules.Resolve(catalog, item.Id).Packages.Select(p => new ContentReference(p.Id, p.Version, p.Sha256)).ToList();
                next.Add(new(item, refs, DateTimeOffset.UtcNow));
            }
            Save(next);
            progress?.Invoke(new(itemId, "completed", total, total));
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch (IOException) { }
            operations.Release();
        }
    }
    static void ValidatePayload(string directory, ContentPackage package)
    {
        try{ValidatePayloadCore(directory,package);}catch(Exception e)when(e is JsonException or KeyNotFoundException or InvalidOperationException){throw new InvalidDataException("Package metadata is invalid.",e);}
    }
    static void ValidatePayloadCore(string directory, ContentPackage package)
    {
        var style = Path.Combine(directory, "style.json"); var widget = Path.Combine(directory, "widget.json");
        if (package.Id.StartsWith("style.", StringComparison.Ordinal))
        {
            if (!File.Exists(style)) throw new InvalidDataException("Style resources are missing.");
            if(new FileInfo(style).Length>65536)throw new InvalidDataException("Style metadata is too large.");
            using var document = JsonDocument.Parse(File.ReadAllText(style)); var value = document.RootElement;var id=value.GetProperty("id").GetString();
            if (!CatalogRules.ValidId(id) || package.Id!="style."+id || !value.GetProperty("blur").TryGetDouble(out var blur) || !double.IsFinite(blur) || blur is < 0 or > 20 || !value.GetProperty("interactionBlur").TryGetDouble(out var interaction) || !double.IsFinite(interaction) || interaction is < 0 or > 20 || !value.GetProperty("tint").TryGetDouble(out var tint) || !double.IsFinite(tint) || tint is < 0 or > .7)
                throw new InvalidDataException("Invalid style parameters.");
            _=value.GetProperty("liquid").GetBoolean();
        }
        else
        {
            if (!File.Exists(widget)) throw new InvalidDataException("Widget resources are missing.");
            if(new FileInfo(widget).Length>65536)throw new InvalidDataException("Widget metadata is too large.");
            using var document = JsonDocument.Parse(File.ReadAllText(widget));
            var kind=document.RootElement.GetProperty("kind").GetString();var pack=document.RootElement.GetProperty("pack").GetString();
            if (!CatalogRules.WidgetKinds.Contains(kind) || !CatalogRules.ValidId(pack) || package.Id!=pack+"."+kind) throw new InvalidDataException("The widget engine is not supported.");
            if(document.RootElement.TryGetProperty("defaultSize",out var size)&&size.GetString() is not("S" or "M" or "L"))throw new InvalidDataException("The default widget size is not supported.");
        }
    }
    public void Remove(string itemId) => RemoveMany([itemId]);
    public void RemoveMany(IEnumerable<string> ids)
    {
        if (!operations.Wait(0)) throw new InvalidOperationException("Wait for the current installation to finish.");
        try
        {
            var remove = ids.ToHashSet(); var previous = Installed;
            var next = previous.Where(i => !remove.Contains(i.Item.Id)).ToList();
            Save(next);
            var keep = next.SelectMany(i => i.Packages).Select(p => p.Id + "/" + p.Version).ToHashSet();
            foreach (var reference in previous.SelectMany(i => i.Packages).DistinctBy(p => p.Id + "/" + p.Version))
                if (!keep.Contains(reference.Id + "/" + reference.Version))
                    try { var directory = Inside(Path.Combine("packages", reference.Id, reference.Version)); if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch (IOException) { RecoveryMessage = "Some unused resources are still in use and will be cleaned later."; }
        }
        finally { operations.Release(); }
    }
}
