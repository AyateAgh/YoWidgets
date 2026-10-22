using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
namespace YoWidgets;

internal static class ManagementWire
{
    internal static string Name(string root)=>"YoWidgets.Management."+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant())))[..20];
    internal static async Task<JsonElement> Read(Stream stream,CancellationToken token,int limit=8*1024*1024)
    {
        var header=new byte[4];await stream.ReadExactlyAsync(header,token);var count=BinaryPrimitives.ReadInt32LittleEndian(header);
        if(count is <2 || count>limit)throw new IOException("Invalid management message size.");
        var data=new byte[count];await stream.ReadExactlyAsync(data,token);using var document=JsonDocument.Parse(data,new(){MaxDepth=40});return document.RootElement.Clone();
    }
    internal static async Task Write(Stream stream,object message,CancellationToken token)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(message,CatalogJson.Options);if(bytes.Length>8*1024*1024)throw new IOException("Management snapshot is too large.");
        var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);await stream.WriteAsync(header,token);await stream.WriteAsync(bytes,token);await stream.FlushAsync(token);
    }
}

internal sealed class ManagementServer:IDisposable
{
    readonly DesktopPlatform platform;readonly ShellController shell;readonly Dispatcher dispatcher;readonly CancellationTokenSource stop=new();readonly Task listener;
    internal ManagementServer(DesktopPlatform platform,ShellController shell,string root,Dispatcher dispatcher){this.platform=platform;this.shell=shell;this.dispatcher=dispatcher;listener=Listen(ManagementWire.Name(root));}
    async Task Listen(string name)
    {
        while(!stop.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe=null;
            try{pipe=new(name,PipeDirection.InOut,16,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await pipe.WaitForConnectionAsync(stop.Token);_ = Handle(pipe);pipe=null;}
            catch(Exception e)when(e is IOException or OperationCanceledException){pipe?.Dispose();if(!stop.IsCancellationRequested)await Task.Delay(150,stop.Token);}
        }
    }
    async Task Handle(NamedPipeServerStream pipe)
    {
        using(pipe)using(var request=CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
        try
        {
            request.CancelAfter(TimeSpan.FromSeconds(30));var message=await ManagementWire.Read(pipe,request.Token,65536);
            var id=PlatformSettings.Text(message,"id");var method=PlatformSettings.Text(message,"method");var args=message.TryGetProperty("args",out var value)?value.Clone():JsonSerializer.SerializeToElement(new{});
            if(method=="subscribe"){request.CancelAfter(Timeout.InfiniteTimeSpan);await Subscribe(pipe,request.Token);return;}
            try
            {
                var result=await dispatcher.InvokeAsync(async()=>{
                    if(method=="host.register"){shell.Register(new IntPtr(args.GetProperty("window").GetInt64()));return (object)new{accepted=true};}
                    return await platform.Execute(method,args,request.Token);
                }).Task.Unwrap();
                await ManagementWire.Write(pipe,new{id,ok=true,result},request.Token);
            }
            catch(Exception e){await ManagementWire.Write(pipe,new{id,ok=false,error=e is InvalidOperationException or InvalidDataException?e.Message:e is OperationCanceledException?"The action was cancelled or timed out.":"This action could not be completed. Try again."},request.Token);}
        }
        catch(Exception e)when(e is IOException or OperationCanceledException or JsonException or InvalidOperationException){}
    }
    async Task Subscribe(NamedPipeServerStream pipe,CancellationToken token)
    {
        var channel=Channel.CreateBounded<bool>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true,SingleWriter=false});
        void Changed()=>channel.Writer.TryWrite(true);
        using var session=CancellationTokenSource.CreateLinkedTokenSource(token);platform.Changed+=Changed;Changed();
        var disconnect=pipe.ReadAsync(new byte[1],session.Token).AsTask();
        try
        {
            while(!session.IsCancellationRequested)
            {
                var available=channel.Reader.WaitToReadAsync(session.Token).AsTask();if(await Task.WhenAny(available,disconnect)==disconnect)break;if(!await available)break;
                while(channel.Reader.TryRead(out _)){}
                var snapshot=await dispatcher.InvokeAsync(platform.Snapshot);
                using var writeTimeout=CancellationTokenSource.CreateLinkedTokenSource(session.Token);writeTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                await ManagementWire.Write(pipe,new{type="snapshot",state=snapshot},writeTimeout.Token);
            }
        }
        finally{platform.Changed-=Changed;session.Cancel();try{await disconnect;}catch(OperationCanceledException){}catch(IOException){}}
    }
    public void Dispose()=>stop.Cancel();
}

internal static class ManagementClient
{
    internal static async Task<JsonElement> Request(string root,string method,JsonElement args,CancellationToken token)
    {
        using var pipe=new NamedPipeClientStream(".",ManagementWire.Name(root),PipeDirection.InOut,PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000,token);await ManagementWire.Write(pipe,new{id=Guid.NewGuid().ToString("N"),method,args},token);return await ManagementWire.Read(pipe,token);
    }
    internal static Task<JsonElement> Request(string root,string method,object args,CancellationToken token)=>Request(root,method,JsonSerializer.SerializeToElement(args,CatalogJson.Options),token);
    internal static async Task Subscribe(string root,Action<JsonElement> receive,CancellationToken token)
    {
        using var pipe=new NamedPipeClientStream(".",ManagementWire.Name(root),PipeDirection.InOut,PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000,token);await ManagementWire.Write(pipe,new{id="events",method="subscribe",args=new{}},token);
        while(!token.IsCancellationRequested)receive(await ManagementWire.Read(pipe,token));
    }
}
