using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Automation;
namespace YoWidgets;

// Native selectable text only: assistant output cannot execute HTML, scripts or commands.
internal static class AssistantMarkdown
{
    public static FrameworkElement Render(string text, Action<string> notice)
    {
        var stack = new StackPanel();
        // Parse fences line-by-line so incomplete streams stay readable.
        var prose = new System.Text.StringBuilder();
        var code = new System.Text.StringBuilder();
        bool fenced = false; string language = "";
        void FlushText()
        {
            if (prose.Length == 0) return;
            stack.Children.Add(Prose(prose.ToString().TrimEnd())); prose.Clear();
        }
        void FlushCode()
        {
            var source = code.ToString().TrimEnd('\n');
            var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var copy = WidgetVisuals.Pill("Copy code", () => Copy(source, notice));
            copy.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(copy, Dock.Right); heading.Children.Add(copy);
            heading.Children.Add(WidgetVisuals.Label(string.IsNullOrWhiteSpace(language) ? "Code" : language, 11));
            var paragraph = new Paragraph { Margin = new Thickness(0), FontWeight = FontWeights.Normal };
            int position = 0;
            foreach (Match token in Regex.Matches(source, "(\"(?:[^\"\\\\]|\\\\.)*\"|'(?:[^'\\\\]|\\\\.)*'|`[^`]*`|//[^\\n]*|\\b(?:const|let|var|function|return|async|await|class|public|private|static|void|if|else|new|import|from|export|def|for|while|true|false|null|None|True|False)\\b)"))
            {
                paragraph.Inlines.Add(new Run(source[position..token.Index]));
                var highlighted = new Run(token.Value); highlighted.SetResourceReference(TextElement.ForegroundProperty, token.Value.StartsWith("//") ? "WidgetInk" : "AssistantCodeInk"); paragraph.Inlines.Add(highlighted);
                position = token.Index + token.Length;
            }
            paragraph.Inlines.Add(new Run(source[position..]));
            var box = Selectable(new FlowDocument(paragraph) { PagePadding = new Thickness(0), FontFamily = new FontFamily("Cascadia Code, Consolas"), FontSize = 12.5, Foreground = Ui.Brush("Text") });
            box.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            box.Document.PageWidth = Math.Max(80, source.Split('\n').Max(l => l.Length) * 7.8 + 12);
            box.MaxHeight = 350;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            AutomationProperties.SetName(box, "Code " + language);
            var surface = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 10), Child = Ui.Stack(heading, box) }; surface.SetResourceReference(Border.BackgroundProperty,"AssistantInset"); surface.SetResourceReference(Border.BorderBrushProperty,"AssistantLine"); stack.Children.Add(surface);
            HoverReveal.Attach(surface,copy);
            code.Clear();
        }
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (fenced) { FlushCode(); fenced = false; }
                else { FlushText(); language = line.Trim().TrimStart('`').Trim(); if (language.Length > 32) language = "Code"; fenced = true; }
            }
            else if (fenced) code.Append(line).Append('\n');
            else prose.Append(line).Append('\n');
        }
        if (fenced) FlushCode(); else FlushText();
        return stack;
    }
    static RichTextBox Prose(string text)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(0), FontFamily = Ui.BodyFont, FontSize = 14, FontWeight = FontWeights.Normal, Foreground = Ui.Brush("Text"), LineHeight = 21 };
        foreach (var line in text.Split('\n'))
        {
            var p = new Paragraph { Margin = new Thickness(0, 0, 0, line.Length == 0 ? 4 : 3) };
            var heading = Regex.Match(line, "^(#{1,4})\\s+(.+)$");
            var value = heading.Success ? heading.Groups[2].Value : line;
            if (heading.Success) { p.FontSize = heading.Groups[1].Length <= 2 ? 18 : 15; p.FontWeight = FontWeights.SemiBold; p.Margin = new Thickness(0, 9, 0, 6); }
            else if (Regex.IsMatch(value, "^\\s*[-*]\\s+")) value = Regex.Replace(value, "^\\s*[-*]\\s+", "•  ");
            else if (value.StartsWith("> ")) { value = value[2..]; p.FontStyle = FontStyles.Italic; p.Margin = new Thickness(10, 4, 0, 6); }
            var offset = 0;
            foreach (Match m in Regex.Matches(value, "(`[^`]+`|\\*\\*[^*]+\\*\\*)"))
            {
                p.Inlines.Add(new Run(value[offset..m.Index]));
                if (m.Value.StartsWith('`')) { var code = new Run(m.Value[1..^1]) { FontFamily = new FontFamily("Cascadia Code, Consolas") }; code.SetResourceReference(TextElement.BackgroundProperty,"AssistantInset"); code.SetResourceReference(TextElement.ForegroundProperty,"AssistantCodeInk"); p.Inlines.Add(code); }
                else p.Inlines.Add(new Bold(new Run(m.Value[2..^2])));
                offset = m.Index + m.Length;
            }
            p.Inlines.Add(new Run(value[offset..])); doc.Blocks.Add(p);
        }
        return Selectable(doc);
    }
    static RichTextBox Selectable(FlowDocument doc)
    {
        doc.SetResourceReference(FlowDocument.ForegroundProperty,"WidgetInk");
        var box = new RichTextBox
        {
        Document = doc, IsReadOnly = true, IsDocumentEnabled = false, Background = Brushes.Transparent,
        BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(0),
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Foreground = Ui.Brush("Text"), FontWeight = FontWeights.Normal, SelectionBrush = Ui.Brush("Accent"), Cursor = Cursors.IBeam
        };
        box.SetResourceReference(Control.ForegroundProperty,"WidgetInk");
        return box;
    }
    internal static void Copy(string text, Action<string> notice)
    {
        try { Clipboard.SetText(text); notice("Copied"); }
        catch (System.Runtime.InteropServices.COMException) { notice("Clipboard is busy. Try copying again."); }
    }
}
