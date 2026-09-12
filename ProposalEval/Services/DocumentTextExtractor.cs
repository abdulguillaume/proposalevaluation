using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UglyToad.PdfPig;

namespace ProposalEval.Services;

public static class DocumentTextExtractor
{
    public static string Extract(StoredFile file)
    {
        if (file.Content is null || file.Content.Length == 0)
            return "[Empty file.]";

        var name = BlobNaming.OriginalName(file.FileName);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        var type = (file.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();

        try
        {
            return Detect(ext, type, file.Content) switch
            {
                Kind.Pdf => FromPdf(file.Content),
                Kind.Docx => FromDocx(file.Content),
                Kind.Doc => FromDoc(file.Content),
                Kind.Xlsx => FromExcel(file.Content, xlsx: true),
                Kind.Xls => FromExcel(file.Content, xlsx: false),
                Kind.Html => FromHtml(file.Content),
                Kind.Text => FromText(file.Content),
                _ => Unreadable(name)
            };
        }
        catch (Exception ex)
        {
            return $"[Could not read '{name}': {ex.Message}]";
        }
    }

    private enum Kind { Unknown, Pdf, Docx, Doc, Xlsx, Xls, Html, Text }

    private static Kind Detect(string ext, string type, byte[] content)
    {
        if (ext is ".pdf" || type == "application/pdf" || IsPdf(content))
            return Kind.Pdf;
        if (ext is ".docx" || type == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" || (ext is ".doc" && IsZip(content)))
            return Kind.Docx;
        if (ext is ".doc" || type == "application/msword")
            return Kind.Doc;
        if (ext is ".xlsx" || type == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" || (ext is ".xls" && IsZip(content)))
            return Kind.Xlsx;
        if (ext is ".xls" || type is "application/vnd.ms-excel" or "application/excel")
            return Kind.Xls;
        if (ext is ".html" or ".htm" || type is "text/html" or "application/xhtml+xml")
            return Kind.Html;
        if (ext is ".txt" or ".md" or ".csv" || type.StartsWith("text/") || type.Contains("json", StringComparison.Ordinal))
            return Kind.Text;
        if (IsPdf(content))
            return Kind.Pdf;
        if (IsZip(content))
            return DetectZipKind(content);
        if (IsOle(content))
            return Kind.Doc;
        return Kind.Unknown;
    }

    private static Kind DetectZipKind(byte[] content)
    {
        var probe = Encoding.ASCII.GetString(content, 0, Math.Min(content.Length, 8192));
        if (probe.Contains("xl/", StringComparison.Ordinal))
            return Kind.Xlsx;
        if (probe.Contains("word/", StringComparison.Ordinal))
            return Kind.Docx;
        return Kind.Docx;
    }

    private static bool IsPdf(byte[] content) =>
        content.Length >= 5 && content[0] == (byte)'%' && content[1] == (byte)'P' && content[2] == (byte)'D' && content[3] == (byte)'F';

    private static bool IsZip(byte[] content) =>
        content.Length >= 2 && content[0] == (byte)'P' && content[1] == (byte)'K';

    private static bool IsOle(byte[] content) =>
        content.Length >= 8 && content[0] == 0xD0 && content[1] == 0xCF && content[2] == 0x11 && content[3] == 0xE0;

    private static string FromPdf(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            if (!string.IsNullOrWhiteSpace(page.Text))
                sb.AppendLine(page.Text);
        }

        return OrUnreadable(sb.ToString(), "PDF had no extractable text.");
    }

    private static string FromDocx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return "[Word file had no body text.]";

        var parts = body.Descendants<Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t));
        return OrUnreadable(string.Join(Environment.NewLine, parts), "Word file had no extractable text.");
    }

    private static string FromDoc(byte[] content)
    {
        var chunks = ExtractPrintableRuns(content);
        return OrUnreadable(string.Join(Environment.NewLine, chunks), "Word 97-2003 file had no extractable text.");
    }

    private static IReadOnlyList<string> ExtractPrintableRuns(byte[] content)
    {
        var runs = new List<string>();
        var ascii = new StringBuilder();
        foreach (var b in content)
        {
            if (b is >= 32 and <= 126)
            {
                ascii.Append((char)b);
                continue;
            }

            if (ascii.Length >= 12)
                runs.Add(ascii.ToString().Trim());
            ascii.Clear();
        }

        if (ascii.Length >= 12)
            runs.Add(ascii.ToString().Trim());

        var utf16 = new StringBuilder();
        for (var i = 0; i + 1 < content.Length; i += 2)
        {
            var ch = (char)(content[i] | (content[i + 1] << 8));
            if (!char.IsControl(ch) && (char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || char.IsWhiteSpace(ch) || ch == '€'))
            {
                utf16.Append(ch);
                continue;
            }

            if (utf16.Length >= 12)
                runs.Add(CollapseWhitespace(utf16.ToString()));
            utf16.Clear();
        }

        if (utf16.Length >= 12)
            runs.Add(CollapseWhitespace(utf16.ToString()));

        return runs
            .Where(r => r.Any(char.IsLetter))
            .Distinct()
            .ToList();
    }

    private static string FromExcel(byte[] content, bool xlsx)
    {
        using var stream = new MemoryStream(content);
        using IWorkbook workbook = xlsx ? new XSSFWorkbook(stream) : new HSSFWorkbook(stream);
        var formatter = new DataFormatter();
        var sb = new StringBuilder();

        for (var i = 0; i < workbook.NumberOfSheets; i++)
        {
            var sheet = workbook.GetSheetAt(i);
            sb.AppendLine($"# Sheet: {sheet.SheetName}");
            for (var r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                    continue;

                if (row.FirstCellNum < 0)
                    continue;

                var cells = new List<string>();
                for (var c = row.FirstCellNum; c < row.LastCellNum; c++)
                {
                    var cell = row.GetCell(c);
                    cells.Add(cell is null ? "" : formatter.FormatCellValue(cell).Trim());
                }

                if (cells.All(string.IsNullOrWhiteSpace))
                    continue;
                sb.AppendLine(string.Join('\t', cells));
            }
        }

        return OrUnreadable(sb.ToString(), "Spreadsheet had no extractable text.");
    }

    private static string FromHtml(byte[] content)
    {
        var html = FromText(content);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style") ?? Enumerable.Empty<HtmlNode>())
            node.Remove();

        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText ?? "") ?? "";
        return OrUnreadable(CollapseWhitespace(text), "HTML had no extractable text.");
    }

    private static string FromText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string CollapseWhitespace(string text)
    {
        var lines = text.Split('\n')
            .Select(l => string.Join(' ', l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        return string.Join(Environment.NewLine, lines.Where(l => l.Length > 0));
    }

    private static string OrUnreadable(string text, string emptyMessage) =>
        string.IsNullOrWhiteSpace(text) ? $"[{emptyMessage}]" : text.Trim();

    private static string Unreadable(string name) =>
        $"[Binary file '{name}' — supported types are pdf, txt, docx, doc, xls, xlsx, html.]";
}
