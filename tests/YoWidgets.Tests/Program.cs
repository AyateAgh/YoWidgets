using YoWidgets.Core;

var failures = 0;
void Test(string name, Action run) { try { run(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
void Eq<T>(T expected, T actual) { if(expected is double a&&actual is double b&&Math.Abs(a-b)<.000001)return; if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}, got {actual}"); }
Test("offscreen position returns to negative-coordinate display", () => { var p=Placement.Clamp(5000,900,300,200,new(-1920,0,1920,1080)); Eq(-300d,p.X); Eq(880d,p.Y); });
Test("oversized widget anchors at work area origin", () => { var p=Placement.Clamp(-400,20,2500,1400,new(100,100,1920,1080)); Eq(100d,p.X); Eq(100d,p.Y); });
Test("cleanup rejects sibling with same prefix",()=>Eq(false,OwnedPaths.IsInside(@"C:\Users\A\YoWidgets",@"C:\Users\A\YoWidgets-old\photo.jpg")));
Test("cleanup rejects root itself",()=>Eq(false,OwnedPaths.IsInside(@"C:\Users\A\YoWidgets",@"C:\Users\A\YoWidgets")));
Test("cleanup accepts child",()=>Eq(true,OwnedPaths.IsInside(@"C:\Users\A\YoWidgets",@"C:\Users\A\YoWidgets\cache\art.png")));
Test("lyrics parses repeated timestamps and sorts",()=> { var l=Lyrics.Parse("[00:09.50][00:19.50]Again\n[00:01.20]First\n[ar:Example]");Eq(3,l.Count);Eq(1.2,l[0].Seconds);Eq("Again",l[2].Text); });
Test("generation gate rejects response after suspension",()=>{var g=new WorkGate();var v=g.Version;g.Suspend();Eq(false,g.CanApply(v));g.Resume();Eq(false,g.CanApply(v));Eq(true,g.CanApply(g.Version));});
Test("invalid numeric settings are normalized",()=>{var s=new AppSettings{GlassStrength=double.NaN,Opacity=9};s.Normalize();Eq(1d,s.GlassStrength);Eq(.85,s.Opacity);});
Test("corrupt settings recover without loss of backup",()=>{var d=Path.Combine(Path.GetTempPath(),"YoWidgetsTest-"+Guid.NewGuid());Directory.CreateDirectory(d);try{var store=new SettingsStore(Path.Combine(d,"settings.json"));store.Save(new AppSettings{City="Lviv"});File.WriteAllText(store.Path,"broken");Eq("Lviv",store.Load().City);Eq(true,File.Exists(store.Path+".corrupt"));}finally{Directory.Delete(d,true);}});
Test("fit wallpaper has letterbox offset",()=>{var r=WallpaperLayout.Calculate(1920,1080,new(0,0,1000,1000),"Fit");Eq(1000d,r.Width);Eq(562.5,r.Height);Eq(218.75,r.Y);});
Test("inactive or paused widgets request no fast timer",()=>{Eq(60,ActivityPolicy.Seconds(false,false,false,false));Eq(1,ActivityPolicy.Seconds(true,false,false,false));Eq(0,ActivityPolicy.Seconds(true,true,true,true));});
Test("duplicate widget ids cannot lose a window during cleanup",()=>{var s=new AppSettings{Widgets=[new(){Id="same",Kind="clock"},new(){Id="same",Kind="music"}]};s.Normalize();Eq(2,s.Widgets.Select(x=>x.Id).Distinct().Count());});
Test("rollback preserves external changes",()=>Eq(false,RollbackPolicy.CanRestore("speaker","speaker",70,45)));
Test("rollback refuses a replacement device",()=>Eq(false,RollbackPolicy.CanRestore("old","new",45,45)));
Test("rollback tolerates device rounding",()=>Eq(true,RollbackPolicy.CanRestore("speaker","speaker",44.8,45)));
Console.WriteLine($"RESULT: {15-failures}/15 passed");return failures==0?0:1;
