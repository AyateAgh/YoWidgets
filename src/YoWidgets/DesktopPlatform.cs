using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
namespace YoWidgets;

internal sealed class InstallJob
{
    public string Id {get;init;}=Guid.NewGuid().ToString("N");public string ItemId {get;init;}="";public string Title {get;init;}="";
    public string Stage {get;set;}="queued";public long Downloaded {get;set;}public long Total {get;set;}public string Error {get;set;}="";
    public DateTimeOffset StartedAt {get;init;}=DateTimeOffset.UtcNow;
    internal string? ImportPath {get;init;}internal string? ImportHash {get;init;}
    [System.Text.Json.Serialization.JsonIgnore] public CancellationTokenSource Cancellation {get;}=new();
}

internal sealed class DesktopPlatform:IDisposable
{
    internal const string OfficialFeed="https://raw.githubusercontent.com/AyateAgh/YoWidgets/main/catalog/v1/catalog.json";
    readonly WidgetManager manager;readonly PlatformSettings preferences;readonly InterfaceAccent accent;readonly string cache;readonly HttpClient http;
    readonly ConcurrentDictionary<string,InstallJob> jobs=new();readonly CancellationTokenSource lifetime=new();readonly SemaphoreSlim refreshGate=new(1,1);
    CatalogDocument catalog;string catalogStatus="cached";string catalogError="";long revision;
    internal ContentStore Content {get;}internal event Action? Changed;
    internal string? RequestedPage {get;set;}internal string? RequestedProvider {get;set;}internal string Notice {get;set;}="";
    internal string FeedUrl {get;}
    internal DesktopPlatform(WidgetManager manager,string root,bool migrate,string? feed=null)
    {
        this.manager=manager;accent=new(()=>manager.Settings.InterfaceAccent=="auto",Publish);preferences=new(manager,accent);FeedUrl=feed??OfficialFeed;
        if(!Uri.TryCreate(FeedUrl,UriKind.Absolute,out var source)||!CatalogRules.AllowedSource(source))throw new InvalidOperationException("Invalid catalog address.");
        cache=Path.Combine(root,"catalog-cache.json");
        using var seed=Assembly.GetExecutingAssembly().GetManifestResourceStream("YoWidgets.CatalogSeed.json")??throw new InvalidOperationException("The catalog seed is missing.");
        catalog=JsonSerializer.Deserialize<CatalogDocument>(seed,CatalogJson.Options)!;CatalogRules.Validate(catalog);
        if(File.Exists(cache))try{var saved=JsonSerializer.Deserialize<CatalogDocument>(File.ReadAllText(cache),CatalogJson.Options)!;CatalogRules.Validate(saved);catalog=saved;}catch(Exception e)when(e is IOException or JsonException or InvalidDataException){catalogError="The saved catalog was reset.";}
        Content=new(Path.Combine(root,"Content"));if(migrate)Content.ImportLegacy(catalog);Content.EnsureRegistry();Content.UpgradeLegacyMetadata();WidgetPacks.LoadInstalled(Content);
        http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(30)};http.DefaultRequestHeaders.UserAgent.ParseAdd("YoWidgets/0.6.7");
        manager.Changed+=Publish;
    }
    internal object Snapshot()=>new {
        revision=Interlocked.Read(ref revision),catalog,catalogStatus,catalogError,feedUrl=FeedUrl,
        installed=Content.Installed.Select(i=>new{i.Item,i.Packages,i.InstalledAt,needsRepair=!Content.IsReady(i.Item.Id)}),widgets=manager.Settings.Widgets.Select(w=>new{w.Id,w.ContentId,w.Kind,w.Pack,w.Size,w.Enabled,w.Monitor,visible=manager.IsOnDesktop(w.Id)}),
        jobs=jobs.Values.OrderByDescending(j=>j.StartedAt).Take(100).ToArray(),settings=preferences.Snapshot(),snoozed=manager.Snoozed,activeCount=manager.ActiveCount,
        requestedPage=RequestedPage,requestedProvider=RequestedProvider,notice=Notice,recovery=Content.RecoveryMessage
    };
    internal void Publish(){Interlocked.Increment(ref revision);Changed?.Invoke();}
    async Task Refresh(CancellationToken token)
    {
        if(!await refreshGate.WaitAsync(0,token))return;
        try
        {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(30));token=deadline.Token;catalogStatus="refreshing";Publish();using var response=await http.GetAsync(FeedUrl,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
            var bytes=await Bounded(response.Content,4*1024*1024,token);var next=JsonSerializer.Deserialize<CatalogDocument>(bytes,CatalogJson.Options)??throw new InvalidDataException("The catalog is empty.");CatalogRules.Validate(next);
            var temporary=cache+".tmp";await File.WriteAllBytesAsync(temporary,bytes,token);File.Move(temporary,cache,true);catalog=next;catalogStatus="online";catalogError="";
        }
        catch(OperationCanceledException){catalogStatus="offline";catalogError="Catalog is offline — your installed widgets are ready.";}
        catch(Exception e)when(e is HttpRequestException or IOException or JsonException or InvalidDataException){catalogStatus="offline";catalogError="Catalog is offline — your installed widgets are ready.";}
        finally{refreshGate.Release();Publish();}
    }
    static async Task<byte[]> Bounded(HttpContent content,int limit,CancellationToken token)
    {
        if(content.Headers.ContentLength>limit)throw new InvalidDataException("The response is too large.");
        await using var stream=await content.ReadAsStreamAsync(token);using var memory=new MemoryStream();var buffer=new byte[32768];int read;
        while((read=await stream.ReadAsync(buffer,token))>0){if(memory.Length+read>limit)throw new InvalidDataException("The response is too large.");await memory.WriteAsync(buffer.AsMemory(0,read),token);}return memory.ToArray();
    }
    async Task<Stream> Download(ContentPackage package,CancellationToken token)
    {
        var uri=new Uri(package.Url);var source=new Uri(FeedUrl);
        if(source.IsLoopback){if(!uri.IsLoopback||uri.Port!=source.Port)throw new InvalidDataException("Development packages must use the selected local feed.");}
        else if(uri.Scheme!="https"||uri.Host!="raw.githubusercontent.com"||!uri.AbsolutePath.StartsWith("/AyateAgh/YoWidgets/",StringComparison.Ordinal))throw new InvalidDataException("The package is outside the official repository.");
        var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
        try{response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength is {} size&&size!=package.Bytes)throw new InvalidDataException("The package size does not match the catalog.");return new ResponseStream(await response.Content.ReadAsStreamAsync(token),response);}catch{response.Dispose();throw;}
    }
    async Task Install(InstallJob job,CatalogDocument source)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation.Token,lifetime.Token);linked.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            long last=0;
            await Content.InstallAsync(source,job.ItemId,Download,p=>{job.Stage=p.Stage;job.Downloaded=p.Downloaded;job.Total=p.Total;var now=Environment.TickCount64;if(now-last>100||p.Stage!="downloading"){last=now;Publish();}},linked.Token);
            await Application.Current.Dispatcher.InvokeAsync(()=>{WidgetPacks.LoadInstalled(Content);manager.ApplyAppearance();InterfaceAudio.Play("complete",manager);});
            job.Stage="completed";
        }
        catch(OperationCanceledException){job.Stage=job.Cancellation.IsCancellationRequested||lifetime.IsCancellationRequested?"cancelled":"failed";if(job.Stage=="failed")job.Error="The download timed out. Retry when your connection is ready.";}
        catch(Exception e){job.Stage="failed";job.Error=e is HttpRequestException?"The package could not be downloaded. Check your connection and retry.":e is InvalidDataException or IOException or InvalidOperationException?e.Message:"Installation failed. Retry the download.";}
        finally{Publish();}
    }
    async Task Import(InstallJob job,string path,string hash){
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation.Token,lifetime.Token);
        try{using var bundle=new LocalContentBundle(path,hash);if(bundle.Catalog.Items.Any(i=>catalog.Items.Any(c=>c.Id==i.Id)))throw new InvalidDataException("A custom item cannot replace an official catalog item.");var shown=bundle.Catalog.Items.Where(i=>i.Type=="widget").ToArray();if(Content.Installed.Any(old=>shown.Any(i=>i.Pack==old.Item.Pack)&&!shown.Any(i=>i.Id==old.Item.Id)))throw new InvalidDataException("Include every installed widget in this collection before replacing its shared style.");await bundle.InstallAsync(Content,p=>{job.Stage=p.Stage;job.Downloaded=p.Downloaded;job.Total=p.Total;Publish();},linked.Token);await Application.Current.Dispatcher.InvokeAsync(()=>{WidgetPacks.LoadInstalled(Content);manager.ApplyAppearance();});job.Stage="completed";}
        catch(OperationCanceledException){job.Stage="cancelled";}
        catch(Exception e){job.Stage="failed";job.Error=e is InvalidDataException or InvalidOperationException?e.Message:"The local package could not be imported. Check the ZIP and try again.";}
        finally{Publish();}
    }
    object QueueImport(string path,string hash){
        if(hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new InvalidOperationException("Choose a ZIP package first.");
        var existing=jobs.Values.FirstOrDefault(j=>j.ImportHash==hash&&j.Stage is "queued" or "downloading" or "verifying" or "installing");if(existing!=null)return new{jobId=existing.Id};
        var job=new InstallJob{ItemId="import-"+hash[..16],Title="Custom collection",ImportPath=path,ImportHash=hash};jobs[job.Id]=job;_=Task.Run(()=>Import(job,path,hash),lifetime.Token);Publish();return new{jobId=job.Id};
    }
    internal async Task<object> Execute(string method,JsonElement args,CancellationToken token)
    {
        if(method.StartsWith("settings.",StringComparison.Ordinal)){var result=await preferences.Execute(method,args,token);Publish();return result;}
        var id=PlatformSettings.Text(args,"id");
        switch(method)
        {
            case "app.open":if(args.TryGetProperty("settings",out var settings)&&settings.GetBoolean())App.CurrentApp.Shell?.Select("settings/general");else App.CurrentApp.Shell?.Show();return new{accepted=true};
            case "state":return Snapshot();
            case "catalog.refresh":_ = Refresh(lifetime.Token);return new{accepted=true};
            case "content.import":{
                return QueueImport(PlatformSettings.Text(args,"path"),PlatformSettings.Text(args,"hash"));
            }
            case "content.retry":{
                if(!jobs.TryGetValue(id,out var retry)||retry.Stage is not("failed" or "cancelled"))throw new InvalidOperationException("Only an unsuccessful download can be retried.");
                if(retry.ImportPath is {} path&&retry.ImportHash is {} hash)return QueueImport(path,hash);
                return await Execute("content.install",JsonSerializer.SerializeToElement(new{id=retry.ItemId}),token);
            }
            case "content.install":
            {
                var selected=catalog.Items.FirstOrDefault(i=>i.Id==id)??throw new InvalidOperationException("This item is no longer in the catalog.");
                var existing=jobs.Values.FirstOrDefault(j=>j.ItemId==id&&j.Stage is "queued" or "downloading" or "verifying" or "installing");if(existing!=null)return new{jobId=existing.Id};
                var source=catalog;CatalogRules.Resolve(source,id);var job=new InstallJob{ItemId=id,Title=selected.Title};jobs[job.Id]=job;_ = Task.Run(()=>Install(job,source),lifetime.Token);Publish();return new{jobId=job.Id};
            }
            case "content.cancel":if(jobs.TryGetValue(id,out var cancel))cancel.Cancellation.Cancel();return new{accepted=true};
            case "content.clearFinished":foreach(var job in jobs.Values.Where(j=>j.Stage is "completed" or "failed" or "cancelled")){if(jobs.TryRemove(job.Id,out _))job.Cancellation.Dispose();}Publish();return new{accepted=true};
            case "content.visibility":{
                var enabled=args.GetProperty("enabled").GetBoolean();var item=Content.ReadWidget(id);var instances=manager.Settings.Widgets.Where(w=>w.ContentId==id).ToArray();manager.Settings.LibraryResume??=[];
                if(enabled&&manager.Snoozed)manager.SetSnooze(false);
                if(!enabled){manager.Settings.LibraryResume[id]=instances.Where(w=>w.Enabled).Select(w=>w.Id).ToArray();foreach(var widget in instances.Where(w=>w.Enabled))manager.Disable(widget);}
                else if(!instances.Any(w=>manager.IsOnDesktop(w.Id))){
                    if(instances.Length==0){if(manager.Settings.Widgets.Count>=24)throw new InvalidOperationException("Remove a widget instance first.");var handle=args.TryGetProperty("window",out var hwnd)?new IntPtr(hwnd.GetInt64()):IntPtr.Zero;var area=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;manager.Add(item.Kind,item.Pack,new(area.X,area.Y,area.Width,area.Height),id,item.DefaultSize);}
                    else {var ids=manager.Settings.LibraryResume.GetValueOrDefault(id)??[];var restoring=instances.Where(w=>ids.Contains(w.Id)).ToArray();foreach(var widget in restoring.Length>0?restoring:[instances[0]])manager.Enable(widget);}
                }
                manager.Save();Publish();return new{accepted=true};
            }
            case "content.dismiss":if(jobs.TryGetValue(id,out var dismissed)&&dismissed.Stage is "completed" or "failed" or "cancelled"){jobs.TryRemove(id,out _);dismissed.Cancellation.Dispose();}Publish();return new{accepted=true};
            case "content.remove":
            {
                var ids=Content.Installed.Any(i=>i.Item.Id==id)?new[]{id}:catalog.Items.FirstOrDefault(i=>i.Id==id&&i.Type=="pack")?.Includes??throw new InvalidOperationException("The installed item was not found.");
                var instances=manager.Settings.Widgets.Where(w=>ids.Contains(w.ContentId)).ToArray();
                Content.RemoveMany(ids);
                foreach(var widget in instances){if(widget.Enabled)manager.Disable(widget);manager.Settings.Widgets.Remove(widget);}manager.Save();Publish();return new{message="Content removed"};
            }
            case "widget.add":
            {
                var item=Content.ReadWidget(id);WidgetPacks.LoadInstalled(Content);
                if(manager.Settings.Widgets.Count>=24)throw new InvalidOperationException("The current engine supports up to 24 widget instances. Remove an instance first.");
                var window=args.TryGetProperty("window",out var hwnd)?new IntPtr(hwnd.GetInt64()):IntPtr.Zero;var screen=window==IntPtr.Zero?System.Windows.Forms.Screen.PrimaryScreen!:System.Windows.Forms.Screen.FromHandle(window);
                var work=screen.WorkingArea;manager.Add(item.Kind,item.Pack,new(work.X,work.Y,work.Width,work.Height),id,item.DefaultSize);InterfaceAudio.Play("place",manager);Publish();return new{message="Added to desktop"};
            }
            case "widget.locate":manager.Locate(id);return new{accepted=true};
            case "widget.disable":{var widget=manager.Settings.Widgets.FirstOrDefault(w=>w.Id==id)??throw new InvalidOperationException("The widget was not found.");manager.Disable(widget);Publish();return new{accepted=true};}
            case "widget.remove":{var widget=manager.Settings.Widgets.FirstOrDefault(w=>w.Id==id)??throw new InvalidOperationException("The widget was not found.");manager.Disable(widget);manager.Settings.Widgets.Remove(widget);manager.Save();Publish();return new{accepted=true};}
            case "widget.enable":{var widget=manager.Settings.Widgets.FirstOrDefault(w=>w.Id==id)??throw new InvalidOperationException("The widget was not found.");if(!Content.IsReady(widget.ContentId))throw new InvalidOperationException("Install or repair this widget first.");if(manager.Snoozed)manager.SetSnooze(false);manager.Enable(widget);Publish();return new{accepted=true};}
            case "widget.size":{var widget=manager.Settings.Widgets.FirstOrDefault(w=>w.Id==id)??throw new InvalidOperationException("The widget was not found.");var size=PlatformSettings.Text(args,"size");if(size is not("S" or "M" or "L"))throw new InvalidOperationException("Choose S, M or L.");manager.SetSize(widget,size);Publish();return new{accepted=true};}
            case "app.snooze":manager.SetSnooze(!manager.Snoozed);Publish();return new{accepted=true};
            case "app.disableAll":manager.DisableAll();Publish();return new{accepted=true};
            case "app.sound":InterfaceAudio.Play(PlatformSettings.Text(args,"sound"),manager,args.TryGetProperty("preview",out var preview)&&preview.GetBoolean());return new{accepted=true};
            case "app.clearRequest":RequestedPage=null;RequestedProvider=null;Notice="";Publish();return new{accepted=true};
            case "app.quit":_ = Application.Current.Dispatcher.BeginInvoke(App.CurrentApp.ExitApp);return new{accepted=true};
            case "app.uninstall":
                if(!Cleanup.IsInstalled)throw new InvalidOperationException("Uninstall is available for the installed application.");
                if(args.TryGetProperty("restoreControls",out var restore)&&restore.GetBoolean()){var errors=await manager.Data.Controls.RestoreAsync();if(errors.Length>0)throw new InvalidOperationException(errors);}
                _ = Application.Current.Dispatcher.BeginInvoke(Cleanup.LaunchUninstall);return new{accepted=true};
            default:throw new InvalidOperationException("Unknown management action.");
        }
    }
    public void Dispose(){manager.Changed-=Publish;lifetime.Cancel();foreach(var job in jobs.Values)job.Cancellation.Cancel();http.Dispose();accent.Dispose();}
    sealed class ResponseStream(Stream inner,HttpResponseMessage response):Stream
    {
        public override bool CanRead=>inner.CanRead;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>throw new NotSupportedException();public override int Read(byte[] buffer,int offset,int count)=>inner.Read(buffer,offset,count);public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)=>inner.ReadAsync(buffer,token);public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();protected override void Dispose(bool disposing){if(disposing){inner.Dispose();response.Dispose();}base.Dispose(disposing);}
    }
}
