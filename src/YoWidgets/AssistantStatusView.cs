using System.Windows.Automation;
namespace YoWidgets;

internal enum AssistantPhase { Thinking,ToolCall,ToolResult,Streaming,WaitingApproval,Done,Stopped,Error }
internal enum OrbState { Thinking,Searching,Reading,Editing,ToolResult,Streaming,Done,Stopped,Error,Connecting,Generating }
internal sealed record AssistantActivity(string Label,OrbState Orb,string? FullPath=null)
{
    internal static AssistantActivity From(AiService ai,string thinking)
    {
        if(ai.Messages.LastOrDefault()?.Media.LastOrDefault() is {Finished:false} media)return new(media.Stage,OrbState.Generating);
        if(ai.Phase==AssistantPhase.ToolCall&&ai.ToolProgress.Length>0)return new(ai.ToolProgress,OrbState.Connecting);
        if(ai.Phase==AssistantPhase.Thinking&&ai.Messages.LastOrDefault()?.Agent?.Steps.LastOrDefault() is { Status:"Done" } previous) {
            if(previous.Tool.StartsWith("browser_",StringComparison.Ordinal)||previous.Tool=="read_web_page")return new("Reviewing the webpage…",OrbState.Thinking);
            if(previous.Tool.StartsWith("computer_",StringComparison.Ordinal))return new(previous.Detail is not ("" or "Completed")?previous.Detail+(previous.Target.Length>0?" · "+previous.Target:""):"Reviewing "+(previous.Target.Length>0?previous.Target:"the app")+"…",OrbState.Thinking);
            if(previous.Tool=="generate_media")return new("Reviewing generated media…",OrbState.Thinking);
            var name=previous.Path.Replace('\\','/').Split('/').LastOrDefault();if(string.IsNullOrEmpty(name)||name==".")name=Path.GetFileName(ai.EffectiveWorkspaceRoot??"folder");
            var mode=previous.Tool=="read_file"?OrbState.Reading:previous.Tool is "search_files" or "list_directory"?OrbState.Searching:previous.Tool is "edit_file" or "write_file"?OrbState.Editing:OrbState.ToolResult;
            return new("Reviewing "+name+"…",mode,Resolve(ai,previous.Path));
        }
        if(ai.Phase is AssistantPhase.ToolCall or AssistantPhase.ToolResult&&ai.CurrentTool is { } call) {
            var path=call.Arguments.TryGetProperty("path",out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.String?value.GetString()??"":"";
            var full=Resolve(ai,path);
            var name=path.Replace('\\','/').Split('/').LastOrDefault();if(string.IsNullOrEmpty(name)||name==".")name=Path.GetFileName(ai.EffectiveWorkspaceRoot??"folder");
            if(ai.Phase==AssistantPhase.ToolResult)return call.Name.StartsWith("browser_",StringComparison.Ordinal)||call.Name=="read_web_page"?new("Reviewing the webpage…",OrbState.ToolResult):call.Name.StartsWith("computer_",StringComparison.Ordinal)?new("Reviewing the app…",OrbState.ToolResult):new("Reviewing "+name+"…",OrbState.ToolResult,full);
            return call.Name switch {
                "read_file"=>new("Reading "+name+"…",OrbState.Reading,full),
                "search_files"=>new("Searching "+name+"…",OrbState.Searching,full),
                "list_directory"=>new("Exploring "+name+"…",OrbState.Searching,full),
                "delete_file"=>new("Removing "+name+"…",OrbState.Editing,full),
                "create_directory"=>new("Creating folder "+name+"…",OrbState.Editing,full),
                "document_schema"=>new("Checking document format…",OrbState.Reading),
                "document_inspect" or "document_preview"=>new("Inspecting "+name+"…",OrbState.Reading,full),
                "document_create"=>new("Creating "+name+"…",OrbState.Editing,full),
                "planning_decision"=>new("Preparing the plan…",OrbState.Editing),
                "computer_list_apps"=>new("Finding installed apps…",OrbState.Searching),
                "computer_open_app"=>new("Opening the requested app…",OrbState.Connecting),
                "read_web_page" or "browser_read_tab" or "browser_screenshot"=>new("Reading the webpage…",OrbState.Searching),
                "browser_open" or "browser_navigate" or "browser_click" or "browser_fill" or "browser_scroll"=>new("Working in the browser…",OrbState.Connecting),
                "computer_list_windows" or "computer_inspect" or "computer_click" or "computer_type" or "computer_key" or "computer_scroll" or "computer_drag"=>new("Working with the app…",OrbState.Connecting),
                _=>new("Editing "+name+"…",OrbState.Editing,full)
            };
        }
        return ai.Phase switch {
            AssistantPhase.Streaming=>new("Writing the answer…",OrbState.Streaming),
            AssistantPhase.WaitingApproval=>new("Waiting for your approval…",OrbState.Connecting),
            AssistantPhase.Done=>new("Completed",OrbState.Done),
            AssistantPhase.Stopped=>new("Stopped",OrbState.Stopped),
            AssistantPhase.Error=>new("Needs attention",OrbState.Error),
            _=>new(string.IsNullOrWhiteSpace(thinking)?"Thinking…":thinking,OrbState.Thinking)
        };
    }
    static string? Resolve(AiService ai,string path)
    {try {return ai.EffectiveWorkspaceRoot==null?null:new AgentWorkspace(ai.EffectiveWorkspaceRoot).Resolve(path);}catch(Exception e) when(e is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException){return null;}}
}

internal sealed class AssistantStatusView : Grid
{
    readonly ThinkingOrb orb=new();
    readonly TextBlock caption=WidgetVisuals.Label("",12);
    readonly StatusTextSweep sweep;
    readonly TranslateTransform reveal=new();
    internal bool Active { get=>orb.Active;set{orb.Active=value;sweep.Active=value;} }
    internal string Label=>caption.Text;
    internal OrbState State=>orb.State;
    public AssistantStatusView()
    {
        ColumnDefinitions.Add(new() { Width=GridLength.Auto });ColumnDefinitions.Add(new());
        var orbHost=new Viewbox{Width=32,Height=32,Child=orb,Margin=new Thickness(0,0,10,0)};orb.Margin=new Thickness(0);Children.Add(orbHost);caption.TextTrimming=TextTrimming.CharacterEllipsis;caption.VerticalAlignment=VerticalAlignment.Center;
        var text=new Grid{VerticalAlignment=VerticalAlignment.Center,RenderTransform=reveal};text.Children.Add(caption);sweep=new StatusTextSweep(caption);text.Children.Add(sweep);Grid.SetColumn(text,1);Children.Add(text);
        AutomationProperties.SetLiveSetting(caption,AutomationLiveSetting.Polite);
    }
    internal void Update(AiService ai,string thinking)
    {
        var activity=AssistantActivity.From(ai,thinking);if(caption.Text!=activity.Label){caption.Text=activity.Label;if(SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast)reveal.BeginAnimation(TranslateTransform.YProperty,new System.Windows.Media.Animation.DoubleAnimation(2,0,TimeSpan.FromMilliseconds(200)){EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut}});}orb.State=activity.Orb;
        if((caption.ToolTip as ToolTip)?.Content as string!=activity.FullPath)caption.ToolTip=activity.FullPath==null?null:PathTooltip(activity.FullPath);
        Active=ai.Busy&&IsVisible;
    }
    internal static ToolTip PathTooltip(string path)=>new() {Content=path,Style=(Style)System.Windows.Application.Current.FindResource("AssistantPathTip")};
}
