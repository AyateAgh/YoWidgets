using System.Windows.Automation;
namespace YoWidgets;

internal sealed class VoiceCallSettingsView:StackPanel
{
    internal VoiceCallSettingsView(WidgetManager manager,Action<string> status)
    {
        Children.Add(Ui.Text("Live voice",20,weight:FontWeights.SemiBold));Children.Add(Ui.Gap(8));
        Children.Add(Ui.Text("Start call appears when your message is empty and no files are attached. Calls use the saved Gemini or OpenAI chat key and a dedicated live model.",12,"Muted"));Children.Add(Ui.Gap(12));
        var options=manager.Settings.VoiceCall;
        var gemini=Ui.Input(options.GeminiModel);var openai=Ui.Input(options.OpenAIModel);
        var geminiVoice=Picker("Gemini",options.GeminiVoice);var openaiVoice=Picker("OpenAI",options.OpenAIVoice);
        var language=new ComboBox{ItemsSource=new[]{"Ukrainian","English","Auto"},SelectedIndex=Array.IndexOf(VoiceCallOptions.Languages,options.Language),MinHeight=38,Padding=new Thickness(8)};AutomationProperties.SetName(language,"Conversation language");
        Children.Add(Ui.Text("Conversation language",12,"Muted"));Children.Add(Ui.Gap(5));Children.Add(language);Children.Add(Ui.Gap(12));
        var actions=Ui.Toggle("Allow browser, computer and widget actions in calls",options.AllowActions,_=>{});actions.Margin=new Thickness(0,0,0,12);Children.Add(actions);
        var confirm=Ui.Toggle("Ask before browser and app control",manager.Settings.AiAskBeforeControl,_=>{});confirm.Margin=new Thickness(0,0,0,5);Children.Add(confirm);
        Children.Add(Ui.Text("Applies to chat and calls. Off: requested actions run directly; stop anytime with Esc or Stop.",11,"Muted"));Children.Add(Ui.Gap(12));
        foreach(var (name,model,voice) in new[]{("Gemini",gemini,geminiVoice),("OpenAI",openai,openaiVoice)}){
            Children.Add(Ui.Text(name+" · live model",12,"Muted"));Children.Add(Ui.Gap(5));Children.Add(model);Children.Add(Ui.Gap(8));Children.Add(voice);Children.Add(Ui.Gap(14));AutomationProperties.SetName(model,name+" live voice model");
        }
        var feedback=Ui.Text("",12,"Muted");AutomationProperties.SetLiveSetting(feedback,AutomationLiveSetting.Polite);
        Children.Add(Ui.Button("Save voice preferences",()=>{
            var draft=new VoiceCallOptions{GeminiModel=gemini.Text.Trim(),OpenAIModel=openai.Text.Trim(),GeminiVoice=(string)geminiVoice.SelectedItem,OpenAIVoice=(string)openaiVoice.SelectedItem,Language=VoiceCallOptions.Languages[Math.Max(0,language.SelectedIndex)],AllowActions=actions.IsChecked==true};draft.Normalize();
            if(draft.GeminiModel!=gemini.Text.Trim()||draft.OpenAIModel!=openai.Text.Trim()){feedback.Text="Enter valid live model IDs using letters, digits, periods, underscores or hyphens.";return;}
            var previous=manager.Settings.VoiceCall;var previousConfirmation=manager.Settings.AiAskBeforeControl;manager.Settings.VoiceCall=draft;manager.Settings.AiAskBeforeControl=confirm.IsChecked==true;if(!manager.TrySave()){manager.Settings.VoiceCall=previous;manager.Settings.AiAskBeforeControl=previousConfirmation;feedback.Text="Could not save voice preferences. Try again.";return;}
            feedback.Text="Saved. Voice changes apply to your next call; control changes to your next task.";status("Voice preferences saved");
        },true));Children.Add(feedback);Children.Add(Ui.Gap(10));
        Children.Add(Ui.Text("Audio streams directly to your selected API while connected. Mute stops microphone capture; End call stops capture and playback. No audio recordings are saved. Available transcripts stay in this chat until you quit. Headphones help prevent echo. Live access and usage charges depend on your API plan.",11,"Muted"));
    }
    static ComboBox Picker(string provider,string selected){var box=new ComboBox{ItemsSource=VoiceCallOptions.Voices(provider),SelectedItem=selected,MinHeight=38,Padding=new Thickness(8)};AutomationProperties.SetName(box,provider+" call voice");return box;}
}
