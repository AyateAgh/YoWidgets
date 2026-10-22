using System.Windows.Controls.Primitives;
namespace YoWidgets;

internal sealed class ChatConnectionMenu:Popup
{
    readonly WidgetManager manager;readonly Action<string> notice;readonly Action changed;readonly GlassPopupMotion motion;
    bool selecting;
    internal ChatConnectionMenu(FrameworkElement anchor,WidgetManager manager,Action<string> notice,Action changed)
    {
        this.manager=manager;this.notice=notice;this.changed=changed;PlacementTarget=anchor;Placement=PlacementMode.Top;VerticalOffset=-8;
        var body=new StackPanel();var title=Ui.Text("Chat connections",11,"Muted");title.Margin=new Thickness(10,8,10,8);body.Children.Add(title);
        var settings=manager.Settings;ChatConnections.Remember(settings);
        foreach(var provider in AssistantProtocol.Providers){var saved=ChatConnections.Find(settings,provider);var button=WidgetVisuals.IconButton("sparkle","Switch to "+provider,()=>_ =Select(provider),50);button.Width=double.NaN;button.Padding=new Thickness(10,5,10,5);button.Margin=new Thickness(0,1,0,1);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.IsEnabled=!manager.Data.Ai.Busy;
            var row=new DockPanel{Margin=new Thickness(10,3,10,3)};var icon=new PremiumIcon(provider.ToLowerInvariant(),18,"Text",true){Margin=new Thickness(0,0,10,0)};row.Children.Add(icon);var check=WidgetVisuals.Icon(provider==settings.AiProvider&&settings.AiKeyProtected!=null?"check":saved!=null?"arrow-up-right":"plus",15,"Muted");DockPanel.SetDock(check,Dock.Right);row.Children.Add(check);
            var labels=Ui.Stack(Ui.Text(provider=="OpenAI"?"OpenAI · ChatGPT":provider,13),Ui.Text(saved?.Model??"Add API key in Settings",10.5,"Muted"));row.Children.Add(labels);button.Content=row;System.Windows.Automation.AutomationProperties.SetName(button,"Switch to "+provider);body.Children.Add(button);}
        var hint=Ui.Text("Switching services starts a new chat. Draft text and attachments are kept.",10.5,"Muted");hint.Margin=new Thickness(10,10,10,6);body.Children.Add(hint);
        var card=new Border{Child=body,Width=310,Padding=new Thickness(7),CornerRadius=new CornerRadius(20),Background=Ui.Brush("Panel"),BorderBrush=Ui.Brush("Line"),BorderThickness=new Thickness(1)};Child=card;motion=new GlassPopupMotion(this,card,anchor);Opened+=(_,_)=>body.Children.OfType<Button>().FirstOrDefault(b=>b.IsEnabled)?.Focus();
    }
    internal void CloseMenu()=>motion.Close();
    internal bool HandleKey(Key key){if(!IsOpen||key!=Key.Escape)return false;CloseMenu();return true;}
    async Task Select(string provider)
    {
        if(selecting)return;selecting=true;
        await motion.CloseAsync();if(manager.Data.Ai.Busy){notice("Stop the current response before switching connections.");return;}
        var settings=manager.Settings;if(ChatConnections.Find(settings,provider)==null){App.CurrentApp.ShowShell();App.CurrentApp.Shell?.OpenChatSettings(provider);notice("Add and save the "+provider+" API key in Chat connection.");return;}
        if(provider==settings.AiProvider)return;
        var previous=(settings.AiProvider,settings.AiModel,settings.AiKeyProtected,settings.ChatConnections.ToList());ChatConnections.Select(settings,provider);
        if(!manager.TrySave()){(settings.AiProvider,settings.AiModel,settings.AiKeyProtected,settings.ChatConnections)=previous;notice("Could not save the connection. Previous service restored.");return;}
        var ai=manager.Data.Ai;var draft=ai.Draft;var files=ai.DraftAttachments.ToArray();var instructions=ai.DraftPluginInstructions.ToArray();ai.ConfigurationChanged(settings);ai.Draft=draft;ai.DraftAttachments.AddRange(files);foreach(var id in instructions)ai.SelectPluginInstruction(id);changed();notice("Switched to "+provider);
    }
}
