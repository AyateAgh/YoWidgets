using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YoWidgets;

static class MentionUiChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Hidden)!.GetValue(value)!;
    static object? Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Hidden)!.Invoke(value,args);
    internal static void Run(bool preview)
    {
        var root=Path.GetFullPath("artifacts/mention-ui/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var app=new App {ShutdownMode=ShutdownMode.OnExplicitShutdown};typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null,root);Ui.Theme();WidgetVisuals.Tone(false);var manager=new WidgetManager(root,true);typeof(App).GetProperty(nameof(App.Manager))!.SetValue(app,manager);Exception? failure=null;
        var view=new AssistantView(manager,false);var window=new Window {Title="Composer mentions QA",Width=550,Height=790,Left=160,Top=60,Content=new Border {Child=view,Padding=new Thickness(20)},Background=new LinearGradientBrush(Color.FromRgb(30,101,115),Color.FromRgb(124,67,95),35)};
        window.Closed+=(_,_)=>{manager.Dispose();app.Shutdown();};
        app.Startup+=async(_,_)=>{window.Show();try {
            var input=Field<TextBox>(view,"input");input.Focus();
            if(preview){input.Text="@";input.CaretIndex=input.Text.Length;return;}
            input.Text="Create a report @doc";input.CaretIndex=input.Text.Length;await Task.Delay(400);var menu=Field<ComposerMenu>(view,"menu");
            if(!menu.IsOpen||!menu.MentionSearch)throw new Exception("Typed mention did not open search");var buttons=Field<StackPanel>(menu,"body").Children.OfType<Button>().ToArray();if(buttons.Length!=1||AutomationProperties.GetName(buttons[0])!="Documents")throw new Exception("Typed query did not filter to Documents");
            if(LiveBackdrop.Supported&&!menu.UsesLiveBackdrop)throw new Exception("Popup did not attach real compositor blur");if(!menu.HandleKey(Key.Enter))throw new Exception("Keyboard selection failed");await Task.Delay(250);
            if(menu.IsOpen||input.Text!="Create a report "||!manager.Data.Ai.DraftPluginInstructions.Contains("documents")||Field<WrapPanel>(view,"instructionRow").Visibility!=Visibility.Visible)throw new Exception("Mention selection did not close, consume query and render its instruction");
            Console.WriteLine("PASS mention UI: typed filtering, Enter selection, blue instruction chip and actual live blur");
            WidgetVisuals.Tone(true);var instructionText=((StackPanel)((Border)Field<WrapPanel>(view,"instructionRow").Children[0]).Child).Children.OfType<TextBlock>().Single();if(((SolidColorBrush)instructionText.Foreground).Color!=Color.FromRgb(18,76,168))throw new Exception("Light-background instruction ink did not adapt");WidgetVisuals.Tone(false);
            Call(view,"OpenFileMenu");await Task.Delay(260);menu=Field<ComposerMenu>(view,"menu");Call(view,"OpenFileMenu");await Task.Delay(280);if(menu.IsOpen)throw new Exception("Repeated plus click reopened its popup");if(Math.Abs(Field<RotateTransform>(view,"plusRotation").Angle)>.1)throw new Exception("Plus rotation did not return to rest");
            Console.WriteLine("PASS mention UI: repeated plus toggle closes once and returns its rotation");
            Call(view,"OpenFileMenu");await Task.Delay(300);menu=Field<ComposerMenu>(view,"menu");if(!menu.StaysOpen)throw new Exception("Popup still relies on abrupt native auto-dismiss");menu.CloseMenu();if(!menu.IsOpen||!menu.Closing)throw new Exception("Close did not retain the surface during its animation");await Task.Delay(220);if(menu.IsOpen)throw new Exception("Animated close did not finish");Call(view,"OpenFileMenu");await Task.Delay(300);menu=Field<ComposerMenu>(view,"menu");if(!menu.IsOpen)throw new Exception("Animated dismissal swallowed the next separate click");menu.CloseMenu();await Task.Delay(220);Console.WriteLine("PASS mention UI: animated dismissal closes once and permits the next independent click");
            input.Text="@nomatchingtool";input.CaretIndex=input.Text.Length;await Task.Delay(220);menu=Field<ComposerMenu>(view,"menu");if(Field<StackPanel>(menu,"body").Children.OfType<Button>().Any())throw new Exception("No-results search retained stale tools");menu.HandleKey(Key.Escape);await Task.Delay(220);if(menu.IsOpen||input.Text!="@nomatchingtool")throw new Exception("Escape did not dismiss without erasing draft");
            Console.WriteLine("PASS mention UI: no-results and Escape preserve the user's editable draft");
            var chip=Field<WrapPanel>(view,"instructionRow").Children.OfType<Border>().Single();var remove=((StackPanel)chip.Child).Children.OfType<Button>().Single();remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(manager.Data.Ai.DraftPluginInstructions.Count!=0)throw new Exception("Instruction remove did not change the draft");Console.WriteLine("PASS mention UI: removing an instruction updates the real outgoing draft");
        }catch(Exception e){failure=e;}finally {if(!preview)window.Close();}};app.Run();if(failure!=null)throw failure;
    }
}
