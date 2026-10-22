namespace YoWidgets.Core;

public sealed class VoiceCallOptions
{
    public string GeminiModel {get;set;}="gemini-3.8-live";
    public string OpenAIModel {get;set;}="gpt-realtime-2.1";
    public string GeminiVoice {get;set;}="Aoede";
    public string OpenAIVoice {get;set;}="marin";
    public string Language {get;set;}="auto";
    public bool AllowActions {get;set;}=true;
    public static readonly string[] Languages=["uk","en","auto"];
    public void Normalize()
    {
        GeminiModel=Model(GeminiModel,"gemini-3.8-live");OpenAIModel=Model(OpenAIModel,"gpt-realtime-2.1");
        if(!Voices("Gemini").Contains(GeminiVoice))GeminiVoice="Aoede";
        if(!Voices("OpenAI").Contains(OpenAIVoice))OpenAIVoice="marin";
        if(!Languages.Contains(Language))Language="auto";
    }
    static string Model(string? value,string fallback)=>string.IsNullOrWhiteSpace(value)||value.Length>120||value.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '-' and not '_' and not '.')?fallback:value;
    public string ModelFor(string provider)=>provider=="Gemini"?GeminiModel:OpenAIModel;
    public string VoiceFor(string provider)=>provider=="Gemini"?GeminiVoice:OpenAIVoice;
    public static string[] Voices(string provider)=>provider=="Gemini"?["Aoede","Kore","Puck","Charon","Fenrir","Leda","Orus","Zephyr"]:["marin","cedar","alloy","ash","ballad","coral","echo","sage","shimmer","verse"];
    public static bool Supported(string provider)=>provider is "Gemini" or "OpenAI";
    public static bool EmptyDraft(string text,int attachments,bool mediaReference)=>string.IsNullOrWhiteSpace(text)&&attachments==0&&!mediaReference;
}
