namespace YoWidgets.Core;

public sealed record AppearancePreferences
{
    public string Mode {get;init;}="dark";
    public string[] Colors {get;init;}=["#8B9DC3"];
    public bool Transparent {get;init;}
    [System.Text.Json.Serialization.JsonIgnore] public bool Valid=>Mode is ("dark" or "light" or "custom")&&Colors is {Length:>=1 and <=2}&&Colors.All(c=>c is {Length:7}&&c[0]=='#'&&c.Skip(1).All(Uri.IsHexDigit));
    public AppearancePreferences Normalized()=>this with{Colors=Colors.Select(c=>c.ToUpperInvariant()).ToArray()};
}
