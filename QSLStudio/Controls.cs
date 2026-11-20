using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QslStudio
{
    internal static class Ui
    {
        internal const TextFormatFlags PaintText = TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;
        internal static readonly Color Ink = Color.FromArgb(27, 43, 64), Quiet = Color.FromArgb(67, 84, 109),
            Surface = Color.FromArgb(245, 249, 253), Desk = Color.FromArgb(220, 231, 245),
            Line = Color.FromArgb(195, 209, 225), Accent = Color.FromArgb(18, 76, 155),
            Tint = Color.FromArgb(224, 239, 255), White = Color.White;
        internal static float Scale = 1;
        private static string face = "Tahoma";
        private static readonly Dictionary<int, Font> FontCache = new Dictionary<int, Font>();
        internal static void Init()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = Math.Max(1, g.DpiX / 96f);
            foreach (var name in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimSun" })
            {
                using (var font = new Font(name, 12f))
                using (var family = font.FontFamily)
                    if (string.Equals(family.GetName(1033), name, StringComparison.OrdinalIgnoreCase)) { face = name; break; }
            }
        }
        internal static int P(float px) { return (int)Math.Round(px * Scale); }
        internal static Font F(int size, bool bold)
        {
            int key = size * 2 + (bold ? 1 : 0);
            Font f;
            if (!FontCache.TryGetValue(key, out f)) FontCache[key] = f = CardPainter.GetFont(face, size * Scale, bold ? FontStyle.Bold : FontStyle.Regular);
            return f;
        }
        internal static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 2) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            return p;
        }
        internal static void Block(Graphics g, RectangleF rect, Color fill, Color border, float radius)
        {
            if (rect.Width < 1 || rect.Height < 1) return;
            using (var p = Round(rect, radius))
            {
                if (fill.A != 0) using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                if (border.A != 0) using (var pen = new Pen(border, Math.Max(1, Scale))) g.DrawPath(pen, p);
            }
        }
        internal static Label Label(string text, int size, bool bold, Color color)
        {
            return new SurfaceLabel { Text = text, Font = F(size, bold), ForeColor = color,
                TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false, AutoEllipsis = true };
        }
        internal static int TextWidth(string text, Font font)
        {
            int width = TextRenderer.MeasureText(text, font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
            return (int)Math.Ceiling(width / Scale);
        }
        internal static int ScrollContentWidth(ScrollableControl panel, int margins)
        {
            int scrollbar = Math.Max(SystemInformation.VerticalScrollBarWidth, P(16));
            return Math.Max(1, (int)Math.Floor((panel.Width - scrollbar) / Scale) - margins);
        }
        internal static void Put(Control c, int x, int y, int w, int h)
        { c.SetBounds(P(x), P(y), Math.Max(0, P(w)), Math.Max(0, P(h))); }
        internal static Panel Panel(Color color)
        { return new Panel { BackColor = color, Margin = Padding.Empty, Padding = Padding.Empty }; }
    }

    internal sealed class ButtonX : Button
    {
        private bool hover, down;
        internal string IconKey;
        internal bool Primary, Chosen, Border, Pill;
        internal ButtonX(string title, string icon)
        {
            Text = title; IconKey = icon; Font = Ui.F(13, false); UseMnemonic = false; Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            SetStyle(Glass.Buffered, true);
            UseVisualStyleBackColor = false;
            AccessibleName = title;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            // ButtonBase can skip the background pass. Fill the buffer before drawing any translucent pixels.
            Glass.PaintParent(this, e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fore = !Enabled ? Color.FromArgb(120, 137, 158) : Primary ? Ui.White : Chosen ? Ui.Accent : Ui.Ink;
            var bounds = new RectangleF(Ui.P(1), Ui.P(1), Width - Ui.P(2), Height - Ui.P(2));
            float radius = Pill || Primary ? Height / 2f : Ui.P(9);
            if (Enabled && Primary)
            {
                var color = down ? Color.FromArgb(16, 66, 134) : hover ? Color.FromArgb(25, 87, 173) : Ui.Accent;
                Glass.Lens(g, bounds, radius, down ? Ui.Accent : Color.FromArgb(47, 115, 203), color,
                    Color.FromArgb(18, 62, 131), Color.FromArgb(229, 242, 255));
            }
            else if (Border || Chosen || Primary)
            {
                var color = !Enabled ? Color.FromArgb(78, Ui.White) : down ? Color.FromArgb(232, Ui.Tint)
                    : Color.FromArgb(Chosen || hover ? 219 : 133, Ui.White);
                Glass.Lens(g, bounds, radius, Color.FromArgb(Enabled ? 246 : 140, Ui.White), color,
                    Chosen && Enabled ? Color.FromArgb(137, 96, 143, 205) : Color.FromArgb(115, 128, 152, 182), Ui.White);
            }
            else if (Enabled && (hover || down))
                Ui.Block(g, bounds, Color.FromArgb(down ? 113 : 106, down ? Ui.Tint : Ui.White), Color.Transparent, radius);
            if (Focused && ShowFocusCues)
                Ui.Block(g, RectangleF.Inflate(bounds, -Ui.Scale, -Ui.Scale), Color.Transparent, Primary ? Color.White : Ui.Accent, radius);
            int ix = string.IsNullOrEmpty(IconKey) ? 0 : Ui.P(16);
            int gap = ix != 0 && Text.Length != 0 ? Ui.P(6) : 0;
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            int tw = Text.Length == 0 ? 0 : TextRenderer.MeasureText(Text, Font, new Size(Width, Height), flags).Width;
            int left = Math.Max(Ui.P(5), (Width - ix - gap - tw) / 2);
            if (ix > 0) Glyph.Draw(g, IconKey, new RectangleF(left, (Height - ix) / 2f, ix, ix), fore);
            if (tw > 0) TextRenderer.DrawText(g, Text, Font, new Rectangle(left + ix + gap, 0, Math.Max(0, Width - left - ix - gap - Ui.P(4)), Height), fore, flags | Ui.PaintText);
        }
    }

    internal static class Glyph
    {
        internal static void Draw(Graphics g, string icon, RectangleF box, Color color)
        {
            var s = g.Save();
            g.TranslateTransform(box.X, box.Y); g.ScaleTransform(box.Width / 24f, box.Height / 24f);
            using (var p = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (icon)
                {
                    case "new": g.DrawRectangle(p, 5, 3, 14, 18); g.DrawLine(p, 8, 12, 16, 12); g.DrawLine(p, 12, 8, 12, 16); break;
                    case "open": Lines(g,p,3,8,3,20,20,20,22,9,12,9,9,5,3,5,3,8); break;
                    case "save": Lines(g,p,4,3,16,3,20,7,20,21,4,21,4,3); g.DrawRectangle(p, 8, 3, 7, 6); g.DrawRectangle(p, 8, 13, 8, 8); break;
                    case "export": Lines(g,p,4,14,4,20,20,20,20,14); g.DrawLine(p,12,3,12,15); Lines(g,p,7,10,12,15,17,10); break;
                    case "pcb":
                        g.DrawRectangle(p, 3, 5, 18, 14);
                        g.DrawEllipse(p, 6, 8, 3, 3); g.DrawEllipse(p, 15, 13, 3, 3);
                        Lines(g, p, 9, 10, 12, 10, 12, 14, 15, 14); break;
                    case "draw": Lines(g, p, 4, 19, 5, 14, 16, 3, 21, 8, 10, 19, 4, 20, 4, 19);
                        Lines(g, p, 14, 5, 19, 10); break;
                    case "add": g.DrawLine(p,5,12,19,12); g.DrawLine(p,12,5,12,19); break;
                    case "minus": g.DrawLine(p,5,12,19,12); break;
                    case "image": g.DrawRectangle(p,3,4,18,16); g.DrawEllipse(p,13,7,3,3); Lines(g,p,4,17,9,11,13,16,16,13,20,17); break;
                    case "cover": g.DrawRectangle(p,3,4,18,16); Lines(g,p,3,14,8,9,14,15,18,11,21,14); g.DrawLine(p,3,17,21,17); break;
                    case "assets": g.DrawRectangle(p,3,3,7,7); g.DrawRectangle(p,14,3,7,7); g.DrawRectangle(p,3,14,7,7); g.DrawEllipse(p,14,14,7,7); break;
                    case "text": Lines(g,p,4,7,4,4,20,4,20,7); g.DrawLine(p,12,4,12,20); g.DrawLine(p,8,20,16,20); break;
                    case "undo": Lines(g,p,9,5,4,10,9,15); g.DrawArc(p,4,10,16,10,270,177); break;
                    case "redo": Lines(g,p,15,5,20,10,15,15); g.DrawArc(p,4,10,16,10,90,177); break;
                    case "grid": g.DrawRectangle(p,4,4,16,16); g.DrawLine(p,4,12,20,12); g.DrawLine(p,12,4,12,20); break;
                    case "fit": Lines(g,p,9,4,4,4,4,9); Lines(g,p,15,4,20,4,20,9); Lines(g,p,4,15,4,20,9,20); Lines(g,p,15,20,20,20,20,15); break;
                    case "back": Lines(g,p,14,5,7,12,14,19); break;
                    case "up": Lines(g,p,6,12,12,6,18,12); g.DrawLine(p,12,7,12,20); break;
                    case "down": Lines(g,p,6,12,12,18,18,12); g.DrawLine(p,12,4,12,17); break;
                    case "chevron": Lines(g,p,5,9,12,16,19,9); break;
                    case "remove": Lines(g,p,6,6,7,21,17,21,18,6); g.DrawLine(p,4,6,20,6); Lines(g,p,9,6,9,3,15,3,15,6); break;
                    case "copy": g.DrawRectangle(p,8,8,12,12); Lines(g,p,5,16,4,16,4,4,16,4,16,5); break;
                    case "left": case "center": case "right":
                        for (int i=0; i<4; i++) { int start=i%2==0||icon=="left"?4:icon=="center"?7:10; g.DrawLine(p,start,5+i*5,start+(i%2==0?16:10),5+i*5); } break;
                    case "help": g.DrawEllipse(p,3,3,18,18); g.DrawArc(p,9,7,6,5,180,215); Lines(g,p,14,11,12,13,12,14); g.DrawLine(p,12,17,12,17.1f); break;
                    case "more": for (int i=0; i<3; i++) g.DrawEllipse(p,5+i*6,11,1.7f,1.7f); break;
                    case "layers": Lines(g,p,3,8,12,3,21,8,12,13,3,8); Lines(g,p,3,12,12,17,21,12); Lines(g,p,3,16,12,21,21,16); break;
                    case "eye": g.DrawEllipse(p,2,6,20,12); g.DrawEllipse(p,9,9,6,6); break;
                }
            }
            g.Restore(s);
        }
        private static void Lines(Graphics g, Pen p, params float[] vals)
        {
            var points = new PointF[vals.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new PointF(vals[i * 2], vals[i * 2 + 1]);
            g.DrawLines(p, points);
        }
    }

    internal sealed class InputBox : SurfacePanel
    {
        internal readonly TextBox Edit;
        internal InputBox(string label, bool multiline)
        {
            TabStop = false;
            Edit = new TextBox { BorderStyle = BorderStyle.None, BackColor = Ui.White, ForeColor = Ui.Ink,
                Font = Ui.F(13, false), Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                AccessibleName = label, MaxLength = multiline ? 420 : 120 };
            Edit.Enter += (s, e) => Invalidate(); Edit.Leave += (s, e) => Invalidate();
            Controls.Add(Edit);
            Click += (s,e) => Edit.Focus();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Edit == null) return;
            int y = Edit.Multiline ? Ui.P(7) : Math.Max(Ui.P(3), (Height - Edit.PreferredHeight) / 2);
            Edit.SetBounds(Ui.P(9), y, Math.Max(0, Width - Ui.P(18)), Math.Max(0, Height - y - Ui.P(6)));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Ui.Block(e.Graphics, new RectangleF(1, 1, Width - 2, Height - 2), Ui.White,
                Edit.Focused ? Ui.Accent : Color.FromArgb(155, Ui.Line), Ui.P(8));
        }
    }

    internal sealed class TemplateTile : Button
    {
        private readonly Bitmap[] thumbs = new Bitmap[2];
        private readonly string key;
        private int side;
        private bool hover;
        internal bool Chosen;
        internal TemplateTile(string title, string key, int side)
        {
            AccessibleName = title; Text = title; Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            SetStyle(Glass.Buffered, true);
            UseVisualStyleBackColor = false;
            this.key = key;
            this.side = side;
        }
        internal void ShowSide(int side)
        {
            if (this.side == side) return;
            this.side = side;
            Invalidate();
        }
        internal static string PreviewName(string key, int side) { return "preview-" + key + "-" + side + ".jpg"; }
        internal static Bitmap RenderPreview(string key, int side)
        {
            var p = Templates.Create(key);
            var image = new Bitmap(p.Portrait ? 252 : 392, p.Portrait ? 392 : 252);
            using (var g = Graphics.FromImage(image))
                CardPainter.Paint(g, p, side, new RectangleF(0, 0, image.Width, image.Height));
            return image;
        }
        private Bitmap Preview()
        {
            if (thumbs[side] == null)
                using (var stream = typeof(TemplateTile).Assembly.GetManifestResourceStream(PreviewName(key, side)))
                {
                    if (stream == null) thumbs[side] = RenderPreview(key, side);
                    else using (var bitmap = new Bitmap(stream)) thumbs[side] = new Bitmap(bitmap);
                }
            return thumbs[side];
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Glass.PaintParent(this, e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var frame = new RectangleF(Ui.P(2), Ui.P(2), Width - Ui.P(4), Height - Ui.P(5));
            Glass.Shadow(g, frame, Ui.P(12), 3, 2, Color.FromArgb(7, 36, 65, 108));
            Glass.Lens(g, frame, Ui.P(12), Color.FromArgb(235, Ui.White),
                Color.FromArgb(hover ? 211 : 138, Chosen ? Ui.Tint : Ui.White), Color.FromArgb(148, 125, 150, 182), Ui.White);
            if (Chosen || (Focused && ShowFocusCues))
                Ui.Block(g, frame, Color.Transparent, Ui.Accent, Ui.P(12));
            var dest = new RectangleF(Ui.P(8), Ui.P(7), Width - Ui.P(16), Height - Ui.P(34));
            g.InterpolationMode = InterpolationMode.Bilinear;
            var thumb = Preview();
            float scale = Math.Min(dest.Width / thumb.Width, dest.Height / thumb.Height);
            var image = new RectangleF(dest.X + (dest.Width - thumb.Width * scale) / 2,
                dest.Y + (dest.Height - thumb.Height * scale) / 2, thumb.Width * scale, thumb.Height * scale);
            g.DrawImage(thumb, image);
            Ui.Block(g, image, Color.Transparent, Color.FromArgb(155, Ui.Line), 0);
            TextRenderer.DrawText(g, Text, Ui.F(11, Chosen), new Rectangle(0, Height - Ui.P(24), Width, Ui.P(19)), Chosen ? Ui.Accent : Ui.Quiet,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | Ui.PaintText);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var thumb in thumbs) if (thumb != null) thumb.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed partial class Canvas : Control
    {
        internal Project Card;
        internal string SelectedId;
        internal int Side;
        internal bool Guides;
        internal float Zoom = 1;
        internal event Action<string> Selected;
        internal event Action StartedChange, FinishedChange, ViewChanged, EditSelected;
        private PointF pan;
        private RectangleF sheet;
        private float scale;
        private Point start;
        private RectangleF initial;
        private PointF initialPan;
        private Layer moving;
        private bool movingResize, panning, started;
        private Bitmap previewBitmap;
        private Project previewCard;
        private int previewSide, previewLayerEnd;
        private Rectangle previewViewport;
        private RectangleF previewSheet;
        private bool previewDirty = true;

        internal Canvas()
        {
            SetStyle(Glass.Buffered | ControlStyles.Selectable, true);
            TabStop = true;
            AccessibleName = "QSL 卡片画布";
        }
        internal void Fit() { Zoom = 1; pan = PointF.Empty; Invalidate(); if (ViewChanged != null) ViewChanged(); }
        internal void InvalidateContent()
        {
            previewDirty = true;
            Invalidate();
        }
        internal void StepZoom(float ratio)
        {
            Zoom = Math.Max(.5f, Math.Min(3f, Zoom * ratio));
            if (Zoom <= 1.01f) pan = PointF.Empty;
            Invalidate(); if (ViewChanged != null) ViewChanged();
        }
        private float FitScale()
        {
            int pad = Ui.P(30);
            return Math.Max(.02f, Math.Min((Width - 2f * pad) / Card.Width, (Height - 2f * pad) / Card.Height)) * Zoom;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { Glass.PaintParent(this, e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Card == null) return;
            var g = e.Graphics;
            scale = FitScale();
            float sw = Card.Width * scale, sh = Card.Height * scale;
            sheet = new RectangleF((Width - sw) / 2 + pan.X, (Height - sh) / 2 + pan.Y, sw, sh);
            var shadowClip = g.Save();
            // The opaque card covers the interior of every shadow pass.
            g.ExcludeClip(Rectangle.FromLTRB((int)Math.Ceiling(sheet.Left), (int)Math.Ceiling(sheet.Top),
                (int)Math.Floor(sheet.Right), (int)Math.Floor(sheet.Bottom)));
            Glass.Shadow(g, sheet, 0, 10, 6, Color.FromArgb(12, 35, 58, 91));
            g.Restore(shadowClip);
            PaintCard(g);
            if (Side == 0)
            {
                var empty = Card.Layers.Find(layer => layer.Side == Side && layer.Cover && !layer.Hidden && layer.Image == null);
                if (empty != null)
                {
                    var area = Rectangle.Round(RectangleF.Intersect(ClientRectangle, ScreenBox(empty.Bounds)));
                    if (area.Width > 0 && area.Height > 0)
                    {
                        Color color = Card.Background[Side].GetBrightness() < .45f ? Color.Gainsboro : Ui.Quiet;
                        TextRenderer.DrawText(g, "点「底图」添加照片", Ui.F(13, false), area, color,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | Ui.PaintText);
                    }
                }
            }
            if (Guides)
            {
                var safe = RectangleF.Inflate(sheet, -30 * scale, -30 * scale);
                using (var p = new Pen(Color.FromArgb(177, Ui.Accent), Ui.P(1)) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(p, safe.X, safe.Y, safe.Width, safe.Height);
                    g.DrawLine(p, sheet.Left + sw / 2, sheet.Top, sheet.Left + sw / 2, sheet.Bottom);
                    g.DrawLine(p, sheet.Left, sheet.Top + sh / 2, sheet.Right, sheet.Top + sh / 2);
                }
            }
            PaintSketch(g);
            var selected = IsDrawing || SelectedId == null ? null : Card.Find(SelectedId);
            if (selected != null && selected.Side == Side && !selected.Hidden)
            {
                var b = ScreenBox(selected.Bounds);
                using (var bg = new Pen(Ui.White, Ui.P(3))) g.DrawRectangle(bg, b.X, b.Y, b.Width, b.Height);
                using (var pen = new Pen(Ui.Accent, Ui.P(1))) g.DrawRectangle(pen, b.X, b.Y, b.Width, b.Height);
                var h = HandleRect(b);
                using (var b2 = new SolidBrush(Ui.White)) g.FillRectangle(b2, h);
                using (var pen = new Pen(Ui.Accent, Ui.P(1))) g.DrawRectangle(pen, h.X, h.Y, h.Width, h.Height);
            }
        }
        private void PaintCard(Graphics graphics)
        {
            var view = Rectangle.Intersect(ClientRectangle, Rectangle.FromLTRB(
                (int)Math.Floor(sheet.Left), (int)Math.Floor(sheet.Top),
                (int)Math.Ceiling(sheet.Right), (int)Math.Ceiling(sheet.Bottom)));
            if (view.Width <= 0 || view.Height <= 0) return;

            int end = Card.Layers.Count;
            int staticEnd = started && moving != null ? Card.Layers.IndexOf(moving) : end;
            if (staticEnd <= 0)
            {
                CardPainter.PaintPreview(graphics, Card, Side, sheet, 0, end);
                return;
            }

            // While moving a layer, reuse everything under it. Repaint that layer and
            // the upper layers live so overlapping photos and text retain their order.
            if (previewDirty || previewCard != Card || previewSide != Side || previewLayerEnd != staticEnd ||
                previewViewport != view || previewSheet != sheet)
            {
                if (previewBitmap == null || previewBitmap.Width != view.Width || previewBitmap.Height != view.Height)
                {
                    if (previewBitmap != null) previewBitmap.Dispose();
                    previewBitmap = new Bitmap(view.Width, view.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                }
                var visibleSheet = new RectangleF(sheet.X - view.X, sheet.Y - view.Y, sheet.Width, sheet.Height);
                using (var cached = Graphics.FromImage(previewBitmap))
                {
                    cached.Clear(Color.Transparent);
                    CardPainter.PaintPreview(cached, Card, Side, visibleSheet, 0, staticEnd);
                }
                previewCard = Card;
                previewSide = Side;
                previewLayerEnd = staticEnd;
                previewViewport = view;
                previewSheet = sheet;
                previewDirty = false;
            }

            graphics.DrawImageUnscaled(previewBitmap, view.Location);
            if (staticEnd < end) CardPainter.PaintPreview(graphics, Card, Side, sheet, staticEnd, end);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (previewBitmap != null) previewBitmap.Dispose();
                previewBitmap = null;
                previewCard = null;
            }
            base.Dispose(disposing);
        }

        private RectangleF ScreenBox(RectangleF r) { return new RectangleF(sheet.X + r.X * scale, sheet.Y + r.Y * scale, r.Width * scale, r.Height * scale); }
        private RectangleF HandleRect(RectangleF r) { int k = Ui.P(5); return new RectangleF(r.Right - k, r.Bottom - k, k * 2, k * 2); }
        private Layer Hit(Point pt)
        {
            if (Card == null || !sheet.Contains(pt.X, pt.Y)) return null;
            for (int i = Card.Layers.Count - 1; i >= 0; i--)
            {
                var l = Card.Layers[i];
                var box = ScreenBox(l.Bounds);
                if (CardDrawing.IsDrawing(l)) box.Inflate(Ui.P(3), Ui.P(3));
                if (l.Side == Side && !l.Hidden && box.Contains(pt.X, pt.Y)) return l;
            }
            return null;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus(); start = e.Location;
            if (e.Button == MouseButtons.Middle) { panning = true; initialPan = pan; Capture = true; Cursor = Cursors.Hand; return; }
            if (e.Button != MouseButtons.Left || Card == null) return;
            if (BeginSketch(e.Location)) return;
            var old = SelectedId == null ? null : Card.Find(SelectedId);
            movingResize = old != null && !old.Hidden && HandleRect(ScreenBox(old.Bounds)).Contains(e.X, e.Y);
            moving = movingResize ? old : Hit(e.Location);
            if (Selected != null) Selected(moving == null ? null : moving.Id);
            if (moving != null) { initial = moving.Bounds; Capture = true; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float dx = e.X - start.X, dy = e.Y - start.Y;
            if (panning)
            {
                var position = new PointF(initialPan.X + dx, initialPan.Y + dy);
                if (position != pan) { pan = position; Invalidate(); }
                return;
            }
            if (IsDrawing)
            {
                ExtendSketch(e.Location);
                Cursor = Cursors.Cross;
                return;
            }
            if (moving != null && Capture)
            {
                if (!started && (Math.Abs(dx) > Ui.P(2) || Math.Abs(dy) > Ui.P(2)))
                { started = true; if (StartedChange != null) StartedChange(); }
                if (!started) return;
                dx /= scale; dy /= scale;
                var previous = moving.Bounds;
                if (movingResize)
                {
                    float w = initial.Width + dx, h = initial.Height + dy;
                    if ((ModifierKeys & Keys.Shift) != 0) h = w * initial.Height / initial.Width;
                    moving.W = Math.Max(1, Math.Min(2 * Card.Width, w)); moving.H = Math.Max(1, Math.Min(2 * Card.Height, h));
                    Cursor = Cursors.SizeNWSE;
                }
                else
                {
                    float x = initial.X + dx, y = initial.Y + dy;
                    if (Guides && (ModifierKeys & Keys.Alt) == 0) { x = (float)Math.Round(x / 10) * 10; y = (float)Math.Round(y / 10) * 10; }
                    moving.X = Project.ConstrainOffset(x, moving.W, Card.Width);
                    moving.Y = Project.ConstrainOffset(y, moving.H, Card.Height);
                    Cursor = Cursors.SizeAll;
                }
                if (previous != moving.Bounds) Invalidate();
                return;
            }
            var active = Card == null || SelectedId == null ? null : Card.Find(SelectedId);
            Cursor = active != null && !active.Hidden && HandleRect(ScreenBox(active.Bounds)).Contains(e.X, e.Y) ? Cursors.SizeNWSE : Hit(e.Location) != null ? Cursors.SizeAll : Cursors.Default;
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && FinishSketch(e.Location)) return;
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle) EndMove();
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) { DropSketch(); EndMove(); }
        }
        private void EndMove()
        {
            bool changed = started;
            started = false; moving = null; panning = false;
            if (Capture) Capture = false;
            if (changed)
            {
                InvalidateContent();
                if (FinishedChange != null) FinishedChange();
            }
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (!IsDrawing && e.Button == MouseButtons.Left && EditSelected != null)
            {
                EndMove();
                EditSelected();
            }
        }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); StepZoom(e.Delta > 0 ? 1.14f : 1 / 1.14f); }
    }
}
