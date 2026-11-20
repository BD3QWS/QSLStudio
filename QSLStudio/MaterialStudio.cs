using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QslStudio
{
    internal sealed class MaterialStudio : StudioDialog
    {
        internal const string DesignFilter = "特殊材质工程 (*.qslmaterial)|*.qslmaterial";
        private readonly Panel settings;
        private readonly ComboBox material, layout, process, color;
        private readonly NumericUpDown width, height, corner, threshold;
        private readonly CheckBox hangingHole, lightFront, lightBack;
        private readonly Label thresholdLabel, artworkLabel;
        private readonly TextBox[] text = new TextBox[MaterialDesign.Keys.Length];
        private readonly Control[] textLabels = new Control[MaterialDesign.Keys.Length];
        private readonly Control[] textInputs = new Control[MaterialDesign.Keys.Length];
        private readonly Control[] pcbControls;
        private readonly PictureBox[] previews = new PictureBox[2];
        private readonly ButtonX export, open, save, close;
        private readonly ButtonX designSources, drawArtwork;
        private readonly Label format, status;
        private readonly Timer refresh = new Timer { Interval = 180 };
        private readonly Project paper;
        private readonly UserStorage storage;
        private StudioMenu artworkMenu;
        private Project artwork;
        private bool updating, rendering, saving, example;
        private int revision;
        private PcbJob job;
        private string designPath;
        internal string SavedFile { get; private set; }
        internal string DesignPath { get { return designPath; } }

        private CardMaterial Material { get { return (CardMaterial)material.SelectedIndex; } }

        internal MaterialStudio(MaterialDesign draft, string file = null, Project paper = null, UserStorage storage = null) :
            base("PCB / 特殊材质", Fit(850, 618, true), Fit(574, 310, false))
        {
            this.paper = paper;
            this.storage = storage;
            int w = (int)Math.Round(ClientSize.Width / Ui.Scale);
            int h = (int)Math.Round(ClientSize.Height / Ui.Scale);
            LabelAt(this, "PCB / 特殊材质", 23, 12, w - 342, 35, 21, true, Ui.Ink);
            LabelAt(this, "选择加工材质，并检查正反面的图案。", 24, 53, w - 48, 21, 12, false, Ui.Quiet);
            designSources = ButtonAt(this, "载入模板或工程", "assets", w - 312, 16, 158, 34, ShowArtworkSources);
            drawArtwork = ButtonAt(this, "自由绘制…", "draw", w - 146, 16, 130, 34, () => EditArtwork(artwork ?? BlankArtwork()));
            designSources.Border = drawArtwork.Border = true;
            int footer = h - 70;
            var side = new GlassPanel(16);
            Controls.Add(side);
            Ui.Put(side, 16, 78, 335, footer - 115);
            settings = new SurfacePanel { AutoScroll = true };
            side.Controls.Add(settings);
            Ui.Put(settings, 8, 7, 319, footer - 129);

            Control materialLabel, layoutLabel;
            material = Picker("材质", 9, 4, 138, out materialLabel, "PCB 电路板", "木片", "亚克力", "金属卡坯");
            layout = Picker("版式", 158, 4, 133, out layoutLabel, "简明通联", "只印呼号", "自定义设计");
            width = Number("宽（mm）", 9, 54, 138, Project.MinPageEdge / 10m, Project.MaxPageEdge / 10m);
            height = Number("高（mm）", 158, 54, 133, Project.MinPageEdge / 10m, Project.MaxPageEdge / 10m);
            corner = Number("圆角（mm）", 9, 104, 138, 0, 10);
            hangingHole = new SurfaceCheckBox { Text = "加 3 mm 挂孔", Font = Ui.F(12, false) };
            settings.Controls.Add(hangingHole);
            Ui.Put(hangingHole, 158, 124, 133, 25);
            hangingHole.CheckedChanged += delegate { SchedulePreview(); };
            Control processLabel, colorLabel;
            process = Picker("PCB 工艺", 9, 154, 138, out processLabel, "白字丝印", "露铜（可选沉金）");
            color = Picker("阻焊颜色", 158, 154, 133, out colorLabel, "黑色", "绿色", "蓝色");
            pcbControls = new[] { processLabel, colorLabel, process, color };
            artworkLabel = LabelAt(settings, "", 9, 0, 253, 24, 12, true, Ui.Ink);
            lightFront = Check("正面取浅色");
            lightBack = Check("反面取浅色");
            thresholdLabel = LabelAt(settings, "深浅阈值", 9, 0, 126, 19, 11, true, Ui.Ink);
            threshold = new NumericUpDown { Minimum = 1, Maximum = 254, Value = 150, Font = Ui.F(12, false), AccessibleName = "深浅阈值" };
            settings.Controls.Add(threshold);
            threshold.ValueChanged += delegate { SchedulePreview(); };

            for (int i = 0; i < text.Length; i++)
            {
                string label = MaterialDesign.Labels[i];
                textLabels[i] = LabelAt(settings, label, 9, 0, 94, 27, 11, true, Ui.Ink);
                var input = new InputBox(label, false);
                settings.Controls.Add(input);
                textInputs[i] = input;
                text[i] = input.Edit;
                text[i].MaxLength = 64;
                text[i].TextChanged += delegate { SchedulePreview(); };
            }

            int viewX = 364, viewWidth = w - viewX - 16, segment = (footer - 128) / 2;
            var preview = new GlassPanel(16);
            Controls.Add(preview);
            Ui.Put(preview, viewX, 78, viewWidth, footer - 115);
            for (int face = 0; face < previews.Length; face++)
            {
                int y = 9 + segment * face;
                string name = face == 0 ? "正面" : "反面";
                LabelAt(preview, name, 15, y, viewWidth - 30, 20, 12, true, Ui.Ink);
                var image = new PictureBox { BackColor = Ui.Surface, SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = name + "加工预览" };
                preview.Controls.Add(image);
                Ui.Put(image, 15, y + 24, viewWidth - 30, segment - 30);
                previews[face] = image;
            }

            format = LabelAt(this, "", 23, footer - 32, w - 46, 26, 12, true, Ui.Ink);
            status = LabelAt(this, "", 23, footer + 40, w - 46, 22, 11, false, Ui.Quiet);
            open = ButtonAt(this, "打开工程…", "open", 16, footer, 112, 34, OpenDesign);
            save = ButtonAt(this, "保存工程…", "save", 136, footer, 112, 34, SaveDesign);
            close = ButtonAt(this, "返回卡片", "", w - 249, footer, 98, 34, () => DialogResult = DialogResult.Cancel);
            export = ButtonAt(this, "保存制板包…", "export", w - 143, footer, 127, 34, Export);
            open.Border = save.Border = close.Border = true;
            export.Primary = true;
            export.Enabled = false;
            CancelButton = close;
            refresh.Tick += delegate { RenderPreview(); };
            designPath = file;
            ApplyDesign(draft);
            Shown += delegate { SchedulePreview(); };
        }

        private static int Fit(int desired, int minimum, bool horizontal)
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            int available = (int)((horizontal ? area.Width : area.Height) / Ui.Scale) - 46;
            return Math.Min(desired, Math.Max(minimum, available));
        }

        private ComboBox Picker(string title, int x, int y, int w, out Control label, params string[] items)
        {
            label = LabelAt(settings, title, x, y, w, 19, 11, true, Ui.Ink);
            var input = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                Font = Ui.F(12, false), AccessibleName = title };
            input.Items.AddRange(items);
            settings.Controls.Add(input);
            Ui.Put(input, x, y + 20, w, 27);
            input.SelectedIndexChanged += delegate { OptionsChanged(); };
            return input;
        }

        private NumericUpDown Number(string title, int x, int y, int w, decimal min, decimal max)
        {
            LabelAt(settings, title, x, y, w, 19, 11, true, Ui.Ink);
            var input = new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = 1,
                Increment = .1m, Font = Ui.F(12, false), AccessibleName = title };
            settings.Controls.Add(input);
            Ui.Put(input, x, y + 20, w, 27);
            input.ValueChanged += delegate { OptionsChanged(); };
            return input;
        }

        private CheckBox Check(string title)
        {
            var box = new SurfaceCheckBox { Text = title, Font = Ui.F(12, false) };
            settings.Controls.Add(box);
            box.CheckedChanged += delegate { SchedulePreview(); };
            return box;
        }

        private void ApplyDesign(MaterialDesign draft)
        {
            updating = true;
            material.SelectedIndex = (int)draft.Material;
            artwork = draft.Artwork;
            layout.SelectedIndex = draft.UseArtwork ? 2 : draft.ContactSide ? 0 : 1;
            width.Value = draft.Width / 10m;
            height.Value = draft.Height / 10m;
            corner.Maximum = Math.Min(10m, Math.Min(width.Value, height.Value) / 2m);
            corner.Value = Math.Min(corner.Maximum, draft.Board.Radius / 10m);
            process.SelectedIndex = draft.Board.ExposedCopper ? 1 : 0;
            color.SelectedIndex = (int)draft.Board.Mask;
            hangingHole.Checked = draft.Board.HangingHole;
            lightFront.Checked = draft.Board.LightFront;
            lightBack.Checked = draft.Board.LightBack;
            threshold.Value = draft.Board.Threshold;
            example = draft.Example;
            for (int i = 0; i < text.Length; i++) text[i].Text = draft.Text[i] ?? "";
            updating = false;
            OptionsChanged();
        }

        private void OptionsChanged()
        {
            if (updating) return;
            updating = true;
            bool pcb = Material == CardMaterial.Pcb, contact = layout.SelectedIndex == 0, custom = layout.SelectedIndex == 2;
            if (custom && artwork == null) artwork = BlankArtwork();
            corner.Maximum = Math.Min(10m, Math.Min(width.Value, height.Value) / 2m);
            hangingHole.Enabled = Material != CardMaterial.Metal;
            foreach (var control in pcbControls) control.Visible = pcb;
            int scroll = -settings.AutoScrollPosition.Y;
            settings.AutoScrollPosition = Point.Empty;
            int y = pcb ? 208 : 163;
            for (int i = 0; i < text.Length; i++)
            {
                bool show = !custom && (contact || i < 2);
                textLabels[i].Visible = textInputs[i].Visible = show;
                if (!show) continue;
                Ui.Put(textLabels[i], 9, y, 105, 27); Ui.Put(textInputs[i], 117, y, 174, 27);
                y += 31;
            }
            artworkLabel.Visible = lightFront.Visible = lightBack.Visible = thresholdLabel.Visible = threshold.Visible = custom;
            if (custom)
            {
                artworkLabel.Text = "卡面  " + Templates.Title(artwork.Template);
                Ui.Put(artworkLabel, 9, y, 250, 26); y += 34;
                Ui.Put(lightFront, 9, y, 138, 25); Ui.Put(lightBack, 158, y, 133, 25); y += 32;
                Ui.Put(thresholdLabel, 9, y, 126, 19); Ui.Put(threshold, 9, y + 20, 126, 27); y += 50;
            }
            settings.AutoScrollMinSize = new Size(0, Ui.P(y + 3));
            settings.AutoScrollPosition = new Point(0, scroll);
            drawArtwork.Text = artwork == null ? "自由绘制…" : "编辑卡面…";
            drawArtwork.AccessibleName = drawArtwork.Text;
            export.Text = pcb ? "保存制板包…" : "保存加工包…";
            export.AccessibleName = export.Text;
            string size = Project.Millimetres((int)(width.Value * 10)) + " × " + Project.Millimetres((int)(height.Value * 10)) + " mm";
            string kind = pcb ? "Gerber ZIP  /  双层 FR-4" : Material == CardMaterial.Metal ?
                "黑白 PNG  /  已裁卡坯打标" : "黑白 PNG 刻字  /  SVG 裁切线";
            format.Text = kind + "    ·    " + size;
            updating = false;
            SchedulePreview();
        }

        internal MaterialDesign ReadDesign()
        {
            var draft = new MaterialDesign { Width = (int)(width.Value * 10), Height = (int)(height.Value * 10),
                Material = Material, ContactSide = layout.SelectedIndex == 0, Example = example,
                UseArtwork = layout.SelectedIndex == 2, Artwork = artwork };
            draft.Board.Radius = (int)(corner.Value * 10);
            draft.Board.HangingHole = Material != CardMaterial.Metal && hangingHole.Checked;
            draft.Board.Mask = (SolderMask)color.SelectedIndex;
            draft.Board.ExposedCopper = process.SelectedIndex == 1;
            draft.Board.LightFront = lightFront.Checked;
            draft.Board.LightBack = lightBack.Checked;
            draft.Board.Threshold = draft.UseArtwork ? (int)threshold.Value : 150;
            for (int i = 0; i < text.Length; i++) draft.Text[i] = Files.Cut(text[i].Text.Trim(), 64);
            return draft;
        }

        private void SchedulePreview()
        {
            if (updating || saving) return;
            revision++;
            job = null;
            export.Enabled = false;
            if (!Visible) return;
            status.Text = "正在更新预览…";
            refresh.Stop();
            refresh.Start();
        }

        private void RenderPreview()
        {
            refresh.Stop();
            if (rendering) return;
            rendering = true;
            var draft = ReadDesign();
            int requested = revision;
            PcbJob result = null;
            var images = new Bitmap[2];
            var worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                result = draft.Prepare();
                for (int i = 0; i < images.Length; i++) images[i] = result.Preview(i, draft.Material);
            };
            worker.RunWorkerCompleted += (sender, e) =>
            {
                worker.Dispose();
                rendering = false;
                if (!IsDisposed && requested == revision && e.Error == null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        var previous = previews[i].Image;
                        previews[i].Image = images[i];
                        images[i] = null;
                        if (previous != null) previous.Dispose();
                    }
                    job = result;
                    export.Enabled = draft.UseArtwork ? job.HasArtwork : draft.Text[0].Length > 0;
                    status.Text = export.Enabled ? job.PreviewNote(!draft.UseArtwork && draft.ContactSide) :
                        draft.UseArtwork ? "请先在卡面添加可加工的文字或图案。" : "请填写本台呼号。";
                }
                foreach (var image in images) if (image != null) image.Dispose();
                if (IsDisposed) return;
                if (requested != revision && !refresh.Enabled) refresh.Start();
                if (requested == revision && e.Error != null) status.Text = "无法更新预览：" + e.Error.Message;
            };
            worker.RunWorkerAsync();
        }

        private Project BlankArtwork()
        {
            var blank = Templates.Create(width.Value < height.Value ? "portrait-blank" : "blank");
            blank.SetPageSize((int)(width.Value * 10m), (int)(height.Value * 10m));
            return blank;
        }

        private void ShowArtworkSources()
        {
            if (artworkMenu == null)
            {
                artworkMenu = new StudioMenu();
                artworkMenu.Items.Add("套用内置模板…", null, (sender, e) => ChooseTemplate());
                if (paper != null)
                    artworkMenu.Items.Add("使用当前卡片版式", null, (sender, e) => SetArtwork(paper.Copy(), true));
                artworkMenu.Items.Add("打开现有 .qslcard 工程…", null, (sender, e) => OpenCardArtwork());
                artworkMenu.Items.Add("从空白画布开始…", null, (sender, e) => EditArtwork(BlankArtwork(), true));
            }
            artworkMenu.Show(designSources, new Point(0, designSources.Height + Ui.P(4)));
        }

        private void ChooseTemplate()
        {
            string current = artwork == null ? BlankArtwork().Template : artwork.Template;
            using (var picker = new TemplateDialog(current, 0, null, true))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                var design = Templates.Create(Templates.Keys[picker.TemplateIndex]);
                if (paper != null)
                {
                    foreach (var field in Field.All) design.Values[field.Key] = paper.Value(field.Key);
                    design.Example = paper.IsDemo;
                }
                SetArtwork(design, true);
            }
        }

        private void OpenCardArtwork()
        {
            using (var picker = new OpenFileDialog { Title = "打开已有卡片工程", Filter = "QSL 卡片工程 (*.qslcard)|*.qslcard",
                RestoreDirectory = true, CheckFileExists = true })
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    try { SetArtwork(Files.Open(picker.FileName), true); }
                    catch (Exception ex) { status.Text = "无法打开卡片工程：" + ex.Message; }
                }
        }

        private void EditArtwork(Project source, bool fresh = false)
        {
            var draft = source.Copy();
            draft.Resize((int)(width.Value * 10m), (int)(height.Value * 10m));
            using (var editor = new Editor(null, storage, draft))
                if (editor.ShowDialog(this) == DialogResult.OK)
                    SetArtwork(editor.Artwork, fresh || artwork == null);
        }

        private void SetArtwork(Project design, bool newSource)
        {
            artwork = design;
            updating = true;
            layout.SelectedIndex = 2;
            width.Value = design.Width / 10m;
            height.Value = design.Height / 10m;
            if (newSource)
            {
                lightFront.Checked = design.Background[0].GetBrightness() < .45f;
                lightBack.Checked = design.Background[1].GetBrightness() < .45f;
                threshold.Value = 150;
            }
            updating = false;
            OptionsChanged();
        }

        private void OpenDesign()
        {
            using (var picker = new OpenFileDialog { Title = "打开特殊材质工程", Filter = DesignFilter, RestoreDirectory = true })
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    try { ApplyDesign(MaterialDesign.Open(picker.FileName)); designPath = picker.FileName; }
                    catch (Exception ex) { status.Text = "无法打开工程：" + ex.Message; }
                }
        }

        private void SaveDesign()
        {
            string name = designPath == null ? Suggested() : Path.GetFileNameWithoutExtension(designPath);
            using (var picker = new SaveFileDialog { Title = "保存特殊材质工程", Filter = DesignFilter,
                DefaultExt = "qslmaterial", RestoreDirectory = true, FileName = name })
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ReadDesign().Save(picker.FileName);
                        designPath = picker.FileName;
                        status.Text = "已保存 " + Path.GetFileName(designPath);
                    }
                    catch (Exception ex) { status.Text = "无法保存工程：" + ex.Message; }
                }
        }

        private string Suggested()
        {
            string name = layout.SelectedIndex == 2 && artwork != null ? artwork.Value("own").Trim() : text[0].Text.Trim();
            foreach (char bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
            name = Files.Cut(name, 40).Trim(' ', '.');
            return name.Length == 0 ? "QSL" : name;
        }

        private void Export()
        {
            if (job == null || saving) return;
            CardMaterial target = Material;
            bool pcb = target == CardMaterial.Pcb;
            string output;
            using (var picker = new SaveFileDialog { Title = pcb ? "保存 Gerber 制板包" : "保存激光加工包",
                Filter = (pcb ? "Gerber 制板包" : "激光加工包") + " (*.zip)|*.zip",
                DefaultExt = "zip", RestoreDirectory = true, FileName = Suggested() + "_" + target })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                output = picker.FileName;
            }
            var ready = job;
            saving = UseWaitCursor = true;
            settings.Enabled = designSources.Enabled = drawArtwork.Enabled = save.Enabled = open.Enabled = close.Enabled = export.Enabled = false;
            status.Text = "正在保存加工包…";
            var worker = new BackgroundWorker();
            worker.DoWork += delegate { if (pcb) ready.Save(output); else ready.SaveLaser(output, target); };
            worker.RunWorkerCompleted += (sender, e) =>
            {
                worker.Dispose();
                if (IsDisposed) return;
                saving = UseWaitCursor = false;
                settings.Enabled = designSources.Enabled = drawArtwork.Enabled = save.Enabled = open.Enabled = close.Enabled = export.Enabled = true;
                if (e.Error != null) { status.Text = "无法保存加工包：" + e.Error.Message; return; }
                SavedFile = output;
                status.Text = "已保存 " + Path.GetFileName(output);
            };
            worker.RunWorkerAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (saving && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refresh.Dispose();
                if (artworkMenu != null) artworkMenu.Dispose();
                foreach (var picture in previews)
                    if (picture != null && picture.Image != null) { picture.Image.Dispose(); picture.Image = null; }
            }
            base.Dispose(disposing);
        }
    }
}
