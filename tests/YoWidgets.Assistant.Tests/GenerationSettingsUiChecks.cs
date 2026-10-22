using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoWidgets;
internal static class GenerationSettingsUiChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object item,string name)=>(T)item.GetType().GetField(name,Private)!.GetValue(item)!;
    static void Call(object item,string name)=>item.GetType().GetMethod(name,Private)!.Invoke(item,null);
    internal static void Run()
    {
        var root=Path.GetFullPath("artifacts/generation-settings-ui/run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null,root);Ui.Theme();var manager=new WidgetManager(root,true);typeof(App).GetProperty(nameof(App.Manager))!.SetValue(app,manager);Exception? failure=null;
        manager.Settings.AiProvider="Gemini";manager.Settings.AiModel="gemini-flash-lite-latest";manager.Settings.AiKeyProtected=AiService.Protect("fixture-chat-only");manager.Save();
        var view=new AssistantSettingsView(manager,_=>{});var window=new Window{Title="Generation connection settings QA",Width=1060,Height=930,Background=Ui.Brush("Background"),Content=new ScrollViewer{Content=new Border{Child=view,Padding=new Thickness(28)},VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};
        app.Startup+=async(_,_)=>{try{window.Show();await Task.Delay(120);var generation=view.Children.OfType<GenerationSettingsView>().Single();var input=Field<PasswordBox>(generation,"sharedKey");input.Password="sk_fixture_media_only";generation.UseForAll();
            if(input.Password.Length!=0)throw new Exception("Saved input was not cleared");foreach(var kind in GenerationConnections.Categories){var selected=GenerationConnections.Resolve(manager.Settings,kind.Id);if(selected.Provider!="Pollinations"||AiService.UnlockGenerationKey(selected.KeyProtected)!="sk_fixture_media_only")throw new Exception("Separate media key was not routed");}
            if(AiService.UnlockGenerationKey(manager.Settings.AiKeyProtected!)!="fixture-chat-only")throw new Exception("Chat key changed");if(File.ReadAllText(Path.Combine(root,"settings.json")).Contains("sk_fixture_media_only"))throw new Exception("Plaintext key stored");Console.WriteLine("PASS generation settings: one Pollinations key for four independent categories, encrypted persistence and preserved chat key");
            var cards=generation.Children.OfType<GenerationConnectionCard>().ToArray();if(cards.Length!=4)throw new Exception("Missing categories");var image=cards[0];image.IsExpanded=true;await Task.Delay(80);var api=Field<PasswordBox>(image,"key");var mode=Field<ComboBox>(image,"mode");var model=Field<ComboBox>(image,"model");if(AutomationProperties.GetName(api)!="Images API key"||mode.SelectedIndex!=1)throw new Exception("Missing accessible fields");
            model.Text="tongyi-mai/z-image-turbo";api.Password="sk_fixture_image_override";Call(image,"Save");if(AiService.UnlockGenerationKey(manager.Settings.ImageConnection.KeyProtected!)!="sk_fixture_image_override"||AiService.UnlockGenerationKey(manager.Settings.VideoConnection.KeyProtected!)!="sk_fixture_media_only")throw new Exception("Category save leaked to another category");Console.WriteLine("PASS generation settings: category override and keyboard-named controls");
            window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render((Visual)window.Content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(root,"settings.png")))encoder.Save(file);
            Call(image,"Remove");if(manager.Settings.ImageConnection.KeyProtected!=null||manager.Settings.ImageConnection.Mode!="off"||manager.Settings.VideoConnection.KeyProtected==null||manager.Settings.AiKeyProtected==null)throw new Exception("Remove affected other keys");Console.WriteLine("PASS generation settings: scoped key removal disables only its category");
            Console.WriteLine("Generation settings artifacts: "+root);
        }catch(Exception e){failure=e;}finally{window.Close();manager.Dispose();app.Shutdown();}};app.Run();if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
