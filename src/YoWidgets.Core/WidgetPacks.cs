namespace YoWidgets.Core;

public sealed record WidgetPack(string Id, string Name, string Description, string Character, float Blur, float InteractionBlur, double Tint, bool Liquid);

// Declarative materials over fixed native renderers. No downloaded executable code.
public static class WidgetPacks
{
    static IReadOnlyDictionary<string,WidgetPack> installed = new Dictionary<string,WidgetPack>();
    public static void LoadInstalled(ContentStore store)
    {
        var result = new Dictionary<string,WidgetPack>();
        foreach (var reference in store.Installed.SelectMany(i=>i.Packages).Where(p=>p.Id.StartsWith("style.")).OrderByDescending(p=>Version.Parse(p.Version)).DistinctBy(p=>p.Id))
        {
            try
            {
                using var document=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(store.PackageFile(reference.Id,reference.Version,"style.json")));
                var value=document.RootElement;var id=value.GetProperty("id").GetString();if(!CatalogRules.ValidId(id))continue;var name=value.TryGetProperty("name",out var label)?label.GetString()??id!:id!;if(name.Length>80)name=id!;var original=All.FirstOrDefault(p=>p.Id==id)??new WidgetPack(id!,name,"","",2,2,.1,false);
                var blur=value.GetProperty("blur").GetSingle();var interaction=value.GetProperty("interactionBlur").GetSingle();var tint=value.GetProperty("tint").GetDouble();
                if(!float.IsFinite(blur)||!float.IsFinite(interaction)||!double.IsFinite(tint)||blur is <0 or >20||interaction is <0 or >20||tint is <0 or >.7)continue;
                result[id!]=original with{Name=name==id&&All.Any(p=>p.Id==id)?original.Name:name,Blur=blur,InteractionBlur=interaction,Tint=tint,Liquid=value.GetProperty("liquid").GetBoolean()};
            }catch(Exception e)when(e is System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException){}
        }
        installed=result;
    }
    public static IReadOnlyList<WidgetPack> All { get; } = Array.AsReadOnly(new[]
    {
        new WidgetPack("glass", "Glass", "Clear surfaces. A quiet edge.", "Simple & understated", 2f, 2f, .10, false),
        new WidgetPack("liquid", "Liquid Glass", "Light, depth and a little movement.", "Inspired by Apple", 3f, 4.5f, .14, true)
    });
    public static WidgetPack Get(string? id) => id!=null&&installed.TryGetValue(id,out var value)?value:All.FirstOrDefault(p => p.Id == id) ?? All[1];
}
