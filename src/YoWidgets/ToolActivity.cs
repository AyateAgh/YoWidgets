using System.Globalization;
namespace YoWidgets;

internal readonly record struct ToolIdentity(string Id,string Title,string Icon);
internal static class ToolActivity
{
    internal static ToolIdentity Identity(AgentStep step)
    {
        if(step.Tool=="generate_media")return Plugin("media");
        if(step.Tool.StartsWith("computer_",StringComparison.Ordinal))return Plugin("computer");
        if(step.Tool.StartsWith("browser_",StringComparison.Ordinal)||step.Tool=="read_web_page")return Plugin("browser");
        if(step.Tool.StartsWith("document_",StringComparison.Ordinal)){
            var format=step.Format.Length>0?step.Format:Path.GetExtension(step.Path).TrimStart('.').ToLowerInvariant();
            return format switch{"docx"=>Plugin("documents"),"pdf"=>Plugin("pdf"),"xlsx"=>Plugin("spreadsheets"),"pptx"=>Plugin("presentations"),_=>new("document-tools","Document tools","file-text")};
        }
        return step.Tool=="planning_decision"?new("planning","Planning","lightbulb"):new("files","Files","file-code");
    }
    static ToolIdentity Plugin(string id){var plugin=PluginCatalog.Items.First(p=>p.Id==id);return new(id,plugin.Title,plugin.Icon);}
    internal static string Offset(TimeSpan time)=>((int)time.TotalMinutes).ToString(CultureInfo.InvariantCulture)+":"+time.Seconds.ToString("00",CultureInfo.InvariantCulture)+"."+(time.Milliseconds/100).ToString(CultureInfo.InvariantCulture);
    internal static string Duration(TimeSpan time)=>time.TotalSeconds<60?time.TotalSeconds.ToString("0.0",CultureInfo.InvariantCulture)+" s":Offset(time);
    internal static string Timing(AgentStep step)=>Identity(step).Title+" · "+Offset(step.StartedAfter)+" into response · "+Duration(step.Elapsed)+(step.Status=="Working"?" elapsed":"");
    internal static string TimingDetail(AgentStep step)=>"Started "+step.StartedAt.ToLocalTime().ToString("HH:mm:ss",CultureInfo.CurrentCulture)+" · "+Offset(step.StartedAfter)+" into this response\n"+(step.EndedAt.HasValue?"Finished "+step.EndedAt.Value.ToLocalTime().ToString("HH:mm:ss",CultureInfo.CurrentCulture)+" · ":"")+"Duration "+Duration(step.Elapsed);
    internal static FrameworkElement Badge(ToolIdentity identity,double size=24)
    {
        var icon=WidgetVisuals.Icon(identity.Icon,size*.6,"AssistantInstructionInk");
        var badge=new Border{Child=icon,Width=size,Height=size,CornerRadius=new CornerRadius(size*.32),BorderThickness=new Thickness(1),ToolTip=identity.Title};badge.SetResourceReference(Border.BackgroundProperty,"AssistantInset");badge.SetResourceReference(Border.BorderBrushProperty,"AssistantLine");return badge;
    }
    internal static FrameworkElement Header(AgentWorkspace run)
    {
        var row=new DockPanel{LastChildFill=true};var icons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center};
        foreach(var identity in run.Steps.Select(Identity).DistinctBy(i=>i.Id).Take(5)){var icon=Badge(identity,22);icon.Margin=new Thickness(icons.Children.Count==0?0:-4,0,0,0);icons.Children.Add(icon);}row.Children.Add(icons);
        var errors=run.Steps.Count(s=>s.Status=="Failed");var count=run.Steps.Count;
        var tools=run.Steps.Select(Identity).DistinctBy(i=>i.Id).Count();var text=WidgetVisuals.Label((run.Running?"Using ":"Used ")+tools+" tool"+(tools==1?"":"s")+" · "+count+" action"+(count==1?"":"s")+" · "+Duration(run.Elapsed)+(errors>0?" · "+errors+" error"+(errors==1?"":"s"):""),11);text.TextTrimming=TextTrimming.CharacterEllipsis;text.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(text);return row;
    }
}
