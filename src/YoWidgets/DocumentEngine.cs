using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using ClosedXML.Excel;
using M=MigraDoc.DocumentObjectModel;
using W=DocumentFormat.OpenXml.Wordprocessing;
using A=DocumentFormat.OpenXml.Drawing;
namespace YoWidgets;

internal static class DocumentEngine
{
    internal static string Schema(string format)=>format switch {
        "docx" or "pdf"=>"Use spec_json with this structure: {title,font_name,font_size,page:{size:'A4'|'Letter',orientation:'portrait'|'landscape',width_mm,height_mm,margins_mm:{top,right,bottom,left}},header,footer,blocks:[{type:'paragraph',text,font_name,font_size,bold,italic,underline,color:'RRGGBB',align:'left'|'center'|'right'|'justify',space_before_pt,space_after_pt,line_spacing,left_indent_mm,first_line_indent_mm,heading:1,page_break_before:false},{type:'table',rows:[['Heading','Value'],['Item','10']],column_widths_mm:[90,70],font_size,header_rows:1,header_fill:'E8EEF8',cell_padding_mm:2},{type:'image',path:'image.png',width_mm:80,height_mm:50},{type:'page_break'}]}. All fields except blocks are optional. Use the requested actual text, typography, units and margins. Page marker {page} is supported in footer. Images must be workspace PNG/JPEG. No raw HTML, macros or external resource URLs. After creation call document_inspect; preview PDFs with document_preview.",
        "xlsx"=>"spec_json: {title,font_name:'Arial',font_size:11,sheets:[{name:'Sheet1',rows:[['Item','Amount'],['A',12],['Total',{formula:'SUM(B2:B2)',number_format:'0.00'}]],column_widths:[30,16],row_heights:[24,20],header_rows:1,header_fill:'E8EEF8',freeze_rows:1,freeze_columns:0,merges:['A5:B5'],formats:[{range:'A1:B1',bold:true,font_name,font_size,color:'112233',fill:'E8EEF8',align:'left'|'center'|'right',wrap:true,border:true,number_format:'0.00'}]}]}. Rows contain strings, numbers, booleans or {value,formula,number_format}. Explicit formula fields are calculated before save when supported; unsafe external/DDE functions are rejected. Values starting with '=' remain literal text. At most 20 sheets / 50,000 cells. Column widths are Excel character units, row heights are points. No macros, external workbooks or network formulas.",
        "pptx"=>"spec_json: {title,width_in:13.333,height_in:7.5,font_name:'Arial',slides:[{title:'Title',body:'Text with newline-separated paragraphs',background:'FFFFFF',elements:[{type:'text'|'rectangle'|'ellipse'|'image',x_in:0.8,y_in:0.5,width_in:6,height_in:1,text:'Editable text',font_name,font_size:28,bold:true,italic:false,color:'112233',align:'left'|'center'|'right',fill:'E8EEF8',border_color:'112233',border_pt:1,path:'workspace-image.png'}]}]}. All coordinates/dimensions are inches and fonts are points. If elements is omitted, title/body generate editable title and body text boxes. Images must be workspace PNG/JPEG. At most 50 slides / 100 elements per slide. Elements must fit inside the slide. The result is a native editable PowerPoint deck with theme/master/layout; no embedded scripts or macros.",
        _=>throw new InvalidOperationException("Choose docx, pdf, xlsx or pptx.")
    };
    internal static byte[] Create(string format,string json,Func<string,byte[]> image)
    {
        if(json.Length>1_000_000)throw new InvalidOperationException("The document specification is too large.");using var document=JsonDocument.Parse(json,new JsonDocumentOptions {MaxDepth=32});var spec=document.RootElement;
        return format switch {"docx"=>WordDocumentWriter.Create(spec,image),"pdf"=>Pdf(spec,image),"xlsx"=>Excel(spec),"pptx"=>PresentationWriter.Create(spec,image),_=>throw new InvalidOperationException("Unsupported document format.")};
    }
    internal static object Inspect(string name,byte[] bytes,int offset,int limit)
    {
        if(bytes.Length>AgentWorkspace.MaxDocumentBytes)throw new InvalidOperationException("Documents must be smaller than 8 MB.");var extension=Path.GetExtension(name).ToLowerInvariant();var text=new StringBuilder();object metadata;
        void Add(string line) {if(text.Length<2_000_000)text.Append(line.AsSpan(0,Math.Min(line.Length,2_000_000-text.Length))).AppendLine();}
        if(extension==".pdf") {
            using var pdf=UglyToad.PdfPig.PdfDocument.Open(bytes);foreach(var page in pdf.GetPages().Take(200)){Add("Page "+page.Number);Add(page.Text);}metadata=new{pages=pdf.NumberOfPages,read_pages=Math.Min(200,pdf.NumberOfPages)};
        }else {
            CheckZip(bytes);using var stream=new MemoryStream(bytes);
            if(extension==".docx") {
                using var doc=WordprocessingDocument.Open(stream,false);var body=doc.MainDocumentPart?.Document??throw new InvalidOperationException("The Word document has no body.");foreach(var p in body.Descendants<W.Paragraph>())Add(p.InnerText);
                var section=body.Descendants<W.SectionProperties>().LastOrDefault();var margins=section?.GetFirstChild<W.PageMargin>();var page=section?.GetFirstChild<W.PageSize>();
                metadata=new{font_names=body.Descendants<W.RunFonts>().Select(f=>f.Ascii?.Value).Where(v=>v!=null).Distinct().Take(30).ToArray(),font_sizes_pt=body.Descendants<W.FontSize>().Select(f=>double.TryParse(f.Val?.Value,out var n)?n/2:0).Distinct().Take(30).ToArray(),page_width_mm=(page?.Width?.Value??0)*25.4/1440,page_height_mm=(page?.Height?.Value??0)*25.4/1440,margins_mm=new{top=(margins?.Top?.Value??0)*25.4/1440,right=(margins?.Right?.Value??0)*25.4/1440,bottom=(margins?.Bottom?.Value??0)*25.4/1440,left=(margins?.Left?.Value??0)*25.4/1440},tables=body.Descendants<W.Table>().Count()};
            }else if(extension==".xlsx") {
                using var workbook=new XLWorkbook(stream);int cells=0;var sheets=new List<object>();foreach(var sheet in workbook.Worksheets.Take(20)) {Add("Sheet: "+sheet.Name);sheets.Add(new{name=sheet.Name,rows=sheet.LastRowUsed()?.RowNumber()??0,columns=sheet.LastColumnUsed()?.ColumnNumber()??0});foreach(var row in sheet.RowsUsed().Take(10000)){var values=new List<string>();foreach(var cell in row.CellsUsed().Take(100)){if(++cells>50000)break;values.Add(cell.Address.ToString()+"="+cell.CachedValue+(cell.HasFormula?" [formula: "+cell.FormulaA1+"]":""));}Add(string.Join(" | ",values));if(cells>50000)break;}}metadata=new{sheets,scanned_cells=cells};
            }else if(extension==".pptx") {
                using var deck=PresentationDocument.Open(stream,false);var main=deck.PresentationPart??throw new InvalidOperationException("The deck has no presentation.");var presentation=main.Presentation??throw new InvalidOperationException("The presentation is empty.");var slides=presentation.SlideIdList?.Elements<DocumentFormat.OpenXml.Presentation.SlideId>().ToArray()??[];
                foreach(var slide in slides.Take(50)) {var part=(SlidePart)main.GetPartById(slide.RelationshipId!);Add("Slide "+(Array.IndexOf(slides,slide)+1));foreach(var t in part.Slide!.Descendants<A.Text>())Add(t.Text);}
                var size=presentation.SlideSize;metadata=new{slides=slides.Length,width_in=(size?.Cx?.Value??0)/914400d,height_in=(size?.Cy?.Value??0)/914400d};
            }else throw new InvalidOperationException("Use a DOCX, PDF, XLSX or PPTX document.");
        }
        offset=Math.Clamp(offset,0,text.Length);limit=Math.Clamp(limit,100,48000);var source=text.ToString();return new{format=extension.TrimStart('.'),metadata,total_chars=source.Length,offset,next_offset=Math.Min(source.Length,offset+limit),content=source.Substring(offset,Math.Min(limit,source.Length-offset)),truncated=source.Length>=2_000_000};
    }
    static void CheckZip(byte[] bytes)
    {using var zip=new ZipArchive(new MemoryStream(bytes));if(zip.Entries.Count>3000||zip.Entries.Sum(e=>e.Length)>64*1024*1024||zip.Entries.Any(e=>e.Length>16*1024*1024))throw new InvalidOperationException("This document expands beyond the safe analysis limit.");}
    static byte[] Pdf(JsonElement spec,Func<string,byte[]> image)
    {
        DocumentSpec.Keys(spec,"title","font_name","font_size","page","header","footer","blocks");var page=DocumentSpec.Page(spec);var document=new M.Document();document.Info.Title=DocumentSpec.S(spec,"title");var font=DocumentSpec.S(spec,"font_name","Arial");var size=DocumentSpec.N(spec,"font_size",11,6,144);
        document.Styles["Normal"]!.Font.Name=font;document.Styles["Normal"]!.Font.Size=size;var section=document.AddSection();section.PageSetup.PageWidth=M.Unit.FromMillimeter(page.Width);section.PageSetup.PageHeight=M.Unit.FromMillimeter(page.Height);section.PageSetup.TopMargin=M.Unit.FromMillimeter(page.Top);section.PageSetup.RightMargin=M.Unit.FromMillimeter(page.Right);section.PageSetup.BottomMargin=M.Unit.FromMillimeter(page.Bottom);section.PageSetup.LeftMargin=M.Unit.FromMillimeter(page.Left);
        if(DocumentSpec.S(spec,"header") is {Length:>0} header)section.Headers.Primary.AddParagraph(header);
        if(DocumentSpec.S(spec,"footer") is {Length:>0} footer){var p=section.Footers.Primary.AddParagraph();var split=footer.Split("{page}");p.AddText(split[0]);for(var i=1;i<split.Length;i++){p.AddPageField();p.AddText(split[i]);}}
        foreach(var block in DocumentSpec.A(spec,"blocks")) {
            var type=DocumentSpec.S(block,"type","paragraph");if(type=="paragraph") {
                var p=section.AddParagraph(DocumentSpec.S(block,"text"));var f=p.Format;f.Font.Name=DocumentSpec.S(block,"font_name",font);f.Font.Size=DocumentSpec.N(block,"font_size",size,6,144);f.Font.Bold=DocumentSpec.B(block,"bold");f.Font.Italic=DocumentSpec.B(block,"italic");f.Font.Color=M.Color.Parse("#"+DocumentSpec.Color(block,"color"));if(DocumentSpec.B(block,"underline"))f.Font.Underline=M.Underline.Single;
                f.Alignment=DocumentSpec.S(block,"align","left") switch {"center"=>M.ParagraphAlignment.Center,"right"=>M.ParagraphAlignment.Right,"justify"=>M.ParagraphAlignment.Justify,_=>M.ParagraphAlignment.Left};f.SpaceBefore=M.Unit.FromPoint(DocumentSpec.N(block,"space_before_pt",0,0,200));f.SpaceAfter=M.Unit.FromPoint(DocumentSpec.N(block,"space_after_pt",6,0,200));f.LineSpacingRule=M.LineSpacingRule.Multiple;f.LineSpacing=DocumentSpec.N(block,"line_spacing",1.15,.7,4);f.LeftIndent=M.Unit.FromMillimeter(DocumentSpec.N(block,"left_indent_mm",0,0,100));f.FirstLineIndent=M.Unit.FromMillimeter(DocumentSpec.N(block,"first_line_indent_mm",0,0,100));f.PageBreakBefore=DocumentSpec.B(block,"page_break_before");
            }else if(type=="page_break")section.AddPageBreak();
            else if(type=="image") {var path=DocumentSpec.S(block,"path");var item=section.AddImage("base64:"+Convert.ToBase64String(image(path)));item.Width=M.Unit.FromMillimeter(DocumentSpec.N(block,"width_mm",80,1,page.Width-page.Left-page.Right));item.Height=M.Unit.FromMillimeter(DocumentSpec.N(block,"height_mm",50,1,page.Height-page.Top-page.Bottom));}
            else if(type=="table") {
                var rows=DocumentSpec.A(block,"rows",500);var columns=rows.Length==0?0:rows.Max(r=>r.GetArrayLength());if(columns is <1 or >40)throw new InvalidOperationException("Use between 1 and 40 table columns.");var widths=DocumentSpec.A(block,"column_widths_mm",40).Select(v=>v.GetDouble()).ToArray();if(widths.Length>0&&(widths.Length!=columns||widths.Any(w=>w<3)||widths.Sum()>page.Width-page.Left-page.Right))throw new InvalidOperationException("Table widths must fit the page.");
                var table=section.AddTable();table.Borders.Width=.5;table.Format.Font.Size=DocumentSpec.N(block,"font_size",size,6,144);table.TopPadding=table.BottomPadding=M.Unit.FromMillimeter(DocumentSpec.N(block,"cell_padding_mm",2,0,15));for(var i=0;i<columns;i++)table.AddColumn(M.Unit.FromMillimeter(widths.Length>0?widths[i]:(page.Width-page.Left-page.Right)/columns));int rowIndex=0;foreach(var values in rows){var row=table.AddRow();if(rowIndex++<DocumentSpec.N(block,"header_rows",1,0,10)){row.HeadingFormat=true;row.Format.Font.Bold=true;row.Shading.Color=M.Color.Parse("#"+DocumentSpec.Color(block,"header_fill","E8EEF8"));}int col=0;foreach(var value in values.EnumerateArray())row.Cells[col++].AddParagraph(DocumentSpec.Cell(value));}
            }else throw new InvalidOperationException("Unsupported PDF block: "+type);
        }
        var renderer=new MigraDoc.Rendering.PdfDocumentRenderer {Document=document};renderer.RenderDocument();using var stream=new MemoryStream();renderer.PdfDocument.Save(stream,false);return stream.ToArray();
    }
    static byte[] Excel(JsonElement spec)
    {
        DocumentSpec.Keys(spec,"title","font_name","font_size","sheets");using var workbook=new XLWorkbook();workbook.Properties.Title=DocumentSpec.S(spec,"title");workbook.Style.Font.FontName=DocumentSpec.S(spec,"font_name","Arial");workbook.Style.Font.FontSize=DocumentSpec.N(spec,"font_size",11,6,144);int cells=0;
        var sheets=DocumentSpec.A(spec,"sheets",20);if(sheets.Length==0)throw new InvalidOperationException("Add at least one worksheet.");
        foreach(var item in sheets) {
            var sheet=workbook.Worksheets.Add(DocumentSpec.S(item,"name","Sheet"+(workbook.Worksheets.Count+1)));int r=0;foreach(var row in DocumentSpec.A(item,"rows",10000)) {int c=0;r++;foreach(var value in row.EnumerateArray()) {
                if(++cells>50000||++c>200)throw new InvalidOperationException("Use at most 50,000 cells and 200 columns per worksheet.");var cell=sheet.Cell(r,c);var v=value;
                if(value.ValueKind==JsonValueKind.Object) {
                    var formula=DocumentSpec.S(value,"formula");if(formula.Length>0){if(Regex.IsMatch(formula,@"\[|\]|\\|\||https?:|file:|WEBSERVICE|HYPERLINK|RTD\s*\(|CALL\s*\(|REGISTER",RegexOptions.IgnoreCase))throw new InvalidOperationException("External workbook, network and DDE formulas are not supported.");cell.FormulaA1=formula.TrimStart('=');}
                    v=value.TryGetProperty("value",out var actual)?actual:default;var number=DocumentSpec.S(value,"number_format");if(number.Length>0)cell.Style.NumberFormat.Format=number;
                }
                if(v.ValueKind==JsonValueKind.Number)cell.Value=v.GetDouble();else if(v.ValueKind is JsonValueKind.True or JsonValueKind.False)cell.Value=v.GetBoolean();else if(v.ValueKind==JsonValueKind.String)cell.Value=v.GetString()??"";
            }}
            var widths=DocumentSpec.A(item,"column_widths",200);for(var i=0;i<widths.Length;i++){var w=widths[i].GetDouble();if(w<1||w>100)throw new InvalidOperationException("Column widths must be between 1 and 100 characters.");sheet.Column(i+1).Width=w;}
            var heights=DocumentSpec.A(item,"row_heights",10000);for(var i=0;i<heights.Length;i++)sheet.Row(i+1).Height=Math.Clamp(heights[i].GetDouble(),8,400);
            var headers=(int)DocumentSpec.N(item,"header_rows",1,0,20);if(headers>0&&sheet.LastColumnUsed()!=null){var range=sheet.Range(1,1,headers,sheet.LastColumnUsed()!.ColumnNumber());range.Style.Font.Bold=true;range.Style.Fill.BackgroundColor=XLColor.FromHtml("#"+DocumentSpec.Color(item,"header_fill","E8EEF8"));}
            sheet.SheetView.FreezeRows((int)DocumentSpec.N(item,"freeze_rows",headers,0,100));sheet.SheetView.FreezeColumns((int)DocumentSpec.N(item,"freeze_columns",0,0,50));foreach(var merge in DocumentSpec.A(item,"merges",100))sheet.Range(merge.GetString()!).Merge();
            foreach(var format in DocumentSpec.A(item,"formats",300)) {var range=sheet.Range(DocumentSpec.S(format,"range"));if((long)range.RowCount()*range.ColumnCount()>50000)throw new InvalidOperationException("Style a smaller cell range.");if(format.TryGetProperty("bold",out _))range.Style.Font.Bold=DocumentSpec.B(format,"bold");if(format.TryGetProperty("italic",out _))range.Style.Font.Italic=DocumentSpec.B(format,"italic");if(format.TryGetProperty("font_name",out _))range.Style.Font.FontName=DocumentSpec.S(format,"font_name");if(format.TryGetProperty("font_size",out _))range.Style.Font.FontSize=DocumentSpec.N(format,"font_size",11,6,144);if(format.TryGetProperty("color",out _))range.Style.Font.FontColor=XLColor.FromHtml("#"+DocumentSpec.Color(format,"color"));if(format.TryGetProperty("fill",out _))range.Style.Fill.BackgroundColor=XLColor.FromHtml("#"+DocumentSpec.Color(format,"fill"));if(format.TryGetProperty("number_format",out _))range.Style.NumberFormat.Format=DocumentSpec.S(format,"number_format");if(format.TryGetProperty("wrap",out _))range.Style.Alignment.WrapText=DocumentSpec.B(format,"wrap");range.Style.Alignment.Horizontal=DocumentSpec.S(format,"align","left") switch {"center"=>XLAlignmentHorizontalValues.Center,"right"=>XLAlignmentHorizontalValues.Right,_=>XLAlignmentHorizontalValues.Left};if(DocumentSpec.B(format,"border")){range.Style.Border.OutsideBorder=XLBorderStyleValues.Thin;range.Style.Border.InsideBorder=XLBorderStyleValues.Thin;}}
        }
        using var stream=new MemoryStream();workbook.SaveAs(stream,new SaveOptions {ValidatePackage=true,EvaluateFormulasBeforeSaving=true});return stream.ToArray();
    }
}
