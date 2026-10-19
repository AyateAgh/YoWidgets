using System.Globalization;
using System.Text.RegularExpressions;
namespace YoWidgets.Core;
public static partial class Lyrics {
 [GeneratedRegex(@"\[(\d+):(\d+(?:\.\d+)?)\]")] private static partial Regex Timestamp();
 public static List<LyricLine> Parse(string text){var result=new List<LyricLine>();foreach(var line in text.Split('\n')){var matches=Timestamp().Matches(line);var content=Timestamp().Replace(line,"").Trim();foreach(Match m in matches)if(double.TryParse(m.Groups[1].Value,CultureInfo.InvariantCulture,out var min)&&double.TryParse(m.Groups[2].Value,CultureInfo.InvariantCulture,out var sec))result.Add(new(min*60+sec,content));}return result.OrderBy(x=>x.Seconds).ToList();}
}
public sealed class WorkGate {
 private long version;private bool suspended;
 public long Version=>Interlocked.Read(ref version);public bool Suspended=>Volatile.Read(ref suspended);
 public bool CanApply(long value)=>!Suspended&&value==Version;
 public void Suspend(){Volatile.Write(ref suspended,true);Interlocked.Increment(ref version);}
 public void Resume(){Interlocked.Increment(ref version);Volatile.Write(ref suspended,false);}
}
