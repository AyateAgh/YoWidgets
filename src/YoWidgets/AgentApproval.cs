namespace YoWidgets;

internal static class AgentApproval
{
    internal static async Task<bool> Ask(string title,string detail,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var window=new Window {Title="Approve agent action",Width=540,Height=360,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Ui.Brush("Background"),Foreground=Ui.Brush("Text"),ResizeMode=ResizeMode.NoResize,Topmost=true,ShowInTaskbar=true};
        var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var content=Ui.Stack(Ui.Text(title,20),Ui.Gap(14),new ScrollViewer {Content=Ui.Text(detail,13),MaxHeight=170,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Ui.Gap(16));
        var controls=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};controls.Children.Add(Ui.Button("Deny",()=>{completion.TrySetResult(false);window.Close();}));controls.Children.Add(Ui.Button(title=="Allow control for this task?"?"Allow this task":"Allow this action",()=>{completion.TrySetResult(true);window.Close();},true));content.Children.Add(controls);window.Content=new Border {Padding=new Thickness(24),Child=content};
        window.Closed+=(_,_)=>completion.TrySetResult(false);using var registration=token.Register(()=>window.Dispatcher.BeginInvoke(()=>{completion.TrySetCanceled(token);window.Close();}));window.Show();window.Activate();return await completion.Task;
    }
}
