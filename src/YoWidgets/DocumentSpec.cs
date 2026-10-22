using System.Text.Json;
using System.Text.RegularExpressions;
namespace YoWidgets;

internal static class DocumentSpec
{
    internal static string S(JsonElement e,string key,string fallback="")=>e.TryGetProperty(key,out var value)?value.ValueKind==JsonValueKind.String?value.GetString()??fallback:throw new InvalidOperationException(key+" must be text."):fallback;
    internal static double N(JsonElement e,string key,double fallback,double min=0,double max=10000)
    {if(!e.TryGetProperty(key,out var value))return fallback;if(!value.TryGetDouble(out var number)||!double.IsFinite(number)||number<min||number>max)throw new InvalidOperationException(key+$" must be between {min} and {max}.");return number;}
    internal static bool B(JsonElement e,string key,bool fallback=false)=>e.TryGetProperty(key,out var value)?value.ValueKind is JsonValueKind.True or JsonValueKind.False?value.GetBoolean():throw new InvalidOperationException(key+" must be true or false."):fallback;
    internal static JsonElement[] A(JsonElement e,string key,int max=500)
    {if(!e.TryGetProperty(key,out var value))return [];if(value.ValueKind!=JsonValueKind.Array||value.GetArrayLength()>max)throw new InvalidOperationException(key+" contains too many elements or is not an array.");return value.EnumerateArray().ToArray();}
    internal static string Color(JsonElement e,string key,string fallback="111827")
    {var color=S(e,key,fallback).TrimStart('#');if(!Regex.IsMatch(color,"^[0-9A-Fa-f]{6}$"))throw new InvalidOperationException(key+" must be a six-digit RGB color.");return color;}
    internal static string Cell(JsonElement value)=>value.ValueKind==JsonValueKind.String?value.GetString()??"":value.ToString();
    internal static void Keys(JsonElement e,params string[] allowed)
    {if(e.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("Use a JSON object for the document specification.");foreach(var p in e.EnumerateObject())if(!allowed.Contains(p.Name,StringComparer.Ordinal))throw new InvalidOperationException("Unsupported document setting: "+p.Name+". Read document_schema for supported fields.");}
    internal static (double Width,double Height,double Top,double Right,double Bottom,double Left) Page(JsonElement spec)
    {
        if(!spec.TryGetProperty("page",out var page))return(210,297,20,20,20,20);
        var size=S(page,"size","A4");var width=size=="Letter"?215.9:210;var height=size=="Letter"?279.4:297;width=N(page,"width_mm",width,50,600);height=N(page,"height_mm",height,50,600);
        if(S(page,"orientation","portrait")=="landscape"&&width<height)(width,height)=(height,width);
        double top=20,right=20,bottom=20,left=20;if(page.TryGetProperty("margins_mm",out var margins)){top=N(margins,"top",20,0,150);right=N(margins,"right",20,0,150);bottom=N(margins,"bottom",20,0,150);left=N(margins,"left",20,0,150);}
        if(left+right>=width-20||top+bottom>=height-20)throw new InvalidOperationException("Page margins leave insufficient room for content.");return(width,height,top,right,bottom,left);
    }
}
