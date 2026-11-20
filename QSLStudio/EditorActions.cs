using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QslStudio
{
    internal sealed partial class Editor
    {
        private const string ProjectFilter = "QSL 卡片工程 / 模板 (*.qslcard)|*.qslcard";
        private const string SideFilter = "QSL 单面模板 (*.qslside)|*.qslside";
        private MaterialDesign materialDraft;
        private string materialPath;
        private HelpDialog manual;
        private bool CanLeave()
        {
            if (!Dirty) return true;
            DialogResult r = MessageBox.Show(this, "是否保存当前工程的修改？", "保存卡片", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return r == DialogResult.No || r == DialogResult.Yes && Save(false);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!e.Cancel && e.CloseReason == CloseReason.UserClosing && !(materialEditor && DialogResult == DialogResult.OK) && !CanLeave())
                e.Cancel = true;
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tips.Dispose();
                if (manual != null) manual.Dispose();
                if (fileMenu != null) fileMenu.Dispose();
                if (coverMenu != null) coverMenu.Dispose();
                if (clockMenu != null) clockMenu.Dispose();
                if (newMenu != null) newMenu.Dispose();
                if (drawingMenu != null) drawingMenu.Dispose();
                foreach (var field in fields.Values) if (field.Menu != null) field.Menu.Dispose();
            }
            base.Dispose(disposing);
        }
        private void SetProject(Project p, string file)
        {
            path = file; card = p; card.Revision = ++revision; saved = card.Revision;
            if (!hasDocument)
            {
                hasDocument = true;
                UpdateWorkspace();
                LayoutEditor();
            }
            materialDraft = null; materialPath = null;
            editToken = null; undo.Clear(); redo.Clear(); selected = null;
            side = 0; group = 0; SetDrawingTool(DrawingTool.Select); view.Fit(); leftScroll.AutoScrollPosition = Point.Empty;
            LayoutFields(); RefreshAll();
        }
        private void NewProject()
        {
            if (newMenu == null)
            {
                newMenu = new StudioMenu();
                newMenu.Items.Add("从空白横版开始", null, (sender, e) => NewFrom("blank"));
                newMenu.Items.Add("从空白竖版开始", null, (sender, e) => NewFrom("portrait-blank"));
                newMenu.Items.Add("从模板新建…", null, (sender, e) =>
                {
                    using (var picker = new TemplateDialog(card.Template, 0, null, true))
                        if (picker.ShowDialog(this) == DialogResult.OK) NewFrom(Templates.Keys[picker.TemplateIndex]);
                });
            }
            newMenu.Show(newBtn, new Point(0, newBtn.Height + Ui.P(4)));
        }
        private void NewFrom(string template)
        {
            if (!CanLeave()) return;
            SetProject(Templates.Create(template), null);
            ShowStatus(Templates.IsBlank(template) ? "从「绘图 / 模块」绘制图形或添加资料栏。" : "请填写本台呼号。");
        }

        private void SetDrawingTool(DrawingTool tool)
        {
            bool wasDrawing = view.IsDrawing;
            view.UseDrawingTool(tool);
            drawingBtn.Chosen = view.IsDrawing;
            drawingBtn.Invalidate();
            if (view.IsDrawing)
            {
                view.Focus();
                ShowStatus("在卡面拖动鼠标绘制" + CardDrawing.Name(tool) + "。按 Esc 返回选择工具。");
            }
            else if (wasDrawing) ShowStatus("选择与移动");
        }
        private void ShowDrawingMenu()
        {
            if (drawingMenu == null)
            {
                drawingMenu = new StudioMenu();
                drawingMenu.Items.Add("选择与移动", null, (sender, e) => SetDrawingTool(DrawingTool.Select));
                foreach (DrawingTool tool in new[] { DrawingTool.Line, DrawingTool.Rectangle, DrawingTool.Ellipse, DrawingTool.Pen })
                    drawingMenu.Items.Add(CardDrawing.Name(tool), null, (sender, e) => SetDrawingTool(tool));
                drawingMenu.Items.Add(new ToolStripSeparator());
                drawingMenu.Items.Add("插入本台呼号", null, (sender, e) => AddFieldModule("own"));
                drawingMenu.Items.Add("插入通联资料栏", null, (sender, e) => AddReportModule());
                drawingMenu.Items.Add("插入邮寄地址", null, (sender, e) => AddFieldModule("address"));
                drawingMenu.Items.Add("插入收件地址", null, (sender, e) => AddFieldModule("recipient"));
            }
            drawingMenu.Show(drawingBtn, new Point(0, drawingBtn.Height + Ui.P(4)));
        }
        private void ChooseCardSize()
        {
            using (var dialog = new CardSizeDialog(card))
                if (dialog.ShowDialog(this) == DialogResult.OK) ApplyCardSize(dialog.PageWidth, dialog.PageHeight);
        }
        private void ApplyCardSize(int width, int height)
        {
            if (card.Width == width && card.Height == height) return;
            Checkpoint(null);
            card.Resize(width, height);
            view.Fit();
            RefreshAll();
            ShowStatus("卡片尺寸 " + card.PrintSize);
        }
        private void OpenProject()
        {
            using (var d = new OpenFileDialog { Title = "打开卡片工程或模板",
                Filter = materialEditor ? ProjectFilter :
                    "QSL 工程 (*.qslcard;*.qslmaterial)|*.qslcard;*.qslmaterial|" + ProjectFilter + "|" + MaterialStudio.DesignFilter,
                RestoreDirectory = true, CheckFileExists = true })
                if (d.ShowDialog(this) == DialogResult.OK && (IsMaterialFile(d.FileName) || CanLeave())) LoadPath(d.FileName);
        }
        private void LoadPath(string file)
        {
            try
            {
                if (IsMaterialFile(file)) { OpenMaterialStudio(MaterialDesign.Open(file), file); return; }
                SetProject(Files.Open(file), file);
                ShowStatus("已打开 " + Path.GetFileName(file));
            }
            catch (Exception e) { Error("打开工程失败", e); }
        }
        private static bool IsMaterialFile(string file)
        {
            return string.Equals(Path.GetExtension(file), ".qslmaterial", StringComparison.OrdinalIgnoreCase);
        }
        private bool Save(bool asNew)
        {
            string file = path;
            if (asNew || string.IsNullOrEmpty(file))
            {
                using (var d = new SaveFileDialog { Title = "保存工程", Filter = ProjectFilter, FileName = Suggested(false),
                    AddExtension = true, DefaultExt = "qslcard", RestoreDirectory = true })
                { if (d.ShowDialog(this) != DialogResult.OK) return false; file = d.FileName; }
            }
            try
            {
                Files.Save(card, file); path = file; saved = card.Revision; editToken = null;
                MarkDirty(); ShowStatus("已保存 " + Path.GetFileName(file)); return true;
            }
            catch (Exception e) { Error("保存工程失败", e); return false; }
        }
        private void SaveTemplate()
        {
            var copy = CardOutput.ForHandwriting(card);
            copy.ClearDemoFields();
            foreach (var layer in copy.Layers)
                if (layer.Cover && layer.Name == "示例照片") { layer.Image = null; layer.Name = "封面照片"; }
            using (var d = new SaveFileDialog { Title = "保存空白通联模板", Filter = ProjectFilter,
                DefaultExt = "qslcard", FileName = Suggested(false) + "_模板", RestoreDirectory = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { Files.Save(copy, d.FileName); ShowStatus("已保存模板"); }
                catch (Exception e) { Error("保存模板失败", e); }
            }
        }
        private void SaveSideTemplate()
        {
            string face = side == 0 ? "正面" : "反面";
            using (var d = new SaveFileDialog { Title = "保存当前" + face + "的排版和图片", Filter = SideFilter,
                DefaultExt = "qslside", FileName = Suggested(false) + "_" + face, RestoreDirectory = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { Files.Save(card, d.FileName, side); ShowStatus("已保存" + face + "模板"); }
                catch (Exception e) { Error("保存单面模板失败", e); }
            }
        }
        private void OpenSideTemplate()
        {
            using (var d = new OpenFileDialog { Title = "应用单面模板", Filter = SideFilter, RestoreDirectory = true, CheckFileExists = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    int target;
                    var preset = Files.OpenSide(d.FileName, out target);
                    if (preset.Portrait != card.Portrait)
                        throw new InvalidDataException("这份单面模板的尺寸是 " + preset.PrintSize + "。可先在「更换模板」中切换整张卡的方向，再应用该单面模板。");
                    if (card.Layers.FindAll(layer => layer.Side != target).Count + preset.Layers.Count > Project.MaxLayers)
                        throw new InvalidDataException("应用后图层数过多。请先删除一些图层。");
                    string face = target == 0 ? "正面" : "反面";
                    if (card.Customized && MessageBox.Show(this, "用文件中的排版和图片替换当前" + face + "？可用 Ctrl+Z 恢复。", "应用单面模板",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                    preset.Resize(card.Width, card.Height);
                    Checkpoint(null);
                    string back = target == 1 ? preset.Template : card.TemplateOn(1);
                    if (target == 0) card.Template = preset.Template;
                    card.ReverseTemplate = back == card.Template ? null : back;
                    card.Background[target] = preset.Background[target];
                    card.Layers.RemoveAll(layer => layer.Side == target);
                    foreach (var layer in preset.Layers)
                    {
                        layer.Id = Guid.NewGuid().ToString("N");
                        card.Layers.Add(layer);
                    }
                    card.Customized = true;
                    selected = null; view.Fit(); ShowSide(target, true); ShowStatus("已套用" + face + "模板");
                }
                catch (Exception e) { Error("打开单面模板失败", e); }
            }
        }
        private void ShowFileMenu()
        {
            if (fileMenu == null)
            {
                fileMenu = new StudioMenu(true);
                fileMenu.AddCommand("另存工程…", "save", () => Save(true));
                fileMenu.AddCommand("另存为模板…", "assets", SaveTemplate);
                fileMenu.AddCommand("卡片尺寸…", "fit", ChooseCardSize);
                fileMenu.AddCommand("导出回邮地址标签…", "export", ExportAddressSheet);
                fileMenu.AddCommand("套印通联资料…", "export", ExportOverprint);
                fileMenu.Items.Add(new ToolStripSeparator());
                fileMenu.AddCommand("用户手册", "help", Help);
            }
            fileMenu.Show(moreBtn, new Point(0, moreBtn.Height + Ui.P(4)));
        }
        private void EditStorage()
        {
            try
            {
                using (var dialog = new StorageDialog(storage))
                    if (dialog.ShowDialog(this) == DialogResult.OK) ShowStatus("存储位置已更新");
            }
            catch (Exception e) { Error("无法打开存储设置", e); }
        }

        private void ManageStations()
        {
            try
            {
                string folder = storage.Read().Stations;
                using (var dialog = new StationDialog(card, folder, Stations.Read(folder)))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (dialog.Saved) { ShowStatus("已保存台站资料"); return; }
                    if (dialog.Selected == null) return;
                    Checkpoint(null);
                    dialog.Selected.Apply(card);
                    RefreshAll(); ShowStatus("已填入台站资料「" + dialog.Selected.Name + "」");
                }
            }
            catch (Exception e) { Error("读取台站资料失败", e); }
        }
        private void ImportLog()
        {
            using (var file = new OpenFileDialog { Title = "打开 ADIF 通联日志", Filter = "ADIF 通联日志 (*.adi;*.adif)|*.adi;*.adif",
                RestoreDirectory = true, CheckFileExists = true })
            {
                if (file.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    AdifLog log = AdifLog.Read(file.FileName);
                    using (var picker = new LogDialog(card, log))
                    {
                        if (picker.ShowDialog(this) != DialogResult.OK) return;
                        if (picker.Selected != null)
                        {
                            Checkpoint(null);
                            picker.Selected.Apply(card);
                            ShowSide(1, true); ShowStatus("已载入与 " + picker.Selected.Other + " 的通联");
                            return;
                        }
                        using (var output = new BatchDialog(card.Copy(), picker.Batch, picker.BatchFile))
                        {
                            var result = output.ShowDialog(this);
                            if (output.Failure != null) { Error("批量导出失败", output.Failure); return; }
                            if (result == DialogResult.OK)
                            {
                                ShowStatus("已导出 " + picker.Batch.Count + " 张双面卡片");
                                MessageBox.Show(this, "已导出 " + picker.Batch.Count + " 张双面卡片。\n\n" + picker.BatchFile,
                                    "批量导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else ShowStatus("已取消批量导出");
                        }
                    }
                }
                catch (Exception e) { Error("读取 ADIF 日志失败", e); }
            }
        }
        private void ExportAddressSheet()
        {
            var output = card.Copy();
            output.ClearDemoFields();
            if (string.IsNullOrWhiteSpace(output.Value("address")))
            {
                ChangeGroup(0);
                fields["address"].Box.Edit.Focus();
                MessageBox.Show(this, "请先填写「本台」页的邮寄地址。每张 A4 纸排 14 枚 88 × 34 mm 标签。",
                    "回邮地址标签", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var file = new SaveFileDialog { Title = "保存 A4 回邮地址标签（88 × 34 mm）", Filter = "PDF 印刷文件 (*.pdf)|*.pdf",
                DefaultExt = "pdf", FileName = Suggested(false) + "_回邮标签", RestoreDirectory = true })
            {
                if (file.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    UseWaitCursor = true;
                    CardOutput.SaveAddressSheet(output, file.FileName);
                    ShowStatus("已导出回邮地址标签");
                }
                catch (Exception e) { Error("导出回邮地址标签失败", e); }
                finally { UseWaitCursor = false; }
            }
        }
        private string Suggested(bool includeOther)
        {
            var file = card.Value("own").Trim();
            if (string.IsNullOrEmpty(file)) file = "QSL";
            if (includeOther && !string.IsNullOrWhiteSpace(card.Value("other"))) file += "-" + card.Value("other").Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) file = file.Replace(c, '_');
            return Files.Cut(file, 70).Trim(' ', '.');
        }
        private void Export()
        {
            if (materialEditor)
            {
                Artwork = card;
                DialogResult = DialogResult.OK;
                return;
            }
            int format;
            bool pair, bleed, blank, materials, overprint;
            using (var dialog = new ExportDialog(side, card))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                format = dialog.Format; pair = dialog.Both; bleed = dialog.Bleed; blank = dialog.Blank;
                materials = dialog.MaterialRequested; overprint = dialog.OverprintRequested;
            }
            if (materials) { ShowMaterials(); return; }
            if (overprint) { ExportOverprint(); return; }
            string ext = format == 0 ? "pdf" : format == 1 ? "png" : "jpg";
            string kind = format == 0 ? "PDF 印刷文件" : format == 1 ? "PNG 无损图片" : "JPG 图片";
            using (var d = new SaveFileDialog { Title = pair && format != 0 ? "保存图片（文件名自动加上正面、反面）" : "保存 QSL 卡片",
                Filter = kind + " (*." + ext + ")|*." + ext, DefaultExt = ext, FileName = Suggested(!blank) + (blank ? "_印刷底版" : ""),
                RestoreDirectory = true, OverwritePrompt = !pair || format == 0 })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string front = null, back = null;
                if (pair && format != 0)
                {
                    string basePath = Path.Combine(Path.GetDirectoryName(d.FileName), Path.GetFileNameWithoutExtension(d.FileName));
                    front = basePath + "_正面." + ext; back = basePath + "_反面." + ext;
                    if ((File.Exists(front) || File.Exists(back)) && MessageBox.Show(this, "目标文件夹中已有同名卡片图片。是否覆盖？", "确认覆盖", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                }
                try
                {
                    UseWaitCursor = true; ShowStatus("正在导出…"); footer.Update();
                    var output = blank ? CardOutput.ForHandwriting(card) : card;
                    if (format == 0) CardOutput.SavePdf(output, pair ? new[] { 0, 1 } : new[] { side }, d.FileName, bleed);
                    else if (pair) { CardOutput.SaveImage(output, 0, front, format == 1); CardOutput.SaveImage(output, 1, back, format == 1); }
                    else CardOutput.SaveImage(output, side, d.FileName, format == 1);
                    ShowStatus(pair && format != 0 ? "已导出两面图片" : "已导出 " + Path.GetFileName(d.FileName));
                    string names = pair && format != 0 ? Path.GetFileName(front) + "\n" + Path.GetFileName(back) : Path.GetFileName(d.FileName);
                    MessageBox.Show(this, names + "\n\n" + Path.GetDirectoryName(d.FileName), "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception e) { Error("无法导出卡片", e); }
                finally { UseWaitCursor = false; }
            }
        }
        private void ExportOverprint()
        {
            if (!CardPainter.HasOverprintContent(card, 0) && !CardPainter.HasOverprintContent(card, 1))
            {
                ChangeGroup(1);
                MessageBox.Show(this, "请在「通联」填写要套印的资料。自定义版式可通过「绘图 / 模块」插入对应的资料栏。",
                    "套印通联资料", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int face;
            PointF offset;
            using (var dialog = new OverprintDialog(card, side))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                face = dialog.Side; offset = dialog.Offset;
            }
            using (var picker = new SaveFileDialog
            {
                Title = "保存套印 PDF",
                Filter = "PDF 文件 (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = Suggested(true) + (face == 0 ? "_正面套印" : "_反面套印"),
                RestoreDirectory = true
            })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    UseWaitCursor = true;
                    CardOutput.SaveOverprint(card, face, offset, picker.FileName);
                    ShowStatus("已保存 " + Path.GetFileName(picker.FileName));
                    MessageBox.Show(this, "打印时请选择「实际大小」或 100%，并使用与成品一致的纸张尺寸。",
                        "套印 PDF 已保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception e) { Error("无法保存套印 PDF", e); }
                finally { UseWaitCursor = false; }
            }
        }
        private void ShowMaterials()
        {
            OpenMaterialStudio(materialDraft ?? MaterialDesign.FromCard(card), materialPath);
        }
        private void OpenMaterialStudio(MaterialDesign draft, string file)
        {
            using (var studio = new MaterialStudio(draft, file, hasDocument ? card.Copy() : null, storage))
            {
                studio.ShowDialog(this);
                materialDraft = studio.ReadDesign();
                materialPath = studio.DesignPath;
                if (studio.SavedFile != null) ShowStatus("已保存 " + Path.GetFileName(studio.SavedFile));
            }
        }
        private void Error(string label, Exception e)
        {
            ShowStatus(label);
            string reason = e is InvalidDataException ? e.Message : e is UnauthorizedAccessException ? "无法访问该文件夹。请选一个有写入权限的位置。" :
                e is ArgumentException || e is OutOfMemoryException ? "文件格式无效，或图片太大。请换一个文件后再试。" : e.Message;
            MessageBox.Show(this, reason, label, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        private Art ReadPicture(bool cover = false)
        {
            using (var d = new OpenFileDialog { Title = cover ? "选择底图" : "选择图片", Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
                RestoreDirectory = true, CheckFileExists = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return null;
                try { UseWaitCursor = true; return Art.Import(d.FileName); }
                catch (Exception e) { Error("无法导入图片", e); return null; }
                finally { UseWaitCursor = false; }
            }
        }
        private Layer CurrentCover()
        {
            return card.Layers.Find(l => l.Side == side && l.Cover);
        }
        private void ShowCoverTools()
        {
            var cover = CurrentCover();
            if (cover == null || cover.Image == null) { ImportPicture(true); return; }
            if (coverMenu == null)
            {
                coverMenu = new StudioMenu();
                coverMenu.Items.Add("更换底图…", null, (s, e) => ImportPicture(true));
                coverMenu.Items.Add("裁切与拉伸…", null, (s, e) => AdjustPicture(CurrentCover()));
            }
            coverMenu.Show(coverBtn, new Point(0, coverBtn.Height + Ui.P(4)));
        }

        private void ImportPicture(bool cover)
        {
            var current = cover ? CurrentCover() : null;
            if (current == null && !CanAdd()) return;
            var a = ReadPicture(cover); if (a == null) return;
            if (cover) UseCover(a, current);
            else
            {
                float w = Math.Min(360, card.Width * .75f), h = w * a.Bitmap.Height / a.Bitmap.Width;
                float limit = Math.Min(320, card.Height * .75f);
                if (h > limit) { w *= limit / h; h = limit; }
                AddLayer(new Layer { Side = side, Kind = LayerKind.Picture, Image = a, Name = "导入图片",
                    X = (card.Width - w) / 2, Y = (card.Height - h) / 2, W = Math.Max(1, w), H = Math.Max(1, h) });
                ShowStatus("已导入图片");
            }
        }
        private void UseCover(Art image, Layer current)
        {
            Layer draft = null;
            try
            {
                draft = current == null ? new Layer { Kind = LayerKind.Picture, Side = side, Cover = true } : current.Copy();
                if (current == null)
                {
                    var frame = Templates.PageCoverFrame(card, side);
                    draft.X = frame.X; draft.Y = frame.Y; draft.W = frame.Width; draft.H = frame.Height;
                }
                draft.Name = "自定义底图";
                draft.Image = image; draft.Hidden = false;
                draft.ImageFit = PictureFit.Auto; draft.ImageCrop = RectangleF.Empty;
                if (!ChoosePicturePlacement(draft)) return;
                CommitPicture(draft, current);
                ShowStatus("已更换底图");
            }
            finally { if (draft == null || !card.Layers.Contains(draft)) image.Dispose(); }
        }
        private bool ChoosePicturePlacement(Layer draft)
        {
            using (var d = new PictureDialog(card, draft))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return false;
                var source = draft.PictureSource;
                if (d.Mode != draft.EffectiveImageFit || d.Selection != source)
                {
                    draft.ImageFit = d.Mode;
                    draft.ImageCrop = d.Selection;
                }
                return true;
            }
        }
        private void AdjustPicture(Layer layer)
        {
            if (layer == null || layer.Image == null) return;
            var draft = layer.Copy();
            if (!ChoosePicturePlacement(draft) || draft.ImageFit == layer.ImageFit && draft.ImageCrop == layer.ImageCrop) return;
            CommitPicture(draft, layer);
            ShowStatus("已调整图片");
        }
        private void CommitPicture(Layer picture, Layer previous)
        {
            Checkpoint(null);
            if (previous != null) card.Layers[card.Layers.IndexOf(previous)] = picture;
            else
            {
                int first = card.Layers.FindIndex(l => l.Side == picture.Side);
                card.Layers.Insert(first < 0 ? card.Layers.Count : first, picture);
            }
            card.Customized = true;
            selected = picture.Id; view.SelectedId = selected;
            if (showLayers) SetLayers(false);
            RefreshInspector(true); MarkDirty();
        }
        private void ReplaceActive()
        {
            var l = Active; if (l == null || l.Kind != LayerKind.Picture) return;
            Art a = ReadPicture(l.Cover);
            if (a == null) return;
            if (l.Cover) UseCover(a, l);
            else Mutate(null, x => { x.Image = a; x.ImageCrop = RectangleF.Empty; }, true);
        }
        private void ShowAssets()
        {
            try
            {
                using (var d = new AssetsDialog(storage.Read().CacheRoot, AppDomain.CurrentDomain.BaseDirectory, card.Value("own")))
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    if (d.ImportLocal) ImportPicture(false);
                    else if (d.Logo != null)
                    {
                        if (CanAdd()) InsertLogo(d.Logo, d.AssetName);
                        else d.Logo.Dispose();
                    }
                }
            }
            catch (Exception e) { Error("无法打开协会标志", e); }
        }
        private void InsertLogo(Art art, string name)
        {
            float width = Math.Min(175, art.Bitmap.Width * 254f / 300);
            float height = width * art.Bitmap.Height / art.Bitmap.Width;
            if (height > 170) { width *= 170 / height; height = 170; }
            string key = card.TemplateOn(side);
            var native = Templates.ReferenceSize(key);
            PointF location = new PointF((native.Width - width) / 2, (native.Height - height) / 2);
            if (side == 1 && key == "logbook") location = new PointF(54, 704);
            else if (side == 1 && key == "postcard")
            {
                float scale = Math.Min(1f, Math.Min(105 / width, 129 / height));
                width *= scale; height *= scale;
                location = new PointF(720 + (105 - width) / 2, 123 + (129 - height) / 2);
            }
            else if (side == 0 && key == "event") location = new PointF(53, 48);
            else if (side == 1 && key == "label") location = new PointF(1075, 403);
            else if (side == 1 && (key == "postal" || key == "postal-cn")) location = new PointF(769, 85);
            else if (key == "photojournal" || side == 1 && key == "bureau")
            {
                float scale = Math.Min(1f, Math.Min(92 / width, (side == 0 ? 94 : 161) / height));
                width *= scale; height *= scale;
                location = new PointF(61 + (92 - width) / 2, 55);
            }
            float sx = card.Width / (float)native.Width, sy = card.Height / (float)native.Height;
            AddLayer(new Layer { Side = side, Kind = LayerKind.Picture, Image = art, Name = name,
                X = location.X * sx, Y = location.Y * sy, W = Math.Max(1, width * sx), H = Math.Max(1, height * sy) });
            if (showLayers) SetLayers(false);
            ShowStatus("已插入 " + name);
        }
        private void Help()
        {
            if (manual == null || manual.IsDisposed)
            {
                manual = new HelpDialog();
                manual.Show(this);
            }
            else manual.Activate();
        }
        internal void ShowGettingStarted()
        {
            if (storage.GuideDismissed) return;

            using (var dialog = new GettingStartedDialog())
            {
                bool openManual = dialog.ShowDialog(this) == DialogResult.OK;
                if (dialog.Dismissed)
                {
                    try { storage.DismissGuide(); }
                    catch (IOException) { ShowStatus("无法保存启动引导设置。"); }
                    catch (UnauthorizedAccessException) { ShowStatus("无法保存启动引导设置。"); }
                }
                if (openManual) Help();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (key == (Keys.Control | Keys.N)) { NewProject(); return true; }
            if (key == (Keys.Control | Keys.O)) { OpenProject(); return true; }
            if (key == Keys.F1) { Help(); return true; }
            if (!hasDocument) return base.ProcessCmdKey(ref msg, key);
            if (key == (Keys.Control | Keys.S)) { Save(false); return true; }
            if (key == (Keys.Control | Keys.Shift | Keys.S)) { Save(true); return true; }
            if (key == (Keys.Control | Keys.E)) { Export(); return true; }
            if (key == (Keys.Control | Keys.Z)) { MoveHistory(false); return true; }
            if (key == (Keys.Control | Keys.Y) || key == (Keys.Control | Keys.Shift | Keys.Z)) { MoveHistory(true); return true; }
            if (key == (Keys.Control | Keys.D1)) { ShowSide(0, true); return true; }
            if (key == (Keys.Control | Keys.D2)) { ShowSide(1, true); return true; }
            if (key == (Keys.Control | Keys.D)) { Duplicate(); return true; }
            if (view != null && view.Focused)
            {
                if (key == Keys.Delete) { Remove(); return true; }
                if (key == Keys.Escape)
                {
                    if (view.IsDrawing) { SetDrawingTool(DrawingTool.Select); ShowStatus("选择与移动"); }
                    else SelectLayer(null);
                    return true;
                }
                var k = key & Keys.KeyCode;
                if (Active != null && (k == Keys.Left || k == Keys.Up || k == Keys.Right || k == Keys.Down))
                {
                    int n = (key & Keys.Shift) != 0 ? 10 : 1;
                    Mutate(null, l =>
                    {
                        if (k == Keys.Left || k == Keys.Right) l.X = Project.ConstrainOffset(l.X + (k == Keys.Left ? -n : n), l.W, card.Width);
                        else l.Y = Project.ConstrainOffset(l.Y + (k == Keys.Up ? -n : n), l.H, card.Height);
                    }, true);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, key);
        }
    }
}
