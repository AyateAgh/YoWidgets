using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using YoWidgets.Core;
internal static class LocalBundleChecks
{
    internal static async Task Run(Func<string,Func<Task>,Task> test,string root)
    {
        byte[] Zip(Dictionary<string,byte[]> files){using var data=new MemoryStream();using(var zip=new ZipArchive(data,ZipArchiveMode.Create,true))foreach(var pair in files){var entry=zip.CreateEntry(pair.Key);using var output=entry.Open();output.Write(pair.Value);}return data.ToArray();}
        byte[] Json(object value)=>JsonSerializer.SerializeToUtf8Bytes(value,CatalogJson.Options);
        string Bundle(bool corrupt=false,bool executable=false,bool traversal=false,bool foreign=false){
            var style=Zip(new(){["style.json"]=Json(new{id="aurora",name="Aurora",blur=2,interactionBlur=2,tint=.1,liquid=false})});
            var files=new Dictionary<string,byte[]>{["widget.json"]=Json(new{kind="clock",pack="aurora",defaultSize="S"})};if(executable)files["payload.dll"]=[1,2,3];var clock=Zip(files);
            ContentPackage Package(string id,byte[] value,string[] dependencies)=>new(){Id=id,Version="1.0.0",Bytes=value.Length,Sha256=Convert.ToHexString(SHA256.HashData(value)),Dependencies=dependencies};
            var catalog=new CatalogDocument{Items=[new(){Id="aurora-clock",Pack="aurora",Title="Aurora Clock",Packages=["aurora.clock"]}],Packages=[Package("style.aurora",style,[]),Package("aurora.clock",clock,["style.aurora"])]};
            if(foreign){var extra=Zip(new(){["style.json"]=Json(new{id="glass",blur=20,interactionBlur=20,tint=.7,liquid=false})});catalog.Packages.Add(Package("style.glass",extra,[]));catalog.Packages[1]=catalog.Packages[1] with{Dependencies=["style.aurora","style.glass"]};}
            if(corrupt)catalog=catalog with{Packages=[catalog.Packages[0],catalog.Packages[1] with{Sha256=new string('0',64)}]};
            var entries=new Dictionary<string,byte[]>{["catalog.json"]=Json(catalog),["packages/style.aurora.zip"]=style,["packages/aurora.clock.zip"]=clock};if(foreign)entries["packages/style.glass.zip"]=Zip(new(){["style.json"]=Json(new{id="glass",blur=20,interactionBlur=20,tint=.7,liquid=false})});if(traversal)entries["../outside.json"]=[1];var path=Path.Combine(root,"bundle-"+Guid.NewGuid().ToString("N")+".zip");File.WriteAllBytes(path,Zip(entries));return path;
        }
        async Task Reject(Func<Task> action){try{await action();}catch(InvalidDataException){return;}throw new Exception("Invalid custom package was accepted");}
        await test("local bundle installs actual compatible resources without any network",async()=>{using var bundle=new LocalContentBundle(Bundle());var store=new ContentStore(Path.Combine(root,"local-content"));await bundle.InstallAsync(store,null,CancellationToken.None);var widget=store.ReadWidget("aurora-clock");if(widget.Pack!="aurora"||widget.DefaultSize!="S")throw new Exception("Imported definition was not consumed");});
        await test("local bundle checksum and executable rejection leave registry unchanged",async()=>{foreach(var mode in new[]{0,1}){using var bundle=new LocalContentBundle(Bundle(corrupt:mode==0,executable:mode==1));var store=new ContentStore(Path.Combine(root,"invalid-local-"+mode));await Reject(()=>bundle.InstallAsync(store,null,CancellationToken.None));if(store.Installed.Count!=0)throw new Exception("Failed import was registered");}});
        await test("imported dependencies cannot override another collection",()=>Reject(()=>{using var bundle=new LocalContentBundle(Bundle(foreign:true));return Task.CompletedTask;}));
        await test("outer traversal and changed preview fingerprints are rejected",async()=>{await Reject(()=>{using var bundle=new LocalContentBundle(Bundle(traversal:true));return Task.CompletedTask;});await Reject(()=>{using var bundle=new LocalContentBundle(Bundle(),new string('0',64));return Task.CompletedTask;});});
    }
}
