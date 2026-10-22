using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using A=DocumentFormat.OpenXml.Drawing;
using P=DocumentFormat.OpenXml.Presentation;
namespace YoWidgets;

internal static class PresentationWriter
{
    internal static byte[] Create(JsonElement spec,Func<string,byte[]> image)
    {
        DocumentSpec.Keys(spec,"title","width_in","height_in","font_name","slides");
        var width=DocumentSpec.N(spec,"width_in",13.333,3,30);var height=DocumentSpec.N(spec,"height_in",7.5,3,30);var font=DocumentSpec.S(spec,"font_name","Arial");
        using var stream=new MemoryStream();using(var doc=PresentationDocument.Create(stream,PresentationDocumentType.Presentation)) {
            doc.PackageProperties.Title=DocumentSpec.S(spec,"title");var presentation=doc.AddPresentationPart();presentation.Presentation=new P.Presentation();var master=presentation.AddNewPart<SlideMasterPart>();
            master.SlideMaster=new P.SlideMaster(new P.CommonSlideData(Tree()),new P.ColorMap {Background1=A.ColorSchemeIndexValues.Light1,Text1=A.ColorSchemeIndexValues.Dark1,Background2=A.ColorSchemeIndexValues.Light2,Text2=A.ColorSchemeIndexValues.Dark2,Accent1=A.ColorSchemeIndexValues.Accent1,Accent2=A.ColorSchemeIndexValues.Accent2,Accent3=A.ColorSchemeIndexValues.Accent3,Accent4=A.ColorSchemeIndexValues.Accent4,Accent5=A.ColorSchemeIndexValues.Accent5,Accent6=A.ColorSchemeIndexValues.Accent6,Hyperlink=A.ColorSchemeIndexValues.Hyperlink,FollowedHyperlink=A.ColorSchemeIndexValues.FollowedHyperlink});
            var layout=master.AddNewPart<SlideLayoutPart>();layout.SlideLayout=new P.SlideLayout(new P.CommonSlideData(Tree()),new P.ColorMapOverride(new A.MasterColorMapping())) {Type=P.SlideLayoutValues.Blank,Preserve=true};layout.AddPart(master);
            master.SlideMaster.Append(new P.SlideLayoutIdList(new P.SlideLayoutId {Id=2147483649,RelationshipId=master.GetIdOfPart(layout)}));var theme=master.AddNewPart<ThemePart>();theme.Theme=Theme(font);theme.Theme.Save();master.SlideMaster.Save();layout.SlideLayout.Save();
            presentation.Presentation.Append(new P.SlideMasterIdList(new P.SlideMasterId {Id=2147483648,RelationshipId=presentation.GetIdOfPart(master)}));var ids=new P.SlideIdList();presentation.Presentation.Append(ids);
            var slides=DocumentSpec.A(spec,"slides",50);if(slides.Length==0)throw new InvalidOperationException("Add at least one slide.");uint index=256;
            foreach(var slide in slides) {
                var part=presentation.AddNewPart<SlidePart>();part.AddPart(layout);var tree=Tree();var common=new P.CommonSlideData();common.Append(new P.Background(new P.BackgroundProperties(new A.SolidFill(new A.RgbColorModelHex {Val=DocumentSpec.Color(slide,"background","FFFFFF")}))),tree);part.Slide=new P.Slide(common,new P.ColorMapOverride(new A.MasterColorMapping()));uint shapeId=2;
                var elements=DocumentSpec.A(slide,"elements",100);if(elements.Length==0)elements=[JsonSerializer.SerializeToElement(new{type="text",text=DocumentSpec.S(slide,"title"),x_in=.8,y_in=.5,width_in=width-1.6,height_in=1,font_size=34,bold=true}),JsonSerializer.SerializeToElement(new{type="text",text=DocumentSpec.S(slide,"body"),x_in=.8,y_in=1.8,width_in=width-1.6,height_in=height-2.3,font_size=22})];
                foreach(var element in elements) {
                    var type=DocumentSpec.S(element,"type","text");var x=DocumentSpec.N(element,"x_in",.8,0,width);var y=DocumentSpec.N(element,"y_in",.8,0,height);var w=DocumentSpec.N(element,"width_in",width-1.6,.1,width);var h=DocumentSpec.N(element,"height_in",1,.1,height);if(x+w>width+.01||y+h>height+.01)throw new InvalidOperationException("A slide element extends outside the slide.");
                    var transform=new A.Transform2D(new A.Offset {X=Emu(x),Y=Emu(y)},new A.Extents {Cx=Emu(w),Cy=Emu(h)});
                    if(type=="image") {
                        var path=DocumentSpec.S(element,"path");var ext=Path.GetExtension(path).ToLowerInvariant();var imagePart=part.AddImagePart(ext is ".jpg" or ".jpeg"?ImagePartType.Jpeg:ext==".png"?ImagePartType.Png:throw new InvalidOperationException("Use PNG/JPEG slide images."));using(var input=new MemoryStream(image(path)))imagePart.FeedData(input);
                        tree.Append(new P.Picture(new P.NonVisualPictureProperties(new P.NonVisualDrawingProperties {Id=shapeId++,Name=Path.GetFileName(path)},new P.NonVisualPictureDrawingProperties(new A.PictureLocks {NoChangeAspect=true}),new P.ApplicationNonVisualDrawingProperties()),new P.BlipFill(new A.Blip {Embed=part.GetIdOfPart(imagePart)},new A.Stretch(new A.FillRectangle())),new P.ShapeProperties(transform,new A.PresetGeometry(new A.AdjustValueList()) {Preset=A.ShapeTypeValues.Rectangle})));continue;
                    }
                    if(type is not ("text" or "rectangle" or "ellipse"))throw new InvalidOperationException("Unsupported slide element: "+type);
                    var shape=new P.Shape(new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties {Id=shapeId++,Name="Shape "+shapeId},new P.NonVisualShapeDrawingProperties {TextBox=type=="text"},new P.ApplicationNonVisualDrawingProperties()));
                    var properties=new P.ShapeProperties(transform,new A.PresetGeometry(new A.AdjustValueList()) {Preset=type=="ellipse"?A.ShapeTypeValues.Ellipse:A.ShapeTypeValues.Rectangle});if(type=="text"&&!element.TryGetProperty("fill",out _))properties.Append(new A.NoFill());else properties.Append(new A.SolidFill(new A.RgbColorModelHex {Val=DocumentSpec.Color(element,"fill","E8EEF8")}));
                    if(element.TryGetProperty("border_color",out _))properties.Append(new A.Outline(new A.SolidFill(new A.RgbColorModelHex {Val=DocumentSpec.Color(element,"border_color")})) {Width=(int)(DocumentSpec.N(element,"border_pt",1,0,20)*12700)});shape.Append(properties);
                    var textBody=new P.TextBody(new A.BodyProperties {Wrap=A.TextWrappingValues.Square,LeftInset=0,RightInset=0,TopInset=0,BottomInset=0},new A.ListStyle());
                    foreach(var line in DocumentSpec.S(element,"text").Split('\n')) {
                        var align=DocumentSpec.S(element,"align","left");var p=new A.Paragraph(new A.ParagraphProperties {Alignment=align=="center"?A.TextAlignmentTypeValues.Center:align=="right"?A.TextAlignmentTypeValues.Right:A.TextAlignmentTypeValues.Left});
                        var style=new A.RunProperties {FontSize=(int)(DocumentSpec.N(element,"font_size",22,6,144)*100),Bold=DocumentSpec.B(element,"bold"),Italic=DocumentSpec.B(element,"italic")};style.Append(new A.SolidFill(new A.RgbColorModelHex {Val=DocumentSpec.Color(element,"color")}),new A.LatinFont {Typeface=DocumentSpec.S(element,"font_name",font)});p.Append(new A.Run(style,new A.Text(line)));textBody.Append(p);
                    }shape.Append(textBody);tree.Append(shape);
                }
                part.Slide.Save();ids.Append(new P.SlideId {Id=index++,RelationshipId=presentation.GetIdOfPart(part)});
            }
            presentation.Presentation.Append(new P.SlideSize {Cx=(int)Emu(width),Cy=(int)Emu(height)},new P.NotesSize {Cx=6858000,Cy=9144000});presentation.Presentation.Save();
            var errors=new OpenXmlValidator().Validate(doc).Take(3).Select(e=>e.Description).ToArray();if(errors.Length>0)throw new InvalidOperationException("Slide layout validation failed: "+string.Join("; ",errors));
        }return stream.ToArray();
    }
    static long Emu(double inches)=>(long)Math.Round(inches*914400);
    static P.ShapeTree Tree()=>new(new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties {Id=1,Name=""},new P.NonVisualGroupShapeDrawingProperties(),new P.ApplicationNonVisualDrawingProperties()),new P.GroupShapeProperties(new A.TransformGroup(new A.Offset {X=0,Y=0},new A.Extents {Cx=0,Cy=0},new A.ChildOffset {X=0,Y=0},new A.ChildExtents {Cx=0,Cy=0})));
    static A.Theme Theme(string font)
    {
        A.SolidFill Fill()=>new(new A.SchemeColor {Val=A.SchemeColorValues.Accent1});
        var colors=new A.ColorScheme(new A.Dark1Color(new A.RgbColorModelHex {Val="111827"}),new A.Light1Color(new A.RgbColorModelHex {Val="FFFFFF"}),new A.Dark2Color(new A.RgbColorModelHex {Val="374151"}),new A.Light2Color(new A.RgbColorModelHex {Val="F3F4F6"}),new A.Accent1Color(new A.RgbColorModelHex {Val="78ADFF"}),new A.Accent2Color(new A.RgbColorModelHex {Val="11AA88"}),new A.Accent3Color(new A.RgbColorModelHex {Val="EDAA44"}),new A.Accent4Color(new A.RgbColorModelHex {Val="AD77EE"}),new A.Accent5Color(new A.RgbColorModelHex {Val="EE7799"}),new A.Accent6Color(new A.RgbColorModelHex {Val="55BBEE"}),new A.Hyperlink(new A.RgbColorModelHex {Val="2277DD"}),new A.FollowedHyperlinkColor(new A.RgbColorModelHex {Val="9944CC"})) {Name="YoWidgets"};
        var fonts=new A.FontScheme(new A.MajorFont(new A.LatinFont {Typeface=font},new A.EastAsianFont {Typeface=""},new A.ComplexScriptFont {Typeface=""}),new A.MinorFont(new A.LatinFont {Typeface=font},new A.EastAsianFont {Typeface=""},new A.ComplexScriptFont {Typeface=""})) {Name="YoWidgets"};
        var formats=new A.FormatScheme(new A.FillStyleList(Fill(),Fill(),Fill()),new A.LineStyleList(new A.Outline(Fill()) {Width=9525},new A.Outline(Fill()) {Width=19050},new A.Outline(Fill()) {Width=28575}),new A.EffectStyleList(new A.EffectStyle(new A.EffectList()),new A.EffectStyle(new A.EffectList()),new A.EffectStyle(new A.EffectList())),new A.BackgroundFillStyleList(Fill(),Fill(),Fill())) {Name="YoWidgets"};return new A.Theme(new A.ThemeElements(colors,fonts,formats)) {Name="YoWidgets"};
    }
}
