using System.Globalization;
using System.Text;

namespace FiberBlueprint;

// Minimal PDF writer used by the blueprint code.
// Supports lines, rectangles, colors and text. Coordinates are points
// with origin at the top-left.
public class PdfPage
{
    public double Width { get; }
    public double Height { get; }
    internal StringBuilder Content { get; } = new();

    public PdfPage(double width, double height)
    {
        Width = width;
        Height = height;
    }

    static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public void StrokeColor((double r, double g, double b) c) =>
        Content.Append($"{N(c.r)} {N(c.g)} {N(c.b)} RG\n");

    public void FillColor((double r, double g, double b) c) =>
        Content.Append($"{N(c.r)} {N(c.g)} {N(c.b)} rg\n");

    public void LineWidth(double w) => Content.Append($"{N(w)} w\n");

    public void Line(double x1, double y1, double x2, double y2) =>
        Content.Append($"{N(x1)} {N(Height - y1)} m {N(x2)} {N(Height - y2)} l S\n");

    public void Rect(double x, double y, double w, double h, bool fill = false) =>
        Content.Append($"{N(x)} {N(Height - y - h)} {N(w)} {N(h)} re {(fill ? "f" : "S")}\n");

    // (x,y) is left end of text baseline.
    public void Text(double x, double y, string text, double size, bool bold = false) =>
        Content.Append($"BT /{(bold ? "F2" : "F1")} {N(size)} Tf {N(x)} {N(Height - y)} Td ({Escape(text)}) Tj ET\n");

    // Escape backslash/parentheses; non-Latin-1 chars map to '?'.
    static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s)
        {
            if (ch > 255) sb.Append('?');
            else if (ch == '\\' || ch == '(' || ch == ')') sb.Append('\\').Append(ch);
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}

public class PdfDocument
{
    readonly List<PdfPage> pages = new();

    public int PageCount => pages.Count;

    public PdfPage AddPage(double width, double height)
    {
        var page = new PdfPage(width, height);
        pages.Add(page);
        return page;
    }

    public void Save(string path)
    {
        // Object numbers: 1 catalog, 2 pages, 3 font, 4 bold font.
        // Page i uses objects 5+2i (page) and 6+2i (content).
        var sb = new StringBuilder();
        var offsets = new List<int>();

        void Obj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append($"{offsets.Count} 0 obj\n{body}\nendobj\n");
        }

        sb.Append("%PDF-1.4\n");
        string kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{5 + 2 * i} 0 R"));
        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            string w = p.Width.ToString("0.##", CultureInfo.InvariantCulture);
            string h = p.Height.ToString("0.##", CultureInfo.InvariantCulture);
            Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + 2 * i} 0 R >>");
            string content = p.Content.ToString();
            Obj($"<< /Length {content.Length} >>\nstream\n{content}endstream");
        }

        int xrefStart = sb.Length;
        sb.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (int off in offsets) sb.Append($"{off:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF\n");

        // Using Latin-1 ensures one char = one byte for offsets.
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(sb.ToString()));
    }
}
