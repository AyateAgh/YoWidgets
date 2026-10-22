using System.Speech.Recognition;
namespace YoWidgets;

// In-process dictation: no Windows shortcut, overlay or automatic API submission.
internal sealed class VoiceInput : IDisposable
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<VoiceInput,byte> activeInputs=new();
    internal static bool AnyListening=>activeInputs.Keys.Any(v=>v.Listening);
    SpeechRecognitionEngine? engine;
    readonly Dispatcher dispatcher;
    public bool Listening => engine != null;
    public double Level { get; private set; }
    public string Error { get; private set; } = "";
    public event Action? Changed;
    public event Action<string, bool>? Transcript;
    public VoiceInput(Dispatcher dispatcher) { this.dispatcher = dispatcher; }
    public void Start()
    {
        if (Listening) return;
        Error = "";
        try
        {
            var available = SpeechRecognitionEngine.InstalledRecognizers();
            var language = InputLanguageManager.Current.CurrentInputLanguage;
            var selected = available.FirstOrDefault(r => r.Culture.Name == language.Name) ?? available.FirstOrDefault();
            if (selected == null) { Error = "Install a Windows speech recognition language to use this microphone."; Changed?.Invoke(); return; }
            var current = new SpeechRecognitionEngine(selected); engine = current;activeInputs[this]=0;
            current.LoadGrammar(new DictationGrammar());
            current.SetInputToDefaultAudioDevice();
            current.AudioLevelUpdated += (_,e) => Level = e.AudioLevel / 100d;
            current.SpeechHypothesized += (_, e) => dispatcher.BeginInvoke(() => { if (ReferenceEquals(engine, current)) Transcript?.Invoke(e.Result.Text, false); });
            current.SpeechRecognized += (_, e) => dispatcher.BeginInvoke(() => { if (ReferenceEquals(engine, current) && e.Result.Confidence >= .3) Transcript?.Invoke(e.Result.Text, true); });
            current.RecognizeCompleted += (_, e) => dispatcher.BeginInvoke(() => { if (!ReferenceEquals(engine, current)) return; if (e.Error != null) Error = "Voice input stopped. Check your microphone and Windows speech settings."; Stop(); });
            current.RecognizeAsync(RecognizeMode.Multiple);
        }
        catch (Exception e) when (e is InvalidOperationException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or PlatformNotSupportedException)
        { Error = "Microphone unavailable. Check microphone access and installed speech languages in Windows."; Stop(); }
        Changed?.Invoke();
    }
    public void Stop()
    {
        var previous = engine; engine = null;activeInputs.TryRemove(this,out _); Level = 0;
        if (previous != null) { try { previous.RecognizeAsyncCancel(); } catch (InvalidOperationException) { } previous.Dispose(); }
        Changed?.Invoke();
    }
    public void Dispose() => Stop();
}
