using System.Drawing;

namespace QslStudio
{
    internal static partial class Templates
    {
        internal static readonly int[] PortraitColumns = { 56, 397, 640, 844 };
        internal static readonly int[] HandwriteColumns = { 56, 467, 844 };
        internal const int PortraitRowHeight = 121;
        internal static int PortraitHeader(string key) { return key == "portrait-postal" ? 246 : 57; }

        private static void MakePortrait(Project p)
        {
            bool postal = p.Template == "portrait-postal", photo = p.Template != "portrait-log";
            Color ink = C(photo ? 0x182f39 : 0x1c1f20);
            Color sub = C(photo ? 0x4a5f66 : 0x343839);
            if (photo)
            {
                AddCover(p);
                Text(p, 0, "台站标题", "中国业余无线电台  /  AMATEUR RADIO STATION", 55, 1057, 790, 43, 10, sub, true);
                Add(p, 0, "own", 50, 1100, 795, 166, 53, ink, true);
                Add(p, 0, "location", 57, 1273, 535, 48, 12, sub, false);
                Add(p, 0, "grid", 613, 1273, 229, 48, 12, sub, true);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Far;
            }
            else
            {
                Text(p, 0, "中文台站标题", "中国业余无线电台", 78, 374, 744, 51, 14, sub, true);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                Text(p, 0, "英文台站标题", "AMATEUR RADIO STATION", 78, 438, 744, 49, 13, sub, false);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                Add(p, 0, "own", 72, 516, 756, 200, 58, ink, true);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                Add(p, 0, "location", 78, 826, 744, 54, 14, sub, false);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                Add(p, 0, "operator", 78, 895, 744, 46, 10, sub, false);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
            }

            if (postal)
            {
                Add(p, 1, "postcode", 54, 57, 396, 61, 12, C(0x983e36), true);
                p.Layers[p.Layers.Count - 1].Symbol = "postcode";
                Text(p, 1, "邮票位", "贴邮票", 611, 50, 233, 234, 9, sub, false);
                Layer stamp = p.Layers[p.Layers.Count - 1];
                stamp.Symbol = "stampbox";
                stamp.Align = StringAlignment.Center;
            }

            int top = PortraitHeader(p.Template);
            Text(p, 1, "确认卡标题", "通联确认  /  QSL CONFIRMATION", 56, top, postal ? 537 : 788, 42, 11, sub, true);
            Add(p, 1, "own", 52, top + 48, 565, 110, 33, ink, true);
            Add(p, 1, "location", 631, top + 85, 213, 45, 9, sub, false);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Far;
            Text(p, 1, "对方呼号栏名", "TO RADIO  /  对方呼号", 56, top + 197, 788, 39, 9, sub, true);
            Add(p, 1, "other", 54, top + 239, 790, 72, 21, ink, true);

            int grid = top + 330;
            if (photo)
            {
                QsoCells(p, new[] { "DATE (UTC)", "TIME (UTC)", "RST" }, new[] { "date", "utc", "rst" },
                    PortraitColumns, grid + 4, grid + 41, 13, ink, sub);
                grid += PortraitRowHeight;
                QsoCells(p, new[] { "MHz / BAND", "MODE", "QSL" }, new[] { "mhz", "mode", "reply" },
                    PortraitColumns, grid + 4, grid + 41, 13, ink, sub);
            }
            else
            {
                QsoCells(p, new[] { "DATE / 日期", "TIME (UTC)" }, new[] { "date", "utc" },
                    HandwriteColumns, grid + 4, grid + 38, 14, ink, sub);
                QsoCells(p, new[] { "MHz / BAND", "MODE / 模式" }, new[] { "mhz", "mode" },
                    HandwriteColumns, grid + 112, grid + 146, 14, ink, sub);
                QsoCells(p, new[] { "RST / 报告", "QSL / 回卡" }, new[] { "rst", "reply" },
                    HandwriteColumns, grid + 220, grid + 254, 14, ink, sub);
            }

            if (postal)
            {
                Text(p, 1, "收件栏名", "收件人及地址  /  TO", 56, 843, 788, 47, 11, sub, true);
                Add(p, 1, "recipient", 56, 915, 788, 221, 13, ink, false);
                p.Layers[p.Layers.Count - 1].Symbol = "mailaddress";
                Text(p, 1, "寄件栏名", "寄件地址  /  FROM", 56, 1172, 788, 42, 9, sub, true);
                Add(p, 1, "address", 56, 1226, 788, 92, 10, ink, false);
                p.Layers[p.Layers.Count - 1].Symbol = "paragraph";
            }
            else
            {
                SmallField(p, "QSL VIA", "via", 56, photo ? 685 : 737, 788, sub, ink, 165);
                Text(p, 1, "留言栏名", "留言  /  REMARKS", 56, photo ? 776 : 805, 788, 43, 10, sub, true);
                Add(p, 1, "note", 56, photo ? 843 : 871, 788, 260, 12, ink, false);
                p.Layers[p.Layers.Count - 1].Symbol = "paragraph";
                SmallField(p, "SIGN", "signature", 56, 1249, 554, sub, ink, 86);
            }
        }
    }

    internal static partial class CardPainter
    {
        private static void DrawPortraitRules(Graphics g, string key, int side, Project project)
        {
            Color edge = key == "portrait-log" ? Color.FromArgb(74, 77, 78) : Color.FromArgb(115, 132, 139);
            using (var pen = new Pen(edge, 1.8f))
            {
                if (side == 0)
                {
                    if (key == "portrait-log")
                    {
                        g.DrawRectangle(pen, 46, 48, 808, 1283);
                        g.DrawLine(pen, 345, 771, 555, 771);
                    }
                    else
                    {
                        var cover = Templates.CoverFrame(key, 0);
                        Fill(g, Color.FromArgb(234, 239, 238), cover.X, cover.Y, cover.Width, cover.Height);
                        if (key == "portrait-postal") g.DrawRectangle(pen, cover.X, cover.Y, cover.Width, cover.Height);
                    }
                    return;
                }

                int header = Templates.PortraitHeader(key);
                g.DrawLine(pen, 56, header + 171, 844, header + 171);
                int grid = header + 330;
                if (key == "portrait-log")
                {
                    var columns = Templates.HandwriteColumns;
                    for (int y = grid + 100; y <= grid + 316; y += 108)
                        for (int col = 0; col < columns.Length - 1; col++)
                            g.DrawLine(pen, columns[col], y, columns[col + 1] - (col == 0 ? 25 : 0), y);
                }
                else
                {
                    int row = Templates.PortraitRowHeight;
                    DrawRecords(g, pen, Templates.PortraitColumns, grid, grid + 40, grid + row);
                    grid += row;
                    DrawRecords(g, pen, Templates.PortraitColumns, grid, grid + 40, grid + row);
                }
                if (key == "portrait-postal")
                    g.DrawLine(pen, 56, 1154, 844, 1154);
                else
                {
                    if (string.IsNullOrWhiteSpace(project.Value("note")))
                        for (int y = 912; y <= 1128; y += 72) g.DrawLine(pen, 56, y, 844, y);
                    g.DrawLine(pen, 142, 1290, 609, 1290);
                }
            }
        }
    }
}
