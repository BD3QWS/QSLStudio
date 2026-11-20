using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace QslStudio
{
    internal enum SolderMask { Black, Green, Blue }
    internal enum CardMaterial { Pcb, Wood, Acrylic, Metal }

    internal sealed class PcbOptions
    {
        internal bool ExposedCopper, HangingHole, LightFront, LightBack;
        internal int Radius = 20, Threshold = 150;
        internal SolderMask Mask;
    }

    internal sealed class PcbArtwork
    {
        private readonly byte[][] intensity = new byte[2][];
        private readonly bool[] light;
        internal readonly int Width, Height, Columns, Rows;

        internal PcbArtwork(Project card, bool lightFront = false, bool lightBack = false)
        {
            light = new[] { lightFront, lightBack };
            Width = card.Width;
            Height = card.Height;
            // Each manufacturing cell is at least 0.20 mm. Sample the card twice as finely.
            Columns = Width / 2;
            Rows = Height / 2;
            for (int side = 0; side < 2; side++)
            {
                intensity[side] = new byte[Columns * Rows];
                Sample(card, side, intensity[side], light[side]);
            }
        }

        private void Sample(Project card, int side, byte[] result, bool lightInk)
        {
            using (var image = new Bitmap(Columns * 2, Rows * 2, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(image))
                    CardPainter.Paint(g, card, side, new RectangleF(0, 0, image.Width, image.Height));
                var bits = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, image.PixelFormat);
                try
                {
                    var a = new byte[image.Width * 3];
                    var b = new byte[a.Length];
                    for (int y = 0; y < Rows; y++)
                    {
                        Marshal.Copy(IntPtr.Add(bits.Scan0, 2 * y * bits.Stride), a, 0, a.Length);
                        Marshal.Copy(IntPtr.Add(bits.Scan0, (2 * y + 1) * bits.Stride), b, 0, b.Length);
                        for (int x = 0; x < Columns; x++)
                        {
                            int i = x * 6;
                            int a0 = Luma(a, i), a1 = Luma(a, i + 3), b0 = Luma(b, i), b1 = Luma(b, i + 3);
                            // Retain narrow stems rather than averaging them away during conversion.
                            int sample = lightInk ? Math.Max(Math.Max(a0, a1), Math.Max(b0, b1)) :
                                Math.Min(Math.Min(a0, a1), Math.Min(b0, b1));
                            result[y * Columns + x] = (byte)(sample / 256);
                        }
                    }
                }
                finally { image.UnlockBits(bits); }
            }
        }

        private static int Luma(byte[] row, int i) { return 29 * row[i] + 150 * row[i + 1] + 77 * row[i + 2]; }

        internal PcbJob Prepare(PcbOptions options)
        {
            return new PcbJob(Width, Height, Columns, Rows, intensity, light, options);
        }
    }

    internal sealed class PcbJob
    {
        private const double ArtInset = 0.8, CopperInset = 0.4;
        private const double HoleDiameter = 3.0;
        private readonly double width, height, radius, dx, dy, holeOffset;
        private readonly int columns, rows;
        private readonly bool[][] artwork = new bool[2][];
        private readonly bool hole, exposedCopper;
        private readonly SolderMask mask;
        private readonly double[] coverage = new double[2];

        internal PcbJob(int w, int h, int columns, int rows, byte[][] intensity, bool[] light, PcbOptions options)
        {
            width = w / 10d;
            height = h / 10d;
            radius = options.Radius / 10d;
            hole = options.HangingHole;
            exposedCopper = options.ExposedCopper;
            mask = options.Mask;
            this.columns = columns;
            this.rows = rows;
            dx = width / columns;
            dy = height / rows;
            if (radius < 0 || radius > Math.Min(width, height) / 2)
                throw new InvalidDataException("圆角半径须在短边的一半以内。");
            // Move the drill inward on large radii to leave at least 1 mm of board at the corner.
            holeOffset = Math.Max(4.0, Math.Ceiling((radius - (radius - 2.5) / Math.Sqrt(2)) * 10) / 10);
            for (int side = 0; side < 2; side++)
                artwork[side] = MakeMask(intensity[side], options.Threshold, light[side], side);
        }

        private bool[] MakeMask(byte[] intensity, int threshold, bool light, int side)
        {
            var cells = new bool[intensity.Length];
            double holeX = side == 0 ? holeOffset : width - holeOffset;
            int allowed = 0, filled = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    double left = x * dx, top = y * dy, right = left + dx, bottom = top + dy;
                    double horizontal = Math.Min(left, width - right), vertical = Math.Min(top, height - bottom);
                    if (!Inside(horizontal, vertical, ArtInset)) continue;
                    if (hole)
                    {
                        double ox = Math.Max(0, Math.Max(left - holeX, holeX - right));
                        double oy = Math.Max(0, Math.Max(top - holeOffset, holeOffset - bottom));
                        if (ox * ox + oy * oy < 2.3 * 2.3) continue;
                    }
                    allowed++;
                    int i = y * columns + x;
                    if (light ? intensity[i] >= threshold : intensity[i] <= threshold)
                    {
                        cells[i] = true;
                        filled++;
                    }
                }
            coverage[side] = allowed == 0 ? 0 : filled / (double)allowed;
            return cells;
        }

        private bool Inside(double edgeX, double edgeY, double inset)
        {
            if (edgeX < inset || edgeY < inset) return false;
            double r = Math.Max(0, radius - inset);
            double x = Math.Max(0, radius - edgeX), y = Math.Max(0, radius - edgeY);
            return x * x + y * y <= r * r;
        }

        internal string PreviewNote(bool contactSide)
        {
            for (int side = 0; side < 2; side++)
                if (coverage[side] > .85) return (side == 0 ? "正面" : "反面") + "的大部分区域会印上图案，可调整深浅或阈值。";
            if (contactSide && (coverage[0] == 0 || coverage[1] == 0)) return "请检查预览中的文字是否完整。";
            return "加工前请确认图案距卡边至少 0.8 mm。";
        }

        internal bool HasArtwork { get { return coverage[0] > 0 || coverage[1] > 0; } }

        internal Bitmap Preview(int side)
        {
            Color board = mask == SolderMask.Green ? Color.FromArgb(19, 77, 58)
                : mask == SolderMask.Blue ? Color.FromArgb(20, 62, 120) : Color.FromArgb(30, 36, 45);
            Color ink = exposedCopper ? Color.FromArgb(225, 183, 104) : Color.FromArgb(247, 249, 247);
            return Preview(side, board, ink);
        }

        internal Bitmap Preview(int side, CardMaterial material)
        {
            switch (material)
            {
                case CardMaterial.Wood: return Preview(side, Color.FromArgb(234, 209, 171), Color.FromArgb(90, 54, 30));
                case CardMaterial.Acrylic: return Preview(side, Color.FromArgb(125, 153, 169), Color.FromArgb(250, 252, 253));
                case CardMaterial.Metal: return Preview(side, Color.FromArgb(207, 216, 224), Color.FromArgb(39, 49, 58));
                default: return Preview(side);
            }
        }

        private Bitmap Preview(int side, Color board, Color ink)
        {
            var result = new Bitmap(columns, rows, PixelFormat.Format32bppArgb);
            double holeX = side == 0 ? holeOffset : width - holeOffset;
            try
            {
                var data = result.LockBits(new Rectangle(Point.Empty, result.Size), ImageLockMode.WriteOnly, result.PixelFormat);
                try
                {
                    var row = new int[columns];
                    for (int y = 0; y < rows; y++)
                    {
                        double py = (y + .5) * dy;
                        for (int x = 0; x < columns; x++)
                        {
                            double px = (x + .5) * dx;
                            bool on = Inside(Math.Min(px, width - px), Math.Min(py, height - py), 0);
                            double hx = px - holeX, hy = py - holeOffset;
                            if (hole && hx * hx + hy * hy < HoleDiameter * HoleDiameter / 4) on = false;
                            row[x] = on ? (artwork[side][y * columns + x] ? ink.ToArgb() : board.ToArgb()) : 0;
                        }
                        Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
                    }
                }
                finally { result.UnlockBits(data); }
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        private Bitmap Engraving(int side)
        {
            int w = CardOutput.Pixels((int)Math.Round(width * 10)), h = CardOutput.Pixels((int)Math.Round(height * 10));
            var result = new Bitmap(w, h, PixelFormat.Format1bppIndexed);
            try
            {
                result.SetResolution(300, 300);
                var palette = result.Palette;
                palette.Entries[0] = Color.White;
                palette.Entries[1] = Color.Black;
                result.Palette = palette;
                var data = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, result.PixelFormat);
                try
                {
                    var row = new byte[Math.Abs(data.Stride)];
                    int[] cell = new int[w];
                    for (int x = 0; x < w; x++) cell[x] = x * columns / w;
                    for (int y = 0; y < h; y++)
                    {
                        Array.Clear(row, 0, row.Length);
                        int from = y * rows / h * columns;
                        for (int x = 0; x < w; x++)
                            if (artwork[side][from + cell[x]]) row[x >> 3] |= (byte)(128 >> (x & 7));
                        Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
                    }
                }
                finally { result.UnlockBits(data); }
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        internal void SaveLaser(string file, CardMaterial material)
        {
            Files.Atomic(file, stream =>
            {
                var zip = new GerberZip(stream);
                for (int side = 0; side < 2; side++)
                {
                    int face = side;
                    zip.AddBytes(side == 0 ? "QSL-Mark-Front.png" : "QSL-Mark-Back.png", data =>
                    {
                        // GDI+ requires a seekable stream for its PNG encoder.
                        using (var image = Engraving(face))
                        using (var buffer = new MemoryStream())
                        {
                            image.Save(buffer, ImageFormat.Png);
                            buffer.Position = 0;
                            buffer.CopyTo(data);
                        }
                    });
                }
                if (material != CardMaterial.Metal)
                    zip.Add("QSL-Cut.svg", WriteLaserOutline);
                zip.Add("QSL-Process.txt", text =>
                {
                    text.WriteLine(material == CardMaterial.Metal ? "已裁金属卡 · 激光打标" : material == CardMaterial.Wood ? "木片 · 激光雕刻" : "亚克力 · 激光刻字");
                    text.WriteLine("成品尺寸 " + Measure(width) + " × " + Measure(height) + " mm");
                    text.WriteLine("两张 300 DPI 黑白 PNG 为正、反面的刻字或打标图。黑色为加工区域。");
                    text.WriteLine("若加工软件忽略 DPI，请把图像的实际宽、高设为上述尺寸。");
                    text.WriteLine(material == CardMaterial.Metal
                        ? "按对应尺寸选用已裁卡坯，分别打标正反面。"
                        : "QSL-Cut.svg 是正面坐标的裁切线。先刻字再裁切。做双面时沿左或右边翻面，反面挂孔在右侧。");
                });
                zip.Finish();
            });
        }

        private void WriteLaserOutline(TextWriter text)
        {
            string w = Measure(width), h = Measure(height), r = Measure(radius);
            text.WriteLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}mm\" height=\"{1}mm\" viewBox=\"0 0 {0} {1}\">", w, h);
            text.WriteLine("  <rect x=\"0\" y=\"0\" width=\"{0}\" height=\"{1}\" rx=\"{2}\" ry=\"{2}\" fill=\"none\" stroke=\"#ff0000\" stroke-width=\"0.01\"/>", w, h, r);
            if (hole) text.WriteLine("  <circle cx=\"{0}\" cy=\"{0}\" r=\"{1}\" fill=\"none\" stroke=\"#ff0000\" stroke-width=\"0.01\"/>", Measure(holeOffset), Measure(HoleDiameter / 2));
            text.WriteLine("</svg>");
        }

        private static string Measure(double value) { return value.ToString("0.0#", Files.Inv); }

        internal void Save(string file)
        {
            Files.Atomic(file, stream =>
            {
                var zip = new GerberZip(stream);
                zip.Add("QSL.GKO", text =>
                {
                    var gerber = new PcbGerber(text, "Profile,NP");
                    gerber.Outline(width, height, radius, 0);
                    gerber.End();
                });
                for (int side = 0; side < 2; side++)
                {
                    int face = side;
                    string placement = side == 0 ? "Top" : "Bot", prefix = side == 0 ? "T" : "B";
                    zip.Add("QSL.G" + prefix + "L", text =>
                    {
                        string layer = face == 0 ? "L1,Top" : "L2,Bot";
                        var gerber = new PcbGerber(text, "Copper," + layer, false, hole ? 4.0 : 0);
                        gerber.Area(() => gerber.Outline(width, height, radius, CopperInset));
                        if (hole) gerber.Hole(holeOffset, height - holeOffset, true);
                        gerber.End();
                    });
                    zip.Add("QSL.G" + prefix + "S", text =>
                    {
                        var gerber = new PcbGerber(text, "Soldermask," + placement, true, hole ? 3.6 : 0);
                        if (exposedCopper) PlotArtwork(gerber, face);
                        if (hole) gerber.Hole(holeOffset, height - holeOffset, false);
                        gerber.End();
                    });
                    zip.Add("QSL.G" + prefix + "O", text =>
                    {
                        var gerber = new PcbGerber(text, "Legend," + placement);
                        if (!exposedCopper) PlotArtwork(gerber, face);
                        gerber.End();
                    });
                }
                if (hole) zip.Add("QSL-NPTH.DRL", WriteDrill);
                zip.Add("QSL-Fabrication.md", WriteNotes);
                zip.Finish();
            });
        }

        private void PlotArtwork(PcbGerber gerber, int side)
        {
            // Merge identical horizontal runs across neighbouring rows.
            int stride = columns + 1;
            var active = new Dictionary<int, int>();
            var next = new Dictionary<int, int>();
            for (int y = 0; y <= rows; y++)
            {
                if (y < rows)
                {
                    int x = 0;
                    while (x < columns)
                    {
                        if (!artwork[side][y * columns + x]) { x++; continue; }
                        int start = x++;
                        while (x < columns && artwork[side][y * columns + x]) x++;
                        int key = start * stride + x;
                        int first;
                        if (active.TryGetValue(key, out first)) active.Remove(key);
                        else first = y;
                        next.Add(key, first);
                    }
                }
                foreach (var run in active)
                {
                    double left = run.Key / stride * dx, right = (run.Key % stride) * dx;
                    // Gerber layers share the view from the top of the board.
                    if (side == 1) { double oldLeft = left; left = width - right; right = width - oldLeft; }
                    gerber.Rectangle(left, height - y * dy, right, height - run.Value * dy);
                }
                active.Clear();
                var previous = active;
                active = next;
                next = previous;
            }
        }

        private void WriteDrill(TextWriter text)
        {
            text.WriteLine("M48");
            text.WriteLine("; #@! TF.FileFunction,NonPlated,1,2,NPTH,Drill");
            text.WriteLine("FMAT,2");
            text.WriteLine("METRIC");
            text.WriteLine("G90");
            text.WriteLine("T1C" + HoleDiameter.ToString("0.000", Files.Inv));
            text.WriteLine("%");
            text.WriteLine("G05");
            text.WriteLine("T1");
            text.WriteLine("X" + holeOffset.ToString("0.000", Files.Inv) + "Y" + (height - holeOffset).ToString("0.000", Files.Inv));
            text.WriteLine("M30");
        }

        private void WriteNotes(TextWriter text)
        {
            text.WriteLine("# QSL PCB 制板说明");
            text.WriteLine();
            text.WriteLine("- 双层 FR-4，建议成品厚度 1.0 mm。");
            text.WriteLine("- 成品尺寸 " + width.ToString("0.0", Files.Inv) + " × " + height.ToString("0.0", Files.Inv) + " mm，圆角 R" + radius.ToString("0.0", Files.Inv) + " mm。");
            text.WriteLine("- 下单时选择" + (mask == SolderMask.Green ? "绿色" : mask == SolderMask.Blue ? "蓝色" : "黑色") + "阻焊。");
            text.WriteLine(exposedCopper ? "- 图案在阻焊开窗层，金色效果请选沉金 ENIG。" : "- 图案在双面丝印层，请选白色丝印。");
            text.WriteLine(hole ? "- 正面左上为直径 3.0 mm 的非金属化挂孔，孔心距两边各 " + holeOffset.ToString("0.0", Files.Inv) + " mm。" : "- 孔位数量为 0。");
            text.WriteLine("- 图层坐标统一从正面俯视。背面读字请在查看器中切换到底面。");
            text.WriteLine("- 图案使用约 0.20 mm 的加工单元，小字和密集标志建议放大后再制板。");
            text.WriteLine("- 下单前在板厂预览核对成品大小、图案和加工选择。");
        }
    }

    internal sealed class PcbGerber
    {
        private readonly TextWriter text;
        internal PcbGerber(TextWriter text, string function, bool negative = false, double holeDiameter = 0)
        {
            this.text = text;
            text.WriteLine("%FSLAX36Y36*%");
            text.WriteLine("%MOMM*%");
            text.WriteLine("%TF.Part,Single*%");
            text.WriteLine("%TF.FileFunction," + function + "*%");
            text.WriteLine("%TF.FilePolarity," + (negative ? "Negative" : "Positive") + "*%");
            text.WriteLine("%ADD10C,0.050*%");
            if (holeDiameter > 0) text.WriteLine("%ADD11C," + holeDiameter.ToString("0.000", Files.Inv) + "*%");
            text.WriteLine("%LPD*%");
            text.WriteLine("D10*");
            text.WriteLine("G75*");
            text.WriteLine("G01*");
        }

        private static string Coord(double value) { return Math.Round(value * 1000000).ToString("0", Files.Inv); }
        private void Point(double x, double y, string operation)
        { text.WriteLine("X" + Coord(x) + "Y" + Coord(y) + operation + "*"); }
        private void Arc(double x, double y, double i, double j)
        {
            text.WriteLine("G03X" + Coord(x) + "Y" + Coord(y) + "I" + Coord(i) + "J" + Coord(j) + "D01*");
            text.WriteLine("G01*");
        }
        internal void End() { text.WriteLine("M02*"); }

        internal void Hole(double x, double y, bool clear)
        {
            if (clear) text.WriteLine("%LPC*%");
            text.WriteLine("D11*");
            Point(x, y, "D03");
        }

        internal void Area(Action draw)
        {
            text.WriteLine("G36*");
            draw();
            text.WriteLine("G37*");
        }

        internal void Rectangle(double left, double bottom, double right, double top)
        {
            text.WriteLine("G36*");
            Point(left, bottom, "D02");
            Point(right, bottom, "D01");
            Point(right, top, "D01");
            Point(left, top, "D01");
            Point(left, bottom, "D01");
            text.WriteLine("G37*");
        }

        internal void Outline(double width, double height, double radius, double inset)
        {
            double left = inset, right = width - inset, bottom = inset, top = height - inset;
            double r = Math.Max(0, radius - inset);
            bool horizontal = right - left > 2 * r, vertical = top - bottom > 2 * r;
            Point(left + r, bottom, "D02");
            if (horizontal) Point(right - r, bottom, "D01");
            if (r > 0) Arc(right, bottom + r, 0, r);
            if (vertical) Point(right, top - r, "D01");
            if (r > 0) Arc(right - r, top, -r, 0);
            if (horizontal) Point(left + r, top, "D01");
            if (r > 0) Arc(left, top - r, 0, -r);
            if (vertical) Point(left, bottom + r, "D01");
            if (r > 0) Arc(left + r, bottom, r, 0);
        }
    }
}
