using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QslStudio
{
    internal sealed class StudioMenu : ContextMenuStrip
    {
        private static readonly ToolStripRenderer SurfaceRenderer = new PopupRenderer();
        private readonly List<Bitmap> icons = new List<Bitmap>();
        private readonly bool useSurface;

        internal StudioMenu(bool showIcons = false)
        {
            Font = Ui.F(13, false);
            ShowImageMargin = showIcons;
            ShowCheckMargin = false;
            int iconSize = Ui.P(16);
            ImageScalingSize = new Size(iconSize, iconSize);
            useSurface = !SystemInformation.HighContrast;
            if (useSurface)
            {
                Renderer = SurfaceRenderer;
                Padding = new Padding(Ui.P(6));
                MinimumSize = new Size(Ui.P(showIcons ? 202 : 174), 0);
            }
        }

        internal void AddCommand(string text, string icon, Action command)
        {
            var image = new Bitmap(ImageScalingSize.Width, ImageScalingSize.Height);
            using (var g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Glyph.Draw(g, icon, new RectangleF(PointF.Empty, image.Size), useSurface ? Ui.Quiet : SystemColors.MenuText);
            }
            icons.Add(image);
            Items.Add(text, image, (sender, e) => command());
        }

        protected override void OnItemAdded(ToolStripItemEventArgs e)
        {
            if (useSurface)
            {
                if (e.Item is ToolStripSeparator)
                    e.Item.Margin = new Padding(0, Ui.P(3), 0, Ui.P(3));
                else
                    e.Item.Padding = new Padding(Ui.P(7), Ui.P(7), Ui.P(12), Ui.P(7));
            }
            base.OnItemAdded(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (!useSurface || Width < 2 || Height < 2) return;
            using (var shape = Ui.Round(new RectangleF(0, 0, Width, Height), Ui.P(12)))
                Region = new Region(shape);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                foreach (var icon in icons) icon.Dispose();
        }

        private sealed class PopupRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                var g = e.Graphics;
                var state = g.Save();
                var size = e.ToolStrip.ClientSize;
                using (var baseColor = new SolidBrush(Ui.Surface))
                    g.FillRectangle(baseColor, e.AffectedBounds);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float border = Math.Max(1f, Ui.Scale) / 2f;
                var bounds = new RectangleF(border, border, size.Width - 2 * border, size.Height - 2 * border);
                Glass.Lens(g, bounds, Ui.P(12) - border, Ui.White, Color.FromArgb(239, 246, 254), Ui.Line, Ui.White);
                g.Restore(state);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Enabled || (!e.Item.Selected && !e.Item.Pressed)) return;
                var state = e.Graphics.Save();
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new RectangleF(Ui.P(5), Ui.P(1), e.Item.Width - Ui.P(10), e.Item.Height - Ui.P(2));
                Ui.Block(e.Graphics, bounds, e.Item.Pressed ? Color.FromArgb(209, 229, 252) : Ui.Tint,
                    Color.Transparent, Ui.P(7));
                e.Graphics.Restore(state);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                int left = Ui.P(e.Item.Image == null ? 14 : 38);
                var bounds = new Rectangle(left, 0, Math.Max(0, e.Item.Width - left - Ui.P(11)), e.Item.Height);
                Color ink = !e.Item.Enabled ? Ui.Quiet : e.Item.Selected ? Ui.Accent : Ui.Ink;
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, ink,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | Ui.PaintText);
            }

            protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
            {
                if (e.Image != null)
                    e.Graphics.DrawImageUnscaled(e.Image, Ui.P(12), (e.Item.Height - e.Image.Height) / 2);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int inset = Ui.P(13), center = e.Item.Height / 2;
                using (var pen = new Pen(Ui.Line))
                    e.Graphics.DrawLine(pen, inset, center, e.Item.Width - inset, center);
            }
        }
    }
}
