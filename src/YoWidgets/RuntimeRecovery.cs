using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
namespace YoWidgets;

internal static class RuntimeRecovery
{
    internal static async Task Install()
    {
        var directory=Path.Combine(App.OwnedDirectory("Cache"),"Runtime");Directory.CreateDirectory(directory);var path=Path.Combine(directory,"MicrosoftEdgeWebview2Setup.exe");
        using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(90)};
        using var response=await http.GetAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703",HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
        await using(var input=await response.Content.ReadAsStreamAsync())await using(var output=File.Create(path))
        {var buffer=new byte[32768];long length=0;int read;while((read=await input.ReadAsync(buffer))>0){length+=read;if(length>8*1024*1024)throw new InvalidDataException("Unexpected installer size.");await output.WriteAsync(buffer.AsMemory(0,read));}}
        if(!Trusted(path))throw new InvalidDataException("The Microsoft installer signature could not be verified.");
        var start=new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add("/silent");start.ArgumentList.Add("/install");
        using var process=Process.Start(start)??throw new InvalidOperationException("Installer could not start.");await process.WaitForExitAsync();if(process.ExitCode!=0)throw new InvalidOperationException("WebView2 installation was not completed.");
        File.Delete(path);
    }
    static bool Trusted(string path)
    {
        var info=new FileInfoData{Size=(uint)Marshal.SizeOf<FileInfoData>(),Path=path};var pointer=Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoData>());
        try
        {
            Marshal.StructureToPtr(info,pointer,false);var trust=new TrustData{Size=(uint)Marshal.SizeOf<TrustData>(),UiChoice=2,RevocationChecks=0,UnionChoice=1,File=pointer,ProviderFlags=0x00001000};var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            if(WinVerifyTrust(new IntPtr(-1),ref action,ref trust)!=0)return false;
#pragma warning disable SYSLIB0057
            using var certificate=new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            return certificate.GetNameInfo(X509NameType.SimpleName,false)=="Microsoft Corporation";
        }
        finally{Marshal.DestroyStructure<FileInfoData>(pointer);Marshal.FreeHGlobal(pointer);}
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct FileInfoData{public uint Size;[MarshalAs(UnmanagedType.LPWStr)]public string Path;public IntPtr File,KnownSubject;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct TrustData{public uint Size;public IntPtr PolicyCallback,SipClient;public uint UiChoice,RevocationChecks,UnionChoice;public IntPtr File;public uint StateAction;public IntPtr StateData,Url;public uint ProviderFlags,UiContext;public IntPtr SignatureSettings;}
    [DllImport("wintrust.dll",ExactSpelling=true,SetLastError=true)]static extern int WinVerifyTrust(IntPtr hwnd,ref Guid action,ref TrustData data);
}
