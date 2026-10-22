namespace YoWidgets.Core;

public record RectD(double X, double Y, double Width, double Height);
public record PointD(double X, double Y);
public record LyricLine(double Seconds, string Text);
public sealed record CalendarTask(string Id,string Title,DateTimeOffset Due,bool Completed=false);
public sealed class WidgetState
{
    public string ContentId { get; set; } = "";
    public string Pack { get; set; } = "liquid";
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string Kind { get; set; } = "clock"; public bool Enabled
    {
        get; set;
    }
    public double X { get; set; } = 80; public double Y { get; set; } = 80; public string Size { get; set; } = "M"; public string? ImagePath
    {
        get; set;
    }
    public string Monitor { get; set; } = "";
}
public sealed class AppSettings
{
    public int FocusMinutes {get;set;}=25;
    public bool FocusQuietAppsEnabled {get;set;}=true;
    public string[] FocusQuietApps {get;set;}=["Telegram","Discord","DiscordPTB","DiscordCanary","WhatsApp","ms-teams","Teams","Signal","Slack","Zoom","Skype","Viber"];
    public string InterfaceAccent {get;set;}="neutral";
    public Dictionary<string,string[]> LibraryResume {get;set;}=[];
    public bool InterfaceSounds { get; set; }
    public double InterfaceVolume { get; set; } = .12;
    public string InterfaceTheme { get; set; } = "dark";
    public AppearancePreferences InterfaceAppearance {get;set;}=new();
    public List<WidgetState> Widgets { get; set; } = []; public string City { get; set; } = "Kyiv";
    public List<CalendarTask> CalendarTasks {get;set;}=[];
    public bool Seconds
    {
        get; set;
    }
    public bool Analog
    {
        get; set;
    }
    public bool Fahrenheit
    {
        get; set;
    }
    public bool Hour24 { get; set; } = true; public bool Topmost
    {
        get; set;
    }
    public int FloatingPlacementVersion
    {
        get; set;
    }
    public bool DesktopMode { get; set; } = false; public bool LyricsEnabled
    {
        get; set;
    }
    public bool NotificationsEnabled
    {
        get; set;
    }
    public string AiProvider { get; set; } = "";
    public List<ChatConnection> ChatConnections {get;set;}=[];
    public VoiceCallOptions VoiceCall {get;set;}=new();
    public bool AiAskBeforeControl {get;set;}
    public string AiImageModel {get;set;}="";
    public string AiVideoModel {get;set;}="";
    public GenerationConnection ImageConnection {get;set;}=new();
    public GenerationConnection VideoConnection {get;set;}=new();
    public GenerationConnection Model3DConnection {get;set;}=new(){Mode="off",Provider="Pollinations"};
    public GenerationConnection AudioConnection {get;set;}=new(){Mode="off",Provider="Pollinations"};
    public string AiProcessText { get; set; } = "Thinking it through…";
    public string AiEndpoint { get; set; } = "https://api.openai.com/v1"; public string AiModel { get; set; } = ""; public string? AiKeyProtected
    {
        get; set;
    }
    public void DisableIntegrations()
    {
        NotificationsEnabled = false;
        LyricsEnabled = false;
    }
    public void Normalize()
    {
        InterfaceVolume = double.IsFinite(InterfaceVolume) ? Math.Clamp(InterfaceVolume, 0, 1) : .12;
        InterfaceAppearance=InterfaceAppearance?.Valid==true?InterfaceAppearance.Normalized():new();InterfaceTheme=InterfaceAppearance.Mode;
        if(InterfaceAccent is not("auto" or "neutral" or "cyan" or "emerald" or "violet" or "amber" or "rose" or "blue" or "coral"))InterfaceAccent="neutral";
        // Disable the old experimental Explorer parent even for existing settings.
        DesktopMode = false;
        AiModel ??= "";
        VoiceCall??=new();VoiceCall.Normalize();
        CalendarTasks=(CalendarTasks??[]).Where(t=>t!=null&&Guid.TryParse(t.Id,out _)&&!string.IsNullOrWhiteSpace(t.Title)&&t.Title.Length<=160&&t.Due.Year is >=2000 and <=2200).DistinctBy(t=>t.Id).Take(500).ToList();
        AiImageModel=(AiImageModel??"").Trim();AiVideoModel=(AiVideoModel??"").Trim();
        ImageConnection??=new();VideoConnection??=new();Model3DConnection??=new(){Mode="off",Provider="Pollinations"};AudioConnection??=new(){Mode="off",Provider="Pollinations"};
        ImageConnection.Normalize();VideoConnection.Normalize();Model3DConnection.Normalize();AudioConnection.Normalize();
        AiProcessText = string.IsNullOrWhiteSpace(AiProcessText) ? "Thinking it through…" : AiProcessText.Trim()[..Math.Min(80,AiProcessText.Trim().Length)];
        if (!AssistantProtocol.Providers.Contains(AiProvider))
            AiProvider = AiModel.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) || AiModel.StartsWith("o1", StringComparison.OrdinalIgnoreCase) || AiModel.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ? "OpenAI" : "Gemini";
        if (string.IsNullOrWhiteSpace(AiModel) || AiModel.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            AiModel = AssistantProtocol.DefaultModel(AiProvider);
        ChatConnections=(ChatConnections??[]).Where(c=>c!=null&&AssistantProtocol.Providers.Contains(c.Provider)&&!string.IsNullOrWhiteSpace(c.KeyProtected)&&!string.IsNullOrWhiteSpace(c.Model)).DistinctBy(c=>c.Provider).Take(AssistantProtocol.Providers.Length).ToList();
        YoWidgets.Core.ChatConnections.Remember(this);
        City = string.IsNullOrWhiteSpace(City) ? "Kyiv" : City.Trim()[..Math.Min(City.Trim().Length, 120)];
        Widgets ??= [];
        Widgets = Widgets.Where(w => w != null && new[] { "clock", "calendar", "weather", "music", "photo", "focus", "quick", "notifications", "assistant" }.Contains(w.Kind)).Take(24).ToList();
        var ids = new HashSet<string>();
        foreach (var w in Widgets)
        {
            w.Pack = CatalogRules.ValidId(w.Pack)?w.Pack:"liquid";
            if(!CatalogRules.ValidId(w.ContentId))w.ContentId=w.Pack+"-"+w.Kind;
            if (!ids.Add(w.Id))
            {
                w.Id = Guid.NewGuid().ToString("N");
                ids.Add(w.Id);
            }
            if (!double.IsFinite(w.X))
                w.X = 80;
            if (!double.IsFinite(w.Y))
                w.Y = 80;
            if (!new[] { "S", "M", "L" }.Contains(w.Size))
                w.Size = "M";
            if (string.IsNullOrEmpty(w.Id))
                w.Id = Guid.NewGuid().ToString("N");
        }
    }
}
