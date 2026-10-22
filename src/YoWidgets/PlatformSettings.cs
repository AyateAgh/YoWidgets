using System.Text.Json;
namespace YoWidgets;

internal sealed class PlatformSettings(WidgetManager manager,InterfaceAccent? accent=null)
{
    AppSettings Settings => manager.Settings;
    internal object Snapshot()
    {
        var s=Settings;
        return new {
            s.City,s.Analog,s.Fahrenheit,s.Seconds,s.Hour24,s.Topmost,s.InterfaceSounds,s.InterfaceVolume,s.InterfaceTheme,s.InterfaceAccent,accentColor=accent?.Resolve(s.InterfaceAccent)??"#F5F5F5",
            appearance=s.InterfaceAppearance,startup=StartupRegistration.Enabled,s.LyricsEnabled,s.NotificationsEnabled,notificationStatus=manager.Data.Notifications.Status,
            s.AiAskBeforeControl,s.AiProcessText,voice=s.VoiceCall,
            voiceChoices=new {gemini=VoiceCallOptions.Voices("Gemini"),openai=VoiceCallOptions.Voices("OpenAI")},
            chatProvider=s.AiProvider,chatModel=s.AiModel,
            chat=AssistantProtocol.Providers.Select(p=>new {provider=p,model=ChatConnections.Find(s,p)?.Model??(s.AiProvider==p?s.AiModel:AssistantProtocol.DefaultModel(p)),connected=ChatConnections.Find(s,p)!=null||s.AiProvider==p&&!string.IsNullOrWhiteSpace(s.AiKeyProtected)}).ToArray(),
            generation=GenerationConnections.Categories.Select(c=>{var g=GenerationConnections.Get(s,c.Id);return new {id=c.Id,title=c.Title,g.Mode,g.Provider,g.Model,g.Voice,keySaved=!string.IsNullOrEmpty(g.KeyProtected),providers=GenerationConnections.Providers(c.Id)};}).ToArray(),
            mediaSource=manager.Data.Media.SelectedSource,mediaSources=manager.Data.Media.Sources,bridgeStatus=manager.Data.Media.BrowserBridgeStatus,
            canUninstall=Cleanup.IsInstalled,version="0.6.7",platform=Environment.OSVersion.VersionString
        };
    }
    internal async Task<object> Execute(string method,JsonElement args,CancellationToken token)
    {
        var s=Settings;
        switch(method)
        {
            case "settings.general":
                Mutate(()=> {
                    if(args.TryGetProperty("appearance",out var appearance)){var chosen=JsonSerializer.Deserialize<AppearancePreferences>(appearance,CatalogJson.Options);if(chosen?.Valid!=true)throw new InvalidOperationException("Choose Dark, Light or Custom with one or two valid hex colors.");s.InterfaceAppearance=chosen.Normalized();s.InterfaceTheme=chosen.Mode;}
                    if(args.TryGetProperty("city",out var city)){var text=city.GetString()?.Trim()??"";if(text.Length is <1 or >120)throw new InvalidOperationException("Enter a city name of 1–120 characters.");s.City=text;}
                    if(args.TryGetProperty("interfaceAccent",out var requestedAccent)){var mode=requestedAccent.GetString()??"";if(mode!="auto"&&!InterfaceAccent.Presets.ContainsKey(mode))throw new InvalidOperationException("Choose an available accent color.");s.InterfaceAccent=mode;}
                    SetBool(args,"analog",v=>s.Analog=v);SetBool(args,"fahrenheit",v=>s.Fahrenheit=v);SetBool(args,"seconds",v=>s.Seconds=v);SetBool(args,"hour24",v=>s.Hour24=v);SetBool(args,"topmost",v=>s.Topmost=v);
                    SetBool(args,"interfaceSounds",v=>s.InterfaceSounds=v);SetBool(args,"lyricsEnabled",v=>s.LyricsEnabled=v);SetBool(args,"notificationsEnabled",v=>s.NotificationsEnabled=v);SetBool(args,"aiAskBeforeControl",v=>s.AiAskBeforeControl=v);
                    if(args.TryGetProperty("interfaceVolume",out var volume)){var value=volume.GetDouble();if(!double.IsFinite(value)||value is <0 or >1)throw new InvalidOperationException("Choose a volume between 0 and 100%.");s.InterfaceVolume=value;}
                    if(args.TryGetProperty("startup",out var startup))StartupRegistration.Set(startup.GetBoolean());
                });
                if(args.TryGetProperty("interfaceAccent",out _)&&s.InterfaceAccent=="auto")accent?.Refresh();if(args.TryGetProperty("notificationsEnabled",out _)&&!s.NotificationsEnabled)manager.Data.Notifications.Suspend();if(args.EnumerateObject().Any(p=>p.Name is "analog" or "hour24" or "seconds" or "topmost" or "fahrenheit"))manager.ApplyAppearance();manager.Data.UpdateActivity();return new {message="Preferences saved"};
            case "settings.chat.save":
            {
                var provider=Text(args,"provider");var model=Text(args,"model");ValidateChat(provider,model,Text(args,"key"));
                Mutate(()=>{ChatConnections.Remember(s);var saved=ChatConnections.Find(s,provider);var current=s.AiProvider==provider?s.AiKeyProtected:null;var key=Text(args,"key");s.AiProvider=provider;s.AiModel=model;s.AiKeyProtected=key.Length>0?AiService.Protect(key):saved?.KeyProtected??current;if(string.IsNullOrWhiteSpace(s.AiKeyProtected))throw new InvalidOperationException("Enter an API key for "+provider+".");if(args.TryGetProperty("processText",out var label))s.AiProcessText=label.GetString()??"";ChatConnections.Remember(s);});
                manager.Data.Ai.ConfigurationChanged(s);return new {message="Connection saved"};
            }
            case "settings.chat.check":
            {
                var provider=Text(args,"provider");var key=Text(args,"key");
                if(!AssistantProtocol.Providers.Contains(provider))throw new InvalidOperationException("Choose a supported service.");
                if(key.Length==0){var saved=ChatConnections.Find(s,provider)?.KeyProtected??(s.AiProvider==provider?s.AiKeyProtected:null);if(saved==null)throw new InvalidOperationException("Enter an API key first.");key=AiService.UnlockGenerationKey(saved);}
                var models=await AiService.CheckConnectionAsync(provider,key,token);return new {message="Connection verified",models};
            }
            case "settings.chat.remove":
                Mutate(()=>{var provider=Text(args,"provider");s.ChatConnections.RemoveAll(c=>c.Provider==provider);if(s.AiProvider==provider)s.AiKeyProtected=null;});manager.Data.Ai.ConfigurationChanged();return new {message="Saved key removed"};
            case "settings.generation.save":
            {
                var kind=Text(args,"kind");var configuration=GenerationDraft(args);
                Mutate(()=>GenerationConnections.Set(s,kind,configuration));return new {message="Generation preferences saved"};
            }
            case "settings.generation.check":
            {
                var kind=Text(args,"kind");var configuration=GenerationDraft(args);var resolved=GenerationConnections.Resolve(s,kind,configuration);
                using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(25)};
                var result=await GenerationConnections.Check(http,resolved,AiService.UnlockGenerationKey(resolved.KeyProtected),token);
                return new {message=result.Message,models=result.Models};
            }
            case "settings.voice":
            {
                var value=args.GetProperty("voice").Deserialize<VoiceCallOptions>(CatalogJson.Options)??throw new InvalidOperationException("Voice settings are missing.");
                var gemini=value.GeminiModel;var openai=value.OpenAIModel;value.Normalize();
                if(gemini!=value.GeminiModel||openai!=value.OpenAIModel)throw new InvalidOperationException("Enter valid voice model IDs.");
                Mutate(()=>{s.VoiceCall=value;SetBool(args,"aiAskBeforeControl",v=>s.AiAskBeforeControl=v);});return new {message="Voice preferences saved"};
            }
            case "settings.weather.locate":return new{city=await ApproximateCity.Locate(token)};
            case "settings.weather.refresh":
                if(manager.Snoozed)throw new InvalidOperationException("Resume widgets before refreshing weather.");
                await manager.Data.RefreshWeatherAsync();return new {message=manager.Data.Weather.Error.Length==0?"Weather updated":manager.Data.Weather.Error};
            case "settings.notifications.request":
                if(manager.Snoozed||!s.NotificationsEnabled)throw new InvalidOperationException("Enable notification access and resume widgets first.");
                await manager.Data.Notifications.RefreshAsync(true);return new {message=manager.Data.Notifications.Status};
            case "settings.media.source":manager.Data.Media.SelectSource(Text(args,"source") is "Automatic" or ""?null:Text(args,"source"));return new {message="Music source selected"};
            case "settings.placement.reset":manager.ResetPositions();return new {message="Widgets centered"};
            default:throw new InvalidOperationException("Unknown settings action.");
        }
    }
    GenerationConnection GenerationDraft(JsonElement args)
    {
        var kind=Text(args,"kind");var old=GenerationConnections.Get(Settings,kind);
        var mode=Text(args,"mode");var provider=Text(args,"provider");var model=Text(args,"model");var key=Text(args,"key");
        if(mode is not("chat" or "separate" or "off")||!GenerationConnections.Providers(kind).Contains(provider))throw new InvalidOperationException("Choose a supported generation connection.");
        if(model.Length>160||model.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '.' and not '_' and not ':' and not '/' and not '-'))throw new InvalidOperationException("Enter a valid model ID.");
        return new(){Mode=mode,Provider=provider,Model=model,Voice=Text(args,"voice"),KeyProtected=key.Length>0?AiService.Protect(key):old.Provider==provider?old.KeyProtected:null};
    }
    void Mutate(Action update)
    {
        var backup=JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(Settings))!;var startup=StartupRegistration.Enabled;
        try{update();if(!manager.TrySave())throw new InvalidOperationException("Preferences could not be saved. Check disk access and try again.");}
        catch{foreach(var property in typeof(AppSettings).GetProperties().Where(p=>p.CanWrite))property.SetValue(Settings,property.GetValue(backup));if(StartupRegistration.Enabled!=startup)StartupRegistration.Set(startup);throw;}
    }
    static void SetBool(JsonElement args,string key,Action<bool> set){if(args.TryGetProperty(key,out var value))set(value.GetBoolean());}
    static void ValidateChat(string provider,string model,string key)
    {
        if(!AssistantProtocol.Providers.Contains(provider))throw new InvalidOperationException("Choose a supported service.");
        if(key.Length>4096||key.Any(char.IsControl))throw new InvalidOperationException("Enter a valid API key.");
        if(key.StartsWith("sk-ant-")&&provider!="Claude"||key.StartsWith("sk-or-")&&provider!="OpenRouter"||key.StartsWith("AIza")&&provider!="Gemini")throw new InvalidOperationException("Choose the service that issued this key.");
        try{using var request=AssistantProtocol.CreateRequest(provider,model,"validation",[]);}catch(ArgumentException){throw new InvalidOperationException("Enter a valid model ID.");}
    }
    internal static string Text(JsonElement args,string property)=>args.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.String?(value.GetString()??"").Trim():"";
}
