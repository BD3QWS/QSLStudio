using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QslStudio
{
    internal sealed partial class Editor
    {
        private ComboBox layerPick, fontPick;
        private Label editLabel, fontLabel, sizeLabel, colorLabel, strokeLabel, geomLabel, leftLabel, topLabel, widthLabel, heightLabel, emptyTitle, emptyText, layerHint, picHint;
        private InputBox activeText;
        private NumericUpDown size, stroke, xPos, yPos, width, height;
        private ButtonX boldBtn, italicBtn, colorBtn, bgBtn, pageSizeBtn, aboveBtn, belowBtn, cloneBtn, hideBtn, deleteBtn, replaceBtn, cropBtn, leftAlign, centerAlign, rightAlign;
        private CheckBox fitText;
        private Control[] textControls, geometryControls;
        private int inspectorMode = -1, inspectorWidth = -1;
        private bool fontListLoaded;

        private sealed class Pick
        {
            internal readonly string Id, Title;
            internal Pick(string id, string title) { Id = id; Title = title; }
            public override string ToString() { return Title; }
        }

        private ComboBox Combo(Control parent, string name)
        {
            var box = new ComboBox { Font = Ui.F(13, false), ForeColor = Ui.Ink, BackColor = Ui.White,
                FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList, IntegralHeight = true, MaxDropDownItems = 13,
                AccessibleName = name };
            parent.Controls.Add(box); return box;
        }
        private NumericUpDown Num(string name, decimal min, decimal max, decimal inc, Action<Layer, float> apply)
        {
            var c = new NumericUpDown { Font = Ui.F(13, false), Minimum = min, Maximum = max, DecimalPlaces = 1, Increment = inc,
                BackColor = Ui.White, ForeColor = Ui.Ink, AccessibleName = name, ThousandsSeparator = false, BorderStyle = BorderStyle.FixedSingle };
            c.ValueChanged += delegate { if (!sync) Mutate(c, l => apply(l, (float)c.Value), false); };
            c.Leave += delegate { editToken = null; };
            inspector.Controls.Add(c); return c;
        }
        private void BuildInspector()
        {
            layerPick = Combo(right, "选择当前面的元素");
            layerPick.SelectedIndexChanged += delegate
            { if (!sync) { var p = layerPick.SelectedItem as Pick; SelectLayer(p == null ? null : p.Id); } };
            inspector = Pn(right); inspector.AutoScroll = true;
            emptyTitle = L(inspector, "页面", 14, true, Ui.Ink);
            emptyText = L(inspector, "选中画布上的元素，即可调整属性。", 12, false, Ui.Quiet);
            emptyText.TextAlign = ContentAlignment.TopLeft;
            pageSizeBtn = B(inspector, "卡片尺寸…", "", ChooseCardSize, "设置两面成品的宽度与高度"); pageSizeBtn.Border = true;
            bgBtn = B(inspector, "页面底色", "", PageColor, "更改当前面底色；铺满的照片会盖住底色"); bgBtn.Border = true;
            layerHint = L(inspector, "", 11, false, Ui.Quiet);
            editLabel = L(inspector, "文字内容", 11, true, Ui.Quiet);
            activeText = new InputBox("选中文字的内容", true); inspector.Controls.Add(activeText);
            activeText.Edit.TextChanged += delegate
            {
                if (sync || Active == null || Active.Kind != LayerKind.Text) return;
                var a = Active;
                string value = ReadFieldInput(activeText.Edit, a.Field);
                if (value == a.Value(card)) return;
                Checkpoint(activeText.Edit);
                a.SetValue(card, value);
                if (a.Field.Length == 0) card.Customized = true;
                WithSync(() => { if (a.Field.Length != 0) fields[a.Field].Box.Edit.Text = value; });
                MarkDirty();
            };
            activeText.Edit.Leave += delegate { editToken = null; };
            fontLabel = L(inspector, "字体", 11, true, Ui.Quiet);
            fontPick = Combo(inspector, "字体");
            fontPick.DropDownWidth = Ui.P(240);
            fontPick.DropDown += delegate { LoadFontList(); };
            fontPick.SelectedIndexChanged += delegate
            { if (!sync && fontPick.SelectedItem != null) Mutate(null, l => l.Font = fontPick.SelectedItem.ToString(), false); };
            sizeLabel = L(inspector, "字号（pt）", 11, true, Ui.Quiet);
            size = Num("字号（pt）", 1, 180, .5m, (l, v) => l.Size = v);
            colorLabel = L(inspector, "颜色", 11, true, Ui.Quiet);
            colorBtn = B(inspector, "", "", ElementColor, "选择文字或图标颜色"); colorBtn.Border = true;
            strokeLabel = L(inspector, "线宽（mm）", 11, true, Ui.Quiet);
            stroke = Num("线宽（mm）", .1m, 18m, .1m, (l, v) => l.Size = v * 10f);
            boldBtn = B(inspector, "粗体", "", () => Mutate(null, l => l.Style ^= FontStyle.Bold, true), "切换粗体");
            italicBtn = B(inspector, "斜体", "", () => Mutate(null, l => l.Style ^= FontStyle.Italic, true), "切换斜体");
            leftAlign = B(inspector, "", "left", () => Align(StringAlignment.Near), "左对齐");
            centerAlign = B(inspector, "", "center", () => Align(StringAlignment.Center), "居中");
            rightAlign = B(inspector, "", "right", () => Align(StringAlignment.Far), "右对齐");
            fitText = new SurfaceCheckBox { Text = "文字过长时自动缩小", Font = Ui.F(12, false), ForeColor = Ui.Ink, AutoSize = false };
            fitText.CheckedChanged += delegate { if (!sync) Mutate(null, l => l.Fit = fitText.Checked, false); };
            inspector.Controls.Add(fitText);
            replaceBtn = B(inspector, "替换图片", "image", ReplaceActive, "保留该图片的位置和大小，替换内容"); replaceBtn.Border = true;
            cropBtn = B(inspector, "裁切与拉伸…", "", () => AdjustPicture(Active), "拖动选区并预览卡面"); cropBtn.Border = true;
            picHint = L(inspector, "", 11, false, Ui.Quiet);
            geomLabel = L(inspector, "位置与尺寸（mm）", 13, true, Ui.Ink);
            leftLabel = L(inspector, "横向 X", 11, false, Ui.Quiet); topLabel = L(inspector, "纵向 Y", 11, false, Ui.Quiet);
            widthLabel = L(inspector, "宽度", 11, false, Ui.Quiet); heightLabel = L(inspector, "高度", 11, false, Ui.Quiet);
            xPos = Num("横向位置（mm）", -card.Width / 10m, card.Width / 10m - .1m, .1m, (l, v) => l.X = v * 10);
            yPos = Num("纵向位置（mm）", -card.Height / 10m, card.Height / 10m - .1m, .1m, (l, v) => l.Y = v * 10);
            width = Num("元素宽度（mm）", .1m, card.Width / 5m, .1m, (l, v) => l.W = v * 10);
            height = Num("元素高度（mm）", .1m, card.Height / 5m, .1m, (l, v) => l.H = v * 10);
            aboveBtn = B(inspector, "", "up", () => Reorder(true), "上移一层"); aboveBtn.Border = true;
            belowBtn = B(inspector, "", "down", () => Reorder(false), "下移一层"); belowBtn.Border = true;
            cloneBtn = B(inspector, "复制", "copy", Duplicate, "复制当前元素（Ctrl+D）"); cloneBtn.Border = true;
            hideBtn = B(inspector, "隐藏", "eye", () => Mutate(null, l => l.Hidden = !l.Hidden, true), "隐藏或重新显示当前元素");
            deleteBtn = B(inspector, "删除", "remove", Remove, "移除当前元素（Delete）");
            textControls = new Control[] { editLabel, activeText, fontLabel, fontPick, sizeLabel, size, boldBtn, italicBtn, leftAlign, centerAlign, rightAlign, fitText };
            geometryControls = new Control[] { geomLabel, leftLabel, topLabel, widthLabel, heightLabel, xPos, yPos, width, height, aboveBtn, belowBtn, cloneBtn, hideBtn, deleteBtn };
        }
        private void LoadFontList()
        {
            if (fontListLoaded) return;
            var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (string name in fontPick.Items) names.Add(name);
            using (var collection = new InstalledFontCollection())
                foreach (var family in collection.Families)
                    using (family) names.Add(family.Name);
            names.Add("Arial");
            var choices = new string[names.Count];
            names.CopyTo(choices);
            WithSync(() =>
            {
                object current = fontPick.SelectedItem;
                fontPick.BeginUpdate();
                try { fontPick.Items.Clear(); fontPick.Items.AddRange(choices); fontPick.SelectedItem = current; }
                finally { fontPick.EndUpdate(); }
            });
            fontListLoaded = true;
        }
        private void LayoutInspector()
        {
            if (inspector == null) return;
            var l = Active;
            int mode = l == null ? 0 : l.Kind == LayerKind.Text ? 1 : l.Kind == LayerKind.Picture ? 2 : CardDrawing.IsDrawing(l) ? 4 : 3;
            int w = Ui.ScrollContentWidth(inspector, 16);
            if (mode == inspectorMode && w == inspectorWidth) return;
            int oldScroll = mode == inspectorMode ? -inspector.AutoScrollPosition.Y : 0;
            inspectorMode = mode;
            inspectorWidth = w;
            inspector.AutoScrollPosition = Point.Empty;
            inspector.SuspendLayout();
            const int x = 8;
            int half = (w - 9) / 2, hx = x + half + 9;
            emptyTitle.Visible = emptyText.Visible = pageSizeBtn.Visible = bgBtn.Visible = mode == 0;
            layerHint.Visible = mode != 0;
            replaceBtn.Visible = cropBtn.Visible = picHint.Visible = mode == 2;
            foreach (var c in textControls) c.Visible = mode == 1;
            foreach (var c in geometryControls) c.Visible = mode != 0;
            colorLabel.Visible = colorBtn.Visible = mode == 1 || mode == 3 || mode == 4;
            strokeLabel.Visible = stroke.Visible = mode == 4;
            int y = 4;
            if (mode == 0)
            {
                Ui.Put(emptyTitle, x, 11, w, 28);
                Ui.Put(pageSizeBtn, x, 43, w, 33);
                Ui.Put(bgBtn, x, 83, w, 33);
                Ui.Put(emptyText, x, 130, w, 53);
                inspector.AutoScrollMinSize = new Size(0, Ui.P(184));
            }
            else
            {
                Ui.Put(layerHint, x, 1, w, 22); y = 29;
                if (mode == 1)
                {
                    Ui.Put(editLabel, x, y, w, 18); Ui.Put(activeText, x, y + 20, w, 56); y += 83;
                    Ui.Put(fontLabel, x, y, w, 18); Ui.Put(fontPick, x, y + 21, w, 26); y += 55;
                    y = LayoutNumberAndColor(sizeLabel, size, x, y, w);
                    const int alignment = 28, gap = 7;
                    int style = Math.Max(Ui.TextWidth(boldBtn.Text, boldBtn.Font), Ui.TextWidth(italicBtn.Text, italicBtn.Font)) + 10;
                    bool separate = w < 2 * style + 3 * alignment + 2 * gap;
                    int toggleWidth = separate ? (w - gap) / 2 : style;
                    Ui.Put(boldBtn, x, y, toggleWidth, 30);
                    Ui.Put(italicBtn, x + toggleWidth + gap, y, toggleWidth, 30);
                    if (separate) y += 35;
                    int alignWidth = separate ? w / 3 : alignment;
                    int alignX = x + w - 3 * alignWidth;
                    Ui.Put(leftAlign, alignX, y, alignWidth, 30);
                    Ui.Put(centerAlign, alignX + alignWidth, y, alignWidth, 30);
                    Ui.Put(rightAlign, alignX + 2 * alignWidth, y, alignWidth, 30);
                    Ui.Put(fitText, x, y + 36, w, 26); y += 77;
                }
                else if (mode == 2)
                {
                    Ui.Put(cropBtn, x, y, w, 33); Ui.Put(replaceBtn, x, y + 39, w, 33);
                    Ui.Put(picHint, x, y + 77, w, 46); y += 127;
                }
                else if (mode == 4)
                {
                    y = LayoutNumberAndColor(strokeLabel, stroke, x, y, w);
                }
                else
                {
                    Ui.Put(colorLabel, x, y, w, 18); Ui.Put(colorBtn, x, y + 22, w, 32); y += 70;
                }
                Ui.Put(geomLabel, x, y, w, 24); y += 31;
                Ui.Put(leftLabel, x, y, half, 16); Ui.Put(topLabel, hx, y, half, 16);
                Ui.Put(xPos, x, y + 19, half, 26); Ui.Put(yPos, hx, y + 19, half, 26); y += 50;
                Ui.Put(widthLabel, x, y, half, 16); Ui.Put(heightLabel, hx, y, half, 16);
                Ui.Put(width, x, y + 19, half, 26); Ui.Put(height, hx, y + 19, half, 26); y += 57;
                Ui.Put(aboveBtn, x, y, 32, 30);
                Ui.Put(belowBtn, x + 38, y, 32, 30);
                LayoutAction(cloneBtn, "copy", x + 76, y, w - 76, 30);
                y += 37;
                LayoutAction(hideBtn, "eye", x, y, half, 31);
                LayoutAction(deleteBtn, "remove", hx, y, half, 31);
                y += 42;
                inspector.AutoScrollMinSize = new Size(0, Ui.P(y));
            }
            inspector.ResumeLayout();
            inspector.AutoScrollPosition = new Point(0, oldScroll);
        }
        private static void LayoutAction(ButtonX button, string icon, int x, int y, int width, int height)
        {
            button.IconKey = width >= Ui.TextWidth(button.Text, button.Font) + 31 ? icon : "";
            Ui.Put(button, x, y, width, height);
        }

        private int LayoutNumberAndColor(Label caption, NumericUpDown number, int x, int y, int width)
        {
            const int gap = 9;
            int hexWidth = Math.Max(Ui.TextWidth("#DDDDDD", colorBtn.Font), Ui.TextWidth("#888888", colorBtn.Font)) + 12;
            int colorWidth = Math.Max((width - gap) / 2, hexWidth);
            int numberWidth = width - gap - colorWidth;
            bool separate = numberWidth < Math.Max(62, Ui.TextWidth(caption.Text, caption.Font) + 3);
            if (separate) numberWidth = width;
            Ui.Put(caption, x, y, numberWidth, 18);
            Ui.Put(number, x, y + 22, numberWidth, 27);
            if (separate)
            {
                y += 55;
                colorWidth = width;
            }
            int colorX = x + width - colorWidth;
            Ui.Put(colorLabel, colorX, y, colorWidth, 18);
            Ui.Put(colorBtn, colorX, y + 20, colorWidth, 29);
            return y + 57;
        }

        private void RefreshInspector(bool rebuild)
        {
            if (layerPick == null) return;
            var l = Active;
            WithSync(() =>
            {
                if (rebuild || layerPick.Items.Count == 0)
                {
                    layerPick.BeginUpdate(); layerPick.Items.Clear(); layerPick.Items.Add(new Pick(null, "选择元素…"));
                    for (int i = card.Layers.Count - 1; i >= 0; i--)
                    {
                        var item = card.Layers[i];
                        if (item.Side == side) layerPick.Items.Add(new Pick(item.Id, item.Name + (item.Hidden ? "（已隐藏）" : "")));
                    }
                    layerPick.EndUpdate();
                }
                int index = 0;
                for (int i = 1; i < layerPick.Items.Count; i++) if (((Pick)layerPick.Items[i]).Id == selected) { index = i; break; }
                layerPick.SelectedIndex = index;
                if (showLayers)
                {
                    layerList.BeginUpdate();
                    layerList.Items.Clear();
                    for (int i = 1; i < layerPick.Items.Count; i++) layerList.Items.Add(layerPick.Items[i]);
                    layerList.SelectedIndex = index - 1;
                    layerList.EndUpdate();
                }
                if (l != null)
                {
                    layerHint.Text = l.Kind == LayerKind.Text ? (l.Field == "" ? "自由文字" : "与左侧资料同步")
                        : l.Kind == LayerKind.Picture ? (l.Cover ? "底图" : "图片") : CardDrawing.IsDrawing(l) ? l.Name : "矢量图标";
                    if (l.Kind == LayerKind.Picture) UpdateArtHint(l);
                    if (CardDrawing.IsDrawing(l)) SetNum(stroke, l.Size / 10);
                    if (l.Kind == LayerKind.Text)
                    {
                        activeText.Edit.MaxLength = l.Field == "postcode" ? 120 : Field.Limit(l.Field);
                        activeText.Edit.Text = l.Value(card);
                        if (!fontPick.Items.Contains(l.Font)) fontPick.Items.Insert(0, l.Font);
                        fontPick.SelectedItem = l.Font;
                        SetNum(size, l.Size);
                        boldBtn.Chosen = (l.Style & FontStyle.Bold) != 0; italicBtn.Chosen = (l.Style & FontStyle.Italic) != 0;
                        leftAlign.Chosen = l.Align == StringAlignment.Near; centerAlign.Chosen = l.Align == StringAlignment.Center; rightAlign.Chosen = l.Align == StringAlignment.Far;
                        foreach (var b in new[] { boldBtn, italicBtn, leftAlign, centerAlign, rightAlign }) b.Invalidate();
                        fitText.Checked = l.Fit;
                    }
                    colorBtn.Text = "#" + Files.Hex(l.Color);
                    xPos.Minimum = -card.Width / 10m; xPos.Maximum = card.Width / 10m - .1m;
                    yPos.Minimum = -card.Height / 10m; yPos.Maximum = card.Height / 10m - .1m;
                    width.Maximum = card.Width / 5m; height.Maximum = card.Height / 5m;
                    SetNum(xPos, l.X / 10); SetNum(yPos, l.Y / 10); SetNum(width, l.W / 10); SetNum(height, l.H / 10);
                    int pos = card.Layers.IndexOf(l);
                    aboveBtn.Enabled = Adjacent(pos, 1) >= 0; belowBtn.Enabled = Adjacent(pos, -1) >= 0;
                    hideBtn.Text = l.Hidden ? "显示" : "隐藏";
                }
            });
            LayoutInspector();
        }
        private static void SetNum(NumericUpDown c, float value)
        { c.Value = Math.Max(c.Minimum, Math.Min(c.Maximum, Math.Round((decimal)value, c.DecimalPlaces))); }
        private void Align(StringAlignment value) { Mutate(null, l => l.Align = value, true); }
        private void Mutate(object token, Action<Layer> edit, bool refresh)
        {
            var l = Active;
            if (l == null) return;
            Checkpoint(token); edit(l); card.Customized = true; MarkDirty();
            if (refresh) RefreshInspector(true);
            else if (l.Kind == LayerKind.Picture) UpdateArtHint(l);
        }
        private void UpdateArtHint(Layer l)
        {
            cropBtn.Enabled = l.Image != null;
            if (l.Image == null) { picHint.Text = "暂无图片，点「替换图片」选择文件。"; return; }
            var crop = l.PictureSource;
            var frame = l.PictureBounds(crop);
            float widthPixels = crop.Width * l.Image.Bitmap.Width, heightPixels = crop.Height * l.Image.Bitmap.Height;
            int dpi = (int)Math.Round(254 * Math.Min(widthPixels / frame.Width, heightPixels / frame.Height));
            picHint.Text = dpi >= 300 ? "当前尺寸可满足 300 DPI。" : "按当前尺寸约 " + dpi + " DPI。印刷时建议缩小或换高清原图。";
        }
        private void ElementColor()
        {
            var l = Active; if (l == null) return;
            using (var d = new ColorDialog { Color = l.Color, FullOpen = true })
                if (d.ShowDialog(this) == DialogResult.OK && d.Color.ToArgb() != l.Color.ToArgb()) Mutate(null, a => a.Color = d.Color, true);
        }
        private void PageColor()
        {
            using (var d = new ColorDialog { Color = card.Background[side], FullOpen = true })
                if (d.ShowDialog(this) == DialogResult.OK) { Checkpoint(null); card.Background[side] = d.Color; card.Customized = true; MarkDirty(); }
        }
        private int Adjacent(int index, int step)
        {
            for (int i = index + step; i >= 0 && i < card.Layers.Count; i += step) if (card.Layers[i].Side == side) return i;
            return -1;
        }
        private void Reorder(bool up)
        {
            var l = Active; if (l == null) return;
            int i = card.Layers.IndexOf(l), j = Adjacent(i, up ? 1 : -1); if (j < 0) return;
            Checkpoint(null); card.Layers[i] = card.Layers[j]; card.Layers[j] = l; card.Customized = true; RefreshInspector(true); MarkDirty();
        }
        private bool CanAdd(int count = 1)
        {
            if (card.Layers.Count + count <= Project.MaxLayers) return true;
            ShowStatus("卡片的元素数量已满，请先移除几个元素。"); return false;
        }
        private void Duplicate()
        {
            var l = Active; if (l == null || !CanAdd()) return;
            Checkpoint(null);
            var c = l.Copy(); c.Id = Guid.NewGuid().ToString("N"); c.Name = Files.Cut(c.Name + " 副本", 64);
            c.X = Math.Min(card.Width - 20, c.X + 35); c.Y = Math.Min(card.Height - 20, c.Y + 35);
            card.Layers.Add(c); card.Customized = true; selected = c.Id; view.SelectedId = selected; RefreshInspector(true); MarkDirty();
        }
        private void Remove()
        {
            var l = Active; if (l == null) return;
            Checkpoint(null); card.Layers.Remove(l); card.Customized = true; selected = null; view.SelectedId = null;
            RefreshInspector(true); MarkDirty(); ShowStatus("元素已移除 · Ctrl+Z 可恢复");
        }
        private void AddText()
        {
            if (!CanAdd()) return;
            var color = Templates.DefaultInk(card, side);
            float w = Math.Min(434, card.Width * .75f), h = Math.Min(98, card.Height * .5f);
            var l = new Layer { Side = side, Name = "自定义文字", X = (card.Width - w) / 2,
                Y = (card.Height - h) / 2 - Math.Min(8, card.Height * .05f), W = w, H = h, Size = 17, Color = color };
            AddLayer(l);
            if (showLayers) SetLayers(false);
            activeText.Edit.Focus();
        }
        private void AddFieldModule(string field)
        {
            if (!CanAdd()) return;
            bool call = field == "own";
            var layer = new Layer { Side = side, Field = field, Name = Field.Find(field).Label,
                X = card.Width * .18f, W = card.Width * .64f, Y = card.Height * .32f, H = card.Height * .23f,
                Size = call ? 30 : 10, Style = call ? FontStyle.Bold : FontStyle.Regular,
                Align = call ? StringAlignment.Center : StringAlignment.Near, Color = Templates.DefaultInk(card, side),
                Symbol = field == "recipient" ? "mailaddress" : call ? "" : "paragraph" };
            AddLayer(layer);
        }

        private void AddReportModule()
        {
            string[] keys = { "other", "date", "utc", "mhz", "mode", "rst" };
            string[] labels = { "TO RADIO", "DATE", "UTC", "MHz", "MODE", "RST" };
            if (!CanAdd(keys.Length * 2)) return;
            var layers = new List<Layer>();
            var ink = Templates.DefaultInk(card, side);
            float slot = card.Width * .88f / keys.Length;
            for (int i = 0; i < keys.Length; i++)
            {
                float x = card.Width * .06f + i * slot;
                layers.Add(new Layer { Side = side, Name = labels[i], Text = labels[i], X = x,
                    Y = card.Height * .53f, W = slot, H = card.Height * .055f, Size = 8,
                    Color = ink, Align = StringAlignment.Center });
                layers.Add(new Layer { Side = side, Name = Field.Find(keys[i]).Label, Field = keys[i], X = x,
                    Y = card.Height * .60f, W = slot, H = card.Height * .09f, Size = 11,
                    Color = ink, Align = StringAlignment.Center, Style = FontStyle.Bold });
            }
            AddLayers(layers, false);
        }

        private void AddLayer(Layer layer, bool keepDrawing = false)
        {
            AddLayers(new[] { layer }, keepDrawing);
        }

        private void AddLayers(IList<Layer> layers, bool keepDrawing)
        {
            if (!keepDrawing && view.IsDrawing) SetDrawingTool(DrawingTool.Select);
            Checkpoint(null);
            card.Layers.AddRange(layers);
            card.Customized = true;
            selected = layers[0].Id;
            view.SelectedId = selected;
            RefreshInspector(true);
            MarkDirty();
        }
    }
}
