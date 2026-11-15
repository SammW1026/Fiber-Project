using System;
using System.Linq;
using System.Collections.Generic;

namespace FiberBlueprint;

public class RenderOptions
{
    public bool Overview { get; set; } = true;   // sheet 1: every splitter and how they connect
    public bool Schedule { get; set; } = true;   // last sheet(s): table of every cable
}

// Render a Network to PDF:
//   overview sheet -> one detail sheet per splitter -> cable schedule.
public static class BlueprintRenderer
{
    // Letter landscape (points).
    const double PageW = 792, PageH = 612, Margin = 24, TitleBlockW = 150;
    const double TbX = PageW - Margin - TitleBlockW;          // left edge of title block
    const double DrawLeft = Margin + 14, DrawRight = TbX - 14; // drawing area
    const int ScheduleRowsPerPage = 34;

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
    static readonly (double, double, double) White = (1, 1, 1);
    static readonly (double, double, double) Gray = (0.45, 0.45, 0.45);
    static readonly (double, double, double) Shade = (0.93, 0.93, 0.93);
    static readonly (double, double, double) LightShade = (0.97, 0.97, 0.97);

    record ScheduleRow(Cable Cable, SplitterRef From, string To);

    public static int Render(Network net, string path, RenderOptions? opts = null)
    {
        opts ??= new RenderOptions();
        var topo = new Topology(net);
        var doc = new PdfDocument();

        var rows = topo.All
            .SelectMany(r => r.Splitter.Cables.OrderBy(c => c.Port)
                .Select(c => new ScheduleRow(c, r, topo.IsDrop(c) ? c.Endpoint : $"splitter {c.Feeds}")))
            .ToList();

        int overviewSheets = opts.Overview ? 1 : 0;
        int schedulePages = opts.Schedule && rows.Count > 0
            ? (rows.Count + ScheduleRowsPerPage - 1) / ScheduleRowsPerPage : 0;
        int total = overviewSheets + topo.All.Count + schedulePages;

        // Sheet number of each splitter's detail sheet, for cross references.
        var sheetOf = new Dictionary<Splitter, int>();
        for (int i = 0; i < topo.All.Count; i++)
            sheetOf[topo.All[i].Splitter] = overviewSheets + 1 + i;

        int sheet = 1;
        if (opts.Overview)
            DrawOverview(doc.AddPage(PageW, PageH), net, topo, sheetOf, sheet++, total);
        foreach (var r in topo.All)
            DrawDetail(doc.AddPage(PageW, PageH), net, topo, r, sheetOf, sheet++, total);
        for (int p = 0; p < schedulePages; p++)
            DrawSchedule(doc.AddPage(PageW, PageH), net, sheetOf,
                rows.Skip(p * ScheduleRowsPerPage).Take(ScheduleRowsPerPage).ToList(),
                rows.Count, p + 1, schedulePages, sheet++, total);

        doc.Save(path);
        return doc.PageCount;
    }

    // ---------- shared frame + title block ----------

    // Draws the border, heading and title block. Returns the next free y in the title block.
    static double DrawFrame(PdfPage page, Network net, string heading,
        IEnumerable<(string label, string value)> fields, int sheetNo, int sheetCount)
    {
        page.StrokeColor(Black);
        page.LineWidth(1.5);
        page.Rect(Margin, Margin, PageW - 2 * Margin, PageH - 2 * Margin);
        page.LineWidth(1);
        page.Line(TbX, Margin, TbX, PageH - Margin);

        // Project banner at the top of the title block.
        page.FillColor(Black);
        page.Rect(TbX, Margin, TitleBlockW, 30, fill: true);
        page.FillColor(White);
        page.Text(TbX + 8, Margin + 13, "FIBER BLUEPRINT", 7, bold: true);
        page.Text(TbX + 8, Margin + 24, Fit(net.Name, 30), 8.5, bold: true);

        double ty = Margin + 48;
        void Field(string label, string value)
        {
            page.FillColor(Gray);
            page.Text(TbX + 8, ty, label, 6.5);
            page.FillColor(Black);
            page.Text(TbX + 8, ty + 12, Fit(value, 24), 9.5, bold: true);
            page.StrokeColor(Shade);
            page.LineWidth(0.6);
            page.Line(TbX + 6, ty + 19, PageW - Margin - 6, ty + 19);
            ty += 31;
        }

        foreach (var (label, value) in fields) Field(label, value);
        Field("REVISION", string.IsNullOrWhiteSpace(net.Revision) ? "-" : net.Revision);
        Field("DRAWN BY", string.IsNullOrWhiteSpace(net.DrawnBy) ? "-" : net.DrawnBy);
        Field("SHEET", $"{sheetNo} of {sheetCount}");

        page.FillColor(Black);
        page.Text(DrawLeft, Margin + 26, heading, 14, bold: true);
        return ty;
    }

    // Two-column swatch list of the 12 fiber colors, at the bottom of the title block.
    static void DrawColorLegend(PdfPage page)
    {
        double y = PageH - Margin - 104;
        page.FillColor(Gray);
        page.Text(TbX + 8, y, "FIBER COLOR CODE", 6.5);
        for (int i = 0; i < 12; i++)
        {
            double x = TbX + 8 + (i / 6) * 70;
            double ry = y + 10 + (i % 6) * 14;
            page.FillColor(FiberColors[i]);
            page.Rect(x, ry, 12, 8, fill: true);
            page.StrokeColor(Gray);
            page.LineWidth(0.4);
            page.Rect(x, ry, 12, 8);
            page.FillColor(Black);
            page.Text(x + 16, ry + 7, $"{i + 1} {FiberNames[i]}", 7);
        }
    }

    // ---------- sheet 1: system overview ----------

    static void DrawOverview(PdfPage page, Network net, Topology topo,
        Dictionary<Splitter, int> sheetOf, int sheetNo, int sheetCount)
    {
        var allCables = topo.All.SelectMany(r => r.Splitter.Cables).ToList();
        int drops = allCables.Count(topo.IsDrop);
        int spare = topo.All.Sum(r => r.Splitter.Ratio - r.Splitter.Cables.Count);
        int crossLinks = topo.Links.Count(l => l.CrossesPedestal);

        DrawFrame(page, net, "System overview", new[]
        {
            ("PEDESTALS", net.Pedestals.Count.ToString()),
            ("SPLITTERS", topo.All.Count.ToString()),
            ("DROP CABLES", drops.ToString()),
            ("SPARE PORTS", spare.ToString()),
            ("LINKS BETWEEN PEDESTALS", crossLinks.ToString()),
        }, sheetNo, sheetCount);

        double top = Margin + 46, bottom = PageH - Margin - 30;
        const double inputW = 70, boxW = 96, bandHead = 14, bandGap = 8;

        // Columns by depth: the first splitter on the left, the ones it feeds to its right.
        int levels = topo.All.Count == 0 ? 1 : topo.All.Max(r => topo.DepthOf(r.Splitter)) + 1;
        double x0 = DrawLeft + inputW;
        double colStep = levels > 1 ? Math.Min(210, (DrawRight - x0 - boxW) / (levels - 1)) : 0;

        // Rows by pedestal: each pedestal is a dashed band; splitters at the same depth stack inside it.
        var bands = net.Pedestals.Select(p =>
        {
            var byDepth = p.Splitters.GroupBy(s => topo.DepthOf(s)).ToDictionary(g => g.Key, g => g.ToList());
            int slots = byDepth.Count == 0 ? 1 : byDepth.Values.Max(l => l.Count);
            return (ped: p, byDepth, slots);
        }).ToList();

        int totalSlots = Math.Max(1, bands.Sum(b => b.slots));
        double slotH = Math.Min(64, (bottom - top - bands.Count * (bandHead + bandGap)) / totalSlots);
        double boxH = Math.Max(12, Math.Min(42, slotH - 10));

        var boxes = new Dictionary<Splitter, (double x, double y)>();
        double y = top;
        foreach (var band in bands)
        {
            double bandH = bandHead + band.slots * slotH;
            page.StrokeColor(Gray);
            page.LineWidth(0.7);
            page.Dash(4, 3);
            page.Rect(DrawLeft - 6, y, DrawRight - DrawLeft + 12, bandH);
            page.Solid();
            page.FillColor(Gray);
            page.Text(DrawLeft, y + 10, Fit(band.ped.Name.ToUpperInvariant(), 40), 7.5, bold: true);

            foreach (var (depth, list) in band.byDepth)
                for (int k = 0; k < list.Count; k++)
                    boxes[list[k]] = (x0 + depth * colStep, y + bandHead + k * slotH + (slotH - boxH) / 2);

            y += bandH + bandGap;
        }

        // Links between splitters (drawn first so boxes sit on top).
        var linksFrom = new Dictionary<Splitter, int>();
        foreach (var link in topo.Links)
        {
            if (!boxes.TryGetValue(link.From.Splitter, out var p) || !boxes.TryGetValue(link.To.Splitter, out var c))
                continue;
            int port = link.Cable.Port;
            double sy = p.y + boxH * (port - 0.5) / link.From.Splitter.Ratio;
            double ey = c.y + boxH / 2;
            int idx = linksFrom.TryGetValue(link.From.Splitter, out var n) ? n : 0;
            linksFrom[link.From.Splitter] = idx + 1;
            double gap = c.x - (p.x + boxW);
            double xm = p.x + boxW + 10 + Math.Min(idx * 5, Math.Max(0, gap - 60));

            page.StrokeColor(FiberColors[(port - 1) % 12]);
            page.LineWidth(link.CrossesPedestal ? 1.7 : 1.2);
            if (link.CrossesPedestal) page.Dash(5, 2.5);
            page.Line(p.x + boxW, sy, xm, sy);
            page.Line(xm, sy, xm, ey);
            page.Line(xm, ey, c.x, ey);
            page.Solid();

            // Label sits just left of the child box so it never crosses the vertical bus lines.
            int room = (int)((c.x - xm - 10) / (6.5 * 0.52));
            string label = Fit($"{link.Cable.Label} p{port}", Math.Max(4, room));
            page.FillColor(Black);
            page.Text(c.x - 4 - PdfPage.TextWidth(label, 6.5), ey - 3, label, 6.5);
        }

        // Splitter boxes.
        foreach (var r in topo.All)
        {
            if (!boxes.TryGetValue(r.Splitter, out var b)) continue;
            var sp = r.Splitter;

            if (!topo.FedBy.ContainsKey(sp))
            {
                // Network input stub.
                double my = b.y + boxH / 2;
                page.StrokeColor(Black);
                page.LineWidth(1.8);
                page.Line(DrawLeft, my, b.x, my);
                page.FillColor(Gray);
                page.Text(DrawLeft, my - 4, Fit(sp.Input, 20), 6.5);
            }

            page.FillColor(White);
            page.Rect(b.x, b.y, boxW, boxH, fill: true);
            page.StrokeColor(Black);
            page.LineWidth(1.1);
            page.Rect(b.x, b.y, boxW, boxH);

            page.FillColor(Black);
            if (boxH >= 26)
            {
                page.Text(b.x + 5, b.y + 11, Fit(sp.Name, 18), 8.5, bold: true);
                page.Text(b.x + 5, b.y + 21, $"1:{sp.Ratio}   {sp.Cables.Count}/{sp.Ratio} used", 7);
                if (boxH >= 34)
                {
                    page.FillColor(Gray);
                    page.Text(b.x + 5, b.y + 31, $"see sheet {sheetOf[sp]}", 6.5);
                }
            }
            else
            {
                page.Text(b.x + 4, b.y + boxH / 2 + 2.5, Fit($"{sp.Name} 1:{sp.Ratio}", 20), 7, bold: true);
            }
        }

        // Legend.
        double ly = PageH - Margin - 14;
        page.StrokeColor(Black);
        page.LineWidth(1.2);
        page.Line(DrawLeft, ly - 2.5, DrawLeft + 24, ly - 2.5);
        page.FillColor(Black);
        page.Text(DrawLeft + 30, ly, "link inside a pedestal", 7);
        page.LineWidth(1.7);
        page.Dash(5, 2.5);
        page.Line(DrawLeft + 130, ly - 2.5, DrawLeft + 154, ly - 2.5);
        page.Solid();
        page.Text(DrawLeft + 160, ly, "cable run between pedestals", 7);
        page.FillColor(Gray);
        page.Text(DrawLeft + 290, ly, "line color = fiber color of the output port (pN)", 7);
    }

    // ---------- detail sheet: one splitter ----------

    static void DrawDetail(PdfPage page, Network net, Topology topo, SplitterRef r,
        Dictionary<Splitter, int> sheetOf, int sheetNo, int sheetCount)
    {
        var ped = r.Pedestal;
        var sp = r.Splitter;
        topo.FedBy.TryGetValue(sp, out var feed);
        int used = sp.Cables.Count;

        string fedFrom = feed != null ? $"{feed.From.Splitter.Name} port {feed.Cable.Port}" : sp.Input;
        DrawFrame(page, net, $"{ped.Name} - {sp.Name} (1:{sp.Ratio} splitter)", new[]
        {
            ("PEDESTAL", ped.Name),
            ("SPLITTER", $"{sp.Name}  1:{sp.Ratio}"),
            ("FED FROM", fedFrom),
            ("PORTS USED", $"{used} of {sp.Ratio} ({sp.Ratio - used} spare)"),
        }, sheetNo, sheetCount);
        DrawColorLegend(page);

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
        if (feed != null)
        {
            page.StrokeColor(FiberColors[(feed.Cable.Port - 1) % 12]);
            if (feed.CrossesPedestal) page.Dash(5, 2.5);
        }
        page.Line(Margin + 16, midY, boxX, midY);
        page.Solid();
        page.FillColor(Gray);
        page.Text(Margin + 16, midY - 6, "INPUT", 7);
        page.FillColor(Black);
        if (feed != null)
        {
            page.Text(Margin + 16, midY + 14, Fit(fedFrom, 30), 8.5, bold: true);
            page.Text(Margin + 16, midY + 25, Fit($"cable {feed.Cable.Label} ({FiberNames[(feed.Cable.Port - 1) % 12]})", 32), 8);
            page.FillColor(Gray);
            string from = feed.CrossesPedestal ? $"from {feed.From.Pedestal.Name}, " : "";
            page.Text(Margin + 16, midY + 36, Fit($"{from}see sheet {sheetOf[feed.From.Splitter]}", 34), 7);
        }
        else
        {
            page.Text(Margin + 16, midY + 16, Fit(sp.Input, 30), 9);
        }

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
                bool isFeed = !topo.IsDrop(cable);
                page.LineWidth(isFeed ? 2.4 : 1.6);
                page.Line(boxX + boxW, y, 420, y);
                page.FillColor(Black);
                page.Text(300, y - 3, Fit(cable.Label, 18), 8);
                page.Text(426, y + 3, Fit(cable.Connector, 8), 8);

                string end = cable.Endpoint;
                if (isFeed && topo.ByName.TryGetValue(cable.Feeds!, out var child))
                    end = $"-> {child.Splitter.Name} (sheet {sheetOf[child.Splitter]})";
                page.Text(466, y + 3, Fit(end, 26), 8, bold: true);
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

    // ---------- cable schedule ----------

    static void DrawSchedule(PdfPage page, Network net, Dictionary<Splitter, int> sheetOf,
        List<ScheduleRow> rows, int totalCables, int pageNo, int pageCount, int sheetNo, int sheetCount)
    {
        string heading = pageCount > 1 ? $"Cable schedule (page {pageNo} of {pageCount})" : "Cable schedule";
        DrawFrame(page, net, heading, new[]
        {
            ("CONTENTS", "Cable schedule"),
            ("CABLES", totalCables.ToString()),
        }, sheetNo, sheetCount);

        // Column x positions and max characters.
        (string title, double x, int chars)[] cols =
        {
            ("CABLE", DrawLeft + 4, 14), ("FROM SPLITTER / PORT", DrawLeft + 80, 22),
            ("FIBER", DrawLeft + 196, 9), ("CONN.", DrawLeft + 254, 8), ("TO", DrawLeft + 300, 30),
            ("PEDESTAL", DrawLeft + 466, 18), ("SHEET", DrawLeft + 540, 4),
        };

        double y = Margin + 46;
        const double headH = 16, rowH = 14;
        page.FillColor(Shade);
        page.Rect(DrawLeft, y, DrawRight - DrawLeft, headH, fill: true);
        page.FillColor(Black);
        foreach (var c in cols) page.Text(c.x, y + 11, c.title, 7, bold: true);
        y += headH;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int port = row.Cable.Port;
            if (i % 2 == 1)
            {
                page.FillColor(LightShade);
                page.Rect(DrawLeft, y, DrawRight - DrawLeft, rowH, fill: true);
            }
            double ty = y + 10;
            page.FillColor(Black);
            page.Text(cols[0].x, ty, Fit(row.Cable.Label, cols[0].chars), 7.5, bold: true);
            page.Text(cols[1].x, ty, Fit($"{row.From.Splitter.Name} / {port}", cols[1].chars), 7.5);

            page.FillColor(FiberColors[(port - 1) % 12]);
            page.Rect(cols[2].x, ty - 6.5, 9, 7, fill: true);
            page.StrokeColor(Gray);
            page.LineWidth(0.4);
            page.Rect(cols[2].x, ty - 6.5, 9, 7);
            page.FillColor(Black);
            page.Text(cols[2].x + 12, ty, FiberNames[(port - 1) % 12], 7.5);

            page.Text(cols[3].x, ty, Fit(row.Cable.Connector, cols[3].chars), 7.5);
            page.Text(cols[4].x, ty, Fit(row.To, cols[4].chars), 7.5);
            page.Text(cols[5].x, ty, Fit(row.From.Pedestal.Name, cols[5].chars), 7.5);
            page.Text(cols[6].x, ty, sheetOf[row.From.Splitter].ToString(), 7.5);
            y += rowH;
        }

        page.StrokeColor(Gray);
        page.LineWidth(0.6);
        page.Line(DrawLeft, y, DrawRight, y);
    }

    // Truncate long text to fit.
    static string Fit(string? s, int max)
    {
        s ??= "";
        return s.Length <= max ? s : s[..(max - 1)] + ".";
    }
}
