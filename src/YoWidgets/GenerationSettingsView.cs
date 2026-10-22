using System.Windows.Automation;
namespace YoWidgets;

internal sealed class GenerationSettingsView : StackPanel
{
    internal static readonly HttpClient Http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
    readonly WidgetManager manager;readonly Action<string> status;
    readonly PasswordBox sharedKey=new(){MaxLength=512,Padding=new Thickness(12)};
    readonly TextBlock sharedStatus=Ui.Text("Paste your Pollinations key here to check it or use it for all four media categories.",12,"Muted");
    readonly List<GenerationConnectionCard> cards=[];
    CancellationTokenSource? checking;
    internal GenerationSettingsView(WidgetManager manager,Action<string> status)
    {
        this.manager=manager;this.status=status;
        Resources["WidgetInk"]=Ui.Brush("Text");
        sharedKey.SetResourceReference(StyleProperty,"ConnectionPasswordBox");
        Children.Add(Ui.Text("Generation connections",20,weight:FontWeights.SemiBold));Children.Add(Ui.Gap(6));Children.Add(Ui.Text("Independent services and keys for each type of media. Changing these does not replace your chat connection.",12,"Muted"));Children.Add(Ui.Gap(14));
        var connect=new StackPanel();connect.Children.Add(Ui.Text("Connect Pollinations",16,weight:FontWeights.SemiBold));connect.Children.Add(Ui.Gap(6));connect.Children.Add(Ui.Text("One key can serve images, video, 3D and audio. Model permissions and Pollen balance still apply.",12,"Muted"));connect.Children.Add(Ui.Gap(10));
        sharedKey.Background=Ui.Brush("Field");sharedKey.Foreground=Ui.Brush("Text");sharedKey.BorderBrush=Ui.Brush("Line");AutomationProperties.SetName(sharedKey,"Pollinations API key for all media");connect.Children.Add(sharedKey);connect.Children.Add(Ui.Gap(8));connect.Children.Add(sharedStatus);
        var buttons=new WrapPanel{Margin=new Thickness(0,12,0,0)};buttons.Children.Add(Ui.Button("Check Pollinations key",async()=>await CheckShared()));buttons.Children.Add(Ui.Button("Use for all media",UseForAll,true));connect.Children.Add(buttons);
        connect.Children.Add(Ui.Gap(8));connect.Children.Add(Ui.Text("Checks read key permissions and balance without generating. Test generation below uses API credits. Pollinations outputs may be accessible through public media URLs.",11,"Muted"));Children.Add(Ui.Panel(connect));Children.Add(Ui.Gap(14));
        foreach(var category in GenerationConnections.Categories){var card=new GenerationConnectionCard(manager,category.Id,status);cards.Add(card);Children.Add(card);Children.Add(Ui.Gap(10));}
        sharedKey.PasswordChanged+=(_,_)=>{checking?.Cancel();checking=null;sharedStatus.Text="Key changed · check or save it.";};Unloaded+=(_,_)=>{checking?.Cancel();checking=null;};
    }
    async Task CheckShared()
    {
        var secret=sharedKey.Password.Trim();var source="entered key";
        if(secret.Length==0){var saved=GenerationConnections.Categories.Select(c=>(Category:c.Title,Connection:GenerationConnections.Get(manager.Settings,c.Id))).FirstOrDefault(c=>c.Connection.Provider=="Pollinations"&&c.Connection.KeyProtected!=null);if(saved.Connection!=null){secret=AiService.UnlockGenerationKey(saved.Connection.KeyProtected!);source="saved "+saved.Category+" key";}}
        if(secret.Length==0){sharedStatus.Text="Paste a Pollinations key first.";sharedKey.Focus();return;}
        checking?.Cancel();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(25));checking=stop;sharedStatus.Text="Checking key, permissions and Pollen balance…";
        try {var result=await PollinationsCatalog.Account(Http,secret,stop.Token);if(!stop.IsCancellationRequested&&ReferenceEquals(checking,stop))sharedStatus.Text=source+" · "+result.Summary+"\nAllowed models/categories: "+(result.AllowedModels==null?"all":string.Join(", ",result.AllowedModels))+". Generation not tested.";}
        catch(OperationCanceledException){if(ReferenceEquals(checking,stop))sharedStatus.Text="Check cancelled or timed out.";}
        catch(Exception e){if(ReferenceEquals(checking,stop))sharedStatus.Text=e is InvalidOperationException?e.Message:"Could not check Pollinations. Check your network and try again.";}
        finally {if(ReferenceEquals(checking,stop))checking=null;}
    }
    internal void UseForAll()
    {
        if(manager.Data.Ai.Busy){sharedStatus.Text="Stop the current response before changing its generation connections.";return;}
        var raw=sharedKey.Password.Trim();if(raw.Length==0){sharedStatus.Text="Paste a Pollinations key first.";sharedKey.Focus();return;}
        var previous=GenerationConnections.Categories.ToDictionary(c=>c.Id,c=>GenerationConnections.Get(manager.Settings,c.Id) with{});
        var secret=AiService.Protect(raw);foreach(var item in GenerationConnections.Categories)GenerationConnections.Set(manager.Settings,item.Id,new(){Mode="separate",Provider="Pollinations",KeyProtected=secret,Model=GenerationConnections.DefaultModel(item.Id,"Pollinations"),Voice="alloy"});
        if(!manager.TrySave()){foreach(var item in previous)GenerationConnections.Set(manager.Settings,item.Key,item.Value);sharedStatus.Text="Could not save. Existing connections were restored; your entered key is still here.";return;}
        checking?.Cancel();checking=null;sharedKey.Clear();foreach(var card in cards)card.Reload();sharedStatus.Text="Pollinations saved for images, video, 3D and audio. Chat connection preserved. Check a category to load its current models and prices.";status("Generation connections saved");
    }
}

internal sealed class GenerationConnectionCard : Expander
{
    readonly WidgetManager manager;readonly string kind;readonly Action<string> status;
    readonly ComboBox mode=new(){ItemsSource=new[]{"Use chat connection","Separate API key","Off"},MinHeight=38};
    readonly ComboBox provider=new(){MinHeight=38};
    readonly ComboBox model=new(){IsEditable=true,MinHeight=38};
    readonly PasswordBox key=new(){MaxLength=512,Padding=new Thickness(12)};
    readonly TextBox voice=Ui.Input("");
    readonly TextBlock feedback=Ui.Text("",12,"Muted"),details=Ui.Text("",11,"Muted"),summary=Ui.Text("",12,"Muted");
    readonly StackPanel separate=new(),fields=new();readonly Button check,save,test,view;
    GenerationConnection original=new();CancellationTokenSource? work;GeneratedMedia? result;bool loading;int operationVersion;
    IReadOnlyList<PollinationsModel> catalog=[];
    internal GenerationConnectionCard(WidgetManager manager,string kind,Action<string> status)
    {
        this.manager=manager;this.kind=kind;this.status=status;Foreground=Ui.Brush("Text");HorizontalContentAlignment=HorizontalAlignment.Stretch;
        key.SetResourceReference(StyleProperty,"ConnectionPasswordBox");
        Style=(Style)Application.Current.FindResource("AgentActionExpander");
        var category=GenerationConnections.Categories.First(c=>c.Id==kind);var heading=new DockPanel();var icon=WidgetVisuals.Icon(category.Icon,20,"Text");icon.Margin=new Thickness(0,0,10,0);heading.Children.Add(icon);heading.Children.Add(Ui.Stack(Ui.Text(category.Title,16,weight:FontWeights.SemiBold),summary));Header=heading;
        var content=new StackPanel{Margin=new Thickness(4,12,4,4)};content.Children.Add(Ui.Text("Connection",12,"Muted"));content.Children.Add(Ui.Gap(4));content.Children.Add(mode);content.Children.Add(Ui.Gap(10));
        fields.Children.Add(Ui.Text("Service",12,"Muted"));fields.Children.Add(provider);fields.Children.Add(Ui.Gap(10));
        separate.Children.Add(Ui.Text("API key · encrypted for your Windows account",12,"Muted"));key.Background=Ui.Brush("Field");key.Foreground=Ui.Brush("Text");key.BorderBrush=Ui.Brush("Line");separate.Children.Add(key);separate.Children.Add(Ui.Gap(10));fields.Children.Add(separate);
        fields.Children.Add(Ui.Text("Generation model · check key to load available models",12,"Muted"));fields.Children.Add(model);fields.Children.Add(Ui.Gap(6));fields.Children.Add(details);
        if(kind=="audio"){fields.Children.Add(Ui.Gap(8));fields.Children.Add(Ui.Text("Voice · used by speech models",12,"Muted"));fields.Children.Add(voice);voice.MaxLength=100;}
        content.Children.Add(fields);content.Children.Add(Ui.Gap(12));content.Children.Add(feedback);
        var actions=new WrapPanel{Margin=new Thickness(0,12,0,0)};save=Ui.Button("Save "+category.Title,Save,true);check=Ui.Button("Check key",async()=>await Check());test=Ui.Button("Test generation · uses credits",async()=>await Test());view=Ui.Button("Open test result",()=>{if(result!=null)MediaLightbox.Show(result);});view.Visibility=Visibility.Collapsed;
        actions.Children.Add(save);actions.Children.Add(check);actions.Children.Add(test);actions.Children.Add(view);content.Children.Add(actions);
        var remove=Ui.Button("Remove this key",Remove);remove.HorizontalAlignment=HorizontalAlignment.Left;remove.Margin=new Thickness(0,10,0,0);content.Children.Add(remove);Content=Ui.Panel(content,16);
        AutomationProperties.SetName(mode,category.Title+" connection mode");AutomationProperties.SetName(provider,category.Title+" service");AutomationProperties.SetName(model,category.Title+" generation model");AutomationProperties.SetName(key,category.Title+" API key");AutomationProperties.SetName(voice,category.Title+" voice");AutomationProperties.SetLiveSetting(feedback,AutomationLiveSetting.Polite);
        mode.SelectionChanged+=(_,_)=>{if(loading)return;Cancel();if(mode.SelectedIndex==1&&original.Mode!="separate"){provider.SelectedItem="Pollinations";model.Text=GenerationConnections.DefaultModel(kind,"Pollinations");}Layout();};
        provider.SelectionChanged+=(_,_)=>{if(loading)return;Cancel();key.Clear();model.ItemsSource=null;model.Text=GenerationConnections.DefaultModel(kind,Provider);catalog=[];Layout();};
        model.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,new TextChangedEventHandler((_,_)=>{if(!loading)Cancel();}));
        model.LostKeyboardFocus+=(_,_)=>ModelDetails();model.SelectionChanged+=(_,_)=>{if(!loading)Cancel();ModelDetails();};key.PasswordChanged+=(_,_)=>{if(!loading)Cancel();};voice.TextChanged+=(_,_)=>{if(!loading)Cancel();};Unloaded+=(_,_)=>Cancel();Reload();
    }
    string Provider=>provider.SelectedItem as string??"Pollinations";
    string Mode=>mode.SelectedIndex switch{0=>"chat",1=>"separate",_=>"off"};
    internal void Reload()
    {
        Cancel();loading=true;original=GenerationConnections.Get(manager.Settings,kind) with{};mode.SelectedIndex=original.Mode switch{"chat"=>0,"separate"=>1,_=>2};provider.ItemsSource=GenerationConnections.Providers(kind);provider.SelectedItem=GenerationConnections.Providers(kind).Contains(original.Provider)?original.Provider:"Pollinations";
        model.ItemsSource=null;model.Text=original.Model.Length>0?original.Model:original.Mode=="chat"?(kind=="image"?manager.Settings.AiImageModel:kind=="video"?manager.Settings.AiVideoModel:""):GenerationConnections.DefaultModel(kind,Provider);voice.Text=original.Voice.Length>0?original.Voice:"alloy";key.Clear();loading=false;feedback.Text=original.KeyProtected!=null?"Key saved. Leave the key field empty to keep it.":"No separate key saved.";Layout();
    }
    void Layout(){separate.Visibility=Mode=="separate"?Visibility.Visible:Visibility.Collapsed;fields.Visibility=Mode=="off"?Visibility.Collapsed:Visibility.Visible;provider.IsEnabled=Mode=="separate";if(Mode=="chat")provider.SelectedItem=GenerationConnections.Providers(kind).Contains(manager.Settings.AiProvider)?manager.Settings.AiProvider:null;summary.Text=Mode=="off"?"Disabled":Mode=="chat"?"Uses chat connection · "+manager.Settings.AiProvider:Provider+" · separate key";check.IsEnabled=test.IsEnabled=Mode!="off";ModelDetails();}
    GenerationConnection Draft()
    {
        var value=original with{Mode=Mode,Provider=Provider,Model=model.Text.Trim(),Voice=voice.Text.Trim()};
        if(Mode=="separate"){
            if(key.Password.Trim().Length>0)value.KeyProtected=AiService.Protect(key.Password.Trim());
            else if(Provider!=original.Provider||original.KeyProtected==null)throw new InvalidOperationException("Paste an API key for "+Provider+" first.");
        }return value;
    }
    ResolvedGeneration Connection()
    =>GenerationConnections.Resolve(manager.Settings,kind,Draft());
    void Save()
    {
        if(manager.Data.Ai.Busy){feedback.Text="Stop the current response before changing its generation connections.";return;}
        try{var value=Draft();if(value.Mode!="off")GenerationConnections.Resolve(manager.Settings,kind,value);var previous=GenerationConnections.Get(manager.Settings,kind);GenerationConnections.Set(manager.Settings,kind,value);if(!manager.TrySave()){GenerationConnections.Set(manager.Settings,kind,previous);feedback.Text="Could not save. Previous settings restored; your entered key is still here.";return;}Reload();feedback.Text="Saved · chat connection preserved.";status("Generation connection saved");}catch(InvalidOperationException e){feedback.Text=e.Message;}
    }
    void Remove()
    {if(manager.Data.Ai.Busy){feedback.Text="Stop the current response before removing its key.";return;}var previous=GenerationConnections.Get(manager.Settings,kind);GenerationConnections.Set(manager.Settings,kind,previous with{Mode="off",KeyProtected=null});if(!manager.TrySave()){GenerationConnections.Set(manager.Settings,kind,previous);feedback.Text="Could not remove this key.";return;}Reload();feedback.Text="Separate key removed · this category is off. Chat key preserved.";status("Generation key removed");}
    async Task Check()
    {
        if(work!=null){Cancel();feedback.Text="Check cancelled.";return;}
        var version=operationVersion;
        try{var connection=Connection();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(25));work=stop;check.IsEnabled=false;feedback.Text="Checking credentials, model access and balance…";
            try{var result=await GenerationConnections.Check(GenerationSettingsView.Http,connection,AiService.UnlockGenerationKey(connection.KeyProtected),stop.Token);if(stop.IsCancellationRequested||!ReferenceEquals(work,stop))return;var selected=model.Text;loading=true;model.ItemsSource=result.Models;model.Text=selected;loading=false;if(connection.Provider=="Pollinations")catalog=await PollinationsCatalog.Models(GenerationSettingsView.Http,kind,stop.Token);stop.Token.ThrowIfCancellationRequested();feedback.Text=result.Message;ModelDetails();}
            finally{if(ReferenceEquals(work,stop)){work=null;check.IsEnabled=true;}}
        }catch(OperationCanceledException){if(version==operationVersion)feedback.Text="Check cancelled or timed out.";}catch(Exception e){if(version==operationVersion)feedback.Text=e is InvalidOperationException?e.Message:"Connection check failed. Check your network and try again.";}
    }
    async Task Test()
    {
        if(work!=null){Cancel();feedback.Text="Test cancelled.";return;}
        var version=operationVersion;
        try{var connection=Connection();using var stop=new CancellationTokenSource();work=stop;test.Content="Cancel test";save.IsEnabled=check.IsEnabled=false;feedback.Text="Generating one synthetic test output · API credits may be used…";result=null;view.Visibility=Visibility.Collapsed;
            var prompt=kind switch{"image"=>"A simple blue circle on a plain white background.","video"=>"A blue ball gently rolling on a plain white background, short clip.","audio"=>"YoWidgets API connection test.",_=>"A simple low-poly blue cube."};var job=new GeneratedMediaJob(kind,prompt,kind=="video"?"16:9":"1:1");
            void Changed(){if(!stop.IsCancellationRequested&&ReferenceEquals(work,stop))Dispatcher.Invoke(()=>feedback.Text=job.Stage+(job.Percent.HasValue?" "+job.Percent.Value+"%":""));}job.Changed+=Changed;
            try{var generated=await new GeneratedMediaService(GenerationSettingsView.Http,connection.Provider,AiService.UnlockGenerationKey(connection.KeyProtected),connection.Model,connection.Model,connection.Voice).Generate(job,null,null,stop.Token);stop.Token.ThrowIfCancellationRequested();if(version!=operationVersion)return;result=generated;feedback.Text="Generation succeeded · "+result.MimeType+" · "+new FileInfo(result.Path).Length.ToString("N0")+" bytes. The original test file is saved locally.";view.Visibility=Visibility.Visible;}
            finally{job.Changed-=Changed;if(ReferenceEquals(work,stop)){work=null;test.Content="Test generation · uses credits";save.IsEnabled=check.IsEnabled=true;}}
        }catch(OperationCanceledException){if(version==operationVersion)feedback.Text="Test stopped. No late result will be displayed.";}catch(Exception e){if(version==operationVersion)feedback.Text=e is InvalidOperationException?e.Message:"Generation test failed. Check the service and try again.";}
    }
    void ModelDetails()
    {var selected=catalog.FirstOrDefault(m=>m.Id==model.Text||m.Aliases.Contains(model.Text));details.Text=selected==null?"Checks validate credentials; generation is tested only with the Test generation button.":selected.Title+" · "+(selected.PaidOnly?"Paid Pollen required":"Quest Pollen eligible; generation still uses Pollen")+"\nInput: "+string.Join(", ",selected.Inputs)+" · Output: "+string.Join(", ",selected.Outputs)+"\nCatalog base rates: "+selected.Price+(selected.Limits.Length>0?"\n"+selected.Limits:"")+(selected.Voices.Length>0?"\nVoices: "+string.Join(", ",selected.Voices):"");}
    void Cancel(){operationVersion++;work?.Cancel();work=null;check.IsEnabled=save.IsEnabled=true;test.Content="Test generation · uses credits";}
}
