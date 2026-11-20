using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace QslStudio
{
    internal static class CallsignBadge
    {
        private static readonly Color Blue = Color.FromArgb(20, 102, 223);
        internal static string Normalize(string call) { return call.Trim().ToUpperInvariant(); }

        internal static bool Valid(string call)
        {
            if (call.Length == 0 || call.Length > 16) return false;
            bool alphanumeric = false;
            foreach (char c in call)
            {
                if (c >= 'A' && c <= 'Z' || c >= '0' && c <= '9') alphanumeric = true;
                else if (c != '/' && c != '-') return false;
            }
            return alphanumeric;
        }

        internal static Bitmap Draw(Art circle, string callsign)
        {
            var image = new Bitmap(circle.Bitmap);
            if (!Valid(callsign)) return image;
            try
            {
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    DrawArc(graphics, image.Size, callsign, true);
                }
                return image;
            }
            catch { image.Dispose(); throw; }
        }

        internal static Art MakeRound(Art crest)
        {
            using (var image = new Bitmap(800, 800, PixelFormat.Format24bppRgb))
            using (var graphics = Graphics.FromImage(image))
            using (var pen = new Pen(Blue, 5f))
            {
                graphics.Clear(Color.White);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawEllipse(pen, 5, 5, 790, 790);
                graphics.DrawEllipse(pen, 109, 109, 582, 582);
                var box = PictureLayout.FitInside(new RectangleF(242, 125, 316, 550),
                    crest.Bitmap.Width, crest.Bitmap.Height);
                graphics.DrawImage(crest.Bitmap, box);
                DrawArc(graphics, image.Size, "CHINESE RADIO AMATEURS CLUB", false);
                using (var bytes = new MemoryStream())
                {
                    image.Save(bytes, ImageFormat.Jpeg);
                    return new Art(bytes.ToArray());
                }
            }
        }

        private static void DrawArc(Graphics g, Size bounds, string text, bool lower)
        {
            float size = Math.Min(bounds.Width, bounds.Height), radius = size * .425f;
            float em = size * (lower ? .096f : .103f), gap = size * .011f, length = -gap;
            float direction = lower ? 1 : -1;
            int style = (int)(lower ? FontStyle.Bold : FontStyle.Regular);
            using (var family = new FontFamily("Arial"))
            using (var brush = new SolidBrush(Blue))
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (var glyph = new GraphicsPath())
            {
                var widths = new float[text.Length];
                for (int i = 0; i < text.Length; i++)
                {
                    glyph.Reset();
                    if (text[i] != ' ') glyph.AddString(text[i].ToString(), family, style, em, PointF.Empty, format);
                    widths[i] = text[i] == ' ' ? em * .3f : glyph.GetBounds().Width;
                    length += widths[i] + gap;
                }
                float shrink = Math.Min(1, radius * (lower ? 1.09f : 4.4f) / length), offset = -length / 2;
                for (int i = 0; i < text.Length; i++)
                {
                    float angle = (offset + widths[i] / 2) * shrink / radius;
                    offset += widths[i] + gap;
                    if (text[i] == ' ') continue;
                    glyph.Reset();
                    glyph.AddString(text[i].ToString(), family, style, em, PointF.Empty, format);
                    var glyphBounds = glyph.GetBounds();
                    var state = g.Save();
                    g.TranslateTransform(bounds.Width / 2f + radius * (float)Math.Sin(angle),
                        bounds.Height / 2f + direction * radius * (float)Math.Cos(angle));
                    g.RotateTransform(-direction * angle * 180f / (float)Math.PI);
                    g.ScaleTransform(shrink, shrink);
                    g.TranslateTransform(-glyphBounds.Left - glyphBounds.Width / 2, -glyphBounds.Top - glyphBounds.Height / 2);
                    g.FillPath(brush, glyph);
                    g.Restore(state);
                }
            }
        }

        internal static Art Encode(Bitmap image)
        {
            using (var bytes = new MemoryStream())
            {
                image.Save(bytes, ImageFormat.Png);
                return new Art(bytes.ToArray());
            }
        }
    }

    internal sealed class LogoSource
    {
        internal readonly string Key, Name, Caption, Policy, Original, Extension, Group, Style;
        internal readonly bool Preload;
        internal LogoSource(string key, string name, string caption, string policy, string original, string extension = ".png",
            string group = null, string style = null, bool preload = true)
        {
            Key = key; Name = name; Caption = caption; Policy = policy; Original = original; Extension = extension;
            Group = group ?? key; Style = style;
            Preload = original != null && preload;
        }

        internal string Title { get { return Group + "   " + (Style ?? Name); } }
    }

    internal static class LogoLibrary
    {
        internal const string CracRound = "CRAC-Round";
        internal static readonly LogoSource[] Catalog = {
            new LogoSource("CRAC", "中国无线电协会业余无线电分会", "个人业余电台 QSL 可用 · 150 × 300 PNG", "https://www.crac.org.cn/?p=999",
                "https://www.crac.org.cn/wp-content/themes/crac-themes/images/crac-logo.png", style: "官网竖版"),
            new LogoSource("IOTA", "Islands on the Air", "个人 QSL 请保持原样 · 1925 × 1279 PNG", "https://www.iota-world.org/copyright-notice.html",
                "https://www.iota-world.org/islands-on-the-air/downloads/download-file.html?path=iota_logo.png"),
            new LogoSource("SOTA", "Summits on the Air", "个人非商业 QSL · 77 × 77 JPG，适合小尺寸", "https://www.sota.org.uk/Joining-In/FAQs",
                "https://www.sota.org.uk/assets/media/sota_logo.jpg", ".jpg"),
            new LogoSource("ARRAM", "摩洛哥皇家业余无线电协会", "内置原图 · CC0", "https://commons.wikimedia.org/wiki/File:Logo_officiel_de_l%27Association_Royale_des_RadioAmateurs_du_Maroc.png", null),
            new LogoSource("URV-ABRA", "比斯开业余无线电协会", "内置原图缩小版 · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Logo_URV-ABRA_Uni%C3%B3n_de_Radioaficionados_de_Vizcaya_-_Asociaci%C3%B3n_Bizkaia_de_Radioaficionados.png", null),
            new LogoSource("MARC", "芒格洛尔业余无线电俱乐部", "内置原图 · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Logo_of_Mangalore_Amateur_Radio_Club.jpg", null, ".jpg"),
            new LogoSource(CracRound, "中国无线电协会业余无线电分会", "圆章版式 · 800 × 800 JPG",
                "https://sat.huijiwiki.com/wiki/中国无线电协会业余无线电分会",
                "https://huiji-thumb.huijistatic.com/sat/uploads/thumb/e/e4/CRAC.jpg/800px-CRAC.jpg", ".jpg", "CRAC", "蓝色圆章"),
            new LogoSource("CRSA-Archive", "中国无线电运动协会", "台站历史存档 · 89 × 157 GIF，宜小尺寸",
                "https://www.qsl.net/by6dx/old/textbook_pages/about_crsa.htm",
                "https://www.qsl.net/by6dx/old/image/crsa1.gif", ".gif", "CRSA", "老菱形会徽 · 黑白"),
            new LogoSource("ARRL-Gold", "美国业余无线电转播联盟", "会员与附属俱乐部个人 QSL · 315 × 600 JPG",
                "https://www.arrl.org/copyright",
                "https://www.arrl.org/webroot/files/image/News/ARRL%20Diamond.jpg", ".jpg", "ARRL", "经典黑金菱形"),
            new LogoSource("ARRL-Print", "美国业余无线电转播联盟", "会员与附属俱乐部个人 QSL · 327 × 768 JPG",
                "https://www.arrl.org/copyright",
                "https://www.arrl.org/webroot/files/image/Logos/ARRL_Diamond_Only_ARRL_Black_ForPrint.jpg", ".jpg", "ARRL", "单色印刷版"),
            new LogoSource("JARL", "日本业余无线电联盟", "官网 PNG · 标志使用条件见来源",
                "https://www.jarl.org/",
                "https://www.jarl.org/common/images/footerlogo.png", style: "菱形及文字横版"),
            new LogoSource("JARL-Centennial", "日本业余无线电联盟", "官方发布 · 1578 × 953 PNG",
                "https://www.jarl.org/English/8_info/expo2025.html",
                "https://www.jarl.org/English/8_info/expo2025/JARL_100th.png", group: "JARL", style: "1926–2026 百周年纪念"),
            new LogoSource("RSGB-Colour", "英国无线电协会", "会员非商用 · 530 × 1157 PNG",
                "https://rsgb.org/main/clubs-training/publicity-material/",
                "https://thersgb.org/services/logos/rsgb_logo_large_transparent_background.png", group: "RSGB", style: "彩色透明底", preload: false),
            new LogoSource("RSGB-White", "英国无线电协会", "会员非商用 · 107 × 237 PNG",
                "https://rsgb.org/main/clubs-training/publicity-material/",
                "https://thersgb.org/services/logos/rsgb_logo_2016_no_border.png", group: "RSGB", style: "彩色白底", preload: false),
            new LogoSource("RSGB-Mono", "英国无线电协会", "会员非商用 · 200 × 405 PNG",
                "https://rsgb.org/main/clubs-training/publicity-material/",
                "https://thersgb.org/services/logos/rsgb_logo_2016mono.png", group: "RSGB", style: "黑白透明底", preload: false),
            new LogoSource("CDXA-Colour", "卡罗来纳 DX 协会", "会员个人 QSL · 1200 × 1400 PNG",
                "https://www.cdxa.org/cdxa_logo.php",
                "https://www.cdxa.org/images/cdxa-new-logo.png", group: "CDXA", style: "彩色版", preload: false),
            new LogoSource("CDXA-Mono", "卡罗来纳 DX 协会", "会员个人 QSL · 1228 × 1319 PNG",
                "https://www.cdxa.org/cdxa_logo.php",
                "https://www.cdxa.org/images/cdxa_logo_mono_new.png", group: "CDXA", style: "黑白版", preload: false)
        };

        private const int MaxLogoBytes = 3 * 1024 * 1024;

        internal static string Folder(string installPath) { return Path.Combine(installPath, "Logos"); }
        internal static string FileName(LogoSource source) { return source.Key + source.Extension; }
        internal static Art GetLocal(string installPath, LogoSource source)
        {
            if (source.Original == null)
            {
                using (var input = typeof(LogoLibrary).Assembly.GetManifestResourceStream(FileName(source)))
                    return input == null ? null : ReadLogo(input, source);
            }
            try
            {
                using (var input = File.OpenRead(Path.Combine(Folder(installPath), FileName(source))))
                    return ReadLogo(input, source);
            }
            catch (Exception e)
            {
                if (IsSourceFailure(e)) return null;
                throw;
            }
        }

        internal static Art FetchAndStore(string installPath, LogoSource source)
        {
            Art art = GetLocal(installPath, source);
            if (art != null) return art;
            if (source.Original == null) throw new FileNotFoundException("安装包缺少此标志，请重新安装 QSL 卡片工坊。");
            try { art = Download(source); }
            catch (Exception e)
            {
                if (source.Key != CracRound || !IsSourceFailure(e)) throw;
                var crest = FetchAndStore(installPath, Catalog[0]);
                try { art = CallsignBadge.MakeRound(crest); }
                finally { crest.Dispose(); }
            }
            string directory = Folder(installPath);
            string path = Path.Combine(directory, FileName(source));
            try
            {
                Directory.CreateDirectory(directory);
                Files.Atomic(path, stream => stream.Write(art.Bytes, 0, art.Bytes.Length));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return art;
        }

        internal static int Prepare(string installPath)
        {
            int missing = 0;
            Parallel.ForEach(Catalog, new ParallelOptions { MaxDegreeOfParallelism = 4 }, source =>
            {
                if (!source.Preload) return;
                try
                {
                    using (var bitmap = FetchAndStore(installPath, source).Bitmap) { }
                    if (!File.Exists(Path.Combine(Folder(installPath), FileName(source))))
                        System.Threading.Interlocked.Increment(ref missing);
                }
                catch (Exception e)
                {
                    if (!IsSourceFailure(e)) throw;
                    System.Threading.Interlocked.Increment(ref missing);
                }
            });
            return missing == 0 ? 0 : 1;
        }

        private static bool IsSourceFailure(Exception e)
        {
            return e is WebException || e is IOException || e is InvalidDataException || e is UnauthorizedAccessException ||
                e is ArgumentException || e is OutOfMemoryException;
        }

        private static Art Download(LogoSource source)
        {
            var request = (HttpWebRequest)WebRequest.Create(source.Original);
            request.UserAgent = "QSLStudio/1.10";
            if (source.Key == CracRound) request.Referer = "https://sat.huijiwiki.com/";
            request.Timeout = 6500;
            request.ReadWriteTimeout = 5000;
            request.MaximumAutomaticRedirections = 3;
            using (var response = request.GetResponse())
            {
                if (response.ResponseUri.Scheme != Uri.UriSchemeHttps || response.ContentLength > MaxLogoBytes)
                    throw new InvalidDataException("官网返回的标志下载地址或文件大小不符合预期。");
                using (var stream = response.GetResponseStream()) return ReadLogo(stream, source);
            }
        }

        private static Art ReadLogo(Stream stream, LogoSource source)
        {
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (output.Length + count > MaxLogoBytes) throw new InvalidDataException("标志文件超出大小限制。");
                    output.Write(buffer, 0, count);
                }
                byte[] data = output.ToArray();
                bool hasImageHeader;
                switch (source.Extension)
                {
                    case ".jpg":
                        hasImageHeader = data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff;
                        break;
                    case ".gif":
                        hasImageHeader = data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' &&
                            data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a';
                        break;
                    default:
                        hasImageHeader = data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4e && data[3] == 0x47 &&
                            data[4] == 0x0d && data[5] == 0x0a && data[6] == 0x1a && data[7] == 0x0a;
                        break;
                }
                if (!hasImageHeader) throw new InvalidDataException("此文件不是协会提供的标志图片。");
                return new Art(data);
            }
        }
    }
}
