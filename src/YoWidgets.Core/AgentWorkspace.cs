using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace YoWidgets.Core;

public sealed record AgentToolCall(string Id,string Name,JsonElement Arguments);
public sealed record AgentToolResult(AgentToolCall Call,string Text,bool Error=false,AssistantAttachment? Image=null);
public sealed record AgentDiffLine(char Kind,int? BeforeLine,int? AfterLine,string Text);
public sealed class AgentFileChange(string path,byte[]? before)
{
    byte[]? after=before;
    IReadOnlyList<AgentDiffLine>? diff;
    public string Path { get; }=path;
    public byte[]? Before { get; }=before;
    public byte[]? After { get=>after; internal set { after=value;diff=null; } }
    public bool Undone { get; internal set; }
    public bool Changed => !Same(Before,After);
    public string Kind => Before==null ? "Created" : After==null ? "Deleted" : "Edited";
    public IReadOnlyList<AgentDiffLine> Diff => diff??=AgentDiff.Create(Before,After);
    public int Added => Diff.Count(l=>l.Kind=='+');
    public int Removed => Diff.Count(l=>l.Kind=='-');
    internal static bool Same(byte[]? a,byte[]? b)=>a==null ? b==null : b!=null && a.AsSpan().SequenceEqual(b);
}
public sealed class AgentStep(string tool,string path,TimeSpan startedAfter=default,string format="")
{
    readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
    TimeSpan? elapsed;
    public DateTimeOffset StartedAt {get;}=DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt {get;private set;}
    public TimeSpan StartedAfter {get;}=startedAfter;
    public TimeSpan Elapsed=>elapsed??clock.Elapsed;
    public string Format {get;internal set;}=format;
    internal void EndTiming(){if(elapsed.HasValue)return;elapsed=clock.Elapsed;clock.Stop();EndedAt=DateTimeOffset.UtcNow;}
    public string Tool { get; }=tool;
    public string Path { get; internal set; }=path;
    public string Status { get; internal set; }="Working";
    public string Detail { get; internal set; }="";
    public string Target {get;internal set;}="";
    public int Added { get; internal set; }
    public int Removed { get; internal set; }
}

public sealed class AgentWorkspace
{
    public const int MaxFileBytes=1024*1024;
    public const int MaxDocumentBytes=8*1024*1024;
    const int MaxBackupBytes=16*1024*1024;
    readonly System.Collections.Concurrent.ConcurrentDictionary<string,AgentFileChange> changes=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,string> observed=new(StringComparer.OrdinalIgnoreCase);
    readonly System.Collections.Concurrent.ConcurrentQueue<string> createdDirectories=[];
    readonly List<AgentStep> steps=[];
    readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
    public TimeSpan Elapsed=>clock.Elapsed;
    public string Root { get; }
    public IReadOnlyList<AgentStep> Steps=>steps;
    public IReadOnlyList<AgentFileChange> Changes=>changes.Values.Where(c=>c.Changed).OrderBy(c=>c.Path,StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyList<string> CreatedFolders=>createdDirectories.Select(p=>System.IO.Path.GetRelativePath(Root,p).Replace('\\','/')).ToArray();
    public bool Running { get; internal set; }=true;
    public bool Undone { get; private set; }
    public string Outcome { get; internal set; }="Working";
    public event Action? Changed;
    public AgentWorkspace(string root)
    {
        Root=System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root));
        if(!Directory.Exists(Root))throw new InvalidOperationException("The selected folder is unavailable. Choose it again.");
        CheckAncestors(Root);
        if(Root==System.IO.Path.GetPathRoot(Root))throw new InvalidOperationException("Choose a project folder rather than an entire drive.");
    }
    public string Resolve(string relative)
    {
        if(relative.Length>500 || System.IO.Path.IsPathRooted(relative) || relative.Contains(':') || relative.Contains('\0'))throw new InvalidOperationException("Use a path relative to the selected folder.");
        var parts=relative.Replace('\\','/').Split('/',StringSplitOptions.RemoveEmptyEntries);
        if(parts.Any(p=>p!="." && (p==".." || p.Equals(".git",StringComparison.OrdinalIgnoreCase) || p.TrimEnd(' ','.')!=p || p.IndexOfAny(['*','?','"','<','>','|'])>=0 || new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(p.Split('.')[0],StringComparer.OrdinalIgnoreCase))))throw new InvalidOperationException("This path is outside the workspace or refers to a reserved path.");
        var full=System.IO.Path.GetFullPath(System.IO.Path.Combine(Root,relative.Length==0?".":relative));
        if(!full.Equals(Root,StringComparison.OrdinalIgnoreCase) && !full.StartsWith(Root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Access is limited to the folder you selected.");
        CheckAncestors(full); return full;
    }
    static void CheckAncestors(string path)
    {
        for(var current=path;!string.IsNullOrEmpty(current);current=System.IO.Path.GetDirectoryName(current))
        {
            if(System.IO.Path.GetFileName(current).Equals(".git",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Git metadata is outside the agent's file tools.");
            try { if(File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))throw new InvalidOperationException("Symbolic links and junctions are not followed by file tools."); }
            catch(FileNotFoundException) {} catch(DirectoryNotFoundException) {}
        }
    }
    static string Hash(byte[]? bytes)=>bytes==null ? "" : Convert.ToHexString(SHA256.HashData(bytes));
    static string Text(byte[] bytes)
    {
        try { var text=new UTF8Encoding(false,true).GetString(bytes); if(text.Any(c=>char.IsControl(c)&& c is not ('\n' or '\r' or '\t')))throw new DecoderFallbackException(); return text.TrimStart('\uFEFF'); }
        catch(DecoderFallbackException) { throw new InvalidOperationException("This file is binary. Use read_file for supported image previews or write_file with base64 encoding."); }
    }
    static byte[]? Read(string path,int limit=MaxFileBytes)
    {
        if(Directory.Exists(path))throw new InvalidOperationException("This path is a directory.");
        if(!File.Exists(path))return null;
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(OperatingSystem.IsWindows()) {
            if(!GetFileInformationByHandle(file.SafeFileHandle,out var information))throw new InvalidOperationException("The file identity could not be verified.");
            if(information.Links>1)throw new InvalidOperationException("Hard-linked files are not accessed by file tools. Use an independent copy.");
        }
        if(file.Length>limit)throw new InvalidOperationException($"This file is too large for this tool (limit {limit/1024/1024} MB). Read a smaller file.");
        var bytes=new byte[file.Length]; file.ReadExactly(bytes); return bytes;
    }
    [StructLayout(LayoutKind.Sequential)] struct FileInformation { public uint Attributes,CreatedLow,CreatedHigh,AccessedLow,AccessedHigh,WrittenLow,WrittenHigh,Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow; }
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInformation information);
    void Expected(string relative,byte[]? actual,string supplied)
    {
        if(!Hash(actual).Equals(supplied,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The file changed. Read it again and use its current sha256 before editing.");
        if(actual!=null && (!observed.TryGetValue(relative,out var readHash)||!readHash.Equals(supplied,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Read this file before changing or deleting it.");
    }
    AgentFileChange Snapshot(string relative,byte[]? before)
    {
        if(changes.TryGetValue(relative,out var change))return change;
        if(changes.Count>=40 || changes.Values.Sum(c=>(long)(c.Before?.Length??0)+(c.After?.Length??0))+(before?.Length??0)>MaxBackupBytes)throw new InvalidOperationException("This run has reached its change snapshot limit. Finish this task before making further changes.");
        changes[relative]=change=new(relative,before); return change;
    }
    public async Task<AgentToolResult> ExecuteAsync(AgentToolCall call,CancellationToken token)
    {
        var relative=call.Arguments.TryGetProperty("path",out var pathValue)&&pathValue.ValueKind==JsonValueKind.String?pathValue.GetString()??"":".";
        var step=new AgentStep(call.Name,relative,clock.Elapsed);steps.Add(step);Changed?.Invoke();
        try
        {
            if(!call.Arguments.TryGetProperty("path",out var suppliedPath)||suppliedPath.ValueKind!=JsonValueKind.String)throw new InvalidOperationException("path must be a workspace-relative string.");
            var result=await Task.Run(()=>Execute(call,relative,token),token);
            step.Status="Done";step.Detail=result.Image==null ? "Completed" : "Image opened";
            if(call.Name is "write_file" or "edit_file" or "delete_file" && changes.TryGetValue(relative.Replace('\\','/'),out var file)) { step.Added=file.Added;step.Removed=file.Removed;step.Detail=file.Kind; }
            Changed?.Invoke();return result;
        }
        catch(OperationCanceledException) { step.Status="Stopped";Changed?.Invoke();throw; }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException or FormatException)
        {
            step.Status="Failed";step.Detail=e is InvalidOperationException?e.Message:"Could not access this path. Check the file and folder permissions.";Changed?.Invoke();return new(call,JsonSerializer.Serialize(new { error=step.Detail }),true);
        }
        finally {step.EndTiming();Changed?.Invoke();}
    }
    public async Task<AgentToolResult> ExecuteCustomAsync(AgentToolCall call,Func<CancellationToken,Task<AgentToolResult>> action,CancellationToken token)
    {
        var path=call.Arguments.TryGetProperty("path",out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??"":call.Arguments.TryGetProperty("url",out var u)&&u.ValueKind==JsonValueKind.String?u.GetString()??"":".";
        var format=call.Arguments.TryGetProperty("format",out var f)&&f.ValueKind==JsonValueKind.String?f.GetString()??"":"";
        var step=new AgentStep(call.Name,path,clock.Elapsed,format);steps.Add(step);Changed?.Invoke();
        try {token.ThrowIfCancellationRequested();var result=await action(token);step.Status=result.Error?"Failed":"Done";step.Detail=result.Error?result.Text:"Completed";
            using(var parsed=JsonDocument.Parse(result.Text)) {
                var data=parsed.RootElement;
                if(call.Name.StartsWith("document_",StringComparison.Ordinal)){var document=data.TryGetProperty("document",out var inspected)&&inspected.ValueKind==JsonValueKind.Object?inspected:data;if(document.TryGetProperty("format",out var actualFormat)&&actualFormat.ValueKind==JsonValueKind.String)step.Format=actualFormat.GetString()??step.Format;}
                if(result.Error&&data.TryGetProperty("error",out var error))step.Detail=error.GetString()??result.Text;
                else if(call.Name=="generate_media"){step.Path="";step.Target=data.TryGetProperty("model",out var model)?model.GetString()??"":"";step.Detail=data.TryGetProperty("mime_type",out var mime)?"Generated "+mime.GetString():"Generated media";}
                else if(call.Name.StartsWith("computer_",StringComparison.Ordinal)||call.Name.StartsWith("browser_",StringComparison.Ordinal)||call.Name=="read_web_page") {
                    foreach(var key in new[]{"app","title","url"})if(data.TryGetProperty(key,out var target)&&target.ValueKind==JsonValueKind.String){step.Target=target.GetString()??"";if(step.Target.Length>0)break;}
                    if(data.TryGetProperty("windows",out var windows)&&windows.ValueKind==JsonValueKind.Array)step.Detail=$"Found {windows.GetArrayLength()} open windows";
                    else if(data.TryGetProperty("apps",out var apps)&&apps.ValueKind==JsonValueKind.Array){step.Detail=$"Found {apps.GetArrayLength()} installed apps";step.Target=string.Join(", ",apps.EnumerateArray().Take(4).Select(a=>a.GetProperty("name").GetString()));}
                    else if(data.TryGetProperty("controls",out var controls)&&controls.ValueKind==JsonValueKind.Array)step.Detail=$"Inspected {controls.GetArrayLength()} controls";
                }
                if(call.Name=="planning_decision"&&!result.Error&&data.TryGetProperty("path",out var saved)&&saved.ValueKind==JsonValueKind.String)step.Path=path=saved.GetString()!;
            }
            if(changes.TryGetValue(path.Replace('\\','/'),out var file)){step.Added=file.Added;step.Removed=file.Removed;step.Detail=file.Kind;}Changed?.Invoke();return result;}
        catch(OperationCanceledException){step.Status="Stopped";Changed?.Invoke();throw;}
        catch(Exception e) when(e is not OutOfMemoryException and not StackOverflowException) {step.Status="Failed";step.Detail=e is InvalidOperationException or ArgumentException?e.Message:"The tool could not complete this action. Check the supplied format and permissions.";Changed?.Invoke();return new(call,JsonSerializer.Serialize(new{error=step.Detail}),true);}
        finally {step.EndTiming();Changed?.Invoke();}
    }
    public (byte[] Bytes,string Sha256) ReadDocument(string relative)
    {var path=Resolve(relative);relative=System.IO.Path.GetRelativePath(Root,path).Replace('\\','/');var bytes=Read(path,MaxDocumentBytes)??throw new InvalidOperationException("File not found.");observed[relative]=Hash(bytes);return(bytes,Hash(bytes));}
    public string WriteDocument(string relative,byte[] bytes,string expected,CancellationToken token)
    {
        if(bytes.Length>MaxDocumentBytes)throw new InvalidOperationException("Generated files must be smaller than 8 MB.");var path=Resolve(relative);if(path==Root)throw new InvalidOperationException("Choose a file path.");relative=System.IO.Path.GetRelativePath(Root,path).Replace('\\','/');
        var before=Read(path,MaxDocumentBytes);Expected(relative,before,expected);
        if(!Directory.Exists(System.IO.Path.GetDirectoryName(path)))throw new InvalidOperationException("Create the parent directory first.");
        changes.TryGetValue(relative,out var existing);
        if(changes.Values.Sum(c=>(long)(c.Before?.Length??0)+(c.After?.Length??0))-(existing?.After?.Length??0)+bytes.Length+(existing==null?before?.Length??0:0)>MaxBackupBytes)throw new InvalidOperationException("Change snapshot limit reached. Finish this task first.");
        var change=Snapshot(relative,before);token.ThrowIfCancellationRequested();Resolve(relative);Commit(path,before,bytes);change.After=bytes;observed[relative]=Hash(bytes);Changed?.Invoke();return Hash(bytes);
    }
    AgentToolResult Execute(AgentToolCall call,string relative,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var path=Resolve(relative);relative=System.IO.Path.GetRelativePath(Root,path).Replace('\\','/');
        string Arg(string key)=>call.Arguments.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String ? value.GetString()! : throw new InvalidOperationException("Missing argument: "+key);
        object result;
        switch(call.Name)
        {
            case "list_directory":
                if(!Directory.Exists(path))throw new InvalidOperationException("Directory not found.");
                var entries=Directory.EnumerateFileSystemEntries(path).Take(301).Select(p=>new { name=System.IO.Path.GetFileName(p), directory=Directory.Exists(p), link=File.GetAttributes(p).HasFlag(FileAttributes.ReparsePoint) }).ToArray();
                result=new { path=relative,entries=entries.Take(300),truncated=entries.Length>300 };break;
            case "read_file":
                var bytes=Read(path)??throw new InvalidOperationException("File not found.");observed[relative]=Hash(bytes);
                var ext=System.IO.Path.GetExtension(path).ToLowerInvariant();
                if(ext is ".png" or ".jpg" or ".jpeg" or ".webp") {
                    var mime=ext==".png"?"image/png":ext==".webp"?"image/webp":"image/jpeg";
                    return new(call,JsonSerializer.Serialize(new { path=relative,sha256=Hash(bytes),mime_type=mime }),false,new(relative,mime,Convert.ToBase64String(bytes),null,bytes.Length));
                }
                var text=Text(bytes);var lines=text.Replace("\r\n","\n").Split('\n');
                var start=call.Arguments.TryGetProperty("start_line",out var startValue)?Math.Max(1,startValue.GetInt32()):1;
                var count=call.Arguments.TryGetProperty("line_count",out var countValue)?Math.Clamp(countValue.GetInt32(),1,300):200;
                var slice=string.Join('\n',lines.Skip(start-1).Take(count));
                result=new { path=relative,sha256=Hash(bytes),total_lines=lines.Length,start_line=start,content=slice.Length<=32000?slice:slice[..32000],truncated=slice.Length>32000 || start-1+count<lines.Length };break;
            case "search_files":
                var query=Arg("query");if(query.Length<1||query.Length>200)throw new InvalidOperationException("Use a literal search query between 1 and 200 characters.");
                var matches=new List<object>();int scanned=0,visited=0,seen=0;var queue=new Queue<string>();queue.Enqueue(path);
                while(queue.TryDequeue(out var directory) && scanned<1000 && matches.Count<100 && visited++<500 && seen<5000) {
                    token.ThrowIfCancellationRequested();
                    foreach(var entry in Directory.EnumerateFileSystemEntries(directory).Take(1001)) {
                        if(++seen>5000)break;
                        if(File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint))continue;
                        if(Directory.Exists(entry)) { if(!new[]{".git","node_modules","bin","obj",".vs"}.Contains(System.IO.Path.GetFileName(entry),StringComparer.OrdinalIgnoreCase))queue.Enqueue(entry);continue; }
                        if(++scanned>1000)break;
                        try { var data=Read(entry);if(data==null)continue;var content=Text(data).Replace("\r\n","\n").Split('\n');for(var i=0;i<content.Length && matches.Count<100;i++)if(content[i].Contains(query,StringComparison.OrdinalIgnoreCase))matches.Add(new { path=System.IO.Path.GetRelativePath(Root,entry).Replace('\\','/'),line=i+1,text=content[i][..Math.Min(300,content[i].Length)] }); }catch(IOException){}catch(InvalidOperationException){}
                    }
                }
                result=new { matches,scanned_files=scanned,limited=scanned>=1000||matches.Count>=100||visited>=500||seen>=5000 };break;
            case "write_file": case "edit_file": case "delete_file":
                if(path==Root)throw new InvalidOperationException("Select a file inside the workspace.");
                var before=Read(path,call.Name=="delete_file"?MaxDocumentBytes:MaxFileBytes);Expected(relative,before,Arg("expected_sha256"));
                byte[]? after;
                if(call.Name=="delete_file") { if(before==null)throw new InvalidOperationException("File not found.");after=null; }
                else if(call.Name=="edit_file") {
                    if(before==null)throw new InvalidOperationException("File not found.");var current=Text(before);var old=Arg("old_text");
                    if(old.Length==0 || current.IndexOf(old,StringComparison.Ordinal)<0 || current.IndexOf(old,StringComparison.Ordinal)!=current.LastIndexOf(old,StringComparison.Ordinal))throw new InvalidOperationException("old_text must match exactly once. Read the file and include more surrounding text.");
                    var changed=current.Replace(old,Arg("new_text"),StringComparison.Ordinal);after=new UTF8Encoding(before.AsSpan().StartsWith(new byte[]{239,187,191})).GetPreamble().Concat(Encoding.UTF8.GetBytes(changed)).ToArray();
                }
                else { var content=Arg("content");var encoding=Arg("encoding");after=encoding=="base64" ? Convert.FromBase64String(content) : encoding=="utf8" ? Encoding.UTF8.GetBytes(content) : throw new InvalidOperationException("encoding must be utf8 or base64."); }
                if(after?.Length>MaxFileBytes)throw new InvalidOperationException("Write at most 1 MB per file.");
                if(!Directory.Exists(System.IO.Path.GetDirectoryName(path)))throw new InvalidOperationException("Parent folder not found. Call create_directory first.");
                if(changes.Values.Sum(c=>(long)(c.Before?.Length??0)+(c.After?.Length??0))+(after?.Length??0)>MaxBackupBytes)throw new InvalidOperationException("Change snapshot limit reached. Finish this task first.");
                var snapshot=Snapshot(relative,before); token.ThrowIfCancellationRequested();Resolve(relative);
                Commit(path,before,after);snapshot.After=after;observed[relative]=Hash(after);
                result=new { path=relative,operation=snapshot.Kind,sha256=Hash(after),added=snapshot.Added,removed=snapshot.Removed };break;
            case "create_directory":
                if(File.Exists(path))throw new InvalidOperationException("A file already exists at this path.");
                var missing=new List<string>();for(var current=path;current!=Root&&!Directory.Exists(current);current=System.IO.Path.GetDirectoryName(current)!)missing.Add(current);
                if(createdDirectories.Count+missing.Count>60)throw new InvalidOperationException("Too many directories in one task.");
                foreach(var directory in missing.AsEnumerable().Reverse()) { token.ThrowIfCancellationRequested();Resolve(System.IO.Path.GetRelativePath(Root,directory));Directory.CreateDirectory(directory);createdDirectories.Enqueue(directory); }
                result=new { path=relative,created=true };break;
            default: throw new InvalidOperationException("Unknown tool. Use only the declared file tools.");
        }
        return new(call,JsonSerializer.Serialize(result));
    }
    static void Commit(string path,byte[]? expected,byte[]? after)
    {
        // Validate again immediately before the atomic replacement. Never silently
        // overwrite an editor's newer version; never recursively delete a folder.
        if(!AgentFileChange.Same(Read(path,MaxDocumentBytes),expected))throw new InvalidOperationException("The file changed before the operation. Read it again.");
        if(after==null) { File.Delete(path);return; }
        var temporary=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!,".yowidgets-"+Guid.NewGuid().ToString("N")+".tmp");
        try { using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {stream.Write(after);stream.Flush(true);} CheckAncestors(path);if(!AgentFileChange.Same(Read(path,MaxDocumentBytes),expected))throw new InvalidOperationException("The file changed before the operation. Read it again.");File.Move(temporary,path,true); }
        finally { if(File.Exists(temporary))File.Delete(temporary); }
    }
    public void Finish(string outcome) { clock.Stop();foreach(var step in steps){if(step.Status=="Working")step.Status="Stopped";step.EndTiming();}Running=false;Outcome=outcome;Changed?.Invoke(); }
    public string Undo()
    {
        if(Running) return "Stop the agent before undoing its changes.";
        if(Undone)return "Changes already undone.";
        try {
            foreach(var change in Changes.Where(c=>!c.Undone))if(!AgentFileChange.Same(Read(Resolve(change.Path),MaxDocumentBytes),change.After))return "Undo stopped: "+change.Path+" was changed outside this run. Your newer version was preserved.";
            foreach(var change in Changes.Where(c=>!c.Undone).Reverse()) { Commit(Resolve(change.Path),change.After,change.Before);change.Undone=true; }
            foreach(var directory in createdDirectories.AsEnumerable().Reverse()) {Resolve(System.IO.Path.GetRelativePath(Root,directory));if(Directory.Exists(directory)&&!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory);}
            Undone=true;Changed?.Invoke();return "Changes undone.";
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException) { Changed?.Invoke();return "Undo stopped. Some files could not be restored; inspect the change list before retrying."; }
    }
}

public static class AgentDiff
{
    public static IReadOnlyList<AgentDiffLine> Create(byte[]? before,byte[]? after)
    {
        string[] Lines(byte[]? bytes) { if(bytes==null||bytes.Length==0)return [];var text=new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF').Replace("\r\n","\n"); if(text.Any(c=>c=='\0'))throw new DecoderFallbackException();var lines=text.Split('\n');return text.EndsWith('\n')?lines[..^1]:lines; }
        try {
            var a=Lines(before);var b=Lines(after);var output=new List<AgentDiffLine>();int prefix=0,suffix=0;
            while(prefix<Math.Min(a.Length,b.Length)&&a[prefix]==b[prefix]) {output.Add(new(' ',prefix+1,prefix+1,a[prefix]));prefix++;}
            while(suffix<Math.Min(a.Length,b.Length)-prefix&&a[^(suffix+1)]==b[^(suffix+1)])suffix++;
            int n=a.Length-prefix-suffix,m=b.Length-prefix-suffix;
            if((long)n*m<=1_000_000) {
                var matrix=new int[n+1,m+1];for(int i=n-1;i>=0;i--)for(int j=m-1;j>=0;j--)matrix[i,j]=a[prefix+i]==b[prefix+j]?matrix[i+1,j+1]+1:Math.Max(matrix[i+1,j],matrix[i,j+1]);
                int x=0,y=0;while(x<n||y<m) { if(x<n&&y<m&&a[prefix+x]==b[prefix+y]){output.Add(new(' ',prefix+x+1,prefix+y+1,a[prefix+x]));x++;y++;}else if(y<m&&(x==n||matrix[x,y+1]>matrix[x+1,y])){output.Add(new('+',null,prefix+y+1,b[prefix+y++]));}else output.Add(new('-',prefix+x+1,null,a[prefix+x++])); }
            } else {for(var i=0;i<n;i++)output.Add(new('-',prefix+i+1,null,a[prefix+i]));for(var i=0;i<m;i++)output.Add(new('+',null,prefix+i+1,b[prefix+i]));}
            for(var i=suffix;i>0;i--)output.Add(new(' ',a.Length-i+1,b.Length-i+1,a[^i]));return output;
        }catch(DecoderFallbackException) { return []; }
    }
}
