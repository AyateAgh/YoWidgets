namespace YoWidgets.Core;

// A media connection owns its credentials independently of the text chat account.
public sealed record GenerationConnection
{
    public string Mode {get;set;}="chat";
    public string Provider {get;set;}="Gemini";
    public string Model {get;set;}="";
    public string Voice {get;set;}="";
    public string? KeyProtected {get;set;}
    public void Normalize()
    {
        if(Mode is not ("chat" or "separate" or "off"))Mode="off";
        Provider=(Provider??"").Trim();Model=(Model??"").Trim();Voice=(Voice??"").Trim();
    }
}
