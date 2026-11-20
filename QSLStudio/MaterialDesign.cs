using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace QslStudio
{
    internal sealed class MaterialDesign
    {
        internal const string Layout = "material";
        internal static readonly string[] Labels = { "本台呼号", "副标题", "对方呼号", "日期 / UTC", "频率/模式/RST" };
        internal static readonly string[] Keys = { "own", "caption", "other", "utc", "report" };
        internal readonly string[] Text = new string[Keys.Length];
        internal int Width = 856, Height = 540;
        internal bool ContactSide = true, Example;
        internal bool UseArtwork;
        internal Project Artwork;
        internal CardMaterial Material;
        internal readonly PcbOptions Board = new PcbOptions();

        internal static MaterialDesign FromCard(Project card)
        {
            var draft = new MaterialDesign { Example = card.IsDemo };
            draft.Text[0] = card.Value("own");
            draft.Text[1] = card.Value("location");
            draft.Text[2] = card.Value("other");
            draft.Text[3] = (card.Value("date") + "  " + card.Value("utc")).Trim();
            string mhz = card.Value("mhz");
            decimal number;
            string frequency = decimal.TryParse(mhz, NumberStyles.AllowDecimalPoint, Files.Inv, out number) ? mhz + " MHz" : mhz;
            draft.Text[4] = (frequency + "  " + card.Value("mode") + "  " + card.Value("rst")).Trim();
            return draft;
        }

        internal bool IsExample
        {
            get
            {
                if (!Example) return false;
                return LegacyExample.IsPlaceholderCall(Text[2] ?? "") ||
                    string.IsNullOrWhiteSpace(Text[2]) && LegacyExample.IsDefault("own", (Text[0] ?? "").Trim().ToUpperInvariant());
            }
        }

        internal PcbJob Prepare()
        {
            if (UseArtwork && Artwork != null)
            {
                var custom = Artwork.Copy();
                custom.Resize(Width, Height);
                return new PcbArtwork(custom, Board.LightFront, Board.LightBack).Prepare(Board);
            }
            var card = new Project { Template = Layout };
            card.SetPageSize(Width, Height);
            float title = Math.Min(58, Height * .093f);
            float body = Math.Min(17, Height * .028f);
            AddLine(card, 0, Text[1], .14f, .11f, body);
            AddLine(card, 0, Text[0], .30f, .40f, title);
            if (IsExample) AddLine(card, 0, "版式示例", .78f, .12f, body);
            if (ContactSide)
            {
                AddLine(card, 1, IsExample ? "示例通联" : "QSL CONFIRMATION", .13f, .11f, body);
                AddLine(card, 1, Text[2], .28f, .28f, Math.Min(37, Height * .062f));
                string date = Text[3] ?? "";
                if (date.Length > 0 && date.IndexOf("UTC", StringComparison.OrdinalIgnoreCase) < 0) date += " UTC";
                AddLine(card, 1, date, .61f, .12f, body);
                AddLine(card, 1, Text[4], .76f, .12f, body);
            }
            return new PcbArtwork(card).Prepare(Board);
        }

        private static void AddLine(Project p, int side, string title, float y, float h, float points)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            p.Layers.Add(new Layer
            {
                Side = side, Name = title, Text = title, Kind = LayerKind.Text,
                X = p.Width * .08f, W = p.Width * .84f, Y = p.Height * y, H = p.Height * h,
                Font = "Microsoft YaHei", Size = points, Fit = true, Style = FontStyle.Bold,
                Align = StringAlignment.Center, Color = Color.Black
            });
        }

        internal void Save(string file)
        {
            var doc = new XElement("qsl-material",
                new XAttribute("version", Artwork == null ? 1 : 2), new XAttribute("material", Material),
                new XAttribute("width", Width), new XAttribute("height", Height),
                new XAttribute("contactSide", ContactSide), new XAttribute("example", Example),
                new XAttribute("radius", Board.Radius), new XAttribute("mask", Board.Mask),
                new XAttribute("hole", Board.HangingHole), new XAttribute("copper", Board.ExposedCopper),
                new XAttribute("custom", UseArtwork), new XAttribute("threshold", Board.Threshold),
                new XAttribute("lightFront", Board.LightFront), new XAttribute("lightBack", Board.LightBack));
            for (int i = 0; i < Keys.Length; i++) doc.Add(new XElement(Keys[i], Text[i] ?? ""));
            if (Artwork != null) doc.Add(Files.ProjectXml(Artwork));
            Files.Atomic(file, stream => doc.Save(stream));
        }

        internal static MaterialDesign Open(string file)
        {
            var root = Files.ReadXml(file, Files.MaxProjectBytes);
            CardMaterial material;
            int w, h;
            string version = root == null ? "" : Files.Attr(root, "version", "");
            if (root == null || root.Name != "qsl-material" || (version != "1" && version != "2") ||
                !Enum.TryParse(Files.Attr(root, "material", ""), out material) || !Enum.IsDefined(typeof(CardMaterial), material) ||
                !int.TryParse(Files.Attr(root, "width", ""), out w) || !int.TryParse(Files.Attr(root, "height", ""), out h) ||
                !Project.ValidPageSize(w, h))
                throw new InvalidDataException("无法读取这份特殊材质工程。");
            var draft = new MaterialDesign { Width = w, Height = h, Material = material,
                ContactSide = Files.Attr(root, "contactSide", "true") == "true",
                Example = Files.Attr(root, "example", "false") == "true" };
            draft.Board.Radius = (int)Files.Number(root, "radius", 20, 0, Math.Min(100, Math.Min(w, h) / 2));
            SolderMask mask;
            if (Enum.TryParse(Files.Attr(root, "mask", ""), out mask) && Enum.IsDefined(typeof(SolderMask), mask))
                draft.Board.Mask = mask;
            draft.Board.HangingHole = material != CardMaterial.Metal && Files.Attr(root, "hole", "false") == "true";
            draft.Board.ExposedCopper = Files.Attr(root, "copper", "false") == "true";
            draft.Board.Threshold = (int)Files.Number(root, "threshold", 150, 1, 254);
            draft.Board.LightFront = Files.Attr(root, "lightFront", "false") == "true";
            draft.Board.LightBack = Files.Attr(root, "lightBack", "false") == "true";
            if (version == "2")
            {
                int face;
                draft.Artwork = Files.ReadProject(root.Element("qslcard"), false, out face);
                draft.UseArtwork = Files.Attr(root, "custom", "false") == "true";
            }
            for (int i = 0; i < Keys.Length; i++) draft.Text[i] = Files.Cut((string)root.Element(Keys[i]) ?? "", 64).Trim();
            return draft;
        }
    }
}
