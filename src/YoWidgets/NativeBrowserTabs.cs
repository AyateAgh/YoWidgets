using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Automation;
namespace YoWidgets;

// Read browser chrome only during discovery. A page's accessibility tree is
// inspected only after the user attaches that particular tab and allows access.
internal sealed class NativeBrowserTabs
{
    sealed record Entry(BrowserTab Tab,nint Window,int Pid,int[] RuntimeId);
    readonly Dictionary<string,Entry> entries=[];
    readonly Dictionary<string,string> authorized=[];
    readonly SemaphoreSlim gate=new(1);
    internal IReadOnlyList<BrowserTab> Tabs=>entries.Values.Select(e=>e.Tab).Take(100).ToArray();
    internal async Task RefreshAsync()
    {
        if(!await gate.WaitAsync(0))return;
        // Keep the single worker occupied even when a provider is slow; do not
        // accumulate abandoned UIAutomation workers on repeated menu openings.
        var worker=Task.Run(Scan);_ =worker.ContinueWith(_=>gate.Release(),TaskScheduler.Default);
        try {var found=await worker.WaitAsync(TimeSpan.FromSeconds(3));entries.Clear();foreach(var entry in found)entries[entry.Tab.Id]=entry;}
        catch(Exception e) when(e is TimeoutException or ElementNotAvailableException or InvalidOperationException or COMException){}
    }
    static List<Entry> Scan()
    {
        var found=new List<Entry>();
        EnumWindows((hwnd,_)=> {
            if(!IsWindowVisible(hwnd)||IsIconic(hwnd))return true;GetWindowThreadProcessId(hwnd,out var pid);
            try {
                using var process=Process.GetProcessById((int)pid);if(process.ProcessName.ToLowerInvariant() is not ("chrome" or "msedge"))return true;
                var root=AutomationElement.FromHandle(hwnd);foreach(var element in ChromeTree(root)) {
                    if(element.Current.ControlType!=ControlType.TabItem)continue;var title=element.Current.Name;if(string.IsNullOrWhiteSpace(title))continue;
                    var runtime=element.GetRuntimeId();var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("-",runtime))))[..16];var id="native/"+hwnd.ToInt64()+"/"+hash;
                    var url=element.TryGetCurrentPattern(SelectionItemPattern.Pattern,out var pattern)&&((SelectionItemPattern)pattern).Current.IsSelected?Address(root):"";
                    found.Add(new(new(id,title,url,process.ProcessName=="chrome"?"Chrome":"Edge"),hwnd,(int)pid,runtime));if(found.Count>=100)return false;
                }
            }catch(Exception e) when(e is ArgumentException or ElementNotAvailableException or InvalidOperationException or COMException){}return true;
        },0);return found;
    }
    static IEnumerable<AutomationElement> ChromeTree(AutomationElement root)
    {
        var nodes=new Queue<(AutomationElement,int)>();nodes.Enqueue((root,0));var walker=TreeWalker.ControlViewWalker;var count=0;
        while(nodes.TryDequeue(out var entry)&&count++<1200) {
            var (element,depth)=entry;if(element.Current.ControlType==ControlType.Document)continue;yield return element;
            if(depth>=14)continue;for(var child=walker.GetFirstChild(element);child!=null&&nodes.Count<1200;child=walker.GetNextSibling(child))nodes.Enqueue((child,depth+1));
        }
    }
    static string Address(AutomationElement root)
    {
        // Chromium exposes the full document URL through ValuePattern even when
        // its address bar visually elides the scheme or trailing slash.
        var document=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));
        if(document!=null&&document.TryGetCurrentPattern(ValuePattern.Pattern,out var documentValue)) {
            var url=((ValuePattern)documentValue).Current.Value;if(BrowserAssistantBridge.ValidUrl(url))return new Uri(url).AbsoluteUri;
        }
        foreach(var element in ChromeTree(root)) {
            if(element.Current.ControlType!=ControlType.Edit||element.Current.IsPassword||!element.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern))continue;
            var value=((ValuePattern)pattern).Current.Value.Trim();if(BrowserAssistantBridge.ValidUrl(value))return new Uri(value).AbsoluteUri;
        }return "";
    }
    Entry Get(string id)
    {if(!entries.TryGetValue(id,out var entry)||!IsWindowVisible(entry.Window)||IsIconic(entry.Window))throw new InvalidOperationException("This tab is no longer available. Attach it again.");GetWindowThreadProcessId(entry.Window,out var pid);if(pid!=entry.Pid)throw new InvalidOperationException("The browser window changed.");return entry;}
    static AutomationElement Select(Entry entry)
    {
        var root=AutomationElement.FromHandle(entry.Window);var tab=ChromeTree(root).FirstOrDefault(e=>e.Current.ControlType==ControlType.TabItem&&e.GetRuntimeId().SequenceEqual(entry.RuntimeId));
        if(tab==null||!tab.TryGetCurrentPattern(SelectionItemPattern.Pattern,out var pattern))throw new InvalidOperationException("Windows cannot select this tab. Use the browser companion instead.");
        ((SelectionItemPattern)pattern).Select();return root;
    }
    internal async Task<BrowserTab> AuthorizeAsync(BrowserTab tab)
    {
        var entry=Get(tab.Id);var url=await Task.Run(()=>Address(Select(entry))).WaitAsync(TimeSpan.FromSeconds(4));
        if(!BrowserAssistantBridge.ValidUrl(url))throw new InvalidOperationException("Windows did not expose this tab's HTTP/HTTPS address. Connect the browser companion for this browser.");
        authorized[tab.Id]=url;return entry.Tab with {Url=url};
    }
    internal string WindowId(string id)=>Get(id).Window.ToInt64().ToString();
    internal async Task<string> VerifyAsync(string id,CancellationToken token)
    {
        if(!authorized.TryGetValue(id,out var expected))throw new InvalidOperationException("Attach this browser tab first.");var entry=Get(id);
        var url=await Task.Run(()=>Address(Select(entry)),token).WaitAsync(TimeSpan.FromSeconds(4),token);
        if(url!=expected){authorized.Remove(id);throw new InvalidOperationException("The attached tab navigated. Attach it again before reading or controlling it.");}return url;
    }
    delegate bool EnumProc(nint hwnd,nint parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,nint parameter);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
}
