using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;

namespace QslStudio
{
    internal sealed class Field
    {
        internal readonly string Key, Label;
        internal readonly bool Station;
        internal Field(string key, string label, bool station)
        { Key = key; Label = label; Station = station; }
        internal static readonly Field[] All = {
            new Field("own", "本台呼号", true),
            new Field("location", "台站位置（QTH）", true),
            new Field("operator", "操作员", true),
            new Field("address", "邮寄地址", true),
            new Field("email", "电子邮箱", true),
            new Field("signature", "签名", true),
            new Field("grid", "网格定位", true),
            new Field("cq", "CQ 分区", true),
            new Field("itu", "ITU 分区", true),
            new Field("rig", "设备 / Rig", true),
            new Field("antenna", "天线 / Antenna", true),
            new Field("power", "功率 / Power", true),
            new Field("activity", "活动或俱乐部名称", true),
            new Field("reference", "活动编号 / Ref.", true),
            new Field("other", "对方呼号", false),
            new Field("date", "日期（UTC）", false),
            new Field("utc", "时间（UTC）", false),
            new Field("mhz", "频率（MHz）或频段", false),
            new Field("mode", "模式", false),
            new Field("rst", "信号报告 / RST", false),
            new Field("confirm", "确认类型", false),
            new Field("via", "回卡途径（QSL via）", false),
            new Field("reply", "回卡请求", false),
            new Field("note", "留言", false),
            new Field("date2", "第 2 次日期", false),
            new Field("utc2", "时间（UTC）", false),
            new Field("mhz2", "频率（MHz）", false),
            new Field("mode2", "模式", false),
            new Field("rst2", "信号报告 / RST", false),
            new Field("date3", "第 3 次日期", false),
            new Field("utc3", "时间（UTC）", false),
            new Field("mhz3", "频率（MHz）", false),
            new Field("mode3", "模式", false),
            new Field("rst3", "信号报告 / RST", false),
            new Field("worked", "通联电台（WKD）", false),
            new Field("sat", "卫星名称", false),
            new Field("mhzRx", "下行频率（MHz）", false),
            new Field("otherGrid", "对方网格", false),
            new Field("recipient", "收件地址", false),
            new Field("venue", "见面地点", false),
            new Field("stx", "交换（发）", false),
            new Field("srx", "交换（收）", false),
            new Field("postcode", "收件邮编（6 位）", false)
        };
        internal const int PostcodeDigits = 6;
        internal static bool IsMultiline(string key) { return key == "note" || key == "recipient" || key == "address"; }
        internal static int Limit(string key)
        {
            if (key == "postcode") return PostcodeDigits;
            return key == "" || IsMultiline(key) ? 420 : 120;
        }
        internal static string PostalDigits(string value)
        {
            char[] digits = new char[PostcodeDigits];
            int length = 0;
            foreach (char ch in value)
            {
                char digit = ch;
                if (digit >= '０' && digit <= '９') digit = (char)('0' + (digit - '０'));
                if (digit < '0' || digit > '9') continue;
                digits[length++] = digit;
                if (length == digits.Length) break;
            }
            return new string(digits, 0, length);
        }
        internal static Field Find(string key) { return Array.Find(All, f => f.Key == key); }
    }

    internal sealed class Art
    {
        internal readonly byte[] Bytes;
        internal readonly Bitmap Bitmap;
        private Bitmap preview;

        internal Bitmap Preview
        {
            get
            {
                const int maxSide = 1280;
                int longestSide = Math.Max(Bitmap.Width, Bitmap.Height);
                if (longestSide <= maxSide) return Bitmap;
                if (preview == null)
                {
                    int width = Math.Max(1, (int)Math.Round(Bitmap.Width * (double)maxSide / longestSide));
                    int height = Math.Max(1, (int)Math.Round(Bitmap.Height * (double)maxSide / longestSide));
                    var scaled = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                    try
                    {
                        using (var graphics = Graphics.FromImage(scaled))
                        {
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            graphics.PixelOffsetMode = PixelOffsetMode.Half;
                            using (var attributes = new ImageAttributes())
                            {
                                attributes.SetWrapMode(WrapMode.TileFlipXY);
                                graphics.DrawImage(Bitmap, new Rectangle(0, 0, width, height), 0, 0,
                                    Bitmap.Width, Bitmap.Height, GraphicsUnit.Pixel, attributes);
                            }
                        }
                        preview = scaled;
                    }
                    catch
                    {
                        scaled.Dispose();
                        throw;
                    }
                }
                return preview;
            }
        }

        internal Art(byte[] bytes)
        {
            if (bytes.Length < 8 || bytes.Length > 28 * 1024 * 1024) throw new InvalidDataException("图片太大或不是有效的图片。");
            Bytes = bytes;
            using (var input = new MemoryStream(bytes, false))
            using (var img = Image.FromStream(input, true, true))
            {
                Validate(img);
                Bitmap = new Bitmap(img);
            }
        }
        internal void Dispose()
        {
            if (preview != null) preview.Dispose();
            Bitmap.Dispose();
        }
        private static void Validate(Image img)
        {
            if (img.Width < 1 || img.Height < 1 || img.Width > 16000 || img.Height > 16000 || (long)img.Width * img.Height > 56000000)
                throw new InvalidDataException("图片尺寸过大，请先缩小图片后再导入。");
        }
        internal static Art Import(string path)
        {
            if (new FileInfo(path).Length > 45 * 1024 * 1024) throw new InvalidDataException("请选择小于 45 MB 的图片。");
            using (var file = File.OpenRead(path))
            using (var img = Image.FromStream(file, true, true))
            {
                Validate(img);
                if (Array.IndexOf(img.PropertyIdList, 274) >= 0)
                {
                    var item = img.GetPropertyItem(274);
                    if (item != null && item.Value.Length > 0)
                    {
                        var rotations = new[] { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone,
                            RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX,
                            RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX,
                            RotateFlipType.Rotate270FlipNone };
                        int orientation = item.Value[0];
                        if (orientation > 1 && orientation < rotations.Length) img.RotateFlip(rotations[orientation]);
                    }
                }
                double ratio = Math.Min(1, 3000.0 / Math.Max(img.Width, img.Height));
                bool jpeg = img.RawFormat.Guid == ImageFormat.Jpeg.Guid;
                using (var copy = new Bitmap(Math.Max(1, (int)(img.Width * ratio)), Math.Max(1, (int)(img.Height * ratio)),
                    jpeg ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb))
                using (var stream = new MemoryStream())
                {
                    using (var g = Graphics.FromImage(copy))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(img, new Rectangle(0, 0, copy.Width, copy.Height));
                    }
                    if (jpeg) CardOutput.WriteJpeg(copy, stream, 93L);
                    else copy.Save(stream, ImageFormat.Png);
                    return new Art(stream.ToArray());
                }
            }
        }
    }

    internal enum LayerKind { Text, Picture, Mark }
    internal enum PictureFit { Auto, Fill, Fit, Stretch }

    internal static class PictureLayout
    {
        internal static readonly RectangleF Full = new RectangleF(0, 0, 1, 1);

        internal static RectangleF CenterCrop(RectangleF area, float ratio)
        {
            float width = Math.Min(area.Width, area.Height * ratio);
            float height = Math.Min(area.Height, area.Width / ratio);
            return new RectangleF(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
        }

        internal static RectangleF FitInside(RectangleF area, float width, float height)
        {
            float scale = Math.Min(area.Width / width, area.Height / height);
            width *= scale;
            height *= scale;
            return new RectangleF(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
        }
    }

    internal sealed class Layer
    {
        internal string Id = Guid.NewGuid().ToString("N");
        internal string Name = "文字", Field = "", Text = "", Font = "Arial", Symbol = "radio";
        internal int Side;
        internal LayerKind Kind;
        internal float X, Y, W = 300, H = 75, Size = 12;
        internal Color Color = Color.FromArgb(24, 44, 43);
        internal FontStyle Style;
        internal StringAlignment Align;
        internal bool Fit = true, Cover, Hidden;
        internal Art Image;
        internal PictureFit ImageFit;
        internal RectangleF ImageCrop;
        internal PointF[] StrokePoints;
        internal RectangleF Bounds { get { return new RectangleF(X, Y, W, H); } }
        internal PictureFit EffectiveImageFit { get { return ImageFit == PictureFit.Auto ? (Cover ? PictureFit.Fill : PictureFit.Fit) : ImageFit; } }
        internal float CropRatio { get { return (W / H) * Image.Bitmap.Height / Image.Bitmap.Width; } }
        internal RectangleF PictureSource
        {
            get
            {
                RectangleF source = ImageCrop.IsEmpty ? PictureLayout.Full : ImageCrop;
                return EffectiveImageFit == PictureFit.Fill ? PictureLayout.CenterCrop(source, CropRatio) : source;
            }
        }
        internal RectangleF PictureBounds(RectangleF source)
        {
            return EffectiveImageFit == PictureFit.Fit
                ? PictureLayout.FitInside(Bounds, source.Width * Image.Bitmap.Width, source.Height * Image.Bitmap.Height) : Bounds;
        }
        internal Layer Copy() { return (Layer)MemberwiseClone(); }
        internal string Value(Project project) { return Field.Length == 0 ? Text : project.Value(Field); }
        internal void SetValue(Project project, string value) { if (Field.Length == 0) Text = value; else project.Values[Field] = value; }
    }

    internal sealed class Project
    {
        internal const int LongEdge = 1400, ShortEdge = 900, MaxLayers = 180;
        internal const int MinPageEdge = 100, MaxPageEdge = 4200;
        internal string Template = "postcard";
        internal string ReverseTemplate;
        internal readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        internal readonly List<Layer> Layers = new List<Layer>();
        internal Color[] Background = { Color.White, Color.White };
        internal bool Customized, Example;
        internal int Revision;
        private int pageWidth, pageHeight;
        internal bool Portrait { get { return Templates.IsPortrait(Template); } }
        internal bool HasCustomSize { get { return pageWidth > 0; } }
        internal int Width { get { return HasCustomSize ? pageWidth : (Portrait ? ShortEdge : LongEdge); } }
        internal int Height { get { return HasCustomSize ? pageHeight : (Portrait ? LongEdge : ShortEdge); } }
        internal string PrintSize { get { return Millimetres(Width) + " × " + Millimetres(Height) + " mm"; } }
        internal static string Millimetres(int units) { return (units / 10m).ToString("0.#", CultureInfo.InvariantCulture); }
        internal static string SizeLabel(bool portrait) { return portrait ? "90 × 140 mm" : "140 × 90 mm"; }
        internal static bool ValidPageSize(int width, int height)
        { return width >= MinPageEdge && width <= MaxPageEdge && height >= MinPageEdge && height <= MaxPageEdge; }
        internal void SetPageSize(int width, int height)
        {
            if (!ValidPageSize(width, height)) throw new ArgumentOutOfRangeException("width", "卡片宽、高均须在 10 到 420 mm 之间。");
            pageWidth = width;
            pageHeight = height;
        }
        internal void Resize(int width, int height)
        {
            int oldWidth = Width, oldHeight = Height;
            SetPageSize(width, height);
            if (oldWidth == width && oldHeight == height) return;
            float sx = width / (float)oldWidth, sy = height / (float)oldHeight;
            foreach (var layer in Layers)
            {
                layer.W = Math.Max(1f, layer.W * sx);
                layer.H = Math.Max(1f, layer.H * sy);
                layer.X = Math.Max(-width, Math.Min(width - 1, layer.X * sx));
                layer.Y = Math.Max(-height, Math.Min(height - 1, layer.Y * sy));
                layer.Size = Math.Max(1f, Math.Min(180f, layer.Size * Math.Min(sx, sy)));
            }
        }
        internal static float ConstrainOffset(float position, float length, int page)
        {
            float visible = Math.Min(20, length);
            return Math.Max(Math.Max(-page, visible - length), Math.Min(page - visible, position));
        }
        internal string Value(string key) { string v; return Values.TryGetValue(key, out v) ? v : ""; }
        internal string TemplateOn(int side) { return side == 1 && ReverseTemplate != null ? ReverseTemplate : Template; }
        internal string StationCall
        {
            get
            {
                string call = Value("own").Trim();
                return IsDemo && (LegacyExample.IsPlaceholderCall(call) || LegacyExample.IsDefault("own", call.ToUpperInvariant())) ? "" : call;
            }
        }
        internal void ClearDemoFields()
        {
            if (!Example) return;
            foreach (var field in Field.All)
            {
                string value = Value(field.Key);
                if (LegacyExample.IsDefault(field.Key, value) ||
                    (field.Key == "own" || field.Key == "other") && LegacyExample.IsPlaceholderCall(value))
                    Values[field.Key] = "";
            }
            Example = false;
        }
        internal bool IsDemo
        {
            get
            {
                if (!Example) return false;
                string own = Value("own"), other = Value("other");
                return LegacyExample.IsPlaceholderCall(own) || LegacyExample.IsPlaceholderCall(other) ||
                    string.IsNullOrWhiteSpace(other) && LegacyExample.IsDefault("own", own.Trim().ToUpperInvariant());
            }
        }
        internal Project Copy()
        {
            var p = new Project { Template = Template, ReverseTemplate = ReverseTemplate, Revision = Revision, Customized = Customized,
                Example = Example, Background = (Color[])Background.Clone(), pageWidth = pageWidth, pageHeight = pageHeight };
            foreach (var f in Values) p.Values.Add(f.Key, f.Value);
            foreach (var l in Layers) p.Layers.Add(l.Copy());
            return p;
        }
        internal Layer Find(string id) { return Layers.Find(l => l.Id == id); }
    }

    internal static partial class Templates
    {
        private const int PracticalStart = 5, PortraitStart = 15;
        internal const int BuiltInCount = 18;
        internal const string ExampleNotice = "旧版样卡，通联资料仅供演示。";
        internal static readonly string[] Keys = {
            "photojournal", "logbook", "bureau", "postcard", "event", "postal", "label", "satellite", "swl", "eyeball",
            "callsign", "handwritten", "contest", "postal-cn", "open-note",
            "portrait-photo", "portrait-log", "portrait-postal", "photo", "classic", "signal"
        };
        internal static readonly string[] Names = {
            "全幅照片卡", "黑白手填卡", "卡片局多通联", "照片留白卡", "活动纪念卡", "明信片直寄", "卡片局贴签", "卫星通联",
            "SWL 收听确认", "见面交换卡", "纯呼号", "横卡配竖图", "赛事通联", "国内直寄", "大留白手写",
            "竖版照片卡", "竖版黑白手填", "竖版国内直寄",
            "照片明信片（旧版）", "经典印章（旧版）", "电波线稿（旧版）"
        };
        internal static readonly string[] Notes = {
            "用自己的图片铺满正面。反面是一行通联表。", "本台资料靠左，手填格集中在右侧。", "可填写三次通联，对方呼号和 QSL VIA 独立分栏。",
            "照片下方印呼号和地点，反面用于记录通联。", "活动名称、呼号和地点印在照片上方。", "邮票位和收件地址在右，通联资料在左。",
            "反面提供约 92 × 36 mm 的贴签区，预印时可留空。", "记录卫星名称、上下行频率与双方网格。", "分别填写收听者和通联中的另一台。",
            "用于线下换卡。填写对方、日期、地点和签名。", "正面印大呼号。反面可确认通联、收听或见面交换。", "横卡左侧可放自己的竖幅照片。反面用横线手写。",
            "正面印赛事名称与呼号。反面预留双方交换栏。", "六格邮编、邮票位和地址线可拖动。左侧填写通联记录。", "反面用一行写通联数据，下面留给留言和签名。",
            "竖幅照片在上，呼号与地点在下。反面记录通联和留言。",
            "正面凸显呼号。反面的横线适合预印后手填。",
            "反面上方填邮编和盖邮票，下方写收件地址。",
            "旧版模板", "旧版模板", "旧版模板"
        };
        private static Color C(int rgb) { return Color.FromArgb(unchecked((int)0xff000000) | rgb); }
        internal static int Index(string id) { return Math.Max(0, Array.IndexOf(Keys, id)); }
        internal static bool IsLegacy(string id) { return Index(id) >= BuiltInCount; }
        internal static bool IsPractical(int index) { return index >= PracticalStart && index < PortraitStart; }
        internal static bool IsPortrait(string key) { return key != null && key.StartsWith("portrait-", StringComparison.Ordinal); }
        internal static bool IsBlank(string key) { return key == "blank" || key == "portrait-blank"; }
        internal static bool IsKnown(string key) { return IsBlank(key) || Array.IndexOf(Keys, key) >= 0; }
        internal static string Title(string key) { return IsBlank(key) ? "空白画布" : Names[Index(key)]; }
        internal static Size ReferenceSize(string key)
        {
            return IsPortrait(key) ? new Size(Project.ShortEdge, Project.LongEdge) : new Size(Project.LongEdge, Project.ShortEdge);
        }

        internal static RectangleF PageCoverFrame(Project p, int side)
        {
            string key = p.TemplateOn(side);
            var bounds = CoverFrame(key, side);
            var size = ReferenceSize(key);
            float sx = p.Width / (float)size.Width, sy = p.Height / (float)size.Height;
            return new RectangleF(bounds.X * sx, bounds.Y * sy, bounds.Width * sx, bounds.Height * sy);
        }
        internal static Color DefaultInk(Project p, int side)
        {
            if (IsBlank(p.TemplateOn(side))) return p.Background[side].GetBrightness() < .45f ? Color.White : C(0x202324);
            if (side != 0) return C(0x202324);
            bool darkPhoto = HasDarkPhotoFront(p.Template) || p.Template == "photo" || p.Template == "signal";
            bool darkEvent = p.Template == "event" && p.Background[0].GetBrightness() < .44f;
            return darkPhoto || darkEvent ? Color.White : C(0x202324);
        }
        internal static bool HasDarkPhotoFront(string key)
        {
            return key == "photojournal" || key == "postal" || key == "postal-cn";
        }
        internal static RectangleF CoverFrame(string key, int side)
        {
            if (side == 0 && key == "postcard") return new RectangleF(0, 0, 1400, 737);
            if (side == 0 && key == "event") return new RectangleF(0, 205, 1400, 695);
            if (side == 0 && key == "satellite") return new RectangleF(705, 0, 695, 900);
            if (side == 0 && key == "eyeball") return new RectangleF(0, 0, 760, 900);
            if (side == 0 && key == "handwritten") return new RectangleF(61, 59, 536, 756);
            if (side == 0 && key == "portrait-photo") return new RectangleF(0, 0, 900, 1036);
            if (side == 0 && key == "portrait-postal") return new RectangleF(55, 53, 790, 959);
            return IsPortrait(key) ? new RectangleF(0, 0, Project.ShortEdge, Project.LongEdge)
                : new RectangleF(0, 0, Project.LongEdge, Project.ShortEdge);
        }
        internal static Project Create(string key)
        {
            if (IsBlank(key)) return new Project { Template = key };
            int i = Index(key);
            var p = new Project { Template = Keys[i] };
            foreach (var f in Field.All) p.Values.Add(f.Key, "");
            if (p.Portrait) { MakePortrait(p); return p; }
            if (IsPractical(i)) { MakePractical(p); return p; }
            if (i < BuiltInCount)
            {
                if (key == "photojournal") MakeFullBleed(p);
                else if (key == "bureau") MakeBureauFront(p);
                else MakeFront(p, key == "logbook" ? 1 : key == "event" ? 2 : 0);
                if (key == "bureau") MakeBureauBack(p, true);
                else if (key == "photojournal") MakeBureauBack(p, false);
                else MakeBack(p, key == "logbook" ? 1 : key == "event" ? 2 : 0);
                return p;
            }
            i -= BuiltInCount;
            if (i == 0)
            {
                p.Background[0] = C(0x19382e);
                p.Background[1] = C(0xf8faf7);
                AddCover(p);
                Text(p, 0, "台站说明", "中国业余无线电台  /  AMATEUR RADIO STATION", 84, 82, 1050, 62, 14, C(0xffffff), true);
                Add(p, 0, "own", 78, 507, 1252, 214, 52, C(0xffffff), true);
                Add(p, 0, "location", 88, 730, 1180, 82, 17, C(0xf0f8ee), true);
                Mark(p, 0, "电波标记", "radio", 1220, 77, 83, C(0xffffff));
            }
            else if (i == 1)
            {
                p.Background[0] = C(0xf6edda);
                p.Background[1] = C(0xfbf6e9);
                Text(p, 0, "台站说明", "个人业余无线电台", 104, 116, 1080, 65, 16, C(0x6e3528), true);
                Text(p, 0, "英文台站说明", "AMATEUR RADIO STATION  /  CHINA", 107, 188, 1080, 45, 10, C(0x6e3528), true);
                Add(p, 0, "own", 91, 293, 1208, 240, 59, C(0x752e21), true);
                Add(p, 0, "location", 108, 586, 1180, 69, 15, C(0x492e25), true);
                Add(p, 0, "operator", 109, 669, 1160, 56, 12, C(0x624032), false);
                Mark(p, 0, "天线印章", "tower", 1139, 100, 140, C(0x823d2e));
            }
            else
            {
                p.Background[0] = C(0x112b33);
                p.Background[1] = C(0xf6f9fa);
                Text(p, 0, "台站说明", "AMATEUR RADIO  /  CHINA", 83, 102, 990, 62, 14, C(0x9ee6d6), true);
                Add(p, 0, "own", 77, 269, 1192, 216, 53, C(0xf3fffc), true);
                Add(p, 0, "location", 88, 526, 1110, 70, 15, C(0xd1f0ea), true);
                Text(p, 0, "卡片题字", "QSL  /  通联纪念", 88, 755, 660, 66, 13, C(0x9ee6d6), true);
                Mark(p, 0, "电波标记", "radio", 1215, 93, 91, C(0x9ee6d6));
            }
            Back(p, i);
            return p;
        }

        private static void MakeFullBleed(Project p)
        {
            p.Background[0] = C(0x262b30);
            AddCover(p);
            Color white = Color.White;
            Text(p, 0, "中文台站说明", "中国个人业余电台", 164, 43, 690, 43, 10.5f, white, true);
            Text(p, 0, "英文台站说明", "AMATEUR RADIO STATION • CHINA", 164, 91, 690, 29, 7.3f, white, true);
            Add(p, 0, "own", 57, 701, 790, 142, 39, white, true);
            Add(p, 0, "location", 812, 749, 528, 49, 10, white, true);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Far;
            Add(p, 0, "grid", 1090, 800, 250, 37, 8, white, false);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Far;
        }

        private static void MakeBureauFront(Project p)
        {
            p.Background[0] = C(0xf1eee6);
            Color ink = C(0x183a50);
            Text(p, 0, "国家与台站", "CHINA   /   AMATEUR RADIO STATION", 66, 117, 1220, 45, 11, ink, true);
            Add(p, 0, "own", 60, 230, 1260, 245, 63, ink, true);
            Add(p, 0, "operator", 74, 513, 1250, 70, 15, ink, false);
            Add(p, 0, "location", 74, 610, 1230, 80, 17, ink, false);
            Text(p, 0, "卡片题字", "QSL CARD", 74, 765, 585, 42, 11, ink, true);
        }

        private static void MakeBureauBack(Project p, bool multiple)
        {
            Color ink = C(0x111d23), muted = C(0x324d60);
            Text(p, 1, "台站栏名", "中国业余电台 / AMATEUR RADIO STATION", 170, 55, 780, 36, 8.6f, muted, true);
            Add(p, 1, "own", 165, 92, 630, 96, 26, ink, true);
            Add(p, 1, "location", 169, 187, 680, 39, 9, ink, false);
            SmallField(p, "GRID", "grid", 876, 63, 456, muted, ink, 92);
            SmallField(p, "CQ", "cq", 876, 110, 188, muted, ink, 65);
            SmallField(p, "ITU", "itu", 1078, 110, 220, muted, ink, 75);
            if (multiple)
            {
                Text(p, 1, "对方呼号栏名", "TO RADIO / 对方呼号", 974, 276, 337, 39, 8.4f, muted, true);
                Add(p, 1, "other", 976, 330, 334, 76, 18, ink, true);
                Text(p, 1, "卡片经由栏名", "QSL VIA / 经由", 974, 466, 337, 37, 8.4f, muted, true);
                Add(p, 1, "via", 976, 517, 334, 61, 12, ink, false);
            }
            else
            {
                SmallField(p, "TO RADIO / 对方", "other", 64, 254, 710, muted, ink, 235);
                p.Layers[p.Layers.Count - 1].Size = 16;
                SmallField(p, "VIA / 经由", "via", 869, 254, 472, muted, ink, 171);
            }
            Check(p, "confirm", "QSO", "通联复选框", 68, 318, ink);
            Text(p, 1, "通联栏名", "OUR QSO / 通联", 102, 309, 305, 39, 8.2f, ink, false);
            Check(p, "confirm", "SWL", "收听复选框", 408, 318, ink);
            Text(p, 1, "收听栏名", "SWL REPORT / 收听", 439, 309, 381, 39, 8.2f, ink, false);

            string[] keys = { "date", "utc", "mhz", "mode", "rst" };
            string[] labels = multiple ? new[] { "DATE", "UTC", "MHz", "MODE", "RST" }
                : new[] { "DATE / 日期", "UTC / 时间", "MHz / 频率", "MODE / 模式", "RST / 报告" };
            int[] columns = multiple ? RoutingColumns : BureauColumns;
            for (int col = 0; col < keys.Length; col++)
            {
                int x = columns[col] + 8, width = columns[col + 1] - x - 8;
                Text(p, 1, labels[col], labels[col], x, 379, width, 35, 8.2f, muted, true);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                for (int row = 0; row < (multiple ? 3 : 1); row++)
                {
                    string suffix = row == 0 ? "" : (row + 1).ToString(CultureInfo.InvariantCulture);
                    Add(p, 1, keys[col] + suffix, x, 434 + row * 67, width, multiple ? 61 : 127, 12, ink, false);
                    p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                }
            }

            SmallField(p, "RIG", "rig", 65, 649, 294, muted, ink, 59);
            SmallField(p, "PWR", "power", 368, 649, 212, muted, ink, 66);
            SmallField(p, "ANT", "antenna", 586, 649, 450, muted, ink, 71);
            SmallField(p, "OP", "operator", 64, 694, 685, muted, ink, 55);
            SmallField(p, "SIGN / 签名", "signature", 867, 694, 465, muted, ink, 181);
            SmallField(p, "ADDRESS / 地址", "address", 64, 740, 1047, muted, ink, 214);
            SmallField(p, "REMARKS", "note", 64, 795, 760, muted, ink, 132);
            Check(p, "reply", "PSE", "请回卡复选框", 872, 801, ink);
            Text(p, 1, "请回卡", "PSE QSL", 904, 790, 175, 43, 8.5f, muted, true);
            Check(p, "reply", "TNX", "谢回卡复选框", 1104, 801, ink);
            Text(p, 1, "谢回卡", "TNX QSL", 1136, 790, 190, 43, 8.5f, muted, true);
        }

        internal static readonly int[] BureauColumns = { 60, 357, 560, 813, 1081, 1340 };
        internal static readonly int[] RoutingColumns = { 60, 272, 429, 608, 792, 923 };

        private static void MakeFront(Project p, int kind)
        {
            Color black = C(0x171c20), muted = C(0x40494d);
            if (kind == 0)
            {
                AddCover(p);
                Add(p, 0, "own", 50, 757, 506, 101, 26, black, true);
                Add(p, 0, "location", 575, 758, 775, 55, 10, black, true);
                Text(p, 0, "台站说明", "个人业余无线电台  /  AMATEUR RADIO STATION", 575, 817, 775, 34, 7, muted, false);
            }
            else if (kind == 1)
            {
                Text(p, 0, "中文台站说明", "中华人民共和国个人业余电台", 76, 123, 1230, 70, 15, black, true);
                Text(p, 0, "英文台站说明", "PERSONAL AMATEUR RADIO STATION OF P.R. CHINA", 79, 196, 1240, 40, 9, black, false);
                Add(p, 0, "own", 71, 305, 1259, 230, 57, black, true);
                Add(p, 0, "location", 81, 594, 1220, 74, 17, black, false);
                Add(p, 0, "operator", 84, 681, 880, 53, 10, black, false);
                Text(p, 0, "QSL 标题", "QSL CARD", 1040, 715, 280, 40, 9, black, true);
            }
            else
            {
                p.Background[0] = C(0xfffbf0);
                Color rust = C(0x7a3024), secondary = C(0x4b4a43);
                AddCover(p);
                Add(p, 0, "activity", 166, 41, 615, 78, 18, black, true);
                Add(p, 0, "reference", 169, 136, 600, 33, 8.5f, rust, true);
                Add(p, 0, "own", 795, 30, 549, 106, 31, rust, true);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Far;
                Add(p, 0, "location", 779, 136, 563, 33, 8.5f, secondary, false);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Far;
            }
        }

        private static void AddCover(Project p)
        {
            RectangleF frame = CoverFrame(p.Template, 0);
            p.Layers.Add(new Layer { Name = "封面照片", Kind = LayerKind.Picture, Side = 0,
                Cover = true, X = frame.X, Y = frame.Y, W = frame.Width, H = frame.Height });
        }

        private static void MakeBack(Project p, int kind)
        {
            if (kind == 1)
            {
                MakeLogbookBack(p);
                return;
            }
            Color ink = C(0x11171c);
            Color label = C(kind == 2 ? 0x253b59 : 0x303a40);
            int y = kind == 2 ? 347 : 340;
            if (kind == 2)
            {
                Text(p, 1, "活动确认卡标题", "活动通联确认  /  SPECIAL EVENT QSL", 55, 53, 830, 48, 9, label, true);
                Add(p, 1, "own", 54, 117, 590, 86, 23, ink, true);
                Add(p, 1, "activity", 55, 208, 615, 49, 10, ink, true);
                Add(p, 1, "reference", 680, 57, 455, 48, 9, label, true);
            }
            else
            {
                Text(p, 1, "确认卡标题", "中华人民共和国个人业余电台", 56, 46, 820, 44, 11, ink, true);
                Text(p, 1, "英文台站说明", "PERSONAL AMATEUR RADIO STATION OF P.R. CHINA", 56, 90, 840, 28, 7.4f, label, false);
                Add(p, 1, "own", 53, 131, 576, 87, 24, ink, true);
                Add(p, 1, "location", 57, 224, 704, 38, 8.5f, ink, false);
            }

            const int receiverX = 838;
            Text(p, 1, "对方呼号栏名", "TO RADIO / 对方呼号", receiverX + 16, 138, 455, 35, 8.5f, label, true);
            Add(p, 1, "other", receiverX + 16, 178, 1325 - receiverX, 70, 17, ink, true);

            Text(p, 1, "确认栏名", "CONFIRMING / 确认", 56, y - 53, 290, 35, 8, label, true);
            Check(p, "confirm", "QSO", "通联复选框", 358, y - 46, ink);
            Text(p, 1, "通联栏名", "OUR QSO / 通联", 393, y - 54, 285, 36, 8, label, false);
            Check(p, "confirm", "SWL", "收听复选框", 693, y - 46, ink);
            Text(p, 1, "收听栏名", "YOUR SWL REPORT / 收听", 728, y - 54, 575, 36, 8, label, false);

            string[] keys = { "date", "utc", "mhz", "mode", "rst" };
            string[] labels = { "DATE / 日期", "UTC / 世界时", "MHz / 频率", "MODE / 模式", "RST / 报告" };
            int[] xs = ColumnEdges(kind);
            for (int j = 0; j < keys.Length; j++)
            {
                int width = xs[j + 1] - xs[j];
                Text(p, 1, labels[j] + " 栏名", labels[j], xs[j] + 11, y + 7, width - 22, 32, 8.3f, label, true);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                Add(p, 1, keys[j], xs[j] + 10, y + 49, width - 20, 85, 13, ink, false);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
            }

            string[] equipment = { "rig", "power", "antenna", "via" };
            string[] equipmentTitles = { "RIG / 设备", "PWR / 功率", "ANT / 天线", "QSL VIA / 经由" };
            int[] columns = EquipmentEdges(kind);
            for (int j = 0; j < equipment.Length; j++)
            {
                int width = columns[j + 1] - columns[j];
                Text(p, 1, equipmentTitles[j] + " 栏名", equipmentTitles[j], columns[j] + 13, y + 155, width - 26, 28, 7, label, true);
                Add(p, 1, equipment[j], columns[j] + 13, y + 190, width - 26, 56, 10, ink, false);
            }

            int foot = y + 271;
            Check(p, "reply", "PSE", "请回卡复选框", 56, foot + 4, ink);
            Text(p, 1, "请求回卡栏名", "PSE QSL / 请回卡", 91, foot - 3, 345, 38, 8.5f, label, true);
            Check(p, "reply", "TNX", "感谢回卡复选框", 446, foot + 4, ink);
            Text(p, 1, "感谢回卡栏名", "TNX QSL / 谢谢回卡", 482, foot - 3, 360, 38, 8.5f, label, true);
            SmallField(p, "SIGNED / 签名", "signature", 870, foot, 463, label, ink, 200);
            SmallField(p, "OP", "operator", 56, foot + 50, 530, label, ink, 55);
            SmallField(p, "GRID", "grid", 614, foot + 50, 213, label, ink, 75);
            SmallField(p, "CQ", "cq", 855, foot + 50, 150, label, ink, 49);
            SmallField(p, "ITU", "itu", 1014, foot + 50, 152, label, ink, 50);
            SmallField(p, "ADDRESS / 地址", "address", 56, foot + 96, 745, label, ink, 218);
            if (kind == 2) SmallField(p, "QTH", "location", 810, foot + 96, 534, label, ink, 68);
            else SmallField(p, "EMAIL", "email", 810, foot + 96, 534, label, ink, 99);
            SmallField(p, "REMARKS / 备注", "note", 56, foot + 164, 1288, label, ink, 231);
        }

        private static void MakeLogbookBack(Project p)
        {
            Color ink = C(0x171717);
            Text(p, 1, "个人台站栏名", "个人业余电台", 52, 61, 341, 44, 11, ink, true);
            Text(p, 1, "英文台站说明", "AMATEUR RADIO STATION", 52, 108, 341, 29, 6.9f, ink, false);
            Add(p, 1, "own", 49, 159, 356, 75, 21, ink, true);
            Add(p, 1, "location", 53, 254, 341, 44, 7.5f, ink, false);
            SmallField(p, "OP", "operator", 53, 316, 341, ink, ink, 52);
            SmallField(p, "GRID", "grid", 53, 376, 341, ink, ink, 89);
            SmallField(p, "CQ", "cq", 53, 421, 156, ink, ink, 51);
            SmallField(p, "ITU", "itu", 222, 421, 159, ink, ink, 60);
            Text(p, 1, "邮寄地址栏名", "ADDRESS / 寄卡地址", 53, 514, 348, 35, 8, ink, true);
            Add(p, 1, "address", 53, 553, 348, 79, 8, ink, false);
            p.Layers[p.Layers.Count - 1].Symbol = "paragraph";
            Text(p, 1, "邮箱栏名", "EMAIL / 邮箱", 53, 641, 348, 31, 8, ink, true);
            Add(p, 1, "email", 53, 674, 348, 39, 8.3f, ink, false);
            SmallField(p, "QSL VIA", "via", 53, 734, 341, ink, ink, 129);

            Text(p, 1, "对方呼号栏名", "TO RADIO / 对方呼号", 468, 72, 846, 39, 10, ink, true);
            Add(p, 1, "other", 467, 116, 851, 91, 24, ink, true);
            Check(p, "confirm", "QSO", "通联复选框", 449, 250, ink);
            Text(p, 1, "通联栏名", "OUR QSO / 通联", 483, 243, 323, 36, 8.3f, ink, false);
            Check(p, "confirm", "SWL", "收听复选框", 849, 250, ink);
            Text(p, 1, "收听栏名", "YOUR SWL REPORT / 收听", 882, 243, 462, 36, 8.3f, ink, false);

            string[] keys = { "date", "utc", "mhz", "mode", "rst" };
            string[] labels = { "DATE / 日期", "UTC", "MHz", "MODE / 模式", "RST / 报告" };
            int[] xs = ColumnEdges(1);
            for (int j = 0; j < keys.Length; j++)
            {
                int width = xs[j + 1] - xs[j];
                Text(p, 1, labels[j] + " 栏名", labels[j], xs[j] + 8, 310, width - 16, 37, 7.8f, ink, true);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                Add(p, 1, keys[j], xs[j] + 8, 362, width - 16, 100, 12.4f, ink, false);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
            }

            int[] eq = EquipmentEdges(1);
            string[] gear = { "rig", "power", "antenna" };
            string[] captions = { "RIG / 设备", "PWR / 功率", "ANT / 天线" };
            for (int j = 0; j < gear.Length; j++)
            {
                int width = eq[j + 1] - eq[j];
                Text(p, 1, captions[j] + " 栏名", captions[j], eq[j] + 13, 493, width - 26, 30, 7.8f, ink, true);
                Add(p, 1, gear[j], eq[j] + 13, 538, width - 26, 73, 10.4f, ink, false);
            }

            Check(p, "reply", "PSE", "请回卡复选框", 449, 660, ink);
            Text(p, 1, "请求回卡栏名", "PSE QSL / 请回卡", 484, 653, 352, 36, 8.5f, ink, true);
            Check(p, "reply", "TNX", "感谢回卡复选框", 849, 660, ink);
            Text(p, 1, "感谢回卡栏名", "TNX QSL / 谢谢回卡", 884, 653, 452, 36, 8.5f, ink, true);
            SmallField(p, "73!", "note", 449, 716, 900, ink, ink, 59);
            SmallField(p, "SIGNED / 签名", "signature", 449, 782, 900, ink, ink, 222);
        }

        private static void SmallField(Project p, string label, string field, int x, int y, int w, Color fg, Color ink, int labelWidth)
        {
            Text(p, 1, label + " 栏名", label, x, y, labelWidth - 7, 33, 7.4f, fg, true);
            Add(p, 1, field, x + labelWidth, y, w - labelWidth, 33, 8.5f, ink, false);
        }

        private static void Check(Project p, string field, string option, string name, int x, int y, Color ink)
        {
            p.Layers.Add(new Layer { Name = name, Kind = LayerKind.Mark, Side = 1, X = x, Y = y, W = 23, H = 23,
                Field = field, Symbol = "checkbox:" + option, Color = ink });
        }

        internal static int[] ColumnEdges(int kind)
        {
            if (kind == 1) return new[] { 449, 676, 826, 1000, 1177, 1344 };
            return new[] { 56, 382, 606, 849, 1113, 1344 };
        }

        internal static int[] EquipmentEdges(int kind)
        {
            if (kind == 1) return new[] { 449, 757, 971, 1344 };
            if (kind == 2) return new[] { 56, 326, 547, 915, 1344 };
            return new[] { 56, 346, 575, 951, 1344 };
        }
        private static void Back(Project p, int index)
        {
            Color ink = C(index == 1 ? 0x39271e : 0x182d32);
            Color sub = C(index == 1 ? 0x79452e : index == 2 ? 0x284b5c : 0x24584d);
            Text(p, 1, "确认卡标题", "通联确认  /  QSL CONFIRMATION", 77, 72, 820, 49, 12, sub, true);
            Add(p, 1, "own", 71, 125, 691, 167, 39, ink, true);
            Add(p, 1, "location", 768, 106, 548, 54, 11, ink, true);
            Add(p, 1, "operator", 768, 168, 548, 46, 9.5f, sub, false);
            Inline(p, "Grid", "grid", 768, 232, 194, sub, ink);
            Inline(p, "CQ", "cq", 972, 232, 132, sub, ink);
            Inline(p, "ITU", "itu", 1122, 232, 190, sub, ink);

            Text(p, 1, "对方呼号栏名", "确认与以下电台通联  /  CONFIRMING QSO WITH", 77, 317, 510, 47, 10, sub, true);
            Add(p, 1, "other", 604, 300, 712, 82, 21, ink, true);
            string[] keys = { "date", "utc", "mhz", "mode", "rst" };
            string[] titles = { "日期 / DATE", "时间 / UTC", "频率 / MHz", "模式 / MODE", "报告 / RST" };
            int[] xs = { 70, 370, 583, 847, 1099, 1330 };
            for (int j = 0; j < keys.Length; j++)
            {
                Text(p, 1, titles[j] + " 栏名", titles[j], xs[j] + 19, 431, xs[j + 1] - xs[j] - 34, 53, 10, sub, true);
                Add(p, 1, keys[j], xs[j] + 18, 490, xs[j + 1] - xs[j] - 33, 84, j == 0 ? 17 : 19, ink, true);
            }
            Inline(p, "RIG", "rig", 77, 624, 364, sub, ink);
            Inline(p, "ANT", "antenna", 461, 624, 375, sub, ink);
            Inline(p, "PWR", "power", 858, 624, 450, sub, ink);
            Inline(p, "QSL VIA", "via", 77, 690, 650, sub, ink);
            Add(p, 1, "reply", 856, 687, 454, 56, 12, ink, true);
            Add(p, 1, "note", 77, 771, 1125, 61, 11, ink, false);
            Text(p, 1, "致意", "73", 1209, 765, 111, 75, 18, sub, true);
        }
        private static void Inline(Project p, string label, string field, int x, int y, int w, Color sub, Color ink)
        {
            int offset = label.Length >= 6 ? 154 : 91;
            Text(p, 1, label + " 栏名", label, x, y, offset - 8, 42, 8.5f, sub, true);
            Add(p, 1, field, x + offset, y - 4, w - offset, 49, 9.5f, ink, false);
        }
        private static void Text(Project p, int side, string name, string value, int x, int y, int w, int h, float pt, Color color, bool bold)
        {
            p.Layers.Add(new Layer { Name = name, Side = side, Text = value, X = x, Y = y, W = w, H = h, Size = pt,
                Color = color, Style = bold ? FontStyle.Bold : FontStyle.Regular });
        }
        private static void Add(Project p, int side, string field, int x, int y, int w, int h, float pt, Color color, bool bold)
        {
            Text(p, side, Field.Find(field).Label, "", x, y, w, h, pt, color, bold);
            p.Layers[p.Layers.Count - 1].Field = field;
        }
        internal static void Mark(Project p, int side, string name, string key, float x, float y, float size, Color color)
        { p.Layers.Add(new Layer { Kind = LayerKind.Mark, Side = side, Name = name, Symbol = key, X = x, Y = y, W = size, H = size, Color = color }); }
    }

    internal static partial class CardPainter
    {
        internal static void Paint(Graphics g, Project p, int side, RectangleF target)
        {
            PaintLayers(g, p, side, target, false, 0, p.Layers.Count);
        }

        internal static void PaintPreview(Graphics g, Project p, int side, RectangleF target, int first, int end)
        {
            PaintLayers(g, p, side, target, true, first, end);
        }

        internal static bool HasOverprintContent(Project card, int side, PointF offset = default(PointF))
        {
            var page = new RectangleF(-offset.X * 10, -offset.Y * 10, card.Width, card.Height);
            return card.Layers.Exists(layer => layer.Bounds.IntersectsWith(page) && IsOverprintLayer(card, layer, side));
        }

        private static bool IsOverprintLayer(Project card, Layer layer, int side)
        {
            if (layer.Side != side || layer.Hidden || layer.Field.Length == 0) return false;
            var field = Field.Find(layer.Field);
            if (field == null || field.Station) return false;
            string value = layer.Value(card);
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (layer.Kind == LayerKind.Text)
                return layer.Symbol != "postcode" || Field.PostalDigits(value).Length > 0;
            return layer.Kind == LayerKind.Mark && layer.Symbol.StartsWith("checkbox:", StringComparison.Ordinal)
                && MatchesCheckbox(layer, value);
        }

        internal static void PaintOverprint(Graphics g, Project card, int side, RectangleF target, PointF offset)
        {
            if (target.Width <= 0 || target.Height <= 0) return;
            var state = g.Save();
            try
            {
                SetPageTransform(g, card, target);
                g.TranslateTransform(offset.X * 10, offset.Y * 10);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                foreach (var layer in card.Layers)
                {
                    if (!IsOverprintLayer(card, layer, side)) continue;
                    string value = layer.Value(card);
                    if (layer.Kind == LayerKind.Text) DrawText(g, layer, value, false);
                    else DrawCheckbox(g, layer, value, false);
                }
            }
            finally { g.Restore(state); }
        }

        private static void SetPageTransform(Graphics g, Project card, RectangleF target)
        {
            g.TranslateTransform(target.X, target.Y);
            g.ScaleTransform(target.Width / card.Width, target.Height / card.Height);
            g.SetClip(new RectangleF(0, 0, card.Width, card.Height), CombineMode.Intersect);
        }

        private static void PaintLayers(Graphics g, Project p, int side, RectangleF target, bool preview, int first, int end)
        {
            if (target.Width <= 0 || target.Height <= 0) return;
            var state = g.Save();
            try
            {
                SetPageTransform(g, p, target);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = preview ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = preview ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                string template = p.TemplateOn(side);
                if (first == 0)
                {
                    Fill(g, p.Background[side], 0, 0, p.Width, p.Height);
                    if (side == 0) Decoration(g, p, template, side);
                }

                int rulesAt = -1;
                if (side == 1)
                {
                    // The reverse form goes over a bottom cover photo and below the fillable layers.
                    rulesAt = p.Layers.FindIndex(layer => layer.Side == side && !layer.Hidden && !layer.Cover);
                    if (rulesAt < 0) rulesAt = p.Layers.Count;
                }
                for (int i = first; i < end; i++)
                {
                    var layer = p.Layers[i];
                    if (layer.Side != side || layer.Hidden) continue;
                    if (i == rulesAt) Decoration(g, p, template, side);
                    if (layer.Kind == LayerKind.Text) DrawText(g, layer, layer.Value(p));
                    else if (layer.Kind == LayerKind.Picture) DrawPicture(g, layer, template, preview);
                    else if (CardDrawing.IsDrawing(layer)) CardDrawing.Paint(g, layer);
                    else if (layer.Symbol.StartsWith("checkbox:", StringComparison.Ordinal)) DrawCheckbox(g, layer, layer.Value(p));
                    else DrawMark(g, layer.Symbol, layer.Bounds, layer.Color);
                }
                if (rulesAt == p.Layers.Count && end == p.Layers.Count) Decoration(g, p, template, side);
                if (end == p.Layers.Count && p.IsDemo) DrawExampleNotice(g, p);
            }
            finally
            {
                g.Restore(state);
            }
        }

        private static void DrawExampleNotice(Graphics g, Project project)
        {
            var native = Templates.ReferenceSize(project.Template);
            float scale = Math.Min(project.Width / (float)native.Width, project.Height / (float)native.Height);
            var box = new RectangleF((project.Width - 820 * scale) / 2f, project.Height - 45 * scale, 820 * scale, 32 * scale);
            Fill(g, Color.FromArgb(250, 245, 237), box.X, box.Y, box.Width, box.Height);
            using (var font = GetFont("Microsoft YaHei", 23 * scale, FontStyle.Regular))
            using (var ink = new SolidBrush(Color.FromArgb(117, 59, 35)))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(Templates.ExampleNotice, font, ink, box, format);
        }

        private static void Decoration(Graphics g, Project project, string template, int side)
        {
            if (template == MaterialDesign.Layout || Templates.IsBlank(template)) return;
            var size = Templates.ReferenceSize(template);
            var state = g.Save();
            try
            {
                g.ScaleTransform(project.Width / (float)size.Width, project.Height / (float)size.Height);
                DrawRules(g, project, template, side);
            }
            finally { g.Restore(state); }
        }

        private static void DrawRules(Graphics g, Project project, string template, int side)
        {
            int i = Templates.Index(template);
            if (Templates.IsPortrait(template)) { DrawPortraitRules(g, template, side, project); return; }
            if (template == "bureau" || template == "photojournal") { DrawJournalRules(g, side, template == "bureau"); return; }
            if (Templates.IsPractical(i)) { DrawPracticalRules(g, template, side, string.IsNullOrWhiteSpace(project.Value("note"))); return; }
            if (i < Templates.BuiltInCount)
            {
                DrawPaperRules(g, side, template == "logbook" ? 1 : template == "event" ? 2 : 0);
                return;
            }
            i -= Templates.BuiltInCount;
            if (side == 1)
            {
                Color edge = i == 1 ? Color.FromArgb(183, 151, 119) : Color.FromArgb(177, 201, 194);
                Color wash = i == 1 ? Color.FromArgb(26, 178, 115, 71) : Color.FromArgb(20, 52, 118, 108);
                Fill(g, wash, 70, 418, 1260, 178);
                using (var line = new Pen(edge, 2))
                {
                    g.DrawLine(line, 70, 288, 1330, 288);
                    g.DrawRectangle(line, 70, 418, 1260, 178);
                    g.DrawLine(line, 70, 487, 1330, 487);
                    foreach (int x in new[] { 370, 583, 847, 1099 }) g.DrawLine(line, x, 418, x, 596);
                    g.DrawLine(line, 70, 754, 1330, 754);
                }
                return;
            }
            if (i == 1)
            {
                using (var line = new Pen(Color.FromArgb(158, 93, 66), 3))
                {
                    g.DrawRectangle(line, 51, 51, 1298, 798);
                    line.Width = 1;
                    g.DrawRectangle(line, 62, 62, 1276, 776);
                    g.DrawLine(line, 107, 561, 1291, 561);
                    g.DrawEllipse(line, 1120, 80, 181, 181);
                    g.DrawEllipse(line, 1128, 88, 165, 165);
                }
            }
            else if (i == 2 || i == 0)
            {
                using (var line = new Pen(Color.FromArgb(60, 136, 221, 207), 2))
                {
                    for (int a = 0; a < 11; a++)
                    {
                        var points = new PointF[95];
                        for (int j = 0; j < points.Length; j++)
                        {
                            float x = j * 16f - 50;
                            double envelope = Math.Exp(-Math.Pow((x - 1090) / 357.0, 2));
                            float y = 588 + a * 24 + (float)(Math.Sin(x / 129.0 - a * .23) * envelope * 153);
                            points[j] = new PointF(x, y);
                        }
                        g.DrawLines(line, points);
                    }
                    g.DrawLine(line, 86, 711, 1312, 711);
                    for (int j = 0; j <= 24; j++) g.DrawLine(line, 86 + 51 * j, 712, 86 + 51 * j, j % 4 == 0 ? 736 : 722);
                }
            }
        }

        private static void DrawJournalRules(Graphics g, int side, bool multiple)
        {
            using (var pen = new Pen(Color.FromArgb(78, 95, 107), 2))
            {
                if (side == 0)
                {
                    if (multiple)
                    {
                        g.DrawRectangle(pen, 50, 50, 1300, 800);
                        g.DrawLine(pen, 68, 200, 1326, 200);
                        g.DrawLine(pen, 68, 737, 1326, 737);
                    }
                    return;
                }
                g.DrawLine(pen, 60, 235, 1340, 235);
                var columns = multiple ? Templates.RoutingColumns : Templates.BureauColumns;
                int end = columns[columns.Length - 1];
                g.DrawRectangle(pen, 60, 366, end - 60, 265);
                g.DrawLine(pen, 60, 426, end, 426);
                GridLines(g, pen, columns, 366, 631);
                if (multiple)
                {
                    g.DrawRectangle(pen, 952, 257, 388, 374);
                    g.DrawLine(pen, 952, 438, 1340, 438);
                    g.DrawLine(pen, 60, 494, end, 494);
                    g.DrawLine(pen, 60, 563, end, 563);
                }
                else
                {
                    g.DrawLine(pen, 299, 299, 818, 299);
                    g.DrawLine(pen, 1040, 299, 1340, 299);
                }
                g.DrawLine(pen, 1046, 730, 1340, 730);
                g.DrawLine(pen, 195, 837, 824, 837);
            }
        }

        private static void DrawPaperRules(Graphics g, int side, int kind)
        {
            if (side == 0)
            {
                if (kind == 1)
                {
                    using (var border = new Pen(Color.FromArgb(23, 23, 23), 2.4f))
                    {
                        g.DrawRectangle(border, 48, 48, 1304, 804);
                        g.DrawLine(border, 80, 263, 1320, 263);
                        g.DrawLine(border, 80, 564, 1320, 564);
                    }
                }
                else if (kind == 2)
                {
                    using (var border = new Pen(Color.FromArgb(153, 177, 201), 2f))
                    {
                        g.DrawLine(border, 59, 259, 551, 259);
                        g.DrawLine(border, 59, 589, 551, 589);
                    }
                }
                return;
            }

            Color ink = kind == 2 ? Color.FromArgb(79, 100, 125) : Color.FromArgb(64, 66, 66);
            using (var pen = new Pen(ink, 2f))
            {
                if (kind == 1)
                {
                    g.DrawLine(pen, 420, 54, 420, 842);
                    g.DrawRectangle(pen, 449, 62, 895, 157);
                    g.DrawRectangle(pen, 449, 296, 895, 177);
                    g.DrawLine(pen, 449, 351, 1344, 351);
                    GridLines(g, pen, Templates.ColumnEdges(1), 296, 473);
                    g.DrawRectangle(pen, 449, 480, 895, 149);
                    g.DrawLine(pen, 449, 530, 1344, 530);
                    GridLines(g, pen, Templates.EquipmentEdges(1), 480, 629);
                    g.DrawLine(pen, 56, 633, 391, 633);
                    g.DrawLine(pen, 503, 752, 1344, 752);
                    g.DrawLine(pen, 657, 818, 1344, 818);
                    return;
                }

                int y = kind == 2 ? 347 : 340;
                int footer = y + 271;
                g.DrawRectangle(pen, 838, 124, 506, 137);
                if (kind == 2) g.DrawLine(pen, 56, 108, 1344, 108);
                g.DrawRectangle(pen, 56, y, 1288, 142);
                g.DrawLine(pen, 56, y + 44, 1344, y + 44);
                GridLines(g, pen, Templates.ColumnEdges(kind), y, y + 142);
                g.DrawRectangle(pen, 56, y + 142, 1288, 112);
                g.DrawLine(pen, 56, y + 188, 1344, y + 188);
                GridLines(g, pen, Templates.EquipmentEdges(kind), y + 142, y + 254);
                g.DrawLine(pen, 1068, footer + 35, 1344, footer + 35);
                g.DrawLine(pen, 56, footer + 87, 1344, footer + 87);
                g.DrawLine(pen, 282, footer + 201, 1344, footer + 201);
            }
        }

        private static void GridLines(Graphics g, Pen pen, int[] edges, int top, int bottom)
        {
            for (int i = 1; i < edges.Length - 1; i++) g.DrawLine(pen, edges[i], top, edges[i], bottom);
        }

        private static bool MatchesCheckbox(Layer layer, string value)
        {
            string option = layer.Symbol.Substring("checkbox:".Length);
            string selected = value.Trim();
            return selected.Equals(option, StringComparison.OrdinalIgnoreCase) ||
                selected.StartsWith(option + " ", StringComparison.OrdinalIgnoreCase);
        }

        private static void DrawCheckbox(Graphics g, Layer layer, string value, bool guides = true)
        {
            var box = layer.Bounds;
            using (var pen = new Pen(layer.Color, 2f))
            {
                if (guides) g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                if (MatchesCheckbox(layer, value))
                {
                    pen.Width = 3f;
                    g.DrawLines(pen, new[] {
                        new PointF(box.X + box.Width * .18f, box.Y + box.Height * .48f),
                        new PointF(box.X + box.Width * .43f, box.Y + box.Height * .76f),
                        new PointF(box.X + box.Width * .84f, box.Y + box.Height * .23f)
                    });
                }
            }
        }

        internal static Font GetFont(string name, float pixel, FontStyle style)
        {
            try { return new Font(name, pixel, style, GraphicsUnit.Pixel); }
            catch (ArgumentException) { return new Font(FontFamily.GenericSansSerif, pixel, FontStyle.Regular, GraphicsUnit.Pixel); }
        }
        internal static void PaintAddress(Graphics g, string name, string address, int x, int y)
        {
            var heading = new Layer { X = x + 28, Y = y + 25, W = 824, H = 64, Size = 11, Style = FontStyle.Bold, Color = Color.Black };
            DrawText(g, heading, name);
            var detail = new Layer { X = x + 28, Y = y + 99, W = 824, H = 212, Size = 10.5f, Field = "recipient", Color = Color.Black, Font = "Microsoft YaHei" };
            DrawText(g, detail, address);
        }
        private static void DrawText(Graphics g, Layer l, string value, bool guides = true)
        {
            if (l.Symbol == "postcode")
            {
                DrawPostalCode(g, l, Field.PostalDigits(value), guides);
                return;
            }
            if (guides && l.Symbol == "stampbox") DrawStampBox(g, l);
            if (guides && l.Symbol == "mailaddress" && string.IsNullOrWhiteSpace(value))
            {
                DrawAddressLines(g, l);
                return;
            }
            if (string.IsNullOrWhiteSpace(value)) return;
            var box = l.Bounds;
            using (var fmt = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (var b = new SolidBrush(l.Color))
            {
                fmt.Alignment = l.Align;
                fmt.LineAlignment = StringAlignment.Center;
                bool wrapAddress = l.Field == "recipient" || l.Symbol == "paragraph";
                fmt.FormatFlags = StringFormatFlags.MeasureTrailingSpaces;
                if (!wrapAddress) fmt.FormatFlags |= StringFormatFlags.NoWrap;
                else fmt.LineAlignment = StringAlignment.Near;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                float px = Math.Max(3, l.Size * (254f / 72f));
                if (l.Fit)
                {
                    using (var requested = GetFont(l.Font, px, l.Style))
                    {
                        SizeF measured = g.MeasureString(value, requested, new SizeF(wrapAddress ? Math.Max(1, box.Width - 3) : 100000, 100000), fmt);
                        if (measured.Width > 0 && measured.Height > 0)
                            px = Math.Max(3, px * Math.Min(1, Math.Min((box.Width - 3) / measured.Width, (box.Height - 1) / measured.Height)));
                    }
                }
                var clip = g.Save();
                g.SetClip(box, CombineMode.Intersect);
                using (var f = GetFont(l.Font, px, l.Style)) g.DrawString(value, f, b, box, fmt);
                g.Restore(clip);
            }
        }

        private static void DrawPicture(Graphics g, Layer l, string template, bool preview)
        {
            if (l.Image == null) return;
            lock (l.Image) DrawPictureCore(g, l, template, preview);
        }
        private static void DrawPictureCore(Graphics g, Layer l, string template, bool preview)
        {
            var img = preview ? l.Image.Preview : l.Image.Bitmap;
            var source = l.PictureSource;
            var b = l.PictureBounds(source);
            var pixels = new RectangleF(source.X * img.Width, source.Y * img.Height, source.Width * img.Width, source.Height * img.Height);
            g.DrawImage(img, b, pixels, GraphicsUnit.Pixel);
            if (l.Cover)
            {
                if (l.Side == 1) Fill(g, Color.FromArgb(214, 249, 250, 248), b.X, b.Y, b.Width, b.Height);
                else if (Templates.HasDarkPhotoFront(template) || Templates.IsLegacy(template)) using (var tint = new LinearGradientBrush(b, Color.Black, Color.Black, LinearGradientMode.Vertical))
                {
                    tint.InterpolationColors = new ColorBlend {
                        Positions = new[] { 0f, .29f, .43f, 1f },
                        Colors = Templates.HasDarkPhotoFront(template)
                            ? new[] { Color.FromArgb(154, 12, 27, 39), Color.Transparent, Color.Transparent, Color.FromArgb(183, 12, 27, 39) }
                            : new[] { Color.FromArgb(157, 6, 24, 21), Color.FromArgb(13, 6, 24, 21), Color.FromArgb(0, 6, 24, 21), Color.FromArgb(218, 6, 24, 21) }
                    };
                    g.FillRectangle(tint, b);
                }
            }
        }

        internal static void DrawMark(Graphics g, string type, RectangleF box, Color c)
        {
            var saved = g.Save();
            float size = Math.Min(box.Width, box.Height);
            g.TranslateTransform(box.X + (box.Width - size) / 2, box.Y + (box.Height - size) / 2);
            g.ScaleTransform(size / 100f, size / 100f);
            using (var pen = new Pen(c, 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (type == "tower")
                {
                    g.DrawLines(pen, new[] { new PointF(20, 87), new PointF(50, 22), new PointF(80, 87) });
                    g.DrawLine(pen, 31, 66, 68, 66); g.DrawLine(pen, 38, 48, 61, 48); g.DrawLine(pen, 50, 12, 50, 27);
                    g.DrawArc(pen, 17, 4, 66, 52, 219, 25); g.DrawArc(pen, 17, 4, 66, 52, 296, 25);
                }
                else if (type == "compass")
                {
                    g.DrawEllipse(pen, 10, 10, 80, 80);
                    g.DrawPolygon(pen, new[] { new PointF(72, 26), new PointF(59, 60), new PointF(27, 74), new PointF(40, 40) });
                }
                else if (type == "check")
                {
                    g.DrawEllipse(pen, 10, 10, 80, 80); g.DrawLines(pen, new[] { new PointF(29, 50), new PointF(43, 64), new PointF(70, 37) });
                }
                else if (type == "wave")
                {
                    var points = new PointF[42];
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new PointF(6 + i * 88f / 41, 50 + (float)(Math.Sin(i * .44) * Math.Sin(i * Math.PI / 41) * 32));
                    g.DrawLines(pen, points);
                }
                else if (type == "pin")
                {
                    using (var p = new GraphicsPath())
                    {
                        p.AddBezier(50, 91, 37, 75, 19, 55, 19, 39);
                        p.AddBezier(19, 39, 19, -1, 81, -1, 81, 39);
                        p.AddBezier(81, 39, 81, 55, 63, 75, 50, 91);
                        p.CloseFigure(); g.DrawPath(pen, p);
                    }
                    g.DrawEllipse(pen, 38, 27, 24, 24);
                }
                else if (type == "mountain")
                {
                    g.DrawLines(pen, new[] { new PointF(7, 79), new PointF(38, 26), new PointF(56, 58), new PointF(69, 39), new PointF(94, 79), new PointF(7, 79) });
                }
                else if (type == "globe")
                {
                    g.DrawEllipse(pen, 10, 10, 80, 80); g.DrawEllipse(pen, 32, 10, 36, 80);
                    g.DrawLine(pen, 12, 50, 88, 50); g.DrawArc(pen, 13, 20, 74, 60, 18, 145); g.DrawArc(pen, 13, 20, 74, 60, 198, 145);
                }
                else
                {
                    g.DrawArc(pen, 9, 11, 82, 82, 218, 104);
                    g.DrawArc(pen, 21, 23, 58, 58, 220, 100);
                    g.DrawArc(pen, 34, 36, 32, 32, 223, 94);
                    using (var b = new SolidBrush(c)) g.FillEllipse(b, 46, 48, 8, 8);
                    g.DrawLine(pen, 50, 58, 50, 82);
                    g.DrawLine(pen, 34, 82, 66, 82);
                }
            }
            g.Restore(saved);
        }
        internal static void Fill(Graphics g, Color c, float x, float y, float w, float h)
        { using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h); }
    }
}
