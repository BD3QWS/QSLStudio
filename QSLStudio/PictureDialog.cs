using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QslStudio
{
    internal sealed class PictureDialog : StudioDialog
    {
        private readonly CropCanvas source;
        private readonly CardImagePreview preview;
        private readonly Layer picture;
        private readonly Timer refresh = new Timer { Interval = 45 };
        private readonly ToolTip modeTips = new ToolTip();
        private readonly ButtonX fill, fit, stretch;
        private readonly Label size;

        internal PictureFit Mode { get; private set; }
        internal RectangleF Selection { get { return source.Selection; } }

        private static int Available(int pixels, int maximum, int minimum)
        {
            return Math.Min(maximum, Math.Max(minimum, (int)(pixels / Ui.Scale) - 43));
        }

        internal PictureDialog(Project card, Layer draft) : base("裁切与拉伸",
            Available(Screen.PrimaryScreen.WorkingArea.Width, 870, 398),
            Available(Screen.PrimaryScreen.WorkingArea.Height, 586, 301))
        {
            int w = (int)Math.Round(ClientSize.Width / Ui.Scale);
            int h = (int)Math.Round(ClientSize.Height / Ui.Scale);
            const int margin = 18, gap = 14, top = 120;
            int leftWidth = (w - 2 * margin - gap) * 59 / 100;
            int right = margin + leftWidth + gap, rightWidth = w - right - margin;

            LabelAt(this, draft.Cover ? "调整底图" : "调整图片", margin, 9, 230, 35, 20, true, Ui.Ink);
            var modes = new GlassPanel(18, true);
            Controls.Add(modes); Ui.Put(modes, margin - 2, 49, 256, 40);
            fill = ButtonAt(modes, "等比铺满", "", 6, 4, 81, 30, () => Choose(PictureFit.Fill));
            fit = ButtonAt(modes, "等比适应", "", 88, 4, 81, 30, () => Choose(PictureFit.Fit));
            stretch = ButtonAt(modes, "自由拉伸", "", 170, 4, 81, 30, () => Choose(PictureFit.Stretch));
            fill.Pill = fit.Pill = stretch.Pill = true;
            modeTips.SetToolTip(fill, "锁定选框比例，照片等比铺满卡面中的图片框");
            modeTips.SetToolTip(fit, "按原比例显示整个选区");
            modeTips.SetToolTip(stretch, "按图片框的宽和高分别缩放所选区域");
            var reset = ButtonAt(this, "重置选区", "", w - margin - 101, 53, 101, 30, ResetSelection);
            reset.Border = true;

            LabelAt(this, "原图", margin, 93, 44, 22, 12, true, Ui.Ink);
            LabelAt(this, "卡面预览", right, 93, rightWidth, 22, 12, true, Ui.Ink);
            size = LabelAt(this, "", margin + 44, 93, leftWidth - 44, 22, 11, false, Ui.Quiet);
            size.TextAlign = ContentAlignment.MiddleRight;
            source = new CropCanvas(draft.Image) { AccessibleName = "原图裁切选区，方向键移动" };
            Controls.Add(source); Ui.Put(source, margin, top, leftWidth, h - top - 86);

            picture = draft.Copy();
            picture.Hidden = false;
            var sample = card.Copy();
            int at = sample.Layers.FindIndex(l => l.Id == draft.Id);
            if (at == -1)
            {
                at = sample.Layers.FindIndex(l => l.Side == draft.Side);
                sample.Layers.Insert(at < 0 ? sample.Layers.Count : at, picture);
            }
            else sample.Layers[at] = picture;
            preview = new CardImagePreview(sample, draft.Side) { AccessibleName = "当前卡面预览" };
            Controls.Add(preview); Ui.Put(preview, right, top, rightWidth, h - top - 86);

            LabelAt(this, "拖动选区可改变取景。拖动边缘或四角可调整裁切范围。", margin, h - 81, w - 2 * margin, 26, 11, false, Ui.Quiet);
            var cancel = ButtonAt(this, "取消", "", w - margin - 195, h - 46, 82, 33, () => DialogResult = DialogResult.Cancel);
            cancel.Border = true;
            var apply = ButtonAt(this, "应用", "", w - margin - 102, h - 46, 102, 33, () => DialogResult = DialogResult.OK);
            apply.Primary = true;
            AcceptButton = apply;
            CancelButton = cancel;
            ActiveControl = source;
            refresh.Tick += delegate { refresh.Stop(); ApplyPreview(); };
            source.Changed += delegate
            {
                var pixels = draft.Image.Bitmap.Size;
                size.Text = Math.Max(1, Math.Round(Selection.Width * pixels.Width)) + " × " +
                    Math.Max(1, Math.Round(Selection.Height * pixels.Height)) + " px";
                if (!refresh.Enabled) refresh.Start();
            };
            source.SetSelection(draft.ImageCrop.IsEmpty ? PictureLayout.Full : draft.ImageCrop);
            Choose(draft.EffectiveImageFit);
            refresh.Stop();
            ApplyPreview();
        }

        private void Choose(PictureFit mode)
        {
            Mode = mode;
            fill.Chosen = mode == PictureFit.Fill;
            fit.Chosen = mode == PictureFit.Fit;
            stretch.Chosen = mode == PictureFit.Stretch;
            fill.Invalidate(); fit.Invalidate(); stretch.Invalidate();
            source.SetRatio(mode == PictureFit.Fill ? picture.CropRatio : 0);
        }

        private void ResetSelection()
        {
            source.SetSelection(PictureLayout.Full);
            source.Focus();
        }

        private void ApplyPreview()
        {
            picture.ImageFit = Mode;
            picture.ImageCrop = Selection;
            preview.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { refresh.Dispose(); modeTips.Dispose(); }
            base.Dispose(disposing);
        }

        private sealed class CardImagePreview : Control
        {
            private readonly Project card;
            private readonly int side;

            internal CardImagePreview(Project card, int side)
            {
                this.card = card; this.side = side;
                SetStyle(Glass.Buffered | ControlStyles.ResizeRedraw, true);
                BackColor = Color.FromArgb(225, 234, 245);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var space = new RectangleF(Ui.P(11), Ui.P(11), Width - Ui.P(22), Height - Ui.P(22));
                if (space.Width <= 0 || space.Height <= 0) return;
                var frame = PictureLayout.FitInside(space, card.Width, card.Height);
                CardPainter.PaintPreview(e.Graphics, card, side, frame, 0, card.Layers.Count);
            }
        }
    }

    internal sealed class CropCanvas : Control
    {
        private enum Grip { None, Move, Resize, New }
        private readonly Art art;
        private readonly Size pixels;
        private Grip drag;
        private int horizontal, vertical;
        private float lockedRatio;
        private Point pointer;
        private PointF origin;
        private RectangleF original, requested = PictureLayout.Full;

        internal event Action Changed;
        internal RectangleF Selection { get; private set; }

        internal CropCanvas(Art art)
        {
            this.art = art;
            pixels = art.Bitmap.Size;
            Selection = PictureLayout.Full;
            SetStyle(Glass.Buffered | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(33, 43, 55);
            TabStop = true;
        }

        internal void SetSelection(RectangleF area)
        {
            requested = area;
            DisplaySelection(lockedRatio > 0 ? PictureLayout.CenterCrop(requested, lockedRatio) : requested);
        }

        internal void SetRatio(float ratio)
        {
            lockedRatio = ratio;
            SetSelection(requested);
        }

        private void DisplaySelection(RectangleF area)
        {
            Selection = area;
            Invalidate();
            if (Changed != null) Changed();
        }

        private RectangleF ImageBox
        {
            get
            {
                int inset = Ui.P(13);
                return PictureLayout.FitInside(new RectangleF(inset, inset, Math.Max(1, Width - inset * 2),
                    Math.Max(1, Height - inset * 2)), pixels.Width, pixels.Height);
            }
        }

        private RectangleF ProjectSelection(RectangleF box)
        {
            return new RectangleF(box.X + Selection.X * box.Width, box.Y + Selection.Y * box.Height,
                Selection.Width * box.Width, Selection.Height * box.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var image = ImageBox;
            var box = ProjectSelection(image);
            g.InterpolationMode = InterpolationMode.Bilinear;
            lock (art) g.DrawImage(art.Preview, image);
            using (var outside = new Region(image))
            using (var dim = new SolidBrush(Color.FromArgb(148, 13, 21, 32)))
            {
                outside.Exclude(box);
                g.FillRegion(dim, outside);
            }

            using (var grid = new Pen(Color.FromArgb(135, Color.White)))
                for (int i = 1; i < 3; i++)
                {
                    float x = box.Left + box.Width * i / 3;
                    float y = box.Top + box.Height * i / 3;
                    g.DrawLine(grid, x, box.Top, x, box.Bottom);
                    g.DrawLine(grid, box.Left, y, box.Right, y);
                }

            using (var edge = new Pen(Color.FromArgb(230, Color.White), Math.Max(1, Ui.Scale)))
                g.DrawRectangle(edge, box.X, box.Y, box.Width, box.Height);
            int radius = Ui.P(4);
            using (var edge = new Pen(Color.FromArgb(165, BackColor)))
                for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                    {
                        if (row == 1 && column == 1) continue;
                        float x = box.Left + box.Width * column / 2 - radius;
                        float y = box.Top + box.Height * row / 2 - radius;
                        g.FillRectangle(Brushes.White, x, y, 2 * radius, 2 * radius);
                        g.DrawRectangle(edge, x, y, 2 * radius, 2 * radius);
                    }
        }

        private Grip Hit(Point point, out int x, out int y)
        {
            var image = ImageBox;
            var box = ProjectSelection(image);
            int target = Ui.P(9);
            x = y = 0;
            if (RectangleF.Inflate(box, target, target).Contains(point))
            {
                x = Edge(point.X, box.Left, box.Right, target);
                y = Edge(point.Y, box.Top, box.Bottom, target);
                if (x != 0 || y != 0) return Grip.Resize;
                if (box.Contains(point)) return Grip.Move;
            }
            return image.Contains(point) ? Grip.New : Grip.None;
        }

        private static int Edge(float coordinate, float first, float last, int limit)
        {
            float toFirst = Math.Abs(coordinate - first), toLast = Math.Abs(coordinate - last);
            if (Math.Min(toFirst, toLast) > limit) return 0;
            return toFirst < toLast ? -1 : 1;
        }

        private PointF ImagePoint(Point point)
        {
            var box = ImageBox;
            return new PointF((point.X - box.X) / box.Width, (point.Y - box.Y) / box.Height);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            drag = Hit(e.Location, out horizontal, out vertical);
            if (drag == Grip.None) return;
            Focus();
            pointer = e.Location;
            origin = ImagePoint(pointer);
            original = Selection;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Capture && drag != Grip.None)
            {
                if (drag == Grip.New && Math.Abs(e.X - pointer.X) < Ui.P(3) && Math.Abs(e.Y - pointer.Y) < Ui.P(3)) return;
                var position = ImagePoint(e.Location);
                float dx = position.X - origin.X, dy = position.Y - origin.Y;
                RectangleF area = drag == Grip.Move ? Translate(original, dx, dy) : ResizeSelection(dx, dy);
                if (area.Width >= .000001f && area.Height >= .000001f && area != Selection)
                {
                    requested = area;
                    DisplaySelection(area);
                }
                return;
            }
            int x, y;
            var grip = Hit(e.Location, out x, out y);
            Cursor = grip == Grip.Move ? Cursors.SizeAll : grip == Grip.New ? Cursors.Cross : grip != Grip.Resize ? Cursors.Default :
                x == 0 ? Cursors.SizeNS : y == 0 ? Cursors.SizeWE : x == y ? Cursors.SizeNWSE : Cursors.SizeNESW;
        }

        private RectangleF ResizeSelection(float dx, float dy)
        {
            int x = horizontal, y = vertical;
            RectangleF start = original;
            if (drag == Grip.New)
            {
                x = dx < 0 ? -1 : 1;
                y = dy < 0 ? -1 : 1;
                start = new RectangleF(origin, SizeF.Empty);
            }
            float anchorX = x < 0 ? start.Right : x > 0 ? start.Left : start.Left + start.Width / 2;
            float anchorY = y < 0 ? start.Bottom : y > 0 ? start.Top : start.Top + start.Height / 2;
            float maxWidth = Available(anchorX, x), maxHeight = Available(anchorY, y);
            float w = start.Width + dx * x, h = start.Height + dy * y;
            if (lockedRatio > 0)
            {
                float limit = Math.Min(maxHeight, maxWidth / lockedRatio);
                float minimum = Math.Max(1f / pixels.Height, 1f / pixels.Width / lockedRatio);
                bool fromWidth = x != 0 && (y == 0 || Math.Abs(dx) * pixels.Width >= Math.Abs(dy) * pixels.Height);
                h = Math.Min(limit, Math.Max(minimum, fromWidth ? w / lockedRatio : h));
                w = h * lockedRatio;
            }
            else
            {
                w = Math.Min(maxWidth, Math.Max(1f / pixels.Width, w));
                h = Math.Min(maxHeight, Math.Max(1f / pixels.Height, h));
            }
            return new RectangleF(anchorX - Offset(w, x), anchorY - Offset(h, y), w, h);
        }

        private static float Available(float anchor, int direction)
        { return direction < 0 ? anchor : direction > 0 ? 1 - anchor : 2 * Math.Min(anchor, 1 - anchor); }

        private static float Offset(float size, int direction)
        { return direction < 0 ? size : direction > 0 ? 0 : size / 2; }

        private static RectangleF Translate(RectangleF region, float dx, float dy)
        {
            return new RectangleF(Math.Max(0, Math.Min(1 - region.Width, region.X + dx)),
                Math.Max(0, Math.Min(1 - region.Height, region.Y + dy)), region.Width, region.Height);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && Capture) Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) drag = Grip.None;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            float step = e.Shift ? 10 : 1;
            float dx = e.KeyCode == Keys.Left ? -step : e.KeyCode == Keys.Right ? step : 0;
            float dy = e.KeyCode == Keys.Up ? -step : e.KeyCode == Keys.Down ? step : 0;
            if (dx == 0 && dy == 0) return;
            var shifted = Translate(Selection, dx / pixels.Width, dy / pixels.Height);
            if (shifted != Selection)
            {
                requested = shifted;
                DisplaySelection(shifted);
            }
            e.Handled = true;
        }
    }
}
