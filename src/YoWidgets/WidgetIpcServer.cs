using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace YoWidgets;

internal sealed class WidgetIpcServer:IDisposable
{
    readonly WidgetCommandHub hub;readonly Dispatcher dispatcher;readonly CancellationTokenSource stop=new();readonly Task loop;
    internal static string Name(string root)=>"YoWidgets.WidgetBus."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant())))[..20];
    internal Task Completion=>loop;
    internal WidgetIpcServer(WidgetCommandHub hub,Dispatcher dispatcher,string root){this.hub=hub;this.dispatcher=dispatcher;loop=Listen(Name(root));}
    async Task Listen(string name)
    {
        while(!stop.IsCancellationRequested)try{
            using var pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(stop.Token).ConfigureAwait(false);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(12));
            using var reader=new StreamReader(pipe,new UTF8Encoding(false,true),false,1024,true);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
            var buffer=new char[1];var line=new StringBuilder();while(true){var count=await reader.ReadAsync(buffer.AsMemory(),timeout.Token).ConfigureAwait(false);if(count==0)throw new IOException("Incomplete IPC request.");if(buffer[0]=='\n')break;if(line.Length>=16384)throw new IOException("Oversized IPC request.");line.Append(buffer[0]);}
            string reply;try{using var json=JsonDocument.Parse(line.ToString());var request=json.RootElement;var action=request.GetProperty("action").GetString()??"";var args=request.TryGetProperty("args",out var a)?a.Clone():JsonSerializer.SerializeToElement(new{});var result=await dispatcher.InvokeAsync(()=>hub.Execute(action,args,timeout.Token)).Task.Unwrap().ConfigureAwait(false);reply=JsonSerializer.Serialize(new{ok=true,result});}
            catch(Exception e){reply=JsonSerializer.Serialize(new{ok=false,error=e is InvalidOperationException?e.Message:"Invalid or cancelled widget request."});}
            await writer.WriteLineAsync(reply.AsMemory(),timeout.Token).ConfigureAwait(false);
        }catch(OperationCanceledException){}catch(IOException){if(!stop.IsCancellationRequested)await Task.Delay(150,stop.Token).ConfigureAwait(false);}catch(Exception){if(!stop.IsCancellationRequested)await Task.Delay(150,stop.Token).ConfigureAwait(false);}
    }
    public void Dispose()=>stop.Cancel();
}
