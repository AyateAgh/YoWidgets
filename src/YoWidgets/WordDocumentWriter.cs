using System.Globalization;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W=DocumentFormat.OpenXml.Wordprocessing;
using A=DocumentFormat.OpenXml.Drawing;
using DW=DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC=DocumentFormat.OpenXml.Drawing.Pictures;
namespace YoWidgets;

internal static class WordDocumentWriter
{
    internal static byte[] Create(JsonElement spec,Func<string,byte[]> image)
    {
        DocumentSpec.Keys(spec,"title","font_name","font_size","page","header","footer","blocks");
        using var stream=new MemoryStream();using(var doc=WordprocessingDocument.Create(stream,WordprocessingDocumentType.Document,true)) {
            doc.PackageProperties.Title=DocumentSpec.S(spec,"title");var main=doc.AddMainDocumentPart();main.Document=new W.Document(new W.Body());var body=main.Document.Body!;
            var page=DocumentSpec.Page(spec);var font=DocumentSpec.S(spec,"font_name","Arial");var size=DocumentSpec.N(spec,"font_size",11,6,144);
            foreach(var block in DocumentSpec.A(spec,"blocks")) {
                var type=DocumentSpec.S(block,"type","paragraph");
                if(type=="paragraph")body.Append(Paragraph(block,font,size));
                else if(type=="page_break")body.Append(new W.Paragraph(new W.Run(new W.Break {Type=W.BreakValues.Page})));
                else if(type=="table") {
                    var rows=DocumentSpec.A(block,"rows",500);var widths=DocumentSpec.A(block,"column_widths_mm",40).Select(v=>v.GetDouble()).ToArray();var columns=rows.Length==0?0:rows.Max(r=>r.GetArrayLength());if(columns is <1 or >40)throw new InvalidOperationException("Use between 1 and 40 table columns.");
                    if(widths.Length>0&&(widths.Length!=columns||widths.Any(v=>v<3)||widths.Sum()>page.Width-page.Left-page.Right))throw new InvalidOperationException("Table column widths must fit the page.");
                    var table=new W.Table(new W.TableProperties(new W.TableWidth {Type=W.TableWidthUnitValues.Dxa,Width=Twip(page.Width-page.Left-page.Right).ToString(CultureInfo.InvariantCulture)},new W.TableBorders(new W.TopBorder {Val=W.BorderValues.Single,Size=4},new W.LeftBorder {Val=W.BorderValues.Single,Size=4},new W.BottomBorder {Val=W.BorderValues.Single,Size=4},new W.RightBorder {Val=W.BorderValues.Single,Size=4},new W.InsideHorizontalBorder {Val=W.BorderValues.Single,Size=4},new W.InsideVerticalBorder {Val=W.BorderValues.Single,Size=4})));
                    table.Append(new W.TableGrid(Enumerable.Range(0,columns).Select(i=>(OpenXmlElement)new W.GridColumn {Width=Twip(widths.Length>0?widths[i]:(page.Width-page.Left-page.Right)/columns).ToString(CultureInfo.InvariantCulture)})));
                    var header=(int)DocumentSpec.N(block,"header_rows",1,0,10);var padding=Twip(DocumentSpec.N(block,"cell_padding_mm",2,0,15));int rowIndex=0;
                    foreach(var row in rows) {var tr=new W.TableRow();if(rowIndex<header)tr.Append(new W.TableRowProperties(new W.TableHeader()));foreach(var value in row.EnumerateArray()) {
                        var cell=new W.TableCell();var props=new W.TableCellProperties(new W.TableCellMargin(new W.TopMargin {Width=padding.ToString(),Type=W.TableWidthUnitValues.Dxa},new W.LeftMargin {Width=padding.ToString(),Type=W.TableWidthUnitValues.Dxa},new W.BottomMargin {Width=padding.ToString(),Type=W.TableWidthUnitValues.Dxa},new W.RightMargin {Width=padding.ToString(),Type=W.TableWidthUnitValues.Dxa}));if(rowIndex<header)props.AddChild(new W.Shading {Fill=DocumentSpec.Color(block,"header_fill","E8EEF8"),Val=W.ShadingPatternValues.Clear},true);cell.Append(props);
                        var b=JsonSerializer.SerializeToElement(new{text=DocumentSpec.Cell(value),bold=rowIndex<header,font_size=DocumentSpec.N(block,"font_size",size,6,144)});cell.Append(Paragraph(b,font,size));tr.Append(cell);
                    }table.Append(tr);rowIndex++;}body.Append(table);
                }else if(type=="image") {
                    var path=DocumentSpec.S(block,"path");var bytes=image(path);var extension=Path.GetExtension(path).ToLowerInvariant();var part=main.AddImagePart(extension is ".jpg" or ".jpeg"?ImagePartType.Jpeg:extension==".png"?ImagePartType.Png:throw new InvalidOperationException("Use PNG/JPEG for document images."));using(var input=new MemoryStream(bytes))part.FeedData(input);
                    long cx=(long)(DocumentSpec.N(block,"width_mm",80,1,page.Width-page.Left-page.Right)*36000),cy=(long)(DocumentSpec.N(block,"height_mm",50,1,page.Height-page.Top-page.Bottom)*36000);
                    var picture=new PIC.Picture(new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties {Id=(uint)(main.ImageParts.Count()+1),Name=Path.GetFileName(path)},new PIC.NonVisualPictureDrawingProperties()),new PIC.BlipFill(new A.Blip {Embed=main.GetIdOfPart(part)},new A.Stretch(new A.FillRectangle())),new PIC.ShapeProperties(new A.Transform2D(new A.Offset {X=0,Y=0},new A.Extents {Cx=cx,Cy=cy}),new A.PresetGeometry(new A.AdjustValueList()) {Preset=A.ShapeTypeValues.Rectangle}));
                    body.Append(new W.Paragraph(new W.Run(new W.Drawing(new DW.Inline(new DW.Extent {Cx=cx,Cy=cy},new DW.DocProperties {Id=(uint)(main.ImageParts.Count()+1),Name=Path.GetFileName(path)},new A.Graphic(new A.GraphicData(picture) {Uri="http://schemas.openxmlformats.org/drawingml/2006/picture"}))))));
                }else throw new InvalidOperationException("Unsupported document block: "+type);
            }
            var section=new W.SectionProperties();
            if(DocumentSpec.S(spec,"header") is {Length:>0} head) {var part=main.AddNewPart<HeaderPart>();part.Header=new W.Header(Paragraph(JsonSerializer.SerializeToElement(new{text=head}),font,size-1));section.Append(new W.HeaderReference {Id=main.GetIdOfPart(part),Type=W.HeaderFooterValues.Default});}
            if(DocumentSpec.S(spec,"footer") is {Length:>0} foot) {var part=main.AddNewPart<FooterPart>();var p=Paragraph(JsonSerializer.SerializeToElement(new{text=""}),font,size-1);var prototype=p.GetFirstChild<W.Run>()!;var segments=foot.Split("{page}");for(var i=0;i<segments.Length;i++){var run=(W.Run)prototype.CloneNode(true);run.GetFirstChild<W.Text>()!.Text=segments[i];p.Append(run);if(i<segments.Length-1)p.Append(new W.SimpleField(new W.Run((W.RunProperties)prototype.RunProperties!.CloneNode(true),new W.Text("1"))) {Instruction="PAGE"});}prototype.Remove();part.Footer=new W.Footer(p);section.Append(new W.FooterReference {Id=main.GetIdOfPart(part),Type=W.HeaderFooterValues.Default});}
            section.Append(new W.PageSize {Width=(uint)Twip(page.Width),Height=(uint)Twip(page.Height)},new W.PageMargin {Top=Twip(page.Top),Right=(uint)Twip(page.Right),Bottom=Twip(page.Bottom),Left=(uint)Twip(page.Left),Header=500,Footer=500,Gutter=0});body.Append(section);main.Document.Save();
            var errors=new OpenXmlValidator().Validate(doc).Take(3).Select(e=>e.Path?.XPath+": "+e.Description).ToArray();if(errors.Length>0)throw new InvalidOperationException("Word layout validation failed: "+string.Join("; ",errors));
        }return stream.ToArray();
    }
    static W.Paragraph Paragraph(JsonElement b,string font,double size)
    {
        var properties=new W.ParagraphProperties();var align=DocumentSpec.S(b,"align","left");properties.Append(new W.SpacingBetweenLines {Before=((int)(DocumentSpec.N(b,"space_before_pt",0,0,200)*20)).ToString(),After=((int)(DocumentSpec.N(b,"space_after_pt",6,0,200)*20)).ToString(),Line=((int)(DocumentSpec.N(b,"line_spacing",1.15,.7,4)*240)).ToString(),LineRule=W.LineSpacingRuleValues.Auto});
        properties.Append(new W.Indentation {Left=Twip(DocumentSpec.N(b,"left_indent_mm",0,0,100)).ToString(),FirstLine=Twip(DocumentSpec.N(b,"first_line_indent_mm",0,0,100)).ToString()},new W.Justification {Val=align=="center"?W.JustificationValues.Center:align=="right"?W.JustificationValues.Right:align=="justify"?W.JustificationValues.Both:W.JustificationValues.Left});
        if(DocumentSpec.B(b,"page_break_before"))properties.PrependChild(new W.PageBreakBefore());if(b.TryGetProperty("heading",out var heading))properties.Append(new W.OutlineLevel {Val=Math.Clamp(heading.GetInt32()-1,0,8)});
        var run=new W.RunProperties(new W.RunFonts {Ascii=DocumentSpec.S(b,"font_name",font),HighAnsi=DocumentSpec.S(b,"font_name",font),ComplexScript=DocumentSpec.S(b,"font_name",font)});
        if(DocumentSpec.B(b,"bold"))run.Append(new W.Bold());if(DocumentSpec.B(b,"italic"))run.Append(new W.Italic());run.Append(new W.Color {Val=DocumentSpec.Color(b,"color")},new W.FontSize {Val=((int)(DocumentSpec.N(b,"font_size",size,6,144)*2)).ToString()});if(DocumentSpec.B(b,"underline"))run.Append(new W.Underline {Val=W.UnderlineValues.Single});
        return new W.Paragraph(properties,new W.Run(run,new W.Text(DocumentSpec.S(b,"text")) {Space=SpaceProcessingModeValues.Preserve}));
    }
    static int Twip(double mm)=>(int)Math.Round(mm*1440/25.4);
}
