using System.Windows.Automation;
namespace YoWidgets;

internal sealed class AssistantSettingsView : StackPanel
{
    readonly WidgetManager manager;
    readonly Action<string> status;
    readonly ComboBox provider;
    readonly ComboBox model;
    readonly TextBox processText;
    readonly PasswordBox key;
    readonly TextBlock feedback;
    readonly Button check;
    readonly Button save;
    readonly TextBlock chatSummary=Ui.Text("",12,"Muted");
    CancellationTokenSource? checking;
    public AssistantSettingsView(WidgetManager manager, Action<string> status)
    {
        this.manager = manager; this.status = status;
        Resources["WidgetInk"]=Ui.Brush("Text");
        Children.Add(Ui.Text("Chat connection", 20, weight: FontWeights.SemiBold));
        Children.Add(Ui.Gap(8));
        Children.Add(Ui.Text("The chat model uses this connection. Generation services have their own connections below.", 12, "Muted"));
        Children.Add(Ui.Gap(14));
        provider = new ComboBox { ItemsSource = AssistantProtocol.Providers, SelectedItem = manager.Settings.AiProvider, MinHeight = 40, Padding = new Thickness(10), Margin = new Thickness(0, 4, 0, 12) };
        AutomationProperties.SetName(provider, "AI service");
        provider.ItemTemplate = ProviderOption.Template();
        Children.Add(Ui.Text("Service", 12, "Muted")); Children.Add(provider);
        key = new PasswordBox { Padding = new Thickness(12), Background = Ui.Brush("Field"), Foreground = Ui.Brush("Text"), BorderBrush = Ui.Brush("Line"), MaxLength = 512 };
        key.SetResourceReference(StyleProperty,"ConnectionPasswordBox");
        AutomationProperties.SetName(key, "API key");
        Children.Add(Ui.Text("Chat API key · encrypted for your Windows account", 12, "Muted")); Children.Add(Ui.Gap(4)); Children.Add(key);
        feedback = Ui.Text(string.IsNullOrEmpty(manager.Settings.AiKeyProtected) ? "No API key saved." : "API key saved. Leave the field empty to keep it.", 12, "Muted");
        feedback.Margin = new Thickness(0, 8, 0, 8);
        AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
        Children.Add(feedback);
        model = new ComboBox { IsEditable = true, Text = manager.Settings.AiModel, ItemsSource = new[] { manager.Settings.AiModel }, MinHeight = 38, Padding = new Thickness(8) };
        AutomationProperties.SetName(model, "AI model");
        Children.Add(Ui.Text("Chat model",12,"Muted"));Children.Add(Ui.Gap(4));Children.Add(model);Children.Add(Ui.Gap(10));
        processText = Ui.Input(manager.Settings.AiProcessText); processText.MaxLength = 80; AutomationProperties.SetName(processText,"Processing label");
        var advanced = new Expander { Header = Ui.Text("Chat preferences",12),Style=(Style)FindResource("AgentActionExpander"), Foreground = Ui.Brush("Text"), Margin = new Thickness(0, 4, 0, 12), Content = Ui.Stack(Ui.Gap(8),Ui.Text("Processing label",12,"Muted"), processText) };
        Children.Add(advanced);
        var actions = new WrapPanel();
        save = Ui.Button("Save connection", Save, true);
        check = Ui.Button("Check connection", async () => await CheckAsync());
        actions.Children.Add(save); actions.Children.Add(check); Children.Add(actions);
        Children.Add(Ui.Gap(10));
        var remove = Ui.Button("Remove saved key", () => {
            CancelCheck(); var previous = manager.Settings.AiKeyProtected;var profiles=manager.Settings.ChatConnections.ToList();manager.Settings.ChatConnections.RemoveAll(c=>c.Provider==Provider);if(Provider==manager.Settings.AiProvider)manager.Settings.AiKeyProtected = null;
            if (!manager.TrySave()) { manager.Settings.AiKeyProtected = previous;manager.Settings.ChatConnections=profiles; feedback.Text = "Could not remove the saved key. Check disk access and try again."; return; }
            if(Provider==manager.Settings.AiProvider)manager.Data.Ai.ConfigurationChanged(); key.Clear();
            UpdateChatSummary();
            feedback.Text = "API key removed. Add a key to chat again."; status("Saved key removed");
        });
        remove.HorizontalAlignment = HorizontalAlignment.Left; Children.Add(remove);
        Children.Add(Ui.Gap(12));
        Children.Add(Ui.Text("Messages and conversation context go directly to the selected service when you send. Chat history stays in memory until you start a new chat or quit. API usage follows your service plan.", 11, "Muted"));
        Children.Add(Ui.Gap(8));
        Children.Add(Ui.Text("Voice input uses your Gemini/OpenAI key to transcribe recorded audio. Other services use an installed Windows speech language. Recordings stay in memory and are never added to chat automatically.",11,"Muted"));
        Children.Add(Ui.Gap(6));
        Children.Add(Ui.Text("Gemini dictation uses its dedicated speech transcription model; the chat model remains your choice.",11,"Muted"));
        provider.SelectionChanged += (_, _) => {
            CancelCheck();key.Clear();var saved=ChatConnections.Find(manager.Settings,Provider);var chosen=saved?.Model??AssistantProtocol.DefaultModel(Provider);model.ItemsSource=new[]{chosen};model.Text=chosen;
            feedback.Text = saved!=null||Provider == manager.Settings.AiProvider && !string.IsNullOrEmpty(manager.Settings.AiKeyProtected) ? "API key saved. Leave the field empty to keep it." : "Enter an API key for " + Provider + ".";
        };
        key.PasswordChanged += (_, _) => { CancelCheck(); feedback.Text = "Key changed. Check or save the connection."; };
        Unloaded += (_, _) => CancelCheck();
        var chatFields=new StackPanel();foreach(var child in Children.Cast<UIElement>().ToArray()){Children.Remove(child);chatFields.Children.Add(child);}UpdateChatSummary();
        Children.Add(new Expander{Header=Ui.Stack(Ui.Text("Chat connection",16,weight:FontWeights.SemiBold),chatSummary),Style=(Style)FindResource("AgentActionExpander"),IsExpanded=string.IsNullOrWhiteSpace(manager.Settings.AiKeyProtected),Content=Ui.Panel(chatFields)});
        Children.Add(Ui.Gap(24));Children.Add(new Expander{Header=Ui.Text("Live voice",16,weight:FontWeights.SemiBold),Style=(Style)FindResource("AgentActionExpander"),Content=Ui.Panel(new VoiceCallSettingsView(manager,status))});
        Children.Add(Ui.Gap(24));Children.Add(new GenerationSettingsView(manager,status));
    }
    void UpdateChatSummary()=>chatSummary.Text=manager.Settings.AiProvider+" · "+manager.Settings.AiModel+" · "+(string.IsNullOrWhiteSpace(manager.Settings.AiKeyProtected)?"not connected":"key saved");
    internal void SelectProvider(string selected){provider.SelectedItem=selected;if(Children.OfType<Expander>().FirstOrDefault() is {} connection)connection.IsExpanded=true;Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>{key.BringIntoView();key.Focus();});}
    string Provider => provider.SelectedItem as string ?? "Gemini";
    bool Validate()
    {
        if (string.IsNullOrWhiteSpace(key.Password) && ChatConnections.Find(manager.Settings,Provider)==null&&(Provider != manager.Settings.AiProvider || string.IsNullOrEmpty(manager.Settings.AiKeyProtected))) { feedback.Text = "Enter an API key for " + Provider + "."; key.Focus(); return false; }
        var raw = key.Password.Trim();
        if ((raw.StartsWith("sk-ant-", StringComparison.Ordinal) && Provider != "Claude") || (raw.StartsWith("sk-or-", StringComparison.Ordinal) && Provider != "OpenRouter")) { feedback.Text = "Choose the service that issued this key."; provider.Focus(); return false; }
        if ((raw.StartsWith("AIza", StringComparison.Ordinal) && Provider != "Gemini") || (raw.StartsWith("sk-", StringComparison.Ordinal) && Provider == "Gemini")) { feedback.Text = "This key belongs to a different service. Change the service above."; provider.Focus(); return false; }
        try { using var request = AssistantProtocol.CreateRequest(Provider, model.Text.Trim(), "validation", []); }
        catch (ArgumentException e) { feedback.Text = e.Message; model.Focus(); return false; }
        return true;
    }
    void Save()
    {
        if (!Validate()) return;
        CancelCheck();
        var previous = (manager.Settings.AiProvider, manager.Settings.AiModel, manager.Settings.AiKeyProtected, manager.Settings.AiProcessText,manager.Settings.ChatConnections.ToList());ChatConnections.Remember(manager.Settings);
        var selectedKey=ChatConnections.Find(manager.Settings,Provider)?.KeyProtected;
        manager.Settings.AiProvider = Provider; manager.Settings.AiModel = model.Text.Trim();
        manager.Settings.AiProcessText = processText.Text;
        if (!string.IsNullOrWhiteSpace(key.Password)) manager.Settings.AiKeyProtected = AiService.Protect(key.Password.Trim());
        else if(selectedKey!=null)manager.Settings.AiKeyProtected=selectedKey;
        if (!manager.TrySave()) { (manager.Settings.AiProvider, manager.Settings.AiModel, manager.Settings.AiKeyProtected, manager.Settings.AiProcessText,manager.Settings.ChatConnections) = previous; feedback.Text = "Could not save the connection. Your entered key is still here; check disk access and try again."; return; }
        manager.Data.Ai.ConfigurationChanged(manager.Settings); key.Clear();UpdateChatSummary(); feedback.Text = "Connection saved. Ready to chat."; status("Assistant connection saved");
    }
    async Task CheckAsync()
    {
        if (!Validate()) return;
        CancelCheck();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25)); checking = cts;
        check.IsEnabled = false; save.IsEnabled = false; feedback.Text = "Checking key and loading models…";
        try
        {
            var models = !string.IsNullOrWhiteSpace(key.Password) ? await AiService.CheckConnectionAsync(Provider, key.Password.Trim(), cts.Token) : await AiService.CheckConnectionAsync(Provider,AiService.UnlockGenerationKey(ChatConnections.Find(manager.Settings,Provider)?.KeyProtected??manager.Settings.AiKeyProtected!),cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(checking, cts)) return;
            if (models.Count == 0) { feedback.Text = "Key accepted, but no text models are available for this account."; return; }
            var selected = model.Text; model.ItemsSource = models;
            model.Text = models.Contains(selected) ? selected : models.FirstOrDefault(m => m == AssistantProtocol.DefaultModel(Provider)) ?? models.FirstOrDefault(m => m.Contains("flash") && !m.Contains("image") && !m.Contains("audio")) ?? models[0];
            feedback.Text = $"Connected · {models.Count} models available. Save connection to use it.";
        }
        catch (OperationCanceledException) { if (ReferenceEquals(checking, cts)) feedback.Text = "Connection check timed out. Check your internet connection."; }
        catch (Exception e) { if (ReferenceEquals(checking, cts)) feedback.Text = e is InvalidOperationException ? e.Message : "Could not check the key. Check your internet connection or re-enter the key."; }
        finally { if (ReferenceEquals(checking, cts)) { checking = null; check.IsEnabled = true; save.IsEnabled = true; } }
    }
    void CancelCheck() { checking?.Cancel(); checking = null; check.IsEnabled = true; save.IsEnabled = true; }
}
