using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace QslStudio
{
    internal static class Files
    {
        internal const int MaxProjectBytes = 64 * 1024 * 1024;
        internal static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static void Atomic(string path, Action<Stream> write, bool overwrite = true)
        {
            string full = Path.GetFullPath(path);
            string temp = Path.Combine(Path.GetDirectoryName(full), ".qsl-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) write(stream);
                DateTime saved = DateTime.UtcNow;
                File.SetLastWriteTimeUtc(temp, saved);
                if (overwrite && File.Exists(full))
                {
                    // Windows preserves the destination's creation time when replacing it.
                    bool repairCreationTime = File.GetCreationTimeUtc(full).Year < 1980;
                    File.Replace(temp, full, null);
                    if (repairCreationTime) File.SetCreationTimeUtc(full, saved);
                }
                else File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static string Attr(XElement e, string name, string fallback)
        { var a = e.Attribute(name); return a == null ? fallback : a.Value; }
        internal static float Number(XElement e, string name, float fallback, float low, float high)
        {
            float val;
            if (!float.TryParse(Attr(e, name, ""), NumberStyles.Float, Inv, out val) || float.IsNaN(val) || float.IsInfinity(val)) val = fallback;
            return Math.Max(low, Math.Min(val, high));
        }
        internal static string Cut(string value, int max) { return value.Length > max ? value.Substring(0, max) : value; }
        internal static string Hex(Color c) { return (c.ToArgb() & 0xffffff).ToString("X6", Inv); }
        internal static Color ReadColor(string s, Color fallback)
        {
            int v;
            return s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, Inv, out v) ? Color.FromArgb(v | unchecked((int)0xff000000)) : fallback;
        }
        internal static void Save(Project p, string path, int? templateSide = null)
        {
            var doc = new XDocument(ProjectXml(p, templateSide));
            Atomic(path, s => doc.Save(s));
        }

        internal static XElement ProjectXml(Project p, int? templateSide = null)
        {
            var values = new XElement("values");
            if (!templateSide.HasValue)
                foreach (var f in Field.All) values.Add(new XElement("value", new XAttribute("key", f.Key), p.Value(f.Key)));
            var assets = new XElement("assets");
            var assetMap = new Dictionary<Art, string>();
            var layers = new XElement("layers");
            long total = 0;
            bool hasPicturePlacement = false;
            bool hasDrawing = Templates.IsBlank(p.TemplateOn(templateSide ?? 0)) || !templateSide.HasValue && Templates.IsBlank(p.TemplateOn(1));
            foreach (var l in p.Layers)
            {
                if (templateSide.HasValue && (l.Side != templateSide.Value || l.Cover && l.Name == "示例照片")) continue;
                string id = "";
                if (l.Image != null && !assetMap.TryGetValue(l.Image, out id))
                {
                    total += l.Image.Bytes.Length;
                    if (total > 36 * 1024 * 1024) throw new InvalidDataException("工程中的图片超过 36 MB，请移除或缩小部分图片。");
                    id = (assetMap.Count + 1).ToString(Inv);
                    assetMap.Add(l.Image, id);
                    assets.Add(new XElement("asset", new XAttribute("id", id), Convert.ToBase64String(l.Image.Bytes)));
                }
                var layer = new XElement("layer", new XAttribute("id", l.Id), new XAttribute("name", l.Name),
                    new XAttribute("side", l.Side), new XAttribute("kind", l.Kind), new XAttribute("field", l.Field),
                    new XAttribute("x", l.X), new XAttribute("y", l.Y), new XAttribute("w", l.W), new XAttribute("h", l.H),
                    new XAttribute("size", l.Size), new XAttribute("font", l.Font), new XAttribute("style", (int)l.Style),
                    new XAttribute("align", (int)l.Align), new XAttribute("color", Hex(l.Color)), new XAttribute("fit", l.Fit),
                    new XAttribute("hidden", l.Hidden), new XAttribute("cover", l.Cover), new XAttribute("symbol", l.Symbol),
                    new XAttribute("asset", id ?? ""), new XElement("text", l.Text));
                if (l.Kind == LayerKind.Picture && (l.ImageFit != PictureFit.Auto || !l.ImageCrop.IsEmpty))
                {
                    hasPicturePlacement = true;
                    layer.SetAttributeValue("imageFit", l.ImageFit);
                    if (!l.ImageCrop.IsEmpty)
                    {
                        layer.SetAttributeValue("cropX", l.ImageCrop.X);
                        layer.SetAttributeValue("cropY", l.ImageCrop.Y);
                        layer.SetAttributeValue("cropWidth", l.ImageCrop.Width);
                        layer.SetAttributeValue("cropHeight", l.ImageCrop.Height);
                    }
                }
                if (CardDrawing.IsDrawing(l))
                {
                    hasDrawing = true;
                    if (l.StrokePoints != null)
                    {
                        var stroke = new XElement("stroke");
                        foreach (var point in l.StrokePoints)
                            stroke.Add(new XElement("point", new XAttribute("x", point.X), new XAttribute("y", point.Y)));
                        layer.Add(stroke);
                    }
                }
                layers.Add(layer);
            }
            var root = new XElement("qslcard", new XAttribute("version", "1"), new XAttribute("template", p.Template), new XAttribute("customized", p.Customized), new XAttribute("example", p.Example),
                new XAttribute("front", Hex(p.Background[0])), new XAttribute("back", Hex(p.Background[1])), values, assets, layers);
            if (p.ReverseTemplate != null) root.SetAttributeValue("reverseTemplate", p.ReverseTemplate);
            if (templateSide.HasValue)
                root = new XElement("qslside", new XAttribute("version", "1"), new XAttribute("side", templateSide.Value),
                    new XAttribute("template", p.TemplateOn(templateSide.Value)),
                    new XAttribute(templateSide.Value == 0 ? "front" : "back", Hex(p.Background[templateSide.Value])), assets, layers);
            if (p.HasCustomSize)
            {
                root.SetAttributeValue("version", "2");
                root.SetAttributeValue("widthTenthMm", p.Width);
                root.SetAttributeValue("heightTenthMm", p.Height);
            }
            if (hasDrawing) root.SetAttributeValue("version", "4");
            else if (hasPicturePlacement) root.SetAttributeValue("version", "3");
            return root;
        }

        internal static Project Open(string path)
        {
            int side;
            return ReadProject(path, false, out side);
        }
        internal static Project OpenSide(string path, out int side) { return ReadProject(path, true, out side); }
        private static Project ReadProject(string path, bool single, out int face)
        {
            return ReadProject(ReadXml(path, MaxProjectBytes), single, out face);
        }

        internal static XElement ReadXml(string path, int maxBytes)
        {
            if (new FileInfo(path).Length > maxBytes) throw new InvalidDataException("工程太大，无法载入。");
            XDocument doc;
            using (var f = File.OpenRead(path))
            using (var r = XmlReader.Create(f, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = maxBytes }))
                doc = XDocument.Load(r);
            return doc.Root;
        }

        internal static Project ReadProject(XElement root, bool single, out int face)
        {
            face = -1;
            string version = root == null ? "" : Attr(root, "version", "");
            if (root == null || root.Name != (single ? "qslside" : "qslcard") ||
                (version != "1" && version != "2" && version != "3" && version != "4") || root.Element("layers") == null)
                throw new InvalidDataException(single ? "请选择有效的单面模板（.qslside）。" : "请选择有效的 QSL 卡片工程（.qslcard）。单面模板可从「模板」打开。");
            if (single && (!int.TryParse(Attr(root, "side", ""), out face) || face < 0 || face > 1))
                throw new InvalidDataException("单面模板缺少正反面标记。");
            string template = Attr(root, "template", "photo");
            if (!Templates.IsKnown(template)) throw new InvalidDataException("工程使用了未知的模板版本。");
            string reverse = Attr(root, "reverseTemplate", null);
            if (reverse != null && !Templates.IsKnown(reverse)) throw new InvalidDataException("工程的反面使用了未知的模板版本。");
            if (reverse != null && Templates.IsPortrait(template) != Templates.IsPortrait(reverse))
                throw new InvalidDataException("工程的正反面尺寸不一致。");
            var p = new Project { Template = template, ReverseTemplate = reverse, Customized = Attr(root, "customized", "false") == "true" };
            if (version == "2" || (version == "3" || version == "4") &&
                (root.Attribute("widthTenthMm") != null || root.Attribute("heightTenthMm") != null))
            {
                int width, height;
                if (!int.TryParse(Attr(root, "widthTenthMm", ""), NumberStyles.None, Inv, out width)
                    || !int.TryParse(Attr(root, "heightTenthMm", ""), NumberStyles.None, Inv, out height)
                    || !Project.ValidPageSize(width, height))
                    throw new InvalidDataException("工程中的宽高无效。");
                p.SetPageSize(width, height);
            }
            p.Background[0] = ReadColor(Attr(root, "front", "FFFFFF"), Color.White);
            p.Background[1] = ReadColor(Attr(root, "back", "FFFFFF"), Color.White);
            var values = root.Element("values");
            if (values != null) foreach (var v in values.Elements("value"))
            {
                string key = Attr(v, "key", "");
                if (Field.Find(key) != null)
                    p.Values[key] = key == "postcode" ? Field.PostalDigits(v.Value) : Cut(v.Value, Field.Limit(key));
            }
            string example = Attr(root, "example", null);
            p.Example = example == "true" || example == null && (p.Value("own") == "DEMO-01" || p.Value("other") == "DEMO-02" || p.Value("operator") == "示例操作员");
            var encoded = new Dictionary<string, XElement>();
            var assets = root.Element("assets");
            if (assets != null) foreach (var a in assets.Elements("asset"))
            {
                string key = Attr(a, "id", "");
                if (encoded.Count >= Project.MaxLayers || key.Length == 0 || encoded.ContainsKey(key))
                    throw new InvalidDataException("工程的图片索引无效。");
                encoded.Add(key, a);
            }
            var decoded = new Dictionary<string, Art>();
            var ids = new HashSet<string>();
            long pixels = 0;
            foreach (var el in root.Element("layers").Elements("layer"))
            {
                if (p.Layers.Count >= Project.MaxLayers) throw new InvalidDataException("工程的元素数量太多。");
                LayerKind kind;
                if (!Enum.TryParse<LayerKind>(Attr(el, "kind", ""), out kind) || !Enum.IsDefined(typeof(LayerKind), kind))
                    throw new InvalidDataException("工程中有不支持的元素类型。");
                var layer = new Layer { Kind = kind };
                layer.Id = Cut(Attr(el, "id", layer.Id), 60);
                if (!ids.Add(layer.Id)) throw new InvalidDataException("工程的元素索引有重复。");
                layer.Name = Cut(Attr(el, "name", "元素"), 64);
                layer.Side = (int)Number(el, "side", 0, 0, 1);
                if (single && layer.Side != face) throw new InvalidDataException("单面模板含有另一面的图层。");
                layer.Field = Attr(el, "field", "");
                if (Field.Find(layer.Field) == null) layer.Field = "";
                layer.Text = Cut(el.Element("text") == null ? "" : el.Element("text").Value, 420);
                layer.X = Number(el, "x", 0, -p.Width, p.Width - 1);
                layer.Y = Number(el, "y", 0, -p.Height, p.Height - 1);
                layer.W = Number(el, "w", 300, 1, 2 * p.Width);
                layer.H = Number(el, "h", 75, 1, 2 * p.Height);
                layer.Size = Number(el, "size", 12, 1, 180);
                layer.Font = Cut(Attr(el, "font", "Arial"), 125);
                layer.Style = (FontStyle)(int)Number(el, "style", 0, 0, 3);
                layer.Align = (StringAlignment)(int)Number(el, "align", 0, 0, 2);
                layer.Color = ReadColor(Attr(el, "color", "182C2B"), layer.Color);
                layer.Fit = Attr(el, "fit", "true") == "true";
                layer.Hidden = Attr(el, "hidden", "false") == "true";
                layer.Cover = Attr(el, "cover", "false") == "true";
                layer.Symbol = Cut(Attr(el, "symbol", "radio"), 30);
                if (kind == LayerKind.Picture && (version == "3" || version == "4"))
                {
                    PictureFit fit;
                    if (Enum.TryParse(Attr(el, "imageFit", ""), out fit) && Enum.IsDefined(typeof(PictureFit), fit))
                        layer.ImageFit = fit;
                    float x = Number(el, "cropX", 0, 0, 1), y = Number(el, "cropY", 0, 0, 1);
                    float w = Number(el, "cropWidth", 0, 0, 1 - x), h = Number(el, "cropHeight", 0, 0, 1 - y);
                    if (w >= .000001f && h >= .000001f) layer.ImageCrop = new RectangleF(x, y, w, h);
                }
                var stroke = el.Element("stroke");
                if (version == "4" && CardDrawing.IsDrawing(layer) && stroke != null)
                {
                    var points = new List<PointF>();
                    foreach (var point in stroke.Elements("point"))
                    {
                        if (points.Count == CardDrawing.MaxPoints) throw new InvalidDataException("工程中的手绘笔迹点数过多。");
                        points.Add(new PointF(Number(point, "x", 0, 0, 1), Number(point, "y", 0, 0, 1)));
                    }
                    layer.StrokePoints = points.ToArray();
                }
                string asset = Attr(el, "asset", "");
                if (kind == LayerKind.Picture && asset.Length > 0)
                {
                    Art a;
                    if (!decoded.TryGetValue(asset, out a))
                    {
                        XElement e;
                        if (!encoded.TryGetValue(asset, out e)) throw new InvalidDataException("工程缺少图片资源。");
                        a = new Art(Convert.FromBase64String(e.Value));
                        pixels += (long)a.Bitmap.Width * a.Bitmap.Height;
                        if (pixels > 40000000) throw new InvalidDataException("工程内图片像素总量太大。");
                        decoded.Add(asset, a);
                    }
                    layer.Image = a;
                }
                p.Layers.Add(layer);
            }
            return p;
        }
    }

    internal static class CardOutput
    {
        internal static Project ForHandwriting(Project source)
        {
            var output = source.Copy();
            foreach (var field in Field.All) if (!field.Station) output.Values[field.Key] = "";
            output.Values["signature"] = "";
            return output;
        }
        private const int Dpi = 300, Bleed = 20;
        internal static int Pixels(int size) { return (int)Math.Round(size * Dpi / 254d); }
        private static string Points(int size) { return (size * 72d / 254).ToString("0.000000", Files.Inv); }

        internal static Bitmap Render(Project p, int side)
        {
            return RenderPage(p, side, null);
        }

        internal static Bitmap RenderOverprint(Project p, int side, PointF offset)
        {
            return RenderPage(p, side, offset);
        }

        private static Bitmap RenderPage(Project p, int side, PointF? offset)
        {
            var b = new Bitmap(Pixels(p.Width), Pixels(p.Height), PixelFormat.Format24bppRgb);
            try
            {
                b.SetResolution(Dpi, Dpi);
                using (var g = Graphics.FromImage(b))
                {
                    var page = new RectangleF(0, 0, b.Width, b.Height);
                    if (offset.HasValue)
                    {
                        g.Clear(Color.White);
                        CardPainter.PaintOverprint(g, p, side, page, offset.Value);
                    }
                    else CardPainter.Paint(g, p, side, page);
                }
                return b;
            }
            catch { b.Dispose(); throw; }
        }
        internal static void WriteJpeg(Bitmap bitmap, Stream output, long quality)
        {
            ImageCodecInfo jpeg = Array.Find(ImageCodecInfo.GetImageEncoders(), x => x.FormatID == ImageFormat.Jpeg.Guid);
            using (var pars = new EncoderParameters(1))
            {
                pars.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bitmap.Save(output, jpeg, pars);
            }
        }
        internal static void SaveImage(Project p, int side, string file, bool png)
        {
            using (var b = Render(p, side)) Files.Atomic(file, s => { if (png) b.Save(s, ImageFormat.Png); else WriteJpeg(b, s, 96L); });
        }
        internal static void SavePdf(Project p, int[] sides, string file, bool bleed = false)
        {
            WriteCards(p, file, sides.Length, bleed, i => RenderPdfPage(p, sides[i], bleed), null);
        }
        internal static void SaveOverprint(Project card, int side, PointF offset, string file)
        {
            if (!CardPainter.HasOverprintContent(card, side, offset))
                throw new InvalidDataException("卡片范围内没有可套印的资料。请检查所选卡面和偏移量。");
            WriteCards(card, file, 1, false, i => RenderOverprint(card, side, offset), null, true);
        }

        internal static void SaveBatch(Project original, IList<Qso> records, string file, Action<int> progress)
        {
            Qso.CheckBatch(original, records);
            var card = original.Copy();
            WriteCards(card, file, records.Count * 2, false, i =>
            {
                if (i % 2 == 0) records[i / 2].Apply(card);
                return Render(card, i % 2);
            }, progress);
        }
        internal static void SaveAddressSheet(Project project, string file)
        {
            string address = project.Value("address").Trim();
            if (address.Length == 0) throw new InvalidDataException("请先在「本台」填写邮寄地址。");
            string name = (project.StationCall + "   " + project.Value("operator").Trim()).Trim();
            const string width = "595.275591", height = "841.889764";
            WritePdf(file, 1, width, height, "0 0 " + width + " " + height, i => RenderAddressSheet(name, address), null);
        }
        private static Bitmap RenderAddressSheet(string name, string address)
        {
            var page = new Bitmap(2480, 3508, PixelFormat.Format24bppRgb);
            try
            {
                page.SetResolution(300, 300);
                using (var g = Graphics.FromImage(page))
                using (var edge = new Pen(Color.FromArgb(186, 190, 192), 1.5f))
                {
                    g.Clear(Color.White);
                    g.ScaleTransform(page.Width / 2100f, page.Height / 2970f);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    for (int row = 0; row < 7; row++)
                    for (int column = 0; column < 2; column++)
                    {
                        int x = 145 + column * 930, y = 145 + row * 390;
                        g.DrawRectangle(edge, x, y, 880, 340);
                        CardPainter.PaintAddress(g, name, address, x, y);
                    }
                }
                return page;
            }
            catch { page.Dispose(); throw; }
        }
        private static void WriteCards(Project p, string file, int count, bool bleed,
            Func<int, Bitmap> render, Action<int> progress, bool transparentPaper = false)
        {
            int margin = bleed ? Bleed : 0;
            string w = Points(p.Width + 2 * margin), h = Points(p.Height + 2 * margin);
            string trim = bleed ? string.Join(" ", Points(margin), Points(margin), Points(p.Width + margin), Points(p.Height + margin)) : "0 0 " + w + " " + h;
            WritePdf(file, count, w, h, trim, render, progress, transparentPaper);
        }
        private static void WritePdf(string file, int count, string w, string h, string trim,
            Func<int, Bitmap> render, Action<int> progress, bool transparentPaper = false)
        {
            Files.Atomic(file, stream =>
            {
                int info = 3 + count * 3;
                var offsets = new long[info + 1];
                Raw(stream, "%PDF-1.4\n");
                stream.Write(new byte[] { 37, 226, 227, 207, 211, 10 }, 0, 6);
                Obj(stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
                var kids = new StringBuilder();
                for (int i = 0; i < count; i++) kids.Append((3 + i * 3).ToString(Files.Inv)).Append(" 0 R ");
                Obj(stream, offsets, 2, "<< /Type /Pages /Count " + count.ToString(Files.Inv) + " /Kids [ " + kids + "] >>");
                for (int i = 0; i < count; i++)
                {
                    if (progress != null) progress(i);
                    int page = 3 + i * 3, content = page + 1, img = page + 2;
                    Obj(stream, offsets, page, string.Format(Files.Inv,
                        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0} {1}] /BleedBox [0 0 {0} {1}] /TrimBox [{4}] /Resources << /XObject << /Card {2} 0 R >> >> /Contents {3} 0 R >>", w, h, img, content, trim));
                    byte[] commands = Encoding.ASCII.GetBytes("q\n" + w + " 0 0 " + h + " 0 0 cm\n/Card Do\nQ\n");
                    Blob(stream, offsets, content, "", commands);
                    using (var b = render(i))
                    {
                        string dict = string.Format(Files.Inv, "/Type /XObject /Subtype /Image /Width {0} /Height {1} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode", b.Width, b.Height);
                        // A color-key mask leaves the preprinted paper undisturbed.
                        if (transparentPaper) dict += " /Mask [255 255 255 255 255 255]";
                        Blob(stream, offsets, img, dict, ZlibRgb(b));
                    }
                }
                string date = DateTime.UtcNow.ToString("'D:'yyyyMMddHHmmss'Z'", Files.Inv);
                Obj(stream, offsets, info, "<< /CreationDate (" + date + ") /ModDate (" + date + ") >>");
                if (progress != null) progress(count);
                long xref = stream.Position;
                Raw(stream, "xref\n0 " + offsets.Length.ToString(Files.Inv) + "\n0000000000 65535 f \n");
                for (int j = 1; j < offsets.Length; j++) Raw(stream, offsets[j].ToString("D10", Files.Inv) + " 00000 n \n");
                Raw(stream, "trailer\n<< /Root 1 0 R /Info " + info.ToString(Files.Inv) +
                    " 0 R /Size " + offsets.Length.ToString(Files.Inv) + " >>\nstartxref\n" + xref.ToString(Files.Inv) + "\n%%EOF\n");
            });
        }
        private static Bitmap RenderPdfPage(Project p, int side, bool bleed)
        {
            var trim = Render(p, side);
            if (!bleed) return trim;
            // Reflect the artwork only outside the cut. Keep the original pixels at the trim size.
            var page = new Bitmap(Pixels(p.Width + 2 * Bleed), Pixels(p.Height + 2 * Bleed), PixelFormat.Format24bppRgb);
            page.SetResolution(Dpi, Dpi);
            using (trim)
            {
                var source = trim.LockBits(new Rectangle(0, 0, trim.Width, trim.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var target = page.LockBits(new Rectangle(0, 0, page.Width, page.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        int left = (page.Width - trim.Width) / 2, top = (page.Height - trim.Height) / 2;
                        byte[] row = new byte[trim.Width * 3], extended = new byte[page.Width * 3];
                        for (int y = 0; y < page.Height; y++)
                        {
                            int srcY = Reflected(y - top, trim.Height);
                            Marshal.Copy(IntPtr.Add(source.Scan0, srcY * source.Stride), row, 0, row.Length);
                            Buffer.BlockCopy(row, 0, extended, 3 * left, row.Length);
                            for (int x = 0; x < page.Width; x++)
                            {
                                if (x >= left && x < left + trim.Width) continue;
                                Buffer.BlockCopy(row, Reflected(x - left, trim.Width) * 3, extended, x * 3, 3);
                            }
                            Marshal.Copy(extended, 0, IntPtr.Add(target.Scan0, y * target.Stride), extended.Length);
                        }
                    }
                    finally { page.UnlockBits(target); }
                }
                finally { trim.UnlockBits(source); }
            }
            return page;
        }
        private static int Reflected(int n, int length) { return n < 0 ? -n - 1 : n >= length ? 2 * length - n - 1 : n; }
        private static byte[] ZlibRgb(Bitmap b)
        {
            using (var output = new MemoryStream())
            {
                output.WriteByte(0x78); output.WriteByte(0x9c);
                uint a = 1, d = 0;
                var data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var row = new byte[b.Width * 3];
                    using (var def = new DeflateStream(output, CompressionMode.Compress, true))
                    for (int y = 0; y < b.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                        for (int x = 0; x < row.Length; x += 3) { byte swap = row[x]; row[x] = row[x + 2]; row[x + 2] = swap; }
                        foreach (byte v in row) { a = (a + v) % 65521u; d = (d + a) % 65521u; }
                        def.Write(row, 0, row.Length);
                    }
                }
                finally { b.UnlockBits(data); }
                output.WriteByte((byte)(d >> 8)); output.WriteByte((byte)d);
                output.WriteByte((byte)(a >> 8)); output.WriteByte((byte)a);
                return output.ToArray();
            }
        }
        private static void Raw(Stream s, string value)
        { byte[] b = Encoding.ASCII.GetBytes(value); s.Write(b, 0, b.Length); }
        private static void Obj(Stream s, long[] offsets, int id, string content)
        { offsets[id] = s.Position; Raw(s, id.ToString(Files.Inv) + " 0 obj\n" + content + "\nendobj\n"); }
        private static void Blob(Stream s, long[] offsets, int id, string dict, byte[] data)
        {
            offsets[id] = s.Position;
            Raw(s, id.ToString(Files.Inv) + " 0 obj\n<< " + dict + " /Length " + data.Length.ToString(Files.Inv) + " >>\nstream\n");
            s.Write(data, 0, data.Length);
            Raw(s, "\nendstream\nendobj\n");
        }
    }
}
