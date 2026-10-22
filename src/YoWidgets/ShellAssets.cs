using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
namespace YoWidgets;

internal static class ShellAssets
{
    internal static string Ensure()
    {
        using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("YoWidgets.PremiumUi.zip")??throw new InvalidOperationException("The interface bundle is missing. Run scripts/build-ui.ps1 and rebuild YoWidgets.");
        using var data=new MemoryStream();resource.CopyTo(data);var bytes=data.ToArray();var hash=Convert.ToHexString(SHA256.HashData(bytes))[..20];
        var cache=App.OwnedDirectory("Cache");var root=Path.Combine(cache,"UI-assets");Directory.CreateDirectory(root);var target=Path.Combine(root,hash);
        if(File.Exists(Path.Combine(target,".ready")))return target;
        var staging=Path.Combine(root,hash+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        try
        {
            using var archive=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
            foreach(var entry in archive.Entries)
            {
                if(entry.FullName.EndsWith('/'))continue;
                var file=Path.GetFullPath(Path.Combine(staging,entry.FullName));if(!file.StartsWith(staging+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid interface bundle.");
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);entry.ExtractToFile(file);
            }
            File.WriteAllText(Path.Combine(staging,".ready"),hash);Directory.Move(staging,target);return target;
        }
        finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
    }
    internal static bool ProfileNeedsMaintenance(string profile)
    {
        if(!Directory.Exists(profile))return false;long size=0;var count=0;
        try{foreach(var file in Directory.EnumerateFiles(profile,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint,IgnoreInaccessible=true})){if(++count>12000)return true;size+=new FileInfo(file).Length;if(size>100*1024*1024)return true;}}catch(IOException){}
        return false;
    }
}
