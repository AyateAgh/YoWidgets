using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class ComposerMenu : Popup
{
    readonly StackPanel body=new(),tabs=new();
    readonly AiService ai;
    readonly BrowserAssistantBridge browser;
    readonly Action<AssistantAttachment> attach;
    readonly Action files,draw;
    readonly Action<string> pluginSelected;
    readonly Action? beforeSelection;
    readonly Border card;
    readonly List<Button> rows=[];
    readonly ScrollViewer scroller;
    readonly GlassPopupMotion motion;
    bool expanded;
    int highlighted;
    string query="";
    internal bool MentionSearch {get;private set;}
    internal bool Closing=>motion.Closing;
    internal bool UsesLiveBackdrop=>motion.UsesLiveBackdrop;
    internal event Action? CloseStarted;
    internal ComposerMenu(FrameworkElement target,AiService ai,BrowserAssistantBridge browser,Action files,Action draw,Action<AssistantAttachment> attach,Action<string>? pluginSelected=null,Action? beforeSelection=null)
    {
        this.ai=ai;this.browser=browser;this.attach=attach;this.files=files;this.draw=draw;this.pluginSelected=pluginSelected??ai.SelectPluginInstruction;this.beforeSelection=beforeSelection;
        PlacementTarget=target;Placement=PlacementMode.Top;VerticalOffset=-8;AllowsTransparency=true;StaysOpen=false;
        Resources["ComposerDocument"]=new SolidColorBrush(Color.FromRgb(116,173,255));Resources["ComposerPdf"]=new SolidColorBrush(Color.FromRgb(255,132,142));Resources["ComposerSheet"]=new SolidColorBrush(Color.FromRgb(103,211,156));Resources["ComposerSlides"]=new SolidColorBrush(Color.FromRgb(245,190,98));
        scroller=new ScrollViewer {Content=body,MaxHeight=Math.Min(610,SystemParameters.WorkArea.Height*.72),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        card=new Border {Width=Math.Min(410,SystemParameters.WorkArea.Width-40),Background=Ui.Brush("Panel"),BorderBrush=new SolidColorBrush(Color.FromArgb(55,255,255,255)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Padding=new Thickness(8),Child=scroller};Child=card;
        Rebuild();
        motion=new GlassPopupMotion(this,card,target);motion.CloseStarted+=()=>CloseStarted?.Invoke();
        Opened+=async(_,_)=> {browser.Changed+=BrowserChanged;ai.LocalBrowser.Changed+=BrowserChanged;browser.Start();Rebuild();if(!MentionSearch)rows.FirstOrDefault()?.Focus();await ai.NativeTabs.RefreshAsync();if(IsOpen&&!Closing)Rebuild();};
        Closed+=(_,_)=> {browser.Changed-=BrowserChanged;ai.LocalBrowser.Changed-=BrowserChanged;};
    }
    internal void CloseMenu()=>motion.Close();
    Task CloseAsync()=>motion.CloseAsync();
    internal void Search(string value,bool mention=true)
    {MentionSearch=mention;if(query==value){Highlight();return;}query=value;highlighted=0;Rebuild();}
    bool Matches(string title,string description)=>query.Length==0||title.Contains(query,StringComparison.CurrentCultureIgnoreCase)||description.Contains(query,StringComparison.CurrentCultureIgnoreCase);
    async void Commit(Action action){beforeSelection?.Invoke();await CloseAsync();action();}
    void Rebuild()
    {
        var restoreFocus=card.IsKeyboardFocusWithin&&!MentionSearch;
        body.Children.Clear();tabs.Children.Clear();rows.Clear();
        var addItems=new[]{("paperclip","Files and folders","Attach files or one working folder",(Action)(()=>Commit(files))),
            ("target","Goal","Keep this chat focused on a goal",()=>Commit(()=>ai.SetGoal(!ai.GoalEnabled))),
            ("lightbulb","Planning mode","Research a complex task and write a plan",()=>Commit(()=>ai.SetPlanning(!ai.PlanningEnabled))),
            ("pencil","Draw","Sketch and attach an image",()=>Commit(draw))};
        var matching=addItems.Where(i=>Matches(i.Item2,i.Item3)).ToArray();if(matching.Length>0){Section("Add");foreach(var i in matching)Row(i.Item1,i.Item2,i.Item3,i.Item4,i.Item2=="Goal"?()=>ai.GoalEnabled:i.Item2=="Planning mode"?()=>ai.PlanningEnabled:null,i.Item2 is "Goal" or "Planning mode"?!ai.Busy:true);}
        var plugins=PluginCatalog.Items.Where(i=>Matches(i.Title,i.Description)||i.Id.Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();if(plugins.Length>0){Section("Plugins");foreach(var (id,icon,title,description) in plugins)Row(icon,title,description,()=>Commit(()=>pluginSelected(id)),()=>ai.DraftPluginInstructions.Contains(id),!ai.Busy);}
        var available=browser.Tabs.Concat(ai.NativeTabs.Tabs.Where(t=>!browser.Tabs.Any(b=>b.Title==t.Title&&b.Browser==t.Browser))).Concat(ai.LocalBrowser.Tabs).Where(t=>Matches(t.Title,t.Url)).ToArray();
        if(available.Length>0){Section("Browser tabs");body.Children.Add(tabs);foreach(var tab in available.Take(expanded?100:3))Row("globe",tab.Title,(Uri.TryCreate(tab.Url,UriKind.Absolute,out var uri)?uri.Host:"Local tab")+" · "+tab.Browser,()=>Commit(()=>AttachTab(tab)),parent:tabs);if(available.Length>3)Row(expanded?"chevron-up":"chevron-down",expanded?"Show fewer":$"Show {available.Length-3} more tabs","",()=>{expanded=!expanded;Rebuild();},parent:tabs);Row("globe","Browser companion","Connect for richer page access",()=>Commit(BrowserCompanionSetup.Show),parent:tabs);}
        else if(query.Length==0){Section("Browser tabs");body.Children.Add(tabs);Row("globe","Connect browser","Show your open tabs with the browser companion",()=>Commit(BrowserCompanionSetup.Show),parent:tabs);}
        if(rows.Count==0){var empty=Ui.Text("No matching tools or tabs",12,"Muted");empty.Margin=new Thickness(12,18,12,18);body.Children.Add(empty);}
        highlighted=Math.Clamp(highlighted,0,Math.Max(0,rows.Count-1));Highlight();if(restoreFocus&&rows.Count>0)rows[highlighted].Focus();
    }
    async void AttachTab(BrowserTab tab)
    {
        try {var current=tab.Id.StartsWith("native/",StringComparison.Ordinal)?await ai.NativeTabs.AuthorizeAsync(tab):tab.Id.StartsWith("owned/",StringComparison.Ordinal)?ai.LocalBrowser.Tabs.FirstOrDefault(t=>t.Id==tab.Id)??throw new InvalidOperationException("This tab was closed."):browser.Authorize(tab);var shortcut=System.Text.Encoding.UTF8.GetBytes("[InternetShortcut]\r\nURL="+current.Url+"\r\n");attach(new(tab.Title+".url","text/uri-list","","Attached browser tab ID: "+current.Id+"\nURL: "+current.Url+"\nThe user selected this tab. Use browser_read_tab to inspect its live content.",shortcut.Length) {OriginalData=Convert.ToBase64String(shortcut),SourceUri=current.Url,BrowserTabId=current.Id});}
        catch(Exception e) when(e is InvalidOperationException or TimeoutException or System.Runtime.InteropServices.COMException or System.Windows.Automation.ElementNotAvailableException){MessageBox.Show("This tab could not be attached. Refresh the menu or connect the browser companion.\n\n"+e.Message,"Browser · YoWidgets",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    internal bool HandleKey(Key key)
    {
        if(!IsOpen)return false;
        if(Closing)return key is Key.Escape or Key.Enter;
        if(key==Key.Escape){CloseMenu();return true;}
        if(!MentionSearch)return false;
        if(key is Key.Down or Key.Up){if(rows.Count>0){highlighted=(highlighted+(key==Key.Down?1:rows.Count-1))%rows.Count;Highlight();}return true;}
        if(key is Key.Enter or Key.Tab){if(rows.Count>0)rows[highlighted].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));return true;}return false;
    }
    void Highlight(){for(var i=0;i<rows.Count;i++)rows[i].Background=MentionSearch&&i==highlighted?Ui.Brush("Hover"):Brushes.Transparent;if(MentionSearch&&rows.Count>0)rows[highlighted].BringIntoView();}
    void Section(string name) {var text=Ui.Text(name,11,"Muted");text.Margin=new Thickness(10,12,8,5);body.Children.Add(text);}
    void Row(string icon,string title,string description,Action action,Func<bool>? selected=null,bool enabled=true,StackPanel? parent=null)
    {
        var button=WidgetVisuals.IconButton(icon,title,action,42);button.Width=double.NaN;button.Height=description.Length==0?39:48;button.IsEnabled=enabled;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        var row=new Grid {Margin=new Thickness(10,3,10,3)};row.ColumnDefinitions.Add(new() {Width=new GridLength(30)});row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new() {Width=new GridLength(24)});
        var tone=icon switch {"file-text"=>"ComposerDocument","file-type"=>"ComposerPdf","sheet"=>"ComposerSheet","presentation"=>"ComposerSlides",_=>"Text"};var glyph=WidgetVisuals.Icon(icon,18,tone);glyph.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(glyph);var labels=Ui.Stack(Ui.Text(title,13));if(description.Length>0){var detail=Ui.Text(description,10.5,"Muted");detail.TextWrapping=TextWrapping.NoWrap;detail.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(detail);}Grid.SetColumn(labels,1);labels.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(labels);
        var check=WidgetVisuals.Icon("check",16,"Accent");Grid.SetColumn(check,2);row.Children.Add(check);check.Visibility=selected?.Invoke()==true?Visibility.Visible:Visibility.Collapsed;button.Content=row;
        (parent??body).Children.Add(button);if(enabled)rows.Add(button);
    }
    void BrowserChanged(){if(Dispatcher.CheckAccess())Rebuild();else Dispatcher.BeginInvoke(Rebuild);}
}
