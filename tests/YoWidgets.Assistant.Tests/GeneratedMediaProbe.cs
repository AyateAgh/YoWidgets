using System.Text.Json;
using YoWidgets;
internal static class GeneratedMediaProbe
{
    internal static async Task<int> Run()
    {
        var settings=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YoWidgets","settings.json")))!;settings.Normalize();
        var ai=new AiService();ai.SelectPluginInstruction("media");var seen="";
        ai.Changed+=()=>{var job=ai.Messages.LastOrDefault()?.Media.LastOrDefault();if(job!=null&&job.Stage!=seen){seen=job.Stage;Console.WriteLine("LIVE media stage: "+seen);}};
        using var timer=new System.Threading.Timer(_=>ai.Cancel(),null,TimeSpan.FromMinutes(4),Timeout.InfiniteTimeSpan);
        await ai.SendAsync("Synthetic media integration check: use generate_media once to create a still image, square 1:1, showing a simple blue circle on a plain white background. No video, no personal inputs, no other tools. Finish after the actual image is displayed.",settings);
        var image=ai.Messages.Last().Media.LastOrDefault()?.Result;
        if(image==null){Console.WriteLine("LIVE media unsuccessful: "+ai.Error+"; "+ai.Messages.Last().Media.LastOrDefault()?.Error);return 1;}
        Console.WriteLine("LIVE generated: "+image.MimeType+"; bytes="+new FileInfo(image.Path).Length+"; model="+image.Model+"; path="+image.Path);
        await ai.SendAsync("Edit the image you just generated: change only the blue circle to green, keep the white background. Use the actual prior media reference with generate_media once. No video or other tools.",settings);
        var edited=ai.Messages.Last().Media.LastOrDefault()?.Result;var passed=edited!=null&&edited.ParentId==image.Id&&File.Exists(image.Path)&&edited.Path!=image.Path;
        Console.WriteLine("LIVE media follow-up: "+passed+"; error="+ai.Error);return passed?0:1;
    }
}
