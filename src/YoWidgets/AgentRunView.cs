using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Automation;
namespace YoWidgets;

internal sealed class AgentRunView : StackPanel
{
    readonly AgentWorkspace run;
    readonly AiService ai;
    readonly TextBlock feedback;
    readonly StackPanel stages=new(),files=new();
    readonly TextBlock summaryLabel;
    readonly AssistantStatusView status=new();
    readonly Func<string> thinking;
    readonly Dictionary<AgentStep,StageView> stageViews=[];
    readonly Expander history=new();
    readonly StackPanel totals=new();
    readonly Button undo;
    readonly Border summary;
    readonly DispatcherTimer activityClock=new(){Interval=TimeSpan.FromSeconds(1)};
    bool settled;
    readonly Popup popup=new() { AllowsTransparency=true,StaysOpen=true,Placement=PlacementMode.Bottom };
    readonly DispatcherTimer openTimer=new() { Interval=TimeSpan.FromMilliseconds(500) };
    readonly DispatcherTimer closeTimer=new() { Interval=TimeSpan.FromMilliseconds(180) };
    AgentFileChange? pending;
    FrameworkElement? hoverTarget;
    Border? popupCard;
    int animationVersion;
    public AgentRunView(AgentWorkspace run,AiService ai,Func<string>? thinking=null)
    {
        this.run=run;this.ai=ai;this.thinking=thinking??(()=>"Thinking…");Margin=new Thickness(0,8,0,8);
        status.Margin=new Thickness(0,3,0,8);Children.Add(status);history.Content=stages;history.IsExpanded=true;Children.Add(history);
        undo=WidgetVisuals.Pill("Undo changes",Undo);undo.HorizontalAlignment=HorizontalAlignment.Right;
        var title=new DockPanel();DockPanel.SetDock(undo,Dock.Right);title.Children.Add(undo);
        summaryLabel=WidgetVisuals.Label("Changes",12,true);summaryLabel.VerticalAlignment=VerticalAlignment.Center;
        title.Children.Add(Ui.Stack(summaryLabel,totals));
        history.SetResourceReference(StyleProperty,"AgentActionExpander");history.Margin=new Thickness(0,8,0,0);
        summary=new Border { Child=Ui.Stack(title,files),CornerRadius=new CornerRadius(14),Padding=new Thickness(10),BorderThickness=new Thickness(1),Margin=new Thickness(0,9,0,0) };
        summary.SetResourceReference(Border.BackgroundProperty,"AssistantInset");summary.SetResourceReference(Border.BorderBrushProperty,"AssistantLine");Children.Add(summary);
        feedback=WidgetVisuals.Label("",11);feedback.TextWrapping=TextWrapping.Wrap;feedback.Visibility=Visibility.Collapsed;AutomationProperties.SetLiveSetting(feedback,AutomationLiveSetting.Polite);Children.Add(feedback);
        openTimer.Tick+=(_,_)=> {openTimer.Stop();if(pending!=null&&hoverTarget!=null&&(hoverTarget.IsMouseOver||hoverTarget.IsKeyboardFocusWithin))OpenPreview(pending,hoverTarget);};
        closeTimer.Tick+=(_,_)=> {closeTimer.Stop();if(popupCard?.IsMouseOver!=true&&hoverTarget?.IsMouseOver!=true)ClosePreview();};
        activityClock.Tick+=(_,_)=>{if(!run.Running){activityClock.Stop();return;}foreach(var row in stageViews.Values)row.RefreshTiming();history.Header=ToolActivity.Header(run);};
        IsVisibleChanged+=(_,_)=>SyncClock();
        Loaded+=(_,_)=> {run.Changed+=Update;ai.Changed+=Update;Update();SyncClock();};
        Unloaded+=(_,_)=> {run.Changed-=Update;ai.Changed-=Update;openTimer.Stop();closeTimer.Stop();activityClock.Stop();popup.IsOpen=false;};Update();
    }
    void SyncClock(){if(IsLoaded&&IsVisible&&run.Running)activityClock.Start();else activityClock.Stop();}
    void Update()
    {
        if(!Dispatcher.CheckAccess()) {Dispatcher.BeginInvoke(Update);return;}
        status.Visibility=run.Running&&ai.Busy&&ReferenceEquals(ai.Messages.LastOrDefault()?.Agent,run)?Visibility.Visible:Visibility.Collapsed;status.Update(ai,thinking());
        // AiService changes frequently while streaming; rebuild only when file
        // activity changes, preserving hover targets and an open diff preview.
        var key=string.Join('|',run.Steps.Select(s=>$"{s.Tool}:{s.Status}:{s.Detail}:{s.Target}:{s.Added}:{s.Removed}:{s.EndedAt}"))+run.Running+run.Outcome+run.Undone+ai.Busy+string.Join('|',run.Changes.Select(c=>c.Undone));
        if(Tag as string==key)return;Tag=key;
        var changes=run.Changes;
        foreach(var step in run.Steps) {
            if(!stageViews.TryGetValue(step,out var row)) {row=new StageView(this,step);stageViews.Add(step,row);stages.Children.Add(row);}
            row.Update(changes.FirstOrDefault(c=>c.Path.Equals(step.Path.Replace('\\','/'),StringComparison.OrdinalIgnoreCase)));
        }
        history.Header=ToolActivity.Header(run);history.Visibility=run.Steps.Count>0?Visibility.Visible:Visibility.Collapsed;
        if(!run.Running&&!settled) {
            settled=true;activityClock.Stop();Children.Remove(history);((StackPanel)summary.Child).Children.Add(history);history.IsExpanded=false;
            if(changes.Count==0&&run.CreatedFolders.Count==0&&run.Steps.Any(s=>s.Status=="Failed"))history.IsExpanded=true;
        }
        summary.Visibility=!run.Running&&(changes.Count>0||run.CreatedFolders.Count>0||run.Steps.Count>0)?Visibility.Visible:Visibility.Collapsed;
        summaryLabel.Text=changes.Count==0&&run.CreatedFolders.Count==0?"Activity":(run.Undone?"Undone · ":"")+$"{changes.Count} changed file"+(changes.Count==1?"":"s");
        totals.Visibility=changes.Count>0?Visibility.Visible:Visibility.Collapsed;totals.Children.Clear();totals.Children.Add(Counts(changes.Sum(c=>c.Added),changes.Sum(c=>c.Removed)));
        undo.IsEnabled=!run.Running&&!ai.Busy&&!run.Undone;undo.Visibility=run.Undone||changes.Count==0&&run.CreatedFolders.Count==0?Visibility.Collapsed:Visibility.Visible;
        files.Children.Clear();foreach(var change in changes) {
            var button=WidgetVisuals.IconButton("file",change.Path,()=>Review(change));button.Width=double.NaN;button.Height=34;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,3,0,0);button.ToolTip=null;AutomationProperties.SetHelpText(button,FullPath(change.Path)+" · Click to review");
            if((change.After??change.Before) is { } data)button.ContextMenu=AttachmentActions.Menu(AttachmentActions.Snapshot(change.Path,data));
            var content=new DockPanel { Margin=new Thickness(8,0,8,0) };var counts=Counts(change.Added,change.Removed);DockPanel.SetDock(counts,Dock.Right);content.Children.Add(counts);var name=WidgetVisuals.Label((change.Undone?"Undone · ":"")+change.Path,11);name.TextTrimming=TextTrimming.CharacterEllipsis;content.Children.Add(name);button.Content=content;
            button.MouseEnter+=(_,_)=> {closeTimer.Stop();pending=change;hoverTarget=button;openTimer.Stop();openTimer.Start();};
            button.MouseLeave+=(_,_)=> {openTimer.Stop();closeTimer.Stop();closeTimer.Start();};
            button.LostKeyboardFocus+=(_,_)=> {closeTimer.Stop();closeTimer.Start();};files.Children.Add(button);
        }
        foreach(var folder in run.CreatedFolders) {var text=WidgetVisuals.Label((run.Undone?"Restored · ":"Created folder · ")+folder,11);text.Margin=new Thickness(8,7,8,3);files.Children.Add(text);}
    }
    string FullPath(string relative)
    {try {return run.Resolve(relative);}catch(Exception e) when(e is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException){return relative;}}
    sealed class StageView : Expander
    {
        readonly AgentRunView owner;
        readonly AgentStep step;
        readonly TextBlock caption=WidgetVisuals.Label("",12);
        readonly TextBlock timing=WidgetVisuals.Label("",10.5);
        readonly TextBlock detail=WidgetVisuals.Label("",11);
        readonly ContentControl counts=new(),preview=new();
        readonly ContentControl symbol=new() { Margin=new Thickness(0,0,7,0) };
        AgentFileChange? change;
        byte[]? renderedAfter;
        bool rendered;
        public StageView(AgentRunView owner,AgentStep step)
        {
            this.owner=owner;this.step=step;SetResourceReference(StyleProperty,"AgentActionExpander");
            Margin=new Thickness(5,0,0,2);caption.TextTrimming=TextTrimming.CharacterEllipsis;timing.TextTrimming=TextTrimming.CharacterEllipsis;timing.Opacity=.7;timing.Margin=new Thickness(0,3,0,0);
            var header=new DockPanel();DockPanel.SetDock(counts,Dock.Right);header.Children.Add(counts);header.Children.Add(symbol);header.Children.Add(Ui.Stack(caption,timing));Header=header;
            var fullPath=owner.FullPath(step.Path);ToolTip=AssistantStatusView.PathTooltip(fullPath);ToolTipService.SetInitialShowDelay(this,500);ToolTipService.SetShowDuration(this,60000);AutomationProperties.SetHelpText(this,fullPath);
            Expanded+=(_,e)=> {if(ReferenceEquals(e.OriginalSource,this))Render();};
            if(SystemParameters.ClientAreaAnimation){var transform=new TranslateTransform(0,4);RenderTransform=transform;transform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(180)));BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));}
        }
        internal void RefreshTiming(){timing.Text=ToolActivity.Timing(step);timing.ToolTip=AssistantStatusView.PathTooltip(ToolActivity.TimingDetail(step));if(change==null)detail.Text=Detail();}
        string Detail()=>ToolActivity.TimingDetail(step)+"\n\n"+(step.Status=="Working"?"Waiting for the current tool result…":step.Status=="Stopped"?"Stopped before completion.":step.Detail is "" or "Completed"?"Action completed.":step.Detail);
        public void Update(AgentFileChange? file)
        {
            var fullPath=owner.FullPath(step.Path);ToolTip=AssistantStatusView.PathTooltip(fullPath);AutomationProperties.SetHelpText(this,fullPath);
            change=step.Tool is "write_file" or "edit_file" or "delete_file" or "document_create" or "planning_decision"?file:null;
            var verb=step.Tool switch {"read_file"=>"Read","list_directory"=>"Listed","search_files"=>"Searched","write_file" or "document_create"=>step.Detail=="Created"?"Created":"Edited","edit_file"=>"Edited","delete_file"=>"Deleted","create_directory"=>"Created folder","planning_decision"=>"Planned","document_schema"=>"Checked format","document_inspect"=>"Inspected","document_preview"=>"Rendered page","read_web_page" or "browser_read_tab"=>"Read webpage","browser_open"=>"Opened webpage","browser_navigate"=>"Navigated webpage","browser_screenshot"=>"Captured webpage","browser_click"=>"Clicked browser control","browser_fill"=>"Filled browser field","browser_scroll"=>"Scrolled webpage","computer_list_apps"=>"Found installed apps","computer_open_app"=>"Opened app","computer_list_windows"=>"Listed windows","computer_inspect"=>"Inspected app","computer_click"=>"Clicked app control","computer_type"=>"Entered text","computer_key"=>"Pressed key","computer_scroll"=>"Scrolled app","computer_drag"=>"Dragged app control",_=>step.Tool};
            var target=step.Target.Length>0?step.Target:step.Path=="."?"":step.Path;
            if(step.Tool=="generate_media"){verb=step.Status=="Working"?"Generating media":"Generated media";target=step.Target;}
            var listSummary=step.Tool is "computer_list_apps" or "computer_list_windows"&&step.Detail.StartsWith("Found ",StringComparison.Ordinal);
            caption.Text=(step.Status=="Working"?"Working · ":step.Status=="Failed"?"Failed · ":step.Status=="Stopped"?"Stopped · ":"")+(listSummary?step.Detail:verb)+(target.Length>0?(step.Tool.StartsWith("computer_",StringComparison.Ordinal)||step.Tool.StartsWith("browser_",StringComparison.Ordinal)?" · ":" ")+target:"");
            AutomationProperties.SetName(this,caption.Text);
            symbol.Content=ToolActivity.Badge(ToolActivity.Identity(step));RefreshTiming();
            counts.Content=step.Added+step.Removed>0?Counts(step.Added,step.Removed):null;
            if(step.Status=="Failed")((ToolTip)ToolTip).Content=owner.FullPath(step.Path)+"\n"+step.Detail;
            if(change!=null)Content=preview;
            if(change==null){detail.Text=Detail();detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(31,5,4,10);Content=detail;}
            if(step.Status=="Failed")caption.Text+=" · "+step.Detail;
            if(IsExpanded)Render();
        }
        void Render()
        {
            if(change==null||rendered&&ReferenceEquals(renderedAfter,change.After))return;
            preview.Content=PreviewCard(change,double.NaN,true,owner.FullPath(change.Path));rendered=true;renderedAfter=change.After;
            if(SystemParameters.ClientAreaAnimation)preview.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));
        }
    }
    static StackPanel Counts(int added,int removed)
    {
        var row=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center };
        row.Children.Add(new TextBlock { Text="+"+added,Foreground=new SolidColorBrush(Color.FromRgb(100,225,143)),FontSize=11 });row.Children.Add(new TextBlock { Text=" −"+removed,Foreground=new SolidColorBrush(Color.FromRgb(255,145,145)),FontSize=11 });return row;
    }
    void Undo()
    {
        if(ai.Busy||run.Running)return;
        feedback.Text=run.Undo();feedback.Visibility=Visibility.Visible;Update();
    }
    void OpenPreview(AgentFileChange change,FrameworkElement target)
    {
        animationVersion++;popup.PlacementTarget=target;popup.HorizontalOffset=0;popup.VerticalOffset=3;
        popupCard=PreviewCard(change,Math.Min(680,SystemParameters.WorkArea.Width*.75),true,FullPath(change.Path));
        popupCard.MouseEnter+=(_,_)=>closeTimer.Stop();popupCard.MouseLeave+=(_,_)=> {closeTimer.Stop();closeTimer.Start();};
        popup.Child=popupCard;popup.IsOpen=true;
        if(SystemParameters.ClientAreaAnimation)popupCard.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));
    }
    void ClosePreview()
    {
        if(!popup.IsOpen)return;var version=++animationVersion;
        if(popupCard!=null&&SystemParameters.ClientAreaAnimation) {var fade=new DoubleAnimation(0,TimeSpan.FromMilliseconds(140));fade.Completed+=(_,_)=> {if(version==animationVersion)popup.IsOpen=false;};popupCard.BeginAnimation(OpacityProperty,fade);}else popup.IsOpen=false;
    }
    void Review(AgentFileChange change)
    {
        openTimer.Stop();popup.IsOpen=false;
        var review=new Window { Title=change.Path+" · Changes",Width=Math.Min(900,SystemParameters.WorkArea.Width*.85),Height=Math.Min(680,SystemParameters.WorkArea.Height*.85),Background=Ui.Brush("Background"),Foreground=Ui.Brush("Text"),Content=PreviewCard(change,double.NaN,false,FullPath(change.Path)),WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=Window.GetWindow(this) };
        review.Show();
    }
    static Border PreviewCard(AgentFileChange change,double width,bool compact,string? fullPath=null)
    {
        var body=new StackPanel();var header=new DockPanel { Margin=new Thickness(12) };var counts=Counts(change.Added,change.Removed);DockPanel.SetDock(counts,Dock.Right);header.Children.Add(counts);header.Children.Add(Ui.Text(change.Path,12));body.Children.Add(header);
        if((change.After??change.Before) is { } bytes) {var attachment=AttachmentActions.Snapshot(change.Path,bytes);header.ContextMenu=AttachmentActions.Menu(attachment);header.Cursor=Cursors.Hand;header.MouseLeftButtonUp+=(_,e)=>{e.Handled=true;AttachmentActions.Open(attachment);};}
        if(fullPath!=null) {var path=Ui.Text(fullPath,10,"Muted");path.Margin=new Thickness(12,0,12,9);body.Children.Add(path);}
        var images=ImageComparison(change);if(images!=null)body.Children.Add(images);
        else {
            var diff=new StackPanel();var lines=change.Diff;var important=new HashSet<int>();for(var i=0;i<lines.Count;i++)if(lines[i].Kind!=' ')for(var j=Math.Max(0,i-2);j<=Math.Min(lines.Count-1,i+2);j++)important.Add(j);
            var selected=compact?important.Order().Take(160).ToArray():Enumerable.Range(0,Math.Min(10000,lines.Count)).ToArray();int previous=-1;
            foreach(var index in selected) {if(previous>=0&&index>previous+1)diff.Children.Add(Ui.Text("   …",11,"Muted"));previous=index;var line=lines[index];
                var row=new DockPanel { Background=line.Kind=='+'?new SolidColorBrush(Color.FromArgb(45,63,177,98)):line.Kind=='-'?new SolidColorBrush(Color.FromArgb(45,220,79,79)):Brushes.Transparent };
                row.Children.Add(new TextBlock { Text=$"{line.BeforeLine,4} {line.AfterLine,4} {line.Kind} ",FontFamily=new FontFamily("Cascadia Code, Consolas"),FontSize=11,Foreground=Ui.Brush("Muted"),Padding=new Thickness(8,2,0,2) });
                row.Children.Add(new TextBlock { Text=line.Text.Length>2000?line.Text[..2000]+" … [line shortened]":line.Text,FontFamily=new FontFamily("Cascadia Code, Consolas"),FontSize=11,Foreground=Ui.Brush("Text"),Padding=new Thickness(0,2,10,2) });diff.Children.Add(row);
            }
            if(lines.Count==0)diff.Children.Add(Ui.Text("Binary or empty file · "+change.Kind,12,"Muted"));
            else if(important.Count==0)diff.Children.Add(Ui.Text("Only encoding, line endings or the final newline changed.",11,"Muted"));
            if(compact&&important.Count>160||!compact&&lines.Count>10000)diff.Children.Add(Ui.Text("Preview shortened. Inspect the saved file for the complete contents.",11,"Muted"));
            body.Children.Add(new ScrollViewer { Content=diff,MaxHeight=compact?350:520,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
        }
        return new Border { Child=body,Width=width,Background=Ui.Brush("Panel"),BorderBrush=Ui.Brush("Line"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(0,0,0,8) };
    }
    static UIElement? ImageComparison(AgentFileChange change)
    {
        if(!new[]{".png",".jpg",".jpeg",".gif",".bmp",".webp"}.Contains(Path.GetExtension(change.Path).ToLowerInvariant()))return null;
        var grid=new System.Windows.Controls.Primitives.UniformGrid { Columns=2,Margin=new Thickness(10) };
        foreach(var (name,bytes) in new[]{("Before",change.Before),("After",change.After)}) {
            var stack=Ui.Stack(Ui.Text(name,11,"Muted"));
            if(bytes!=null)try {using var stream=new MemoryStream(bytes);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=640;image.StreamSource=stream;image.EndInit();image.Freeze();var attachment=AttachmentActions.Snapshot(change.Path,bytes);var view=new System.Windows.Controls.Image { Source=image,Height=220,Stretch=Stretch.Uniform,Margin=new Thickness(6),Cursor=Cursors.Hand,ContextMenu=AttachmentActions.Menu(attachment) };view.MouseLeftButtonUp+=(_,e)=> {e.Handled=true;AttachmentActions.Open(attachment);};stack.Children.Add(view);}catch {stack.Children.Add(Ui.Text("Preview unavailable",11,"Muted"));}
            else stack.Children.Add(Ui.Text("No file",11,"Muted"));grid.Children.Add(stack);
        }
        return grid;
    }
}
