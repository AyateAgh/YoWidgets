using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace YoWidgets.Core;

// A local bundle is declarative content, never a downloaded code extension.
public sealed class LocalContentBundle:IDisposable
{
    readonly FileStream file;readonly ZipArchive zip;
    public CatalogDocument Catalog {get;}
    public string Hash {get;}
    public long Bytes=>file.Length;
    public string RootId {get;}
    public LocalContentBundle(string path,string? expectedHash=null)
    {
        file=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        try{
            if(file.Length is <22 or >128*1024*1024)throw new InvalidDataException("Choose a package ZIP smaller than 128 MB.");
            Hash=Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();file.Position=0;
            if(expectedHash!=null&&!Hash.Equals(expectedHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("This archive changed after preview. Choose it again.");
            zip=new(file,ZipArchiveMode.Read,true);
            if(zip.Entries.Count>602||zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=zip.Entries.Count)throw new InvalidDataException("The archive has too many or duplicate entries.");
            foreach(var entry in zip.Entries){var name=entry.FullName;if(name=="packages/"&&entry.Length==0)continue;if(name!="catalog.json"&&!(name.StartsWith("packages/",StringComparison.Ordinal)&&name.EndsWith(".zip",StringComparison.Ordinal)&&name.Count(c=>c=='/')==1))throw new InvalidDataException("Use catalog.json and packages/<package-id>.zip. Extra archive paths are not supported.");if(name.Contains("..")||name.Contains('\\')||entry.Length>64*1024*1024)throw new InvalidDataException("Invalid local package path or size.");}
            var manifest=zip.GetEntry("catalog.json")??throw new InvalidDataException("catalog.json is missing from this ZIP.");
            var document=JsonSerializer.Deserialize<CatalogDocument>(Read(manifest,4*1024*1024),CatalogJson.Options)??throw new InvalidDataException("The catalog is empty.");
            if(document.Items is null||document.Packages is null||document.Items.Count>200||document.Packages.Count>600)throw new InvalidDataException("The local catalog is too large.");
            document=document with{Packages=document.Packages.Select(p=>p is null?throw new InvalidDataException("Empty package."):p with{Url="https://local.invalid/"+p.Id+".zip"}).ToList()};
            CatalogRules.Validate(document);
            if(document.Items.Count==0||document.Items.Any(i=>i.Pack is "glass" or "liquid"))throw new InvalidDataException("Custom collections must use their own ID, for example aurora, rather than glass or liquid.");
            var owned=document.Items.Where(i=>i.Type=="widget").SelectMany(i=>new[]{"style."+i.Pack,i.Pack+"."+i.Kind}).ToHashSet(StringComparer.Ordinal);
            if(document.Packages.Any(p=>!owned.Contains(p.Id)||p.Dependencies.Any(d=>!owned.Contains(d))))throw new InvalidDataException("A custom archive may contain only resources belonging to its listed widgets.");
            if(document.Packages.Sum(p=>p.Bytes)>128*1024*1024)throw new InvalidDataException("The expanded bundle is too large.");
            foreach(var p in document.Packages){var entry=zip.GetEntry("packages/"+p.Id+".zip")??throw new InvalidDataException("A declared package is missing: "+p.Id);if(entry.Length!=p.Bytes)throw new InvalidDataException("A package size does not match its manifest.");}
            if(zip.Entries.Count(e=>!e.FullName.EndsWith("/"))!=document.Packages.Count+1)throw new InvalidDataException("The ZIP contains undeclared resources.");
            foreach(var item in document.Items)CatalogRules.Resolve(document,item.Id);
            RootId="import-"+Hash[..16];if(document.Items.Any(i=>i.Id==RootId))throw new InvalidDataException("Reserved import identifier.");
            var root=new CatalogItem{Id=RootId,Title="Imported collection",Type="pack",Pack=document.Items[0].Pack,Includes=document.Items.Where(i=>i.Type=="widget").Select(i=>i.Id).ToArray()};
            Catalog=document with{Items=[..document.Items,root]};CatalogRules.Resolve(Catalog,RootId);
        }catch{file.Dispose();throw;}
    }
    static byte[] Read(ZipArchiveEntry entry,long limit)
    {
        if(entry.Length>limit)throw new InvalidDataException("A local resource is too large.");
        using var source=entry.Open();using var target=new MemoryStream();var buffer=new byte[32768];int count;
        while((count=source.Read(buffer))>0){if(target.Length+count>limit)throw new InvalidDataException("A local resource exceeds its declared size.");target.Write(buffer,0,count);}
        if(target.Length!=entry.Length)throw new InvalidDataException("Truncated local resource.");return target.ToArray();
    }
    public Task InstallAsync(ContentStore store,Action<InstallProgress>? progress,CancellationToken token)=>store.InstallAsync(Catalog,RootId,(package,ct)=>{ct.ThrowIfCancellationRequested();return Task.FromResult<Stream>(new MemoryStream(Read(zip.GetEntry("packages/"+package.Id+".zip")!,package.Bytes)));},progress,token);
    public void Dispose(){zip.Dispose();file.Dispose();}
}
