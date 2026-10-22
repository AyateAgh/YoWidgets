using System.Text.Json;
namespace YoWidgets;

internal sealed class VoiceActionBridge(WidgetManager manager):IDisposable
{
    bool disposed;int ownedRequest=-1,ownedRequestVersion=-1;
    internal Action<string>? Progress;
    internal async Task<AgentToolResult> Execute(AgentToolCall call,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(disposed)throw new OperationCanceledException(token);
        if(!manager.Settings.VoiceCall.AllowActions)throw new InvalidOperationException("Voice actions are disabled in Live voice settings.");
        if(call.Name=="widget_command"){
            var action=call.Arguments.GetProperty("action").GetString()??"";using var arguments=JsonDocument.Parse(call.Arguments.GetProperty("arguments_json").GetString()??"{}");Progress?.Invoke("Widgets · "+action.Replace('_',' '));
            var result=await manager.Commands.Execute(action,arguments.RootElement,token);return new(call,JsonSerializer.Serialize(result));
        }
        if(call.Name!="execute_task")throw new InvalidOperationException("Unknown voice tool.");
        var task=call.Arguments.GetProperty("task").GetString()?.Trim()??"";if(task.Length is <3 or >16000)throw new InvalidOperationException("Describe the requested task in 3–16000 characters.");
        var ai=manager.Data.Ai;if(ai.Busy)throw new InvalidOperationException("Another task is still running. Wait for it to finish or stop it first.");
        var draft=ai.Draft;var files=ai.DraftAttachments.ToArray();var instructions=ai.DraftPluginInstructions.ToArray();
        void Changed()=>Progress?.Invoke(ai.Phase==AssistantPhase.WaitingApproval?"Waiting for your approval…":ai.ToolProgress.Length>0?ai.ToolProgress:AssistantActivity.From(ai,"Working…").Label);
        ai.Changed+=Changed;var session=ai.SessionVersion;var requestVersion=-1;ownedRequest=session;
        using var cancel=token.Register(()=>{if(ownedRequest==session&&ai.SessionVersion==session&&ai.RequestVersion==requestVersion)ai.Cancel();});
        try{
            token.ThrowIfCancellationRequested();var count=ai.Messages.Count;var work=ai.SendAsync("Voice user request:\n"+task+"\nRead actual current sources before answering. For a player's statistics require a specific profile or visible player identity; ask if missing. Never invent statistics or claim actions without verified tool results. Preserve the user's language.",manager.Settings,files,instructions,["browser","computer"]);requestVersion=ai.RequestVersion;ownedRequestVersion=requestVersion;var answer=ai.Messages.Count>count?ai.Messages.LastOrDefault(m=>m.Role=="assistant"):null;if(token.IsCancellationRequested&&ai.SessionVersion==session&&ai.RequestVersion==requestVersion)ai.Cancel();await work;token.ThrowIfCancellationRequested();
            if(ai.SessionVersion!=session)throw new OperationCanceledException(token);
            if(ai.Error.Length>0)return new(call,JsonSerializer.Serialize(new{ok=false,error=ai.Error,observed=answer?.Text??""}),true);
            if(answer==null||!answer.Complete)return new(call,JsonSerializer.Serialize(new{ok=false,error="The task did not return a complete result."}),true);
            return new(call,JsonSerializer.Serialize(new{ok=true,observed=answer.Text,actions=answer.Agent?.Steps.Select(s=>new{s.Tool,s.Status,s.Detail,s.Target}),instruction="Only narrate verified results above. If data is missing, blocked or denied, say so. Never invent player statistics or completed actions."}));
        }finally{ownedRequest=ownedRequestVersion=-1;ai.Changed-=Changed;var ownRequest=ai.RequestVersion==requestVersion||token.IsCancellationRequested&&ai.RequestVersion==requestVersion+1&&ai.Phase==AssistantPhase.Stopped;if(ownRequest&&ai.SessionVersion==session&&!ai.Busy&&ai.Draft.Length==0&&ai.DraftAttachments.Count==0&&ai.DraftPluginInstructions.Count==0){ai.Draft=draft;ai.DraftAttachments.AddRange(files);ai.DraftPluginInstructions.UnionWith(instructions);}}
    }
    public void Dispose(){disposed=true;var ai=manager.Data.Ai;if(ownedRequest>=0&&ai.SessionVersion==ownedRequest&&ai.RequestVersion==ownedRequestVersion)ai.Cancel();}
}
