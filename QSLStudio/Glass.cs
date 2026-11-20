using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace QslStudio
{
    internal static class Glass
    {
        internal const ControlStyles Buffered = ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw;

        private static readonly Bitmap Light = CreateLight();

        internal static void PaintParent(Control child, PaintEventArgs e)
        {
            PaintSurface(e.Graphics, e.ClipRectangle, child.Parent, child.Location, child.BackColor);
        }

        private static void PaintSurface(Graphics g, Rectangle clip, Control control, Point offset, Color fallback)
        {
            var form = control as GlassForm;
            if (form != null)
            {
                form.PaintBackdrop(g, clip, offset);
                return;
            }
            if (control is SurfacePanel)
            {
                // Offset into the ancestors' bitmaps. Never ask a parent HWND to repaint.
                Point parentOffset = new Point(offset.X + control.Left, offset.Y + control.Top);
                PaintSurface(g, clip, control.Parent, parentOffset, control.BackColor);
                var panel = control as GlassPanel;
                if (panel != null) panel.PaintOverlay(g, clip, offset);
                return;
            }
            using (var brush = new SolidBrush(control == null ? fallback : control.BackColor))
                g.FillRectangle(brush, clip);
        }

        internal static void DrawSlice(Graphics g, Bitmap image, Rectangle clip, Point offset)
        {
            var src = clip;
            src.Offset(offset);
            src.Intersect(new Rectangle(Point.Empty, image.Size));
            if (src.Width <= 0 || src.Height <= 0) return;
            var dest = src;
            dest.Offset(-offset.X, -offset.Y);
            g.DrawImage(image, dest, src, GraphicsUnit.Pixel);
        }

        internal static void DrawChoiceLabel(Control control, PaintEventArgs e, int start, bool focused)
        {
            var text = new Rectangle(start, 0, Math.Max(0, control.Width - start), control.Height);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | Ui.PaintText;
            TextRenderer.DrawText(e.Graphics, control.Text, control.Font, text, control.Enabled ? control.ForeColor : Ui.Quiet, flags);
            if (focused)
            {
                int width = TextRenderer.MeasureText(control.Text, control.Font, text.Size, flags).Width;
                text.Width = Math.Min(text.Width, width + Ui.P(2));
                text.Inflate(0, -Ui.P(4));
                ControlPaint.DrawFocusRectangle(e.Graphics, text);
            }
        }

        // The reflected light comes from the editor background, so the same
        // material can be drawn without desktop composition on older Windows.
        private static Bitmap CreateLight()
        {
            var texture = new Bitmap(384, 256, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(texture))
            using (var baseCoat = new LinearGradientBrush(new Rectangle(0, 0, texture.Width, texture.Height),
                Color.FromArgb(235, 243, 250), Color.FromArgb(220, 231, 245), 94f))
            {
                g.FillRectangle(baseCoat, 0, 0, texture.Width, texture.Height);
                Wash(g, new RectangleF(-172, -99, 410, 330), Color.FromArgb(161, 161, 206, 242));
                Wash(g, new RectangleF(18, 11, 277, 240), Color.FromArgb(183, 247, 252, 255));
                Wash(g, new RectangleF(167, -157, 346, 359), Color.FromArgb(151, 177, 183, 239));
                Wash(g, new RectangleF(-124, 96, 395, 276), Color.FromArgb(133, 169, 223, 228));
                Wash(g, new RectangleF(234, 74, 301, 310), Color.FromArgb(146, 181, 201, 241));
            }
            return texture;
        }

        private static void Wash(Graphics g, RectangleF bounds, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(bounds);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = color;
                    brush.SurroundColors = new[] { Color.FromArgb(0, color) };
                    brush.Blend = new Blend { Positions = new[] { 0f, .25f, .78f, 1f }, Factors = new[] { 0f, .12f, .85f, 1f } };
                    g.FillPath(brush, path);
                }
            }
        }

        internal static Bitmap Backdrop(Size size)
        {
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(Light, new Rectangle(Point.Empty, size), 0, 0, Light.Width, Light.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            return bitmap;
        }

        internal static void Shadow(Graphics g, RectangleF bounds, float radius, int spread, int offset, Color color)
        {
            bounds.Offset(0, Ui.P(offset));
            for (int i = spread; i > 0; i--)
            {
                float swell = Ui.P(i);
                float density = (spread - i + 1f) / spread;
                int alpha = Math.Max(1, (int)Math.Round(color.A * density * density));
                Ui.Block(g, RectangleF.Inflate(bounds, swell, swell), Color.FromArgb(alpha, color),
                    Color.Transparent, radius + swell);
            }
        }

        internal static void Lens(Graphics g, RectangleF bounds, float radius, Color top, Color center, Color lowerEdge, Color highlight)
        {
            if (bounds.Width <= 2 * Ui.Scale || bounds.Height <= 2 * Ui.Scale) return;
            using (var shape = Ui.Round(bounds, radius))
            using (var fill = new LinearGradientBrush(bounds, top, center, 90f))
            using (var rim = new LinearGradientBrush(bounds, highlight, lowerEdge, 74f))
            using (var border = new Pen(rim, Math.Max(1f, Ui.Scale)))
            {
                fill.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { top, center, Color.FromArgb((top.A + center.A) / 2, center) },
                    Positions = new[] { 0f, .57f, 1f }
                };
                g.FillPath(fill, shape);
                g.DrawPath(border, shape);
            }

            var inner = RectangleF.Inflate(bounds, -Ui.Scale, -Ui.Scale);
            using (var shape = Ui.Round(inner, radius - Ui.Scale))
            using (var shine = new LinearGradientBrush(inner, Color.FromArgb(highlight.A / 2, highlight),
                Color.FromArgb(0, highlight), 90f))
            using (var pen = new Pen(shine, Ui.Scale))
                g.DrawPath(pen, shape);
        }
    }

    internal class GlassForm : Form
    {
        private const int DwmCaptionColor = 35, DwmTextColor = 36;
        private Bitmap backdrop;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        protected GlassForm()
        {
            BackColor = Ui.Desk;
            SetStyle(Glass.Buffered, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var windows = Environment.OSVersion.Version;
            if (windows.Major < 10 || windows.Build < 22000 || SystemInformation.HighContrast) return;
            int caption = ColorTranslator.ToWin32(Color.FromArgb(232, 240, 250));
            int text = ColorTranslator.ToWin32(Ui.Ink);
            DwmSetWindowAttribute(Handle, DwmCaptionColor, ref caption, sizeof(int));
            DwmSetWindowAttribute(Handle, DwmTextColor, ref text, sizeof(int));
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            PaintBackdrop(e.Graphics, e.ClipRectangle, Point.Empty);
        }

        internal void PaintBackdrop(Graphics g, Rectangle clip, Point offset)
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
            if (backdrop == null || backdrop.Size != ClientSize)
            {
                if (backdrop != null) { backdrop.Dispose(); backdrop = null; }
                backdrop = Glass.Backdrop(ClientSize);
            }
            Rectangle requested = clip;
            requested.Offset(offset);
            if (!ClientRectangle.Contains(requested))
                using (var brush = new SolidBrush(BackColor)) g.FillRectangle(brush, clip);
            Glass.DrawSlice(g, backdrop, clip, offset);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Invalidate(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && backdrop != null) backdrop.Dispose();
            base.Dispose(disposing);
        }
    }

    internal class SurfacePanel : Panel
    {
        internal SurfacePanel()
        {
            SetStyle(Glass.Buffered, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Glass.PaintParent(this, e);
        }

        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);
            Invalidate(true);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (AutoScroll) Invalidate(true);
        }
    }

    internal sealed class GlassPanel : SurfacePanel
    {
        private readonly int radius;
        private readonly bool track;
        private Bitmap overlay;

        internal GlassPanel(int radius, bool track = false)
        {
            this.radius = radius;
            this.track = track;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            PaintOverlay(e.Graphics, e.ClipRectangle, Point.Empty);
        }

        internal void PaintOverlay(Graphics graphics, Rectangle clip, Point offset)
        {
            if (Width < 10 || Height < 10) return;
            if (overlay == null || overlay.Size != Size)
            {
                if (overlay != null) { overlay.Dispose(); overlay = null; }
                overlay = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(overlay))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var rect = new RectangleF(Ui.P(3), Ui.P(2), Width - Ui.P(6), Height - Ui.P(8));
                    float corner = Ui.P(radius);
                    if (track)
                    {
                        Glass.Lens(g, rect, corner, Color.FromArgb(36, 112, 138, 173), Color.FromArgb(24, 144, 166, 192),
                            Color.FromArgb(172, Color.White), Color.FromArgb(45, 83, 121, 165));
                    }
                    else
                    {
                        Glass.Shadow(g, rect, corner, 4, 2, Color.FromArgb(10, 42, 71, 116));
                        Glass.Lens(g, rect, corner, Color.FromArgb(201, Color.White), Color.FromArgb(112, 250, 253, 255),
                            Color.FromArgb(114, 131, 155, 188), Color.White);
                    }
                }
            }
            Glass.DrawSlice(graphics, overlay, clip, offset);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && overlay != null) overlay.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class SurfaceLabel : Label
    {
        protected override void OnPaintBackground(PaintEventArgs e) { Glass.PaintParent(this, e); }
    }

    internal sealed class SurfaceLink : LinkLabel
    {
        protected override void OnPaintBackground(PaintEventArgs e) { Glass.PaintParent(this, e); }
    }

    internal sealed class SurfaceCheckBox : CheckBox
    {
        internal SurfaceCheckBox() { SetStyle(Glass.Buffered, true); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Glass.PaintParent(this, e);
            CheckBoxState state = CheckState == CheckState.Indeterminate
                ? (Enabled ? CheckBoxState.MixedNormal : CheckBoxState.MixedDisabled)
                : Checked ? (Enabled ? CheckBoxState.CheckedNormal : CheckBoxState.CheckedDisabled)
                : (Enabled ? CheckBoxState.UncheckedNormal : CheckBoxState.UncheckedDisabled);
            Size size = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
            CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(0, (Height - size.Height) / 2), state);
            Glass.DrawChoiceLabel(this, e, size.Width + Ui.P(5), Focused && ShowFocusCues);
        }
    }

    internal sealed class SurfaceRadioButton : RadioButton
    {
        internal SurfaceRadioButton() { SetStyle(Glass.Buffered, true); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Glass.PaintParent(this, e);
            RadioButtonState state = Checked ? (Enabled ? RadioButtonState.CheckedNormal : RadioButtonState.CheckedDisabled)
                : (Enabled ? RadioButtonState.UncheckedNormal : RadioButtonState.UncheckedDisabled);
            Size size = RadioButtonRenderer.GetGlyphSize(e.Graphics, state);
            RadioButtonRenderer.DrawRadioButton(e.Graphics, new Point(0, (Height - size.Height) / 2), state);
            Glass.DrawChoiceLabel(this, e, size.Width + Ui.P(5), Focused && ShowFocusCues);
        }
    }
}
