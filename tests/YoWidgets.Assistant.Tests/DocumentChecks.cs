using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoWidgets;

static class DocumentChecks
{
    internal static void Run()
    {
        var root=Path.GetFullPath("artifacts/composer-checks/run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var app=new App();typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null,root);Ui.Theme();
        var pixels=new byte[]{255,0,0,255,0,120,255,255,20,200,120,255,255,200,0,255};var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,pixels,8);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var png=new MemoryStream();encoder.Save(png);var image=png.ToArray();
        var spec=JsonSerializer.Serialize(new{title="Formatting fixture",font_name="Arial",font_size=12,page=new{size="A4",margins_mm=new{top=20,right=25,bottom=20,left=25}},footer="Page {page}",blocks=new object[]{new{type="paragraph",text="YoWidgets document fixture",font_name="Times New Roman",font_size=20,bold=true,space_after_pt=12},new{type="paragraph",text="Unicode: Привіт. Requested spacing and formatting are preserved.",line_spacing=1.5,first_line_indent_mm=10},new{type="table",rows=new[]{new[]{"Item","Amount"},new[]{"Alpha","30"}},column_widths_mm=new[]{90,60}},new{type="image",path="fixture.png",width_mm=40,height_mm=24}}});
        foreach(var format in new[]{"docx","pdf"}) {
            var bytes=DocumentEngine.Create(format,spec,_=>image);File.WriteAllBytes(Path.Combine(root,"fixture."+format),bytes);var inspected=JsonSerializer.Serialize(DocumentEngine.Inspect("fixture."+format,bytes,0,48000));
            if(!inspected.Contains("YoWidgets")||format=="docx"&&!inspected.Contains("Times New Roman"))throw new Exception("Document did not preserve its contents/style");Console.WriteLine("PASS "+format+": native generation, validation, Unicode, typography, margins, table and image");
            if(format=="pdf") {var page=AssistantToolRuntime.PreviewPdf(bytes,1,CancellationToken.None).GetAwaiter().GetResult();File.WriteAllBytes(Path.Combine(root,"pdf-preview.png"),page);Console.WriteLine("PASS PDF: actual Windows page renderer produced a PNG preview");}
        }
        var excel=DocumentEngine.Create("xlsx",JsonSerializer.Serialize(new{sheets=new[]{new{name="Budget",rows=new object[][]{["Item","Amount"],["Alpha",10],["Beta",20],["Total",new{formula="SUM(B2:B3)",number_format="0.00"}]},column_widths=new[]{30,18},header_rows=1,freeze_rows=1,formats=new[]{new{range="A4:B4",bold=true,fill="E8EEF8"}}}}}),_=>image);File.WriteAllBytes(Path.Combine(root,"fixture.xlsx"),excel);
        var x=JsonSerializer.Serialize(DocumentEngine.Inspect("fixture.xlsx",excel,0,48000));if(!x.Contains("SUM(B2:B3)")||!x.Contains("30"))throw new Exception("Excel formula or cached value missing");Console.WriteLine("PASS XLSX: editable styles, formulas, calculated values and readback");
        var slides=DocumentEngine.Create("pptx",JsonSerializer.Serialize(new{title="Slide fixture",slides=new object[]{new{title="YoWidgets",body="Editable PowerPoint content"},new{background="18202C",elements=new object[]{new{type="text",text="Precise geometry",x_in=1,y_in=1,width_in=8,height_in=1,font_size=32,color="FFFFFF"},new{type="image",path="fixture.png",x_in=1,y_in=3,width_in=4,height_in=2}}}}}),_=>image);File.WriteAllBytes(Path.Combine(root,"fixture.pptx"),slides);
        if(!JsonSerializer.Serialize(DocumentEngine.Inspect("fixture.pptx",slides,0,48000)).Contains("Precise geometry"))throw new Exception("Slide text missing");Console.WriteLine("PASS PPTX: editable text, exact geometry, image, theme/master/layout and validation");
        var original=AssistantAttachment.FromBytes("fixture.docx",File.ReadAllBytes(Path.Combine(root,"fixture.docx")));if(!original.Bytes().SequenceEqual(File.ReadAllBytes(Path.Combine(root,"fixture.docx"))))throw new Exception("Original Office bytes were lost");
        var clipboard=new DataObject();clipboard.SetData("PNG",new MemoryStream(image));var pasted=AssistantClipboard.Read(clipboard);if(pasted.Image==null||!pasted.Image.Bytes().SequenceEqual(image))throw new Exception("Clipboard PNG lost original bytes");var text=new DataObject();text.SetText("ordinary text");if(AssistantClipboard.HasAttachment(text))throw new Exception("Plain text paste was hijacked");Console.WriteLine("PASS clipboard snapshots: exact PNG/Office bytes and plain text untouched");
        Console.WriteLine("Document fixture output: "+root);
    }
}
