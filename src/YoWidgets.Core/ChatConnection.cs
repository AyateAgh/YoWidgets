namespace YoWidgets.Core;

public sealed record ChatConnection(string Provider,string Model,string KeyProtected);
public static class ChatConnections
{
    public static ChatConnection? Find(AppSettings settings,string provider)=>settings.ChatConnections.FirstOrDefault(c=>c.Provider==provider);
    public static void Remember(AppSettings settings)
    {
        if(!AssistantProtocol.Providers.Contains(settings.AiProvider)||string.IsNullOrWhiteSpace(settings.AiKeyProtected))return;
        settings.ChatConnections.RemoveAll(c=>c.Provider==settings.AiProvider);settings.ChatConnections.Add(new(settings.AiProvider,settings.AiModel,settings.AiKeyProtected));
    }
    public static bool Select(AppSettings settings,string provider)
    {
        var selected=Find(settings,provider);if(selected==null)return false;Remember(settings);settings.AiProvider=selected.Provider;settings.AiModel=selected.Model;settings.AiKeyProtected=selected.KeyProtected;return true;
    }
}
