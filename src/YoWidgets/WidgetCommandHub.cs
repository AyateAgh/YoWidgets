using System.Text.Json;
using System.Windows.Interop;
namespace YoWidgets;

// The same allowlisted commands serve voice tools and current-user IPC clients.
// This boundary never accepts executable paths, scripts or arbitrary methods.
internal sealed class WidgetCommandHub(WidgetManager manager):IDisposable
{
    Window? overlay;DispatcherTimer? hide;bool disposed;
    internal async Task<object> Execute(string action,JsonElement args,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(disposed)throw new InvalidOperationException("Widget controls are closed.");
        if(manager.Snoozed)throw new InvalidOperationException("Resume YoWidgets before controlling widgets.");
        string S(string name)=>args.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
        if(action=="state")return new{localTime=DateTimeOffset.Now,widgets=manager.Settings.Widgets.Where(w=>w.Enabled).Select(w=>w.Kind).Distinct(),media=new{manager.Data.Media.Available,manager.Data.Media.Title,artist=manager.Data.Media.DisplayArtist,manager.Data.Media.Playing},focusEnd=manager.Data.FocusEnd,calendar=manager.Settings.CalendarTasks.OrderBy(t=>t.Due).Take(30)};
        if(action=="media"){
            var command=S("command");if(command is not("next" or "previous" or "pause" or "resume" or "play" or "seek"))throw new InvalidOperationException("Use next, previous, pause, resume, play (toggle), or seek.");
            var media=manager.Data.Media;if(!media.Running)await media.StartAsync();token.ThrowIfCancellationRequested();if(!media.Available)throw new InvalidOperationException("No compatible media player is available.");
            var value=args.TryGetProperty("seconds",out var seconds)?seconds.GetDouble():0;if(!double.IsFinite(value)||value<0||value>86400)throw new InvalidOperationException("Seek seconds must be between 0 and 86400.");
            await media.Command(command,value);token.ThrowIfCancellationRequested();if(media.Error.Length>0)throw new InvalidOperationException(media.Error);return new{accepted=true,command,media.Title,media.Playing,media.Position,metadataMayBeUpdating=command is "next" or "previous",instruction="Acceptance confirms the transport command, not the next track's title. Re-read state after metadata updates before naming a new track."};
        }
        if(action=="calendar_list")return new{tasks=manager.Settings.CalendarTasks.OrderBy(t=>t.Due).ToArray(),storage="YoWidgets local calendar"};
        if(action is "calendar_add" or "calendar_complete"){
            var previous=manager.Settings.CalendarTasks.ToList();CalendarTask task;
            if(action=="calendar_add"){
                var title=S("title").Trim();var dueText=S("due");if(title.Length is <1 or >160||manager.Settings.CalendarTasks.Count>=500)throw new InvalidOperationException("Use a task title of 1–160 characters; at most 500 tasks can be stored.");
                if(!System.Text.RegularExpressions.Regex.IsMatch(dueText,@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$")||!DateTimeOffset.TryParse(dueText,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var due)||due.Year is <2000 or >2200)throw new InvalidOperationException("Use an ISO date/time with a timezone offset, for example 2026-10-22T18:00:00+03:00.");
                task=new(Guid.NewGuid().ToString("N"),title,due);manager.Settings.CalendarTasks.Add(task);
            }else{var index=manager.Settings.CalendarTasks.FindIndex(t=>t.Id==S("id"));if(index<0)throw new InvalidOperationException("Read calendar_list and use an existing task ID.");task=manager.Settings.CalendarTasks[index] with{Completed=true};manager.Settings.CalendarTasks[index]=task;}
            if(!manager.TrySave()){manager.Settings.CalendarTasks=previous;throw new InvalidOperationException("Could not save the calendar task. Previous tasks restored.");}manager.Data.NotifyWidgets();return new{saved=true,task,storage="YoWidgets local calendar"};
        }
        if(action=="focus"){
            var minutes=args.TryGetProperty("minutes",out var v)?v.GetInt32():0;if(minutes is <0 or >240)throw new InvalidOperationException("Use 1–240 minutes, or 0 to stop focus.");if(minutes==0)manager.Data.StopFocus();else manager.Data.StartFocus(minutes);return new{focusEnd=manager.Data.FocusEnd};
        }
        if(action=="status"){
            var text=S("text").Trim();if(text.Length is <1 or >200)throw new InvalidOperationException("Status text must contain 1–200 characters.");ShowStatus(text);return new{shown=true,seconds=8};
        }
        throw new InvalidOperationException("Unknown widget command.");
    }
    void ShowStatus(string text)
    {
        overlay?.Close();hide?.Stop();var label=Ui.Text(text,14);label.TextWrapping=TextWrapping.Wrap;label.MaxWidth=420;
        overlay=new Window{Title="YoWidgets status",ShowActivated=false,ShowInTaskbar=false,Topmost=true,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,SizeToContent=SizeToContent.WidthAndHeight,Content=new Border{Child=label,Padding=new Thickness(18,13,18,13),CornerRadius=new CornerRadius(18),Background=Ui.Brush("Panel"),BorderBrush=Ui.Brush("Line"),BorderThickness=new Thickness(1)}};
        overlay.Loaded+=(_,_)=>{var area=SystemParameters.WorkArea;overlay.Left=area.Left+(area.Width-overlay.ActualWidth)/2;overlay.Top=area.Top+20;};overlay.SourceInitialized+=(_,_)=>Native.ToolWindow(overlay);overlay.Show();hide=new(){Interval=TimeSpan.FromSeconds(8)};hide.Tick+=(_,_)=>{hide.Stop();overlay?.Close();overlay=null;};hide.Start();
    }
    public void Dispose(){disposed=true;hide?.Stop();overlay?.Close();overlay=null;}
}
