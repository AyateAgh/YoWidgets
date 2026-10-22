using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoWidgets;

static class AgentUiChecks
{
    static IEnumerable<T> All<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T item)yield return item;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in All<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    static void Require(bool condition,string message) {if(!condition)throw new Exception(message);}
    internal static async Task Run(Window widget,AiService ai,Task work,Action release)
    {
        var wait=System.Diagnostics.Stopwatch.StartNew();
        while(ai.Messages.LastOrDefault()?.Agent?.Steps.Count<4||ai.Messages.LastOrDefault()?.Agent?.Steps.LastOrDefault()?.Status!="Done") {await Task.Delay(30);if(wait.Elapsed.TotalSeconds>12)throw new Exception("Agent UI fixture did not reach its held step");}
        await Task.Delay(150);widget.UpdateLayout();
        var statuses=All<AssistantStatusView>(widget).Where(v=>v.IsVisible).ToArray();
        Require(statuses.Length==1&&statuses[0].Active,"Busy agent must have exactly one visible orb/status row");
        Require(!All<TextBlock>(widget).Any(t=>t.IsVisible&&t.Text.Contains("changed files")),"Final summary duplicated the live journal");
        var edit=All<Expander>(widget).First(e=>e.HasContent&&e.Header is DependencyObject header&&All<TextBlock>(header).Any(t=>t.Text.Contains("Edited Greeting.tsx")));
        var fullPath=((ToolTip)edit.ToolTip).Content as string;
        Require(Path.IsPathRooted(fullPath)&&fullPath!.EndsWith("Greeting.tsx"),"Working file tooltip lost its full disk path");
        edit.IsExpanded=true;widget.UpdateLayout();await Task.Delay(180);
        Require(All<TextBlock>(edit).Any(t=>t.IsVisible&&t.Text.Contains("Hello, YoWidgets")),"Inline edit diff did not expand during generation");
        Console.WriteLine("PASS native agent UI: one live orb/status, no duplicate card, expandable live diff and absolute-path tooltip");
        release();await work;await Task.Delay(200);widget.UpdateLayout();
        Require(!All<AssistantStatusView>(widget).Any(v=>v.IsVisible),"Orb survived completion");
        Require(All<TextBlock>(widget).Count(t=>t.IsVisible&&t.Text=="3 changed files")==1,"Completed chat must show one final change card");
        Require(!All<TextBlock>(widget).Any(t=>t.IsVisible&&t.Text=="Edited Greeting.tsx"),"Completed journal duplicated final files");
        Console.WriteLine("PASS native completed UI: one final three-file card, activity collapsed and no processing row");
        var hashes=new HashSet<string>();
        foreach(var state in Enum.GetValues<OrbState>()) {
            var orb=new ThinkingOrb {State=state};orb.Measure(new Size(24,24));orb.Arrange(new Rect(0,0,24,24));
            var bitmap=new RenderTargetBitmap(24,24,96,96,PixelFormats.Pbgra32);bitmap.Render(orb);var pixels=new byte[24*24*4];bitmap.CopyPixels(pixels,24*4,0);hashes.Add(Convert.ToHexString(SHA256.HashData(pixels)));
        }
        Require(hashes.Count==Enum.GetValues<OrbState>().Length,"Orb states did not render distinctly");Console.WriteLine("PASS native orb rendering: "+hashes.Count+" distinct adaptive vector states");
        await Hover();
    }
    static async Task Hover()
    {
        var body=new TextBox {Text="Test message",Height=40};var tools=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,5,0,0)};
        var copy=new Button {Content="Copy",Width=70,Height=28};tools.Children.Add(copy);var host=new StackPanel {Width=280};host.Children.Add(body);host.Children.Add(tools);HoverReveal.Attach(host,tools);
        var window=new Window {Content=host,Left=32,Top=32,Width=320,Height=150,ShowInTaskbar=false};window.Show();window.Activate();window.UpdateLayout();
        if(host.IsMouseOver) {window.Left+=360;window.UpdateLayout();}
        Require(ReferenceEquals(host.InputHitTest(new Point(150,42)),host),"The message/action gap is not hittable");
        Keyboard.Focus(copy);await Task.Delay(210);Require(tools.IsHitTestVisible&&tools.Opacity>.95,"Keyboard focus did not reveal actions");
        Keyboard.Focus(body);await Task.Delay(100);Require(tools.IsHitTestVisible,"Actions became unclickable before leave delay");
        await Task.Delay(450);Require(!tools.IsHitTestVisible&&tools.Opacity<.05,"Actions did not hide after focus left");window.Close();
        Console.WriteLine("PASS native action reveal: transparent gap, keyboard access, leave bridge and completed fade");
    }
}
