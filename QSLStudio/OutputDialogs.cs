using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace QslStudio
{
    internal sealed class CardSizeDialog : StudioDialog
    {
        private readonly NumericUpDown width, height;
        private readonly Label pixels;
        internal int PageWidth { get { return (int)(width.Value * 10); } }
        internal int PageHeight { get { return (int)(height.Value * 10); } }

        internal CardSizeDialog(Project card) : base("卡片尺寸", 432, 298)
        {
            LabelAt(this, "卡片尺寸", 20, 14, 390, 38, 20, true, Ui.Ink);
            ButtonAt(this, "横版 140 × 90", "", 20, 66, 125, 33, () => SetSize(1400, 900)).Border = true;
            ButtonAt(this, "竖版 90 × 140", "", 153, 66, 125, 33, () => SetSize(900, 1400)).Border = true;
            ButtonAt(this, "钱包卡 85.6 × 54", "", 286, 66, 126, 33, () => SetSize(856, 540)).Border = true;
            LabelAt(this, "宽度（mm）", 21, 112, 183, 22, 12, true, Ui.Ink);
            LabelAt(this, "高度（mm）", 222, 112, 183, 22, 12, true, Ui.Ink);
            width = Dimension("卡片宽度（mm）", 20);
            height = Dimension("卡片高度（mm）", 221);
            pixels = LabelAt(this, "", 21, 179, 390, 25, 12, false, Ui.Quiet);
            LabelAt(this, "宽、高均为 10 至 420 mm。正反面使用相同尺寸。", 21, 211, 390, 24, 12, false, Ui.Quiet);
            width.ValueChanged += (sender, e) => UpdatePixels();
            height.ValueChanged += (sender, e) => UpdatePixels();
            SetSize(card.Width, card.Height);
            var cancel = ButtonAt(this, "取消", "", 220, 248, 81, 34, () => DialogResult = DialogResult.Cancel);
            cancel.Border = true;
            var apply = ButtonAt(this, "确定", "", 309, 248, 103, 34, () => DialogResult = DialogResult.OK);
            apply.Primary = true;
            AcceptButton = apply;
            CancelButton = cancel;
            ActiveControl = width;
        }

        private NumericUpDown Dimension(string name, int x)
        {
            var field = new NumericUpDown { AccessibleName = name, DecimalPlaces = 1, Increment = .1m,
                Minimum = Project.MinPageEdge / 10m, Maximum = Project.MaxPageEdge / 10m, Font = Ui.F(15, false) };
            Controls.Add(field);
            Ui.Put(field, x, 140, 191, 32);
            return field;
        }

        private void SetSize(int w, int h)
        {
            width.Value = w / 10m;
            height.Value = h / 10m;
            UpdatePixels();
        }

        private void UpdatePixels()
        {
            pixels.Text = "300 DPI   /   " + CardOutput.Pixels(PageWidth) + " × " + CardOutput.Pixels(PageHeight) + " px";
        }
    }

    internal sealed class OverprintDialog : StudioDialog
    {
        private readonly Project card;
        private readonly ComboBox faces;
        private readonly NumericUpDown horizontal, vertical;
        private readonly SurfacePanel preview;
        internal int Side { get { return ((Face)faces.SelectedItem).Index; } }
        internal PointF Offset { get { return new PointF((float)horizontal.Value, (float)vertical.Value); } }

        internal OverprintDialog(Project card, int currentSide) : base("套印通联资料", Fit(580, 449, true), Fit(483, 311, false))
        {
            this.card = card;
            int w = (int)Math.Round(ClientSize.Width / Ui.Scale);
            int h = (int)Math.Round(ClientSize.Height / Ui.Scale);
            LabelAt(this, "套印通联资料", 20, 11, w - 40, 35, 20, true, Ui.Ink);
            LabelAt(this, "成品尺寸 " + card.PrintSize, 21, 48, w - 42, 21, 12, false, Ui.Quiet);

            int column = (w - 60) / 3;
            LabelAt(this, "卡面", 21, 80, column, 23, 12, true, Ui.Ink);
            faces = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.F(13, false),
                AccessibleName = "套印卡面" };
            foreach (int side in new[] { 1, 0 })
                if (CardPainter.HasOverprintContent(card, side)) faces.Items.Add(new Face(side));
            Controls.Add(faces);
            Ui.Put(faces, 20, 105, column, 29);
            faces.SelectedIndex = 0;
            for (int i = 0; i < faces.Items.Count; i++)
                if (((Face)faces.Items[i]).Index == currentSide) faces.SelectedIndex = i;

            horizontal = OffsetBox("横向偏移（mm）", 30 + column, column);
            vertical = OffsetBox("纵向偏移（mm）", 40 + 2 * column, column);

            preview = new SurfacePanel { AccessibleName = "套印预览" };
            Controls.Add(preview);
            Ui.Put(preview, 20, 146, w - 40, h - 238);
            preview.Paint += PaintPreview;
            var note = LabelAt(this, "", 21, h - 81, w - 42, 25, 11, false, Ui.Quiet);
            var cancel = ButtonAt(this, "取消", "", w - 259, h - 47, 84, 33, () => DialogResult = DialogResult.Cancel);
            cancel.Border = true;
            var save = ButtonAt(this, "保存套印 PDF…", "export", w - 167, h - 47, 147, 33, () => DialogResult = DialogResult.OK);
            save.Primary = true;
            AcceptButton = save;
            CancelButton = cancel;
            EventHandler refresh = delegate
            {
                save.Enabled = CardPainter.HasOverprintContent(card, Side, Offset);
                note.Text = save.Enabled ? "正值向右或向下。打印时请选择「实际大小」。" : "请调整偏移量，使通联资料位于卡片范围内。";
                preview.Invalidate();
            };
            faces.SelectedIndexChanged += refresh;
            horizontal.ValueChanged += refresh;
            vertical.ValueChanged += refresh;
            refresh(this, EventArgs.Empty);
        }

        private static int Fit(int preferred, int minimum, bool horizontal)
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            int available = (int)((horizontal ? area.Width : area.Height) / Ui.Scale) - 42;
            return Math.Min(preferred, Math.Max(minimum, available));
        }

        private NumericUpDown OffsetBox(string label, int x, int w)
        {
            LabelAt(this, label, x, 80, w, 23, 12, true, Ui.Ink);
            var field = new NumericUpDown { AccessibleName = label, DecimalPlaces = 1, Increment = .1m,
                Minimum = -25, Maximum = 25, Font = Ui.F(13, false) };
            Controls.Add(field);
            Ui.Put(field, x, 105, w, 29);
            return field;
        }

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            float scale = Math.Min((preview.Width - Ui.P(16)) / (float)card.Width,
                (preview.Height - Ui.P(16)) / (float)card.Height);
            if (scale <= 0) return;
            float width = card.Width * scale, height = card.Height * scale;
            var paper = new RectangleF((preview.Width - width) / 2f, (preview.Height - height) / 2f, width, height);
            e.Graphics.FillRectangle(Brushes.White, paper);
            CardPainter.PaintOverprint(e.Graphics, card, Side, paper, Offset);
            using (var edge = new Pen(Ui.Line)) e.Graphics.DrawRectangle(edge, paper.X, paper.Y, paper.Width, paper.Height);
        }

        private sealed class Face
        {
            internal readonly int Index;
            internal Face(int index) { Index = index; }
            public override string ToString() { return Index == 0 ? "正面" : "反面"; }
        }
    }
}
