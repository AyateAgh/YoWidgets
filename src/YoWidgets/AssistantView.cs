using System.Windows.Automation;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class AssistantView : Border
{
    readonly WidgetManager manager;
    readonly AiService ai;
    readonly WidgetWindow? window;
    readonly bool preview;
    readonly Grid layout = new();
    readonly Grid response = new();
    readonly StackPanel timeline = new();
    readonly ScrollViewer scroll;
    readonly TextBox input;
    readonly TextBlock placeholder;
    readonly TextBlock error;
    readonly AssistantStatusView processRow;
    readonly VoiceGlow glow = new();
    readonly Button composerBrand;
    ChatConnectionMenu? connections;
    readonly ContentControl headerBrand = new() { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    string brand = "";
    readonly StackPanel fileRow = new() { Orientation = Orientation.Horizontal };
    readonly ScrollViewer fileScroll;
    readonly WrapPanel workspaceBar=new() {Margin=new Thickness(2,0,0,5),Visibility=Visibility.Collapsed };
    readonly Border goalChip,planningChip;
    ComposerMenu? menu;
    readonly WrapPanel instructionRow=new() {Margin=new Thickness(3,0,0,4),Visibility=Visibility.Collapsed};
    string[] displayedInstructions=[];
    readonly RotateTransform plusRotation=new();
    bool mentionQueued,plusMenuOpen;
    MentionQuery? dismissedMention;
    readonly Button workspaceChoice;
    readonly Button workspaceClose;
    readonly Button send;
    readonly Button microphone;
    readonly Button add;
    readonly Button retry;
    readonly TextBlock notice;
    readonly DispatcherTimer noticeTimer = new() { Interval = TimeSpan.FromSeconds(1.6) };
    readonly Border composer;
    readonly VoiceInput voice;
    readonly AudioRecorder recorder;
    VoiceCallView? callView;
    internal Func<string,string,string,string,string,LiveVoiceSession>? VoiceSessionFactory {get;set;}
    CancellationTokenSource? transcription;
    bool transcribing;
    readonly Dictionary<ChatMessage, (ContentControl Body, StackPanel Row, StackPanel Actions)> rendered = [];
    readonly Dictionary<ChatMessage, string> renderedText = [];
    readonly Dictionary<ChatMessage,GeneratedMediaView> renderedMedia=[];
    readonly ContentControl mediaReferenceChip=new();
    AssistantAttachment[] displayedFiles = [];
    CancellationTokenSource? fileWork;
    ChatMessage? lastUser;
    bool responseOpen;
    double editorHeight = 34;
    bool editorMeasureQueued;
    bool dropActive;
    bool composing;
    bool atBottom = true;
    bool changing;
    bool readingFiles;
    string localError = "";
    string speechPrefix = "";
    bool wasListening;
    int sessionVersion;
    public AssistantView(WidgetManager manager, bool preview, WidgetWindow? window = null)
    {
        this.manager = manager; this.window = window; ai = manager.Data.Ai; this.preview = preview;
        sessionVersion = ai.SessionVersion;
        Background = Brushes.Transparent; CornerRadius = new CornerRadius(20); ClipToBounds = true;
        Resources["RecordingInk"] = Brushes.White;
        voice = new VoiceInput(Dispatcher);
        recorder = new AudioRecorder(Dispatcher);
        layout.RowDefinitions.Add(new()); layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); Child = layout;
        if(window != null) window.AttachAssistantGlow(glow); else { Child=null; var layers=new Grid(); layers.Children.Add(glow); layers.Children.Add(layout); Child=layers; }
        response.RowDefinitions.Add(new() { Height = GridLength.Auto });
        response.RowDefinitions.Add(new() { Height = GridLength.Auto });
        response.RowDefinitions.Add(new());
        response.RowDefinitions.Add(new() { Height = GridLength.Auto });
        response.Margin = new Thickness(8, 4, 8, 8); response.Visibility = Visibility.Collapsed;
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        toolbar.Children.Add(WidgetVisuals.IconButton("new-chat", "New chat", NewChat, 28));
        toolbar.Children.Add(WidgetVisuals.IconButton("collapse", "Collapse conversation", Collapse, 28)); var header = new Grid(); header.Children.Add(headerBrand); header.Children.Add(toolbar); response.Children.Add(header);
        processRow = new AssistantStatusView { Margin = new Thickness(2, 8, 0, 8), Visibility = Visibility.Collapsed };
        timeline.Children.Add(processRow);
        scroll = new ScrollViewer { Content = timeline, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 4, 0) };
        scroll.ScrollChanged += (_, e) => { if (e.ExtentHeightChange == 0) atBottom = scroll.ScrollableHeight - scroll.VerticalOffset < 40; };
        Grid.SetRow(scroll, 2); response.Children.Add(scroll);
        retry = WidgetVisuals.Pill("Try again", async () => { if (!preview && !manager.Snoozed) { StopVoice(); await ai.RetryAsync(manager.Settings); } }); retry.HorizontalAlignment = HorizontalAlignment.Left;
        notice = Label("",11); notice.Visibility = Visibility.Collapsed; AutomationProperties.SetLiveSetting(notice,AutomationLiveSetting.Polite);
        noticeTimer.Tick += (_,_) => { noticeTimer.Stop(); notice.Visibility = Visibility.Collapsed; };
        var feedback = Ui.Stack(retry,notice); Grid.SetRow(feedback, 3); response.Children.Add(feedback); layout.Children.Add(response);
        input = Ui.Input(ai.Draft); input.Background = Brushes.Transparent; input.BorderThickness = new Thickness(0); input.Padding = new Thickness(3, 7, 3, 5); input.FontSize = 14; input.FontWeight = FontWeights.Normal; input.AcceptsReturn = true; input.TextWrapping = TextWrapping.Wrap; input.MaxLength = AssistantProtocol.MaxInput;
        input.SetResourceReference(Control.ForegroundProperty, "WidgetInk"); input.SetResourceReference(TextBox.CaretBrushProperty, "WidgetInk"); input.MinHeight = 34; input.MinLines = 1; input.MaxLines = 4; input.Height = 34; input.FocusVisualStyle = null; input.SetResourceReference(StyleProperty,"ComposerInput"); input.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
        AutomationProperties.SetName(input, "Message to assistant");
        placeholder = Label("Message…", 14); placeholder.Margin = new Thickness(4, 7, 0, 0); placeholder.VerticalAlignment = VerticalAlignment.Top; placeholder.Opacity = .78; placeholder.IsHitTestVisible = false;
        var field = new Grid(); field.Children.Add(placeholder); field.Children.Add(input);
        add = WidgetVisuals.IconButton("new-chat", "Add attachments and tools", OpenFileMenu, 32);
        composerBrand=WidgetVisuals.IconButton("sparkle","Switch chat connection",OpenConnectionMenu,28);composerBrand.Margin=new Thickness(0,0,5,2);composerBrand.VerticalAlignment=VerticalAlignment.Bottom;
        var plus=new PremiumIcon("plus",18) {AnimateOnHover=false};add.Content=new Border {Child=plus,RenderTransform=plusRotation,RenderTransformOrigin=new Point(.5,.5)};
        workspaceChoice=WidgetVisuals.Pill("Agent folder",ChooseWorkspace);workspaceChoice.MaxWidth=240;workspaceChoice.ToolTip="Read and edit files in this folder. Undo is available in this chat until you quit.";
        workspaceClose=WidgetVisuals.IconButton("close","Switch to chat without file access",()=>ai.SetWorkspace(null),24);workspaceBar.Children.Add(workspaceChoice);workspaceBar.Children.Add(workspaceClose);
        goalChip=ModeChip("Goal",()=>ai.SetGoal(false));planningChip=ModeChip("Planning",()=>ai.SetPlanning(false));workspaceBar.Children.Add(goalChip);workspaceBar.Children.Add(planningChip);
        microphone = WidgetVisuals.IconButton("microphone", "Start voice input", ToggleVoice, 32);
        send = WidgetVisuals.IconButton("send", "Send message", async () => {if(!ai.Busy&&EmptyCallDraft&&!recorder.Recording&&!voice.Listening)StartCall();else await SendAsync();}, 34); send.SetResourceReference(Control.BackgroundProperty, "WidgetInk");
        var inputRow = new Grid(); inputRow.ColumnDefinitions.Add(new() { Width = new GridLength(34) }); inputRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); inputRow.ColumnDefinitions.Add(new()); inputRow.ColumnDefinitions.Add(new() { Width = new GridLength(34) }); inputRow.ColumnDefinitions.Add(new() { Width = new GridLength(36) });
        inputRow.Children.Add(add); Grid.SetColumn(composerBrand, 1); inputRow.Children.Add(composerBrand); Grid.SetColumn(field, 2); inputRow.Children.Add(field); Grid.SetColumn(microphone, 3); inputRow.Children.Add(microphone); Grid.SetColumn(send, 4); inputRow.Children.Add(send);
        add.VerticalAlignment = microphone.VerticalAlignment = send.VerticalAlignment = VerticalAlignment.Bottom;
        fileScroll = new ScrollViewer { Content = fileRow, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 96, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 6) };
        error = Label("", 11.5); error.TextWrapping = TextWrapping.Wrap; error.Margin = new Thickness(4, 3, 4, 7); error.Visibility = Visibility.Collapsed; AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Assertive);
        composer = new Border { Child = Ui.Stack(workspaceBar,fileScroll,mediaReferenceChip,instructionRow, error, inputRow), Padding = new Thickness(8), CornerRadius = new CornerRadius(20), Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
        instructionRow.SizeChanged+=(_,_)=>RequestSize();
        glow.ReadLevel = () => recorder.Recording ? recorder.Level : voice.Level;
        Grid.SetRow(composer, 1); layout.Children.Add(composer);
        input.TextChanged += (_, _) => { if (!changing) { if (voice.Listening) voice.Stop(); ai.Draft = input.Text; } ScheduleEditorMeasure(); UpdateComposer(); RequestSize();QueueMentionSearch(); };
        input.SelectionChanged+=(_,_)=>QueueMentionSearch();
        PreviewKeyDown+=(_,e)=>{if(composing)return;if(connections?.HandleKey(e.Key)==true||menu?.HandleKey(e.Key)==true){e.Handled=true;return;}if(callView!=null){if(e.Key==Key.Escape){callView.Escape();e.Handled=true;}return;}if(e.Key==Key.Escape){if(voice.Listening||recorder.Recording||transcribing)StopVoice();else if(ai.Busy)ai.Cancel();else if(responseOpen)Collapse();e.Handled=true;}};
        input.SizeChanged += (_,e) => { if(Math.Abs(e.PreviousSize.Width-e.NewSize.Width)>.5) ScheduleEditorMeasure(); };
        TextCompositionManager.AddPreviewTextInputStartHandler(input, (_, _) => composing = true);
        TextCompositionManager.AddPreviewTextInputHandler(input, (_, _) => composing = false);
        input.PreviewKeyDown += async (_, e) => {
            if(!composing&&e.ImeProcessedKey==Key.None&&menu?.HandleKey(e.Key)==true){e.Handled=true;return;}
            if((e.Key==Key.V&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)||e.Key==Key.Insert&&Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))&&!preview&&!manager.Snoozed) {
                try {var data=Clipboard.GetDataObject();if(data!=null&&AssistantClipboard.HasAttachment(data)) {e.Handled=true;var pasted=AssistantClipboard.Read(data);if(pasted.Paths.Length>0)await AddFilesAsync(pasted.Paths);else if(pasted.Image!=null)AddAttachment(pasted.Image);}}catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidOperationException or IOException) {e.Handled=true;Notice(ex is InvalidOperationException?ex.Message:"The clipboard is busy. Try pasting again.");}
            }
            else if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !composing && e.ImeProcessedKey == Key.None) { e.Handled = true; await SendAsync(); }
            else if (e.Key == Key.Escape) { if (voice.Listening || recorder.Recording || transcribing) { StopVoice(); e.Handled = true; } else if (ai.Busy) { ai.Cancel(); e.Handled = true; } else if (responseOpen) { Collapse(); e.Handled = true; } }
        };
        AllowDrop = true;
        PreviewDragOver += (_, e) => { if(!e.Data.GetDataPresent(DataFormats.FileDrop)) return; e.Effects=preview || readingFiles || manager.Snoozed ? DragDropEffects.None : DragDropEffects.Copy; SetDrop(e.Effects==DragDropEffects.Copy); e.Handled=true; };
        DragLeave += (_,e) => { var point=e.GetPosition(this); if(point.X<0 || point.Y<0 || point.X>ActualWidth || point.Y>ActualHeight) SetDrop(false); };
        PreviewDrop += async (_,e) => { SetDrop(false); if(!e.Data.GetDataPresent(DataFormats.FileDrop))return; e.Handled=true; if(e.Data.GetData(DataFormats.FileDrop) is string[] files) await AddFilesAsync(files); };
        voice.Transcript += (text, final) => { if (!voice.Listening) return; changing = true; input.Text = (speechPrefix + (speechPrefix.Length > 0 ? " " : "") + text)[..Math.Min(AssistantProtocol.MaxInput, speechPrefix.Length + (speechPrefix.Length > 0 ? 1 : 0) + text.Length)]; input.CaretIndex = input.Text.Length; changing = false; ai.Draft = input.Text; if (final) speechPrefix = input.Text; };
        voice.Changed += () => { if (wasListening && !voice.Listening) microphone.BeginAnimation(OpacityProperty, null); if (voice.Error.Length > 0) localError = voice.Error; wasListening = voice.Listening; UpdateComposer(); ShowError(); RequestSize(); };
        recorder.Changed += () => { if (recorder.Error.Length > 0) localError = recorder.Error; if (!recorder.Recording) microphone.BeginAnimation(OpacityProperty,null); UpdateComposer(); ShowError(); RequestSize(); };
        recorder.Captured += async audio => await TranscribeAsync(audio);
        Loaded += (_, _) => { ai.Changed += Refresh; manager.Changed += Refresh; if (window != null) window.AssistantPlacementChanged += PlacePanels; ScheduleEditorMeasure(); Refresh(); };
        Unloaded += (_, _) => {callView?.Leave();if(menu!=null)menu.IsOpen=false;if(connections!=null)connections.IsOpen=false;noticeTimer.Stop(); fileWork?.Cancel(); StopVoice(); ai.Changed -= Refresh; manager.Changed -= Refresh; if (window != null) window.AssistantPlacementChanged -= PlacePanels; SetDrop(false); StopDots(); microphone.BeginAnimation(OpacityProperty, null); };
        Refresh();
    }
    static TextBlock Label(string value, double size = 13) { var text = WidgetVisuals.Label(value, size); text.FontWeight = FontWeights.Normal; return text; }
    public void Reveal() { responseOpen = ai.Messages.Count > 0; Refresh(); }
    void Collapse() { responseOpen = false; Refresh(); }
    void NewChat() { StopVoice(); fileWork?.Cancel(); ai.NewChat(); input.Clear(); localError = ""; responseOpen = false; editorHeight = 34; Refresh(); input.Focus(); }
    void PlacePanels()
    {
        var up = window?.AssistantExpandsUp ?? true;
        Grid.SetRow(response, up ? 0 : 1); Grid.SetRow(composer, up ? 1 : 0);
        if(callView!=null)Grid.SetRow(callView,up?0:1);
        layout.RowDefinitions[0].Height = up ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        layout.RowDefinitions[1].Height = up ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
    }
    void RequestSize()
    {
        var height = responseOpen ? AssistantLayout.ResponseHeight : AssistantLayout.CompactHeight + editorHeight - 34;
        if(callView!=null){window?.ResizeAssistant(420);PlacePanels();return;}
        if (!responseOpen && ai.DraftAttachments.Count > 0) height += 96;
        if (!responseOpen && error.Visibility == Visibility.Visible) height += 52;
        if (!responseOpen && workspaceBar.Visibility==Visibility.Visible) height += Math.Max(35,workspaceBar.ActualHeight+5);
        if(!responseOpen&&instructionRow.Visibility==Visibility.Visible)height+=Math.Max(29,instructionRow.ActualHeight+4);
        if(!responseOpen&&ai.DraftMediaReference!=null)height+=32;
        window?.ResizeAssistant(height);
        PlacePanels();
    }
    void SetDrop(bool active)
    {
        if(dropActive==active)return; dropActive=active;
        window?.SetAssistantDrop(active);
        if(window==null) { BorderBrush=active?Ui.Brush("Accent"):Brushes.Transparent; BorderThickness=new Thickness(active?2:0); }
    }
    void ScheduleEditorMeasure()
    {
        if(editorMeasureQueued)return; editorMeasureQueued=true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=> {
            editorMeasureQueued=false; if(!IsLoaded || input.ActualWidth<10)return;
            var current=input.ActualHeight;
            var glyph=new FormattedText("Ag",System.Globalization.CultureInfo.CurrentUICulture,input.FlowDirection,new Typeface(input.FontFamily,input.FontStyle,input.FontWeight,input.FontStretch),input.FontSize,Brushes.White,VisualTreeHelper.GetDpi(input).PixelsPerDip);
            var lineHeight=Math.Max(glyph.Height,input.GetRectFromCharacterIndex(0).Height);
            var maximum=Math.Ceiling(lineHeight*4+input.Padding.Top+input.Padding.Bottom);
            var measure=new FormattedText(input.Text+"\u200b",System.Globalization.CultureInfo.CurrentUICulture,input.FlowDirection,new Typeface(input.FontFamily,input.FontStyle,input.FontWeight,input.FontStretch),input.FontSize,Brushes.White,VisualTreeHelper.GetDpi(input).PixelsPerDip){MaxTextWidth=Math.Max(1,input.ActualWidth-input.Padding.Left-input.Padding.Right-9),LineHeight=lineHeight};
            var target=input.Text.Length==0?34:Math.Clamp(Math.Ceiling(measure.Height+input.Padding.Top+input.Padding.Bottom),34,Math.Max(34,maximum));
            if(Math.Abs(target-editorHeight)>.5||Math.Abs((double)input.GetAnimationBaseValue(HeightProperty)-target)>.5){input.BeginAnimation(HeightProperty,null);input.Height=target;if(SystemParameters.ClientAreaAnimation)input.BeginAnimation(HeightProperty,new DoubleAnimation(current,target,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});editorHeight=target;RequestSize();}
        });
    }
    async Task SendAsync()
    {
        if (preview || manager.Snoozed || !ai.Busy && (readingFiles || transcribing)) return;
        if (ai.Busy) { StopVoice(); ai.Cancel(); return; }
        if (recorder.Recording) { recorder.Stop(true); return; }
        voice.Stop();
        if (string.IsNullOrWhiteSpace(input.Text) && ai.DraftAttachments.Count == 0) return;
        localError = "";
        var previous = ai.Messages.LastOrDefault(m => m.Role == "user");
        var pending = ai.SendAsync(input.Text, manager.Settings, ai.DraftAttachments.ToArray());
        if (!ReferenceEquals(previous, ai.Messages.LastOrDefault(m => m.Role == "user"))) { changing = true; input.Clear(); changing = false; editorHeight = 34; responseOpen = true; atBottom = true; input.Focus(); RenderFiles(); RequestSize(); }
        await pending;
    }
    void Refresh()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(Refresh); return; }
        callView?.ConfigurationChanged();
        if (sessionVersion != ai.SessionVersion) { sessionVersion = ai.SessionVersion; fileWork?.Cancel(); StopVoice(); changing = true; input.Text = ai.Draft; changing = false; ScheduleEditorMeasure(); localError = ""; notice.Visibility = Visibility.Collapsed; }
        if (manager.Snoozed && (voice.Listening || recorder.Recording || transcribing)) StopVoice();
        var user = ai.Messages.LastOrDefault(m => m.Role == "user");
        if (user != null && !ReferenceEquals(lastUser, user)) responseOpen = true;
        lastUser = user;
        if (ai.Messages.Count == 0) responseOpen = false;
        response.Visibility = responseOpen ? Visibility.Visible : Visibility.Collapsed;
        workspaceBar.Visibility=ai.WorkspaceRoot==null&&!ai.GoalEnabled&&!ai.PlanningEnabled?Visibility.Collapsed:Visibility.Visible;
        workspaceChoice.Visibility=workspaceClose.Visibility=ai.WorkspaceRoot==null?Visibility.Collapsed:Visibility.Visible;
        goalChip.Visibility=ai.GoalEnabled?Visibility.Visible:Visibility.Collapsed;planningChip.Visibility=ai.PlanningEnabled?Visibility.Visible:Visibility.Collapsed;
        goalChip.ToolTip=ai.GoalText.Length==0?"The next prompt becomes this chat's goal.":"Chat goal is active";workspaceChoice.MaxWidth=ai.GoalEnabled||ai.PlanningEnabled?130:240;
        goalChip.IsEnabled=planningChip.IsEnabled=!ai.Busy;
        workspaceChoice.Content=WidgetVisuals.Label("Agent · "+Path.GetFileName(ai.WorkspaceRoot??""),11,true);workspaceChoice.IsEnabled=workspaceClose.IsEnabled=!ai.Busy&&!readingFiles;
        foreach (var stale in rendered.Keys.Where(m => !ai.Messages.Contains(m)).ToArray()) { timeline.Children.Remove(rendered[stale].Row); rendered.Remove(stale); renderedText.Remove(stale);renderedMedia.Remove(stale); }
        foreach (var message in ai.Messages)
        {
            if (!rendered.TryGetValue(message, out var part))
            {
                var row = new StackPanel { Margin = new Thickness(0, 7, 0, 12) };
                var body = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
                var contents = new StackPanel();
                if(message.PluginInstructions.Count>0){var selected=new WrapPanel {Margin=new Thickness(0,0,0,4)};foreach(var id in message.PluginInstructions)selected.Children.Add(PluginCatalog.Chip(id));contents.Children.Add(selected);}
                if(message.Attachments.Count>0) { var files=new WrapPanel { Margin=new Thickness(0,0,0,6) }; foreach(var file in message.Attachments) files.Children.Add(AttachmentPreview.Tile(file,inMessage:true,notice:Notice)); contents.Children.Add(files); }
                contents.Children.Add(body);
                if(message.Role=="assistant"){var media=new GeneratedMediaView(message,ai,EditMedia,Notice);renderedMedia[message]=media;contents.Children.Add(media);}
                if(message.Role=="user") {
                    var bubble=new Border { Child=contents,CornerRadius=new CornerRadius(18),Padding=new Thickness(13,10,13,10),BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Right };
                    bubble.SetResourceReference(BackgroundProperty,"AssistantBubble"); bubble.SetResourceReference(BorderBrushProperty,"AssistantLine");
                    row.Children.Add(bubble); row.SizeChanged+=(_,e)=>bubble.MaxWidth=Math.Max(80,e.NewSize.Width*.9);
                    bubble.MaxWidth=Math.Max(80,response.ActualWidth*.9);
                } else { contents.Margin=new Thickness(2,5,2,0); if(message.Agent!=null)contents.Children.Add(new AgentRunView(message.Agent,ai,()=>manager.Settings.AiProcessText));row.Children.Add(contents); }
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
                actions.HorizontalAlignment = message.Role=="user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                actions.Children.Add(WidgetVisuals.IconButton("copy", "Copy message", () => AssistantMarkdown.Copy(message.Text, Notice), 28));
                if (message.Role == "assistant") actions.Children.Add(WidgetVisuals.IconButton("retry", "Regenerate response", async () => { if (!preview && !manager.Snoozed) { StopVoice(); await ai.RetryAsync(manager.Settings); } }, 28));
                row.Children.Add(actions); HoverReveal.Attach(row,actions); timeline.Children.Insert(timeline.Children.Count-1,row); part = (body, row, actions); rendered.Add(message, part); Animate(row);
            }
            if (!renderedText.TryGetValue(message, out var old) || old != message.Text)
            {
                if (message.Role == "assistant") part.Body.Content = AssistantMarkdown.Render(message.Text, Notice);
                else { var text = Label(message.Text, 14); text.TextWrapping = TextWrapping.Wrap; text.TextTrimming = TextTrimming.None; part.Body.Content = text; }
                renderedText[message] = message.Text;
            }
            if(renderedMedia.TryGetValue(message,out var mediaView))mediaView.Refresh();
            part.Row.Visibility = message.Role=="assistant" && message.Text.Length==0 && message.Agent==null&&message.Media.Count==0 ? Visibility.Collapsed : Visibility.Visible;
            part.Body.Visibility=message.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
            part.Actions.Visibility = message.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            if(part.Actions.Children.Count>1) part.Actions.Children[1].IsEnabled=!ai.Busy;
            if (part.Actions.Children.Count > 1) part.Actions.Children[1].Visibility = ReferenceEquals(message, ai.Messages.LastOrDefault()) && !preview ? Visibility.Visible : Visibility.Collapsed;
        }
        var agentActive=ai.Messages.LastOrDefault()?.Agent!=null;
        processRow.Visibility = ai.Busy && responseOpen&&!agentActive&&ai.Messages.LastOrDefault()?.Media.Count==0 ? Visibility.Visible : Visibility.Collapsed;
        processRow.Update(ai,manager.Settings.AiProcessText);
        var selectedBrand = manager.Settings.AiProvider.ToLowerInvariant();
        if (selectedBrand != brand) { brand = selectedBrand; composerBrand.Content = new PremiumIcon(brand,18,"WidgetInk",true);headerBrand.Content = new PremiumIcon(brand,18,"WidgetInk",true); }
        composerBrand.Visibility=Visibility.Visible;headerBrand.Visibility=Visibility.Collapsed;composerBrand.ToolTip="Switch chat connection · "+manager.Settings.AiProvider;
        window?.SetAssistantReadability(responseOpen);
        retry.Visibility = ai.Error.Length > 0 && ai.CanRetry ? Visibility.Visible : Visibility.Collapsed; retry.IsEnabled = !manager.Snoozed && !preview;
        mediaReferenceChip.Content=ai.DraftMediaReference is {} reference?ModeChip("Edit · "+reference.Model,ai.RemoveMediaReference):null;
        ShowError(); RenderFiles();RenderInstructions(); UpdateComposer(); RequestSize();
        if (atBottom) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { if (atBottom) scroll.ScrollToEnd(); });
    }
    void StopDots() { processRow.Active = false; glow.Active = false; }
    void EditMedia(GeneratedMedia media,AssistantAttachment? annotation,string prompt)
    {if(ai.Busy){Notice("Wait for the current response or stop it before editing media.");return;}ai.ReferenceMedia(media,prompt);input.Text=prompt;input.Focus();Refresh();}
    void ShowError() { error.Text = localError.Length > 0 ? localError : ai.Error; error.Visibility = error.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible; }
    void UpdateComposer()
    {
        composer.Visibility=callView==null?Visibility.Visible:Visibility.Collapsed;
        if(callView!=null)response.Visibility=Visibility.Collapsed;
        var listening = voice.Listening || recorder.Recording;
        glow.Processing = transcribing || ai.Busy; glow.Active = listening || transcribing || ai.Busy;
        placeholder.Text = listening ? "Listening…" : transcribing ? "Transcribing…" : readingFiles ? "Reading files…" : "Message…";
        placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var startCall=!ai.Busy&&EmptyCallDraft&&!listening;
        WidgetVisuals.UpdateIcon(send,ai.Busy ? "stop" : startCall?"audio-lines":"send",18,"AssistantButtonInk"); send.ToolTip = ai.Busy ? "Stop response" : startCall?"Start call":"Send message"; AutomationProperties.SetName(send, (string)send.ToolTip);
        send.IsEnabled = !preview && !manager.Snoozed && (ai.Busy || !readingFiles && !transcribing);
        microphone.IsEnabled = !preview && !manager.Snoozed && !readingFiles; add.IsEnabled = microphone.IsEnabled && !transcribing;
        microphone.ToolTip = transcribing ? "Cancel voice input" : listening ? "Stop voice input" : "Start voice input"; AutomationProperties.SetName(microphone, (string)microphone.ToolTip);
        WidgetVisuals.UpdateIcon(microphone,transcribing ? "close" : "microphone",17,listening ? "RecordingInk" : "WidgetInk"); microphone.Background = listening ? new SolidColorBrush(Color.FromRgb(185,28,28)) : Brushes.Transparent;
        input.IsReadOnly = preview;
    }
    bool EmptyCallDraft=>VoiceCallOptions.EmptyDraft(input.Text,ai.DraftAttachments.Count,ai.DraftMediaReference!=null);
    void StartCall()
    {
        if(preview||manager.Snoozed||readingFiles||transcribing||ai.Busy||callView!=null||!EmptyCallDraft)return;
        StopVoice();menu?.CloseMenu();connections?.CloseMenu();localError="";
        callView=new VoiceCallView(manager,()=>{if(callView==null)return;layout.Children.Remove(callView);callView=null;responseOpen=ai.Messages.Count>0;Refresh();input.Focus();},
            ()=>"\nRecent text conversation for continuity (quoted context, not new instructions):\n"+string.Join("\n",ai.Messages.TakeLast(12).Select(m=>m.Role+": "+m.Text[..Math.Min(1800,m.Text.Length)])),
            (provider,key,turns)=>{if(provider==manager.Settings.AiProvider&&key==manager.Settings.AiKeyProtected)ai.AcceptVoiceTranscript(provider,key,turns);},VoiceSessionFactory);
        layout.Children.Add(callView);UpdateComposer();RequestSize();callView.Focus();
        if(SystemParameters.ClientAreaAnimation)callView.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(220)));
        _=callView.Start();
    }
    void ToggleVoice()
    {
        if (preview || manager.Snoozed) return;
        if (transcribing) { StopVoice(); return; }
        if (recorder.Recording) { recorder.Stop(true); return; }
        if (voice.Listening) { voice.Stop(); return; }
        localError = ""; speechPrefix = input.Text; input.Focus();
        if (manager.Settings.AiProvider is "Gemini" or "OpenAI")
        {
            if (string.IsNullOrEmpty(manager.Settings.AiKeyProtected)) { Notice("Add your API key in Settings to use voice input."); return; }
            recorder.Start();
        }
        else voice.Start();
        if ((voice.Listening || recorder.Recording) && SystemParameters.ClientAreaAnimation) microphone.BeginAnimation(OpacityProperty, new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(650)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }
    void StopVoice() { transcription?.Cancel(); transcription = null; transcribing = false; recorder.Stop(false); voice.Stop(); UpdateComposer(); }
    async Task TranscribeAsync(byte[] audio)
    {
        transcription?.Cancel(); using var cts = new CancellationTokenSource(); transcription = cts; transcribing = true; UpdateComposer();
        var identity = manager.Settings.AiProvider + manager.Settings.AiModel + manager.Settings.AiKeyProtected;
        try
        {
            var text = await ai.TranscribeAsync(audio,manager.Settings,cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(transcription,cts) || identity != manager.Settings.AiProvider + manager.Settings.AiModel + manager.Settings.AiKeyProtected) return;
            if (text.Length == 0) { localError = "No speech detected. Try recording again."; return; }
            // Keep edits made while transcription was running.
            var prefix = input.Text;
            input.Text = (prefix + (prefix.Length > 0 ? " " : "") + text)[..Math.Min(AssistantProtocol.MaxInput,prefix.Length + (prefix.Length > 0 ? 1 : 0) + text.Length)]; input.CaretIndex = input.Text.Length;
        }
        catch (OperationCanceledException) { if (ReferenceEquals(transcription,cts)) localError = "Voice input was interrupted. Try recording again."; }
        catch (Exception e) { if (ReferenceEquals(transcription,cts)) localError = e is InvalidOperationException ? e.Message : "Could not transcribe this recording. Check your connection and try again."; }
        finally { if (ReferenceEquals(transcription,cts)) { transcription = null; transcribing = false; ShowError(); UpdateComposer(); RequestSize(); } }
    }
    void ChooseFiles()
    {
        if (preview || manager.Snoozed || readingFiles) return;
        StopVoice();
        var picker = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "All files|*.*", Title = "Attach files" };
        if (picker.ShowDialog(Window.GetWindow(this)) == true) _ = AddFilesAsync(picker.FileNames);
    }
    void OpenFileMenu()
    {
        connections?.CloseMenu();
        if(menu?.IsOpen==true) {DismissMention();menu.CloseMenu();return;}
        ShowMenu(false,"");
    }
    void OpenConnectionMenu()
    {
        if(connections?.IsOpen==true){connections.CloseMenu();return;}menu?.CloseMenu();connections=new ChatConnectionMenu(composerBrand,manager,Notice,()=>{input.Text=ai.Draft;Refresh();input.Focus();});connections.IsOpen=true;
    }
    void ShowMenu(bool mentions,string query)
    {
        if(menu?.IsOpen==true&&!menu.Closing){menu.Search(query,mentions);return;}
        menu=new ComposerMenu(add,ai,manager.Data.Browser,ChooseItems,Draw,AddAttachment,id=>{ai.SelectPluginInstruction(id);input.Focus();},ConsumeMention);
        var current=menu;current.CloseStarted+=()=>{DismissMention();RotatePlus(false);if(current.Child?.IsKeyboardFocusWithin==true)input.Focus();};
        current.Closed+=(_,_)=>{if(!ReferenceEquals(menu,current))return;DismissMention();RotatePlus(false);};
        current.Search(query,mentions);current.IsOpen=true;RotatePlus(true);
    }
    void RotatePlus(bool open)
    {
        if(plusMenuOpen==open)return;plusMenuOpen=open;
        var target=open?45:0;AutomationProperties.SetHelpText(add,open?"Close tools menu":"Open tools menu");
        if(!SystemParameters.ClientAreaAnimation){plusRotation.BeginAnimation(RotateTransform.AngleProperty,null);plusRotation.Angle=target;return;}
        plusRotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(220)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
    }
    void QueueMentionSearch()
    {
        if(mentionQueued||changing)return;mentionQueued=true;Dispatcher.BeginInvoke(DispatcherPriority.Input,()=>{mentionQueued=false;if(!IsLoaded||!input.IsKeyboardFocusWithin||composing||manager.Snoozed)return;var token=MentionQuery.At(input.Text,input.CaretIndex);if(token is { } active){if(active!=dismissedMention)ShowMenu(true,active.Query);}else {dismissedMention=null;if(menu?.MentionSearch==true)menu.CloseMenu();}});
    }
    void DismissMention(){if(menu?.MentionSearch==true)dismissedMention=MentionQuery.At(input.Text,input.CaretIndex);}
    void ConsumeMention()
    {
        input.Focus();if(menu?.MentionSearch!=true||MentionQuery.At(input.Text,input.CaretIndex) is not { } token)return;
        var end=token.Start+token.Length;while(end<input.Text.Length&&(char.IsLetterOrDigit(input.Text[end])||input.Text[end] is '-' or '_'))end++;
        changing=true;input.Text=input.Text.Remove(token.Start,end-token.Start);input.CaretIndex=token.Start;ai.Draft=input.Text;changing=false;dismissedMention=null;
    }
    void RenderInstructions()
    {
        var selected=ai.DraftPluginInstructions.ToArray();if(displayedInstructions.SequenceEqual(selected))return;displayedInstructions=selected;instructionRow.Children.Clear();instructionRow.Visibility=selected.Length==0?Visibility.Collapsed:Visibility.Visible;
        foreach(var id in selected)instructionRow.Children.Add(PluginCatalog.Chip(id,()=>{ai.RemovePluginInstruction(id);input.Focus();}));
    }
    static Border ModeChip(string name,Action remove)
    {var row=new StackPanel {Orientation=Orientation.Horizontal};var label=WidgetVisuals.Label(name,11,true);label.Margin=new Thickness(9,0,2,0);row.Children.Add(label);row.Children.Add(WidgetVisuals.IconButton("close","Remove "+name+" mode",remove,22));var chip=new Border {Child=row,CornerRadius=new CornerRadius(13),BorderThickness=new Thickness(1),Margin=new Thickness(3,2,0,2)};chip.SetResourceReference(Border.BackgroundProperty,"AssistantInset");chip.SetResourceReference(Border.BorderBrushProperty,"AssistantLine");return chip;}
    void Draw()
    {if(preview||manager.Snoozed||readingFiles)return;StopVoice();var drawing=new DrawingWindow {Owner=Window.GetWindow(this)};if(drawing.ShowDialog()==true&&drawing.Result!=null)AddAttachment(drawing.Result);}
    async void ChooseItems()
    {
        if(preview||manager.Snoozed||readingFiles)return;StopVoice();var picker=new AttachmentPicker {Owner=Window.GetWindow(this)};
        if(picker.ShowDialog()!=true)return;
        if(picker.SelectedFolder!=null) {
            if(ai.Busy){Notice("Stop the current response before changing its working folder.");return;}
            var draft=input.Text;var attachments=ai.DraftAttachments.ToArray();var instructions=ai.DraftPluginInstructions.ToArray();try {ai.SetWorkspace(picker.SelectedFolder);ai.Draft=draft;input.Text=draft;ai.DraftAttachments.AddRange(attachments);foreach(var id in instructions)ai.SelectPluginInstruction(id);}catch(InvalidOperationException e){Notice(e.Message);return;}
        }
        if(picker.SelectedFiles.Length>0)await AddFilesAsync(picker.SelectedFiles);else Refresh();
    }
    void ChooseWorkspace()
    {
        if(preview||manager.Snoozed||ai.Busy||readingFiles)return;
        StopVoice();var picker=new Microsoft.Win32.OpenFolderDialog { Title="Choose the folder this agent may read and change",Multiselect=false };
        if(picker.ShowDialog(Window.GetWindow(this))!=true)return;
        try {ai.SetWorkspace(picker.FolderName);localError="";input.Focus();}catch(InvalidOperationException e){Notice(e.Message);}
    }
    async Task AddFilesAsync(IEnumerable<string> paths)
    {
        if (preview || manager.Snoozed || readingFiles) return;
        using var cts = new CancellationTokenSource(); fileWork = cts; readingFiles = true; localError = ""; UpdateComposer();
        try
        {
            foreach (var path in paths)
            {
                if (ai.DraftAttachments.Count >= AssistantAttachment.MaxFiles) throw new InvalidOperationException("Attach up to 5 files at a time.");
                var file = await AttachmentMedia.ReadAsync(path, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                if (ai.DraftAttachments.Sum(f => f.PayloadBytes) + file.PayloadBytes > AssistantAttachment.MaxTotalBytes) throw new InvalidOperationException("Attachments must have a combined size below 12 MB.");
                ai.DraftAttachments.Add(file);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { localError = e is InvalidOperationException ? e.Message : "Could not read this file. Check access or attach another copy."; }
        finally { if (ReferenceEquals(fileWork,cts)) { fileWork = null; readingFiles = false; RenderFiles(); ShowError(); UpdateComposer(); RequestSize(); } }
    }
    void AddAttachment(AssistantAttachment file)
    {
        if(readingFiles)return;
        if(ai.DraftAttachments.Count>=AssistantAttachment.MaxFiles||ai.DraftAttachments.Sum(f=>f.PayloadBytes)+file.PayloadBytes>AssistantAttachment.MaxTotalBytes) {Notice("Attach up to 5 files with a combined size below 12 MB.");return;}
        ai.DraftAttachments.Add(file);RenderFiles();UpdateComposer();RequestSize();
    }
    void RenderFiles()
    {
        if (displayedFiles.SequenceEqual(ai.DraftAttachments)) return;
        displayedFiles = ai.DraftAttachments.ToArray();
        fileRow.Children.Clear(); fileScroll.Visibility = ai.DraftAttachments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var file in ai.DraftAttachments.ToArray())
        {
            fileRow.Children.Add(AttachmentPreview.Tile(file,() => { ai.DraftAttachments.Remove(file); RenderFiles(); UpdateComposer(); RequestSize(); },notice:Notice));
        }
    }
    void Notice(string text) { if (text == "Copied") { notice.Text = text; notice.Visibility = Visibility.Visible; noticeTimer.Stop(); noticeTimer.Start(); } else { localError = text; ShowError(); RequestSize(); } }
    static void Animate(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        element.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));
        var move = new TranslateTransform(); element.RenderTransform = move; move.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(5,0,TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
