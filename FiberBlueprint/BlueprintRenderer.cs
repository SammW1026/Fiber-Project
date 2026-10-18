using System;
using System.Linq;
using System.Collections.Generic;

namespace FiberBlueprint;

// Render a Network to PDF (one sheet per splitter).
public static class BlueprintRenderer
{
    // Letter landscape (points).
    const double PageW = 792, PageH = 612, Margin = 24, TitleBlockW = 150;

    // 12-fiber color code; port N -> (N-1) % 12.
    static readonly string[] FiberNames =
        { "blue", "orange", "green", "brown", "slate", "white",
          "red", "black", "yellow", "violet", "rose", "aqua" };

    static readonly (double r, double g, double b)[] FiberColors =
    {
        (0.00, 0.20, 0.90), (1.00, 0.50, 0.00), (0.00, 0.65, 0.20), (0.50, 0.30, 0.10),
        (0.45, 0.50, 0.55), (0.70, 0.70, 0.70), (0.90, 0.10, 0.10), (0.00, 0.00, 0.00),
        (0.85, 0.75, 0.00), (0.55, 0.20, 0.75), (1.00, 0.45, 0.65), (0.00, 0.75, 0.75),
    };

    static readonly (double, double, double) Black = (0, 0, 0);
    static readonly (double, double, double) Gray = (0.45, 0.45, 0.45);

    public static int Render(Network net, string path)
    {
        var doc = new PdfDocument();
        var sheets = net.Pedestals
            .SelectMany(p => p.Splitters.Select(s => (ped: p, split: s)))
            .ToList();

        for (int i = 0; i < sheets.Count; i++)
            DrawSheet(doc.AddPage(PageW, PageH), net, sheets[i].ped, sheets[i].split, i + 1, sheets.Count);

        doc.Save(path);
        return doc.PageCount;
    }

    static void DrawSheet(PdfPage page, Network net, Pedestal ped, Splitter sp, int sheetNo, int sheetCount)
    {
        // Draw border and right title block.
        page.StrokeColor(Black);
        page.LineWidth(1.5);
        page.Rect(Margin, Margin, PageW - 2 * Margin, PageH - 2 * Margin);
        double tbX = PageW - Margin - TitleBlockW;
        page.LineWidth(1);
        page.Line(tbX, Margin, tbX, PageH - Margin);

        double ty = Margin + 22;
        void TitleField(string label, string value)
        {
            page.FillColor(Gray);
            page.Text(tbX + 8, ty, label, 7);
            page.FillColor(Black);
            page.Text(tbX + 8, ty + 14, Fit(value, 24), 10, bold: true);
            ty += 46;
        }
        TitleField("PROJECT", net.Name);
        TitleField("PEDESTAL", ped.Name);
        TitleField("SPLITTER", $"{sp.Name}  1:{sp.Ratio}");
        TitleField("SHEET", $"{sheetNo} of {sheetCount}");
        TitleField("DATE", DateTime.Today.ToString("MM/dd/yyyy"));

        // Heading.
        page.FillColor(Black);
        page.Text(Margin + 14, Margin + 26, $"{ped.Name} - {sp.Name} (1:{sp.Ratio} splitter)", 14, bold: true);

        // Splitter box: one row per output port.
        double top = Margin + 60, bottom = PageH - Margin - 20;
        double rowH = Math.Min(34, (bottom - top) / sp.Ratio);
        double boxH = rowH * sp.Ratio;
        double boxX = 210, boxW = 16;

        page.StrokeColor(Black);
        page.LineWidth(1.2);
        page.Rect(boxX, top, boxW, boxH);
        page.FillColor(Black);
        page.Text(boxX - 6, top - 8, $"1:{sp.Ratio}", 9, bold: true);

        // Input feed (left).
        double midY = top + boxH / 2;
        page.LineWidth(1.8);
        page.Line(Margin + 16, midY, boxX, midY);
        page.FillColor(Gray);
        page.Text(Margin + 16, midY - 6, "INPUT", 7);
        page.FillColor(Black);
        page.Text(Margin + 16, midY + 16, Fit(sp.Input, 22), 9);

        // One line per output port.
        for (int port = 1; port <= sp.Ratio; port++)
        {
            double y = top + rowH * (port - 0.5);
            var color = FiberColors[(port - 1) % 12];
            string fiber = FiberNames[(port - 1) % 12];
            var cable = sp.Cables.FirstOrDefault(c => c.Port == port);

            page.FillColor(Gray);
            page.Text(boxX + boxW + 4, y - 3, $"{port} {fiber}", 6.5);

            page.StrokeColor(color);
            if (cable != null)
            {
                page.LineWidth(1.6);
                page.Line(boxX + boxW, y, 420, y);
                page.FillColor(Black);
                page.Text(300, y - 3, Fit(cable.Label, 18), 8);
                page.Text(426, y + 3, Fit(cable.Connector, 8), 8);
                page.Text(466, y + 3, Fit(cable.Endpoint, 22), 8, bold: true);
            }
            else
            {
                page.LineWidth(0.8);
                page.Line(boxX + boxW, y, 330, y);
                page.FillColor(Gray);
                page.Text(336, y + 2.5, "spare", 7);
            }
        }
    }

    // Truncate long text to fit.
    static string Fit(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + ".";
}
