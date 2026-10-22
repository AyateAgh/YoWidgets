using System.Text.Json;
using System.Windows;
using YoWidgets;
internal static class VoiceActionsLiveCheck
{
    internal static void Run()
    {
        var saved=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YoWidgets","settings.json")))!;saved.Normalize();if(saved.AiKeyProtected==null||!VoiceCallOptions.Supported(saved.AiProvider))throw new Exception("No supported saved connection.");
        var root=Path.GetFullPath("artifacts/voice-actions-live/run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null,root);Ui.Theme();var manager=new WidgetManager(root,true);typeof(App).GetProperty(nameof(App.Manager))!.SetValue(app,manager);Exception? failure=null;
        app.Startup+=async(_,_)=>{try{
            manager.Settings.AiProvider=saved.AiProvider;manager.Settings.AiModel=saved.AiModel;manager.Settings.AiKeyProtected=saved.AiKeyProtected;manager.Settings.VoiceCall=saved.VoiceCall;manager.Settings.VoiceCall.AllowActions=true;
            var confirmations=0;manager.Settings.AiAskBeforeControl=false;manager.Data.Ai.ToolApproval=(_,_,_)=>{confirmations++;return Task.FromResult(false);};
            var audio=new LiveVoiceChecks.Audio();using var bridge=new VoiceActionBridge(manager);using var session=new LiveVoiceSession(saved.AiProvider,saved.VoiceCall.ModelFor(saved.AiProvider),saved.VoiceCall.VoiceFor(saved.AiProvider),AiService.UnlockGenerationKey(saved.AiKeyProtected),audio:audio){Language=saved.VoiceCall.Language,ExecuteTool=bridge.Execute};bridge.Progress=session.ReportAction;
            var sent=false;session.Changed+=()=>{if(session.Connected&&!sent){sent=true;session.SendText("Synthetic integration test. First call widget_command action=state with arguments_json={}. Then call execute_task to open https://example.com/ using browser_open, read the current page and report its real heading. Only this public example website is approved; do not inspect other apps or personal data. Finally briefly speak the verified heading in English. Use both tools before the final answer.");}};
            var run=session.Run();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(110));long afterTools=-1;var last="";
            try{while(session.Active){if(session.ActionLog.Count>=2&&afterTools<0)afterTools=audio.SubmittedBytes;if(afterTools>=0&&audio.SubmittedBytes>afterTools&&session.CurrentAction.Length==0)break;var progress=session.State+" · "+session.CurrentAction+" · tools="+session.ActionLog.Count;if(progress!=last){last=progress;Console.WriteLine("LIVE "+progress);}await Task.Delay(100,stop.Token);}}finally{session.Stop();await run;}
            if(session.Error.Length>0)throw new Exception(session.Error);
            var steps=manager.Data.Ai.Messages.SelectMany(m=>m.Agent?.Steps??[]).ToArray();
            if(confirmations!=0)throw new Exception("Live voice action unexpectedly requested confirmation");
            if(session.ActionLog.Count<2||steps.All(s=>s.Tool!="browser_open")||!manager.Data.Ai.Answer.Contains("Example Domain",StringComparison.OrdinalIgnoreCase)||audio.Last.Length==0)throw new Exception("Live voice/browser pipeline did not return verified page content and speech. Review its isolated test activity.");
            Console.WriteLine("PASS LIVE "+saved.AiProvider+": native voice function calls → real widget state → chat agent → actual example.com browser DOM → observed heading → streamed spoken response without a confirmation dialog.");
            Console.WriteLine("No microphone, personal windows, personal player actions or audio files were used. Public fixture only. Activity: "+root);
        }catch(Exception e){failure=e;}finally{manager.Settings.AiKeyProtected=null;manager.Settings.ChatConnections.Clear();manager.Data.Ai.CloseBrowser();manager.Dispose();app.Shutdown();}};app.Run();if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
