using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace YoWidgets.Core;

public sealed record AssistantImage(string MimeType, string Data, double Seconds);
public sealed record AssistantAttachment(string Name, string MimeType, string Data, string? Text, long Size)
{
    public string OriginalData { get; init; }="";
    public string? SourceUri { get; init; }
    public string? BrowserTabId { get; init; }
    public byte[] Bytes()=>Convert.FromBase64String(OriginalData.Length>0?OriginalData:Data.Length>0?Data:Convert.ToBase64String(Encoding.UTF8.GetBytes(Text??"")));
    public IReadOnlyList<AssistantImage> Frames { get; init; } = [];
    public long PayloadBytes => Size + Frames.Sum(f => (long)f.Data.Length * 3 / 4);
    public const int MaxFiles = 5;
    public const int MaxFileBytes = 8 * 1024 * 1024;
    public const int MaxTotalBytes = 12 * 1024 * 1024;
    public const int MaxTextChars = 48000;
    public bool IsText => Text != null;
    public static async Task<AssistantAttachment> ReadAsync(string path, CancellationToken token)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length == 0) throw new InvalidOperationException("The file is empty or unavailable.");
        if (file.Length > MaxFileBytes) throw new InvalidOperationException("Each attachment must be smaller than 8 MB.");
        var bytes = await File.ReadAllBytesAsync(path, token);
        return FromBytes(file.Name,bytes);
    }
    public static AssistantAttachment FromBytes(string name,byte[] bytes)
    {
        if(bytes.Length==0)throw new InvalidOperationException("The file is empty.");
        if (bytes.Length > MaxFileBytes) throw new InvalidOperationException("Each attachment must be smaller than 8 MB.");
        name=Path.GetFileName(name);var ext = Path.GetExtension(name).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif", ".pdf" => "application/pdf", ".mp3" => "audio/mpeg", ".wav" => "audio/wav", ".ogg" => "audio/ogg", ".mp4" => "video/mp4", ".webm" => "video/webm", _ => "application/octet-stream" };
        if (mime != "application/octet-stream") return new(name, mime, Convert.ToBase64String(bytes), null, bytes.Length);
        string text;
        if (ext is ".docx" or ".xlsx" or ".pptx") text = ExtractOffice(bytes, ext);
        else if (ext == ".zip") text = ExtractArchive(bytes);
        else
        {
            try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
            catch (DecoderFallbackException) { throw new InvalidOperationException("This binary format cannot be read directly. Use PDF, images, media, Office documents, ZIP or text files."); }
            if (text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) throw new InvalidOperationException("This file is binary. Convert it to PDF or text before attaching it.");
        }
        if (text.Length > MaxTextChars) throw new InvalidOperationException("This document has too much text. Attach a smaller section (up to 48,000 characters).");
        return new(name, "text/plain", "", text, bytes.Length) {OriginalData=Convert.ToBase64String(bytes)};
    }
    static XDocument ReadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > 2 * 1024 * 1024) throw new InvalidOperationException("This Office document is too large to extract.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    static string ExtractOffice(byte[] bytes, string extension)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        if (zip.Entries.Count > 2000) throw new InvalidOperationException("This archive contains too many entries.");
        var text = new StringBuilder();
        if (extension == ".xlsx")
        {
            var shared = zip.GetEntry("xl/sharedStrings.xml") is { } strings ? ReadXml(strings).Descendants().Where(e => e.Name.LocalName == "si").Select(e => string.Concat(e.Descendants().Where(t => t.Name.LocalName == "t").Select(t => t.Value))).ToArray() : [];
            foreach (var sheet in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml")).Take(50))
            {
                text.AppendLine(sheet.Name);
                foreach (var row in ReadXml(sheet).Descendants().Where(e => e.Name.LocalName == "row"))
                    text.AppendLine(string.Join("\t", row.Elements().Where(e => e.Name.LocalName == "c").Select(cell => {
                        var value = cell.Descendants().FirstOrDefault(e => e.Name.LocalName is "v" or "t")?.Value ?? "";
                        return cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var index) && index >= 0 && index < shared.Length ? shared[index] : value;
                    })));
                Limit(text);
            }
        }
        else foreach (var entry in zip.Entries.Where(e => extension == ".docx" ? e.FullName == "word/document.xml" : e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml")).Take(200))
        {
            foreach (var paragraph in ReadXml(entry).Descendants().Where(e => e.Name.LocalName == "p"))
                text.AppendLine(string.Concat(paragraph.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value)));
            Limit(text);
        }
        return text.ToString();
    }
    static string ExtractArchive(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        if (zip.Entries.Count > 200) throw new InvalidOperationException("Attach a ZIP with at most 200 entries.");
        var text = new StringBuilder("ZIP archive. Binary entries are listed by name only.\n");
        foreach (var entry in zip.Entries)
        {
            text.AppendLine("File: " + entry.FullName);
            if (entry.Length == 0 || entry.Length > MaxTextChars) continue;
            try { using var reader = new StreamReader(entry.Open(), new UTF8Encoding(false, true)); var value = reader.ReadToEnd(); if (!value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) text.AppendLine(value); }
            catch (DecoderFallbackException) { }
            Limit(text);
        }
        return text.ToString();
    }
    static void Limit(StringBuilder text) { if (text.Length > MaxTextChars) throw new InvalidOperationException("This document contains too much text. Attach a smaller section."); }
}
