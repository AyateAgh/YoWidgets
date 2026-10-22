using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
namespace YoWidgets;

internal static class BrowserCompanionSetup
{
    internal static string PairingToken()
    {
        var path=Path.Combine(App.DataRoot,"browser-companion.key");var entropy=Encoding.UTF8.GetBytes("YoWidgets.Browser.v1");Directory.CreateDirectory(App.DataRoot);
        try {if(File.Exists(path))return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),entropy,DataProtectionScope.CurrentUser));}catch(CryptographicException){}
        var token=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(token),entropy,DataProtectionScope.CurrentUser));return token;
    }
    internal static string Export()
    {
        var directory=Path.Combine(App.OwnedDirectory("integrations"),"assistant-browser-bridge");Directory.CreateDirectory(directory);var assembly=typeof(App).Assembly;
        foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("AssistantBridge.",StringComparison.Ordinal))) {
            using var stream=assembly.GetManifestResourceStream(name)!;using var output=File.Create(Path.Combine(directory,name["AssistantBridge.".Length..]));stream.CopyTo(output);
        }return directory;
    }
    internal static void Show()
    {
        var directory=Export();App.CurrentApp.Manager.Data.Browser.Start();
        var window=new Window {Title="Connect browser",Width=560,SizeToContent=SizeToContent.Height,MaxHeight=SystemParameters.WorkArea.Height*.9,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Ui.Brush("Background"),Foreground=Ui.Brush("Text")};
        var content=Ui.Stack(Ui.Text("Connect your browser",24),Ui.Gap(14),Ui.Text("1. Open chrome://extensions or edge://extensions.\n2. Enable Developer mode and choose Load unpacked.\n3. Select the companion folder below and review its permissions.\n4. Open the extension, paste your pairing code and connect.",14),Ui.Gap(14),Ui.Text("Only attached tabs can be read by AI. Browser clicks and typing ask for desktop approval. Your API key stays in YoWidgets.",12,"Muted"),Ui.Gap(16));
        content.Children.Add(Ui.Button("Open companion folder",()=>{var start=new ProcessStartInfo("explorer.exe") {UseShellExecute=true};start.ArgumentList.Add(directory);Process.Start(start);}));
        content.Children.Add(Ui.Button("Copy folder path",()=>Clipboard.SetText(directory)));
        content.Children.Add(Ui.Button("Copy pairing code",()=>Clipboard.SetText(PairingToken()),true));window.Content=new ScrollViewer {Content=new Border {Child=content,Padding=new Thickness(24)},VerticalScrollBarVisibility=ScrollBarVisibility.Auto};window.Show();
    }
}
