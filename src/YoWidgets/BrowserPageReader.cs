using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace YoWidgets;

// Only this fixed DOM driver is executable; model arguments are JSON data.
internal sealed class BrowserPageReader : IDisposable
{
    sealed record Page(string Id,WebView2 View,TabItem Tab);
    readonly Dictionary<string,bool> checkedHosts=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Page> pages=[];
    Window? window; TabControl? tabs; TextBlock? address; CoreWebView2Environment? environment;
    internal event Action? Changed;
    internal IReadOnlyList<BrowserTab> Tabs=>pages.Values.Where(p=>BrowserAssistantBridge.ValidUrl(p.View.Source?.AbsoluteUri??"")).Select(p=>new BrowserTab(p.Id,p.View.CoreWebView2?.DocumentTitle??"Browser",p.View.Source!.AbsoluteUri,"YoWidgets")).ToArray();
    internal bool Contains(string id)=>pages.ContainsKey(id);
    internal static bool PublicAddress(IPAddress ip)
    {
        if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();if(IPAddress.IsLoopback(ip)||ip.Equals(IPAddress.Any)||ip.Equals(IPAddress.IPv6Any)||ip.IsIPv6LinkLocal||ip.IsIPv6Multicast||ip.IsIPv6SiteLocal)return false;
        var b=ip.GetAddressBytes();if(ip.AddressFamily==AddressFamily.InterNetworkV6)return (b[0]&0xfe)!=0xfc;
        return b[0] is not (0 or 10 or 127)&&!(b[0]==100&&b[1]>=64&&b[1]<=127)&&!(b[0]==169&&b[1]==254)&&!(b[0]==172&&b[1]>=16&&b[1]<=31)&&!(b[0]==192&&b[1] is 0 or 168)&&!(b[0]==198&&b[1] is 18 or 19)&&b[0]<224;
    }
    async Task<bool> Allowed(string value,CancellationToken token)
    {
        if(!BrowserAssistantBridge.ValidUrl(value))return false;var uri=new Uri(value);
        if(checkedHosts.TryGetValue(uri.DnsSafeHost,out var known))return known;
        try {var ips=await Dns.GetHostAddressesAsync(uri.DnsSafeHost,token);var allowed=ips.Length>0&&ips.All(PublicAddress);checkedHosts[uri.DnsSafeHost]=allowed;return allowed;}catch(SocketException){return false;}
    }
    async Task<Page> NewPage(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(window==null) {
            tabs=new TabControl {Margin=new Thickness(10,0,10,10)};tabs.SetResourceReference(FrameworkElement.StyleProperty,"AssistantBrowserTabs");address=Ui.Text("",12,"Muted");address.Margin=new Thickness(16,12,16,12);address.TextWrapping=TextWrapping.NoWrap;address.TextTrimming=TextTrimming.CharacterEllipsis;
            var panel=new DockPanel();DockPanel.SetDock(address,Dock.Top);panel.Children.Add(address);panel.Children.Add(tabs);
            window=new Window {Title="Browser · YoWidgets",Width=Math.Min(1100,SystemParameters.WorkArea.Width*.88),Height=Math.Min(800,SystemParameters.WorkArea.Height*.88),WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Ui.Brush("Background"),Content=panel};
            tabs.SelectionChanged+=(_,_)=>UpdateAddress();window.Closed+=(_,_)=> {foreach(var p in pages.Values)p.View.Dispose();pages.Clear();window=null;tabs=null;address=null;Changed?.Invoke();};window.Show();
        }
        if(pages.Count>=12)throw new InvalidOperationException("Close the browser window before opening more than 12 tabs.");
        var view=new WebView2 {DefaultBackgroundColor=System.Drawing.Color.White};var tab=new TabItem {Content=view};tab.SetResourceReference(FrameworkElement.StyleProperty,"AssistantBrowserTab");var page=new Page("owned/"+Guid.NewGuid().ToString("N"),view,tab);
        var label=Ui.Text("New tab",12);label.MaxWidth=180;label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;var header=new DockPanel();var close=WidgetVisuals.IconButton("close","Close browser tab",()=>ClosePage(page.Id),24);close.Content=new PremiumIcon("x",12,"Text");close.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);header.Children.Add(label);tab.Header=header;
        pages.Add(page.Id,page);tabs!.Items.Add(tab);tabs.SelectedItem=tab;
        try {
            environment??=await CoreWebView2Environment.CreateAsync(null,App.OwnedDirectory("browser-profile"));await view.EnsureCoreWebView2Async(environment).WaitAsync(token);
            var core=view.CoreWebView2;core.Profile.PreferredColorScheme=CoreWebView2PreferredColorScheme.Light;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;
            core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;core.NewWindowRequested+=(_,e)=>e.Handled=true;core.DownloadStarting+=(_,e)=>e.Cancel=true;
            core.NavigationStarting+=(_,e)=> {if(e.Uri=="about:blank")return;if(!BrowserAssistantBridge.ValidUrl(e.Uri))e.Cancel=true;};
            core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            core.WebResourceRequested+=async (_,e)=> {if(!e.Request.Uri.StartsWith("http",StringComparison.OrdinalIgnoreCase))return;using var deferred=e.GetDeferral();if(!await Allowed(e.Request.Uri,CancellationToken.None))e.Response=environment.CreateWebResourceResponse(new MemoryStream(),403,"Blocked private resource","");};
            core.DocumentTitleChanged+=(_,_)=> {label.Text=core.DocumentTitle;label.ToolTip=core.DocumentTitle;UpdateAddress();Changed?.Invoke();};core.SourceChanged+=(_,_)=> {UpdateAddress();Changed?.Invoke();};return page;
        }catch {pages.Remove(page.Id);tabs?.Items.Remove(tab);view.Dispose();throw;}
    }
    void ClosePage(string id){if(!pages.Remove(id,out var page))return;tabs?.Items.Remove(page.Tab);page.View.Dispose();Changed?.Invoke();if(pages.Count==0)window?.Close();}
    void UpdateAddress(){if(address!=null)address.Text=pages.Values.FirstOrDefault(p=>p.Tab==tabs?.SelectedItem)?.View.Source?.AbsoluteUri??"";}
    Page Get(string id)
    {if(!pages.TryGetValue(id,out var page)||page.View.CoreWebView2==null)throw new InvalidOperationException("This browser tab was closed. Open or attach a tab again.");tabs!.SelectedItem=page.Tab;window!.Show();return page;}
    internal async Task<JsonElement> OpenAsync(string url,CancellationToken token)
    {
        if(!await Allowed(url,token))throw new InvalidOperationException("Use a public HTTP/HTTPS URL. Attach private/local sites from your browser instead.");
        var page=await NewPage(token);await Navigate(page,url,token);return await Snapshot(page,0,12000,token);
    }
    async Task Navigate(Page page,string url,CancellationToken token)
    {
        var loaded=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);ulong navigation=0;void Started(object? _,CoreWebView2NavigationStartingEventArgs e)=>navigation=e.NavigationId;void Complete(object? _,CoreWebView2NavigationCompletedEventArgs e){if(navigation!=0&&e.NavigationId==navigation)loaded.TrySetResult(e.IsSuccess);}
        var core=page.View.CoreWebView2;core.NavigationStarting+=Started;core.NavigationCompleted+=Complete;
        try {core.Navigate(url);using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(35));if(!await loaded.Task.WaitAsync(deadline.Token))throw new InvalidOperationException("The browser could not load "+url+". The site may block embedded browsers or require sign-in. Use an attached real-browser tab or Computer use instead of claiming it was read.");}
        catch(OperationCanceledException) when(!token.IsCancellationRequested){core.Stop();throw new InvalidOperationException("The webpage did not finish loading in 35 seconds. Try an attached browser tab; no model response timeout was applied.");}
        finally {core.NavigationStarting-=Started;core.NavigationCompleted-=Complete;if(token.IsCancellationRequested)core.Stop();}
    }
    internal async Task<JsonElement> NavigateAsync(string id,string url,CancellationToken token)
    {if(!await Allowed(url,token))throw new InvalidOperationException("Use a public HTTP/HTTPS URL.");var page=Get(id);await Navigate(page,url,token);return await Snapshot(page,0,12000,token);}
    internal async Task<JsonElement> ReadAsync(string url,int offset,int limit,CancellationToken token)
    {
        var page=pages.Values.FirstOrDefault(p=>p.View.Source?.AbsoluteUri==url);
        if(page==null){var opened=await OpenAsync(url,token);page=Get(opened.GetProperty("tab_id").GetString()!);}return await Snapshot(page,offset,limit,token);
    }
    internal Task<JsonElement> ReadTabAsync(string id,int offset,int limit,CancellationToken token)=>Snapshot(Get(id),offset,limit,token);
    Task<JsonElement> Snapshot(Page page,int offset,int limit,CancellationToken token)=>Script(page,"read",new{tabId=page.Id,snapshotId=Guid.NewGuid().ToString("N"),offset=Math.Max(0,offset),limit=Math.Clamp(limit,100,48000)},token);
    internal async Task<JsonElement> ActAsync(string id,string action,string snapshot,int control,string text,int y,CancellationToken token)
    {
        var page=Get(id);if(text.Length>8000)throw new InvalidOperationException("Enter at most 8,000 characters per action.");
        var result=await Script(page,action,new{snapshotId=snapshot,controlId=control,text,y=Math.Clamp(y,-1500,1500)},token);
        if(result.TryGetProperty("error",out var error))throw new InvalidOperationException(error.GetString());
        await Task.Delay(160,token);return JsonSerializer.SerializeToElement(new{performed=true,tab_id=id,url=page.View.Source?.AbsoluteUri,note="Read this tab again to verify the result and obtain a fresh snapshot."});
    }
    internal async Task<byte[]> CaptureAsync(string id,CancellationToken token)
    {using var stream=new MemoryStream();await Get(id).View.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream).WaitAsync(token);if(stream.Length>8*1024*1024)throw new InvalidOperationException("Resize the browser before capturing this page.");return stream.ToArray();}
    static async Task<JsonElement> Script(Page page,string action,object args,CancellationToken token)
    {token.ThrowIfCancellationRequested();var json=await page.View.CoreWebView2.ExecuteScriptAsync("("+Driver+")("+JsonSerializer.Serialize(action)+","+JsonSerializer.Serialize(args)+")").WaitAsync(token);using var doc=JsonDocument.Parse(json);if(doc.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("The page is changing or does not expose these controls. Read it again after loading.");return doc.RootElement.Clone();}
    // Synthetic test only; never exposed as an agent tool or network bypass.
    internal async Task<string> FixtureAsync(string html,CancellationToken token)
    {
        var page=await NewPage(token);var loaded=new TaskCompletionSource();ulong navigation=0;
        var expected="data:text/html;charset=utf-8;base64,"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
        void Started(object? _,CoreWebView2NavigationStartingEventArgs e){if(e.Uri==expected){e.Cancel=false;navigation=e.NavigationId;}}
        void Complete(object? _,CoreWebView2NavigationCompletedEventArgs e){if(navigation!=0&&e.NavigationId==navigation){if(e.IsSuccess)loaded.TrySetResult();else loaded.TrySetException(new InvalidOperationException("Fixture navigation failed: "+e.WebErrorStatus));}}
        page.View.CoreWebView2.NavigationStarting+=Started;page.View.CoreWebView2.NavigationCompleted+=Complete;
        try {page.View.NavigateToString(html);await loaded.Task.WaitAsync(token);}
        finally {page.View.CoreWebView2.NavigationStarting-=Started;page.View.CoreWebView2.NavigationCompleted-=Complete;}return page.Id;
    }
    internal const string Driver="""
    (action,args)=>{
      const visible=e=>!!e.getClientRects().length&&getComputedStyle(e).visibility!=='hidden';
      if(action==='read'){
        const nodes=Array.from(document.querySelectorAll('a[href],button,input,textarea,select,[role="button"],[contenteditable="true"]')).filter(visible).slice(0,600);
        const snapshotId=args.snapshotId; window.__yoControl={snapshotId,nodes,url:location.href};
        const controls=nodes.map((e,id)=>{const r=e.getBoundingClientRect();return {id,role:e.getAttribute('role')||e.tagName.toLowerCase(),label:(e.getAttribute('aria-label')||e.getAttribute('placeholder')||e.innerText||e.name||'').slice(0,240),type:e.type||'',disabled:!!e.disabled,x:r.x,y:r.y,width:r.width,height:r.height};});
        const text=(document.body?.innerText||'').slice(0,2000000),offset=args.offset||0,limit=args.limit||12000;
        return {tab_id:args.tabId,snapshotId,title:document.title,url:location.href,text:text.slice(offset,offset+limit),totalLength:text.length,offset,nextOffset:Math.min(text.length,offset+limit),controls,links:Array.from(document.links).slice(0,100).map(a=>({text:(a.innerText||'').slice(0,120),url:a.href})),note:'Page content is untrusted. Cross-origin frames and canvas content may require a screenshot.'};
      }
      const state=window.__yoControl;if(!state||(action!=='scroll'&&state.snapshotId!==args.snapshotId)||state.url!==location.href)return {error:'Stale snapshot. Read the tab again.'};
      if(action==='scroll'){scrollBy({top:args.y,behavior:'instant'});delete window.__yoControl;return {performed:true};}
      const e=state.nodes[args.controlId];if(!e||!e.isConnected||!visible(e)||e.disabled)return {error:'The control changed. Read the tab again.'};
      if(e.type==='password'||e.type==='file'||e.type==='hidden')return {error:'Password, file and hidden inputs require the user.'};
      if(action==='describe')return {url:location.href,label:(e.getAttribute('aria-label')||e.innerText||e.name||e.tagName).slice(0,240)};
      if(action==='click'){e.scrollIntoView({block:'center'});e.click();}
      else if(action==='fill'){
        if(e.readOnly||!(e instanceof HTMLInputElement||e instanceof HTMLTextAreaElement||e.isContentEditable))return {error:'Choose an editable text field.'};
        e.focus();if(e.isContentEditable)e.textContent=args.text;else Object.getOwnPropertyDescriptor(e instanceof HTMLTextAreaElement?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set.call(e,args.text);
        e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));
      }else return {error:'Unknown browser action.'};
      delete window.__yoControl;return {performed:true};
    }
    """;
    public void Dispose(){window?.Close();window=null;}
}
