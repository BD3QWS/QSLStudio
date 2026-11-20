using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace QslStudio
{
    internal sealed partial class Editor : GlassForm
    {
        private Project card;
        private bool hasDocument;
        private int side, group, revision, saved;
        private bool sync, initialized;
        private Size laidOutSize;
        private StudioMenu fileMenu, clockMenu, coverMenu, newMenu, drawingMenu;
        private int fieldWidth = -1, fieldLayout = -1;
        private object editToken;
        private string path, selected;
        private readonly List<Project> undo = new List<Project>(), redo = new List<Project>();
        private readonly ToolTip tips = new ToolTip { ShowAlways = true, AutoPopDelay = 6500, InitialDelay = 400, ReshowDelay = 160 };

        private Panel header, left, center, right, footer, leftScroll, inspector, stageTools, stageStatus;
        private Label title, templateTitle, projectStatus, tipsStatus, zoomText, sideHint, emptyWorkspace;
        private ButtonX newBtn, openBtn, saveBtn, moreBtn, exportBtn, aboutBtn, storageBtn, stationBtn, qsoBtn, clearBtn, nowBtn, profileBtn, logBtn;
        private ButtonX frontBtn, backBtn, undoBtn, redoBtn, libraryBtn, textBtn, picBtn, coverBtn, guideBtn, minusBtn, plusBtn, fitBtn;
        private ButtonX templateBtn, propertiesBtn, layersBtn, materialsBtn, drawingBtn;
        private Panel fileTools, historyTools, insertTools, pageTabs, fieldTabs, propertyTabs, zoomTools;
        private ListBox layerList;
        private bool showLayers;
        private readonly Dictionary<string, FieldControl> fields = new Dictionary<string, FieldControl>();
        private Canvas view;
        private sealed class FieldControl
        {
            internal Field Field;
            internal Label Caption;
            internal InputBox Box;
            internal ButtonX Choices;
            internal StudioMenu Menu;
        }

        private readonly UserStorage storage;
        private readonly bool materialEditor;
        internal Project Artwork { get; private set; }

        internal Editor(string openPath, UserStorage storage = null, Project artwork = null)
        {
            this.storage = storage ?? UserStorage.ForUser();
            materialEditor = artwork != null;
            Text = materialEditor ? "特殊材质卡面编辑" : "QSL 卡片工坊";
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.F(13, false);
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var area = Screen.PrimaryScreen.WorkingArea;
            MinimumSize = new Size(Math.Min(Ui.P(770), area.Width), Math.Min(Ui.P(514), area.Height));
            Size = new Size(Math.Min(Ui.P(1320), Math.Max(MinimumSize.Width, area.Width - Ui.P(48))),
                Math.Min(Ui.P(827), Math.Max(MinimumSize.Height, area.Height - Ui.P(48))));
            StartPosition = materialEditor ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
            card = artwork ?? Templates.Create("blank");
            hasDocument = materialEditor;
            BuildChrome();
            BuildFields();
            BuildInspector();
            initialized = true;
            UpdateWorkspace();
            LayoutEditor();
            if (hasDocument) RefreshAll();
            else ActiveControl = newBtn;
            if (materialEditor) ShowStatus("完成编辑后，选择「应用到加工」。");
            if (!string.IsNullOrEmpty(openPath)) Shown += delegate { LoadPath(openPath); };
        }

        private ButtonX B(Control parent, string title, string icon, Action action, string tip)
        {
            var b = new ButtonX(title, icon);
            if (title == "") b.AccessibleName = tip;
            b.Click += delegate { action(); };
            parent.Controls.Add(b);
            if (!string.IsNullOrEmpty(tip)) tips.SetToolTip(b, tip);
            return b;
        }
        private Label L(Control parent, string text, int size, bool bold, Color color)
        { var l = Ui.Label(text, size, bold, color); parent.Controls.Add(l); return l; }
        private Panel Pn(Control parent) { var p = new SurfacePanel(); parent.Controls.Add(p); return p; }
        private Panel Pane(Control parent, int radius, bool track = false)
        { var panel = new GlassPanel(radius, track); parent.Controls.Add(panel); return panel; }

        private void BuildChrome()
        {
            header = Pn(this);
            left = Pane(this, 18); right = Pane(this, 18);
            center = Pn(this); footer = Pn(this);
            emptyWorkspace = L(this, "新建卡片或打开已有工程", 17, false, Ui.Quiet);
            emptyWorkspace.TextAlign = ContentAlignment.MiddleCenter;
            title = L(header, "未命名卡片", 15, true, Ui.Ink);
            title.TextAlign = ContentAlignment.MiddleCenter;
            fileTools = Pane(header, 22);
            newBtn = B(fileTools, "新建", "new", NewProject, "从空白画布或模板新建（Ctrl+N）");
            openBtn = B(fileTools, "打开", "open", OpenProject, "打开工程或模板（Ctrl+O）");
            saveBtn = B(fileTools, "保存", "save", () => Save(false), "保存工程（Ctrl+S）");
            moreBtn = B(fileTools, "", "more", ShowFileMenu, "另存、卡片尺寸与用户手册");
            moreBtn.AccessibleName = "更多操作";
            newBtn.Pill = openBtn.Pill = saveBtn.Pill = moreBtn.Pill = true;
            materialsBtn = B(header, "PCB / 特殊材质", "pcb", ShowMaterials, "设计 PCB、木片、亚克力或金属 QSL 卡");
            materialsBtn.Border = materialsBtn.Pill = true;
            materialsBtn.Visible = !materialEditor;
            exportBtn = B(header, materialEditor ? "应用到加工" : "导出卡片", materialEditor ? "pcb" : "export", Export,
                materialEditor ? "返回加工预览并应用卡面的修改（Ctrl+E）" : "生成 PDF、PNG、JPG（Ctrl+E）");
            exportBtn.Primary = true;

            templateTitle = L(left, "资料", 14, true, Ui.Ink);
            fieldTabs = Pane(left, 18, true);
            stationBtn = B(fieldTabs, "本台", "", () => ChangeGroup(0), "本台呼号、位置和设备");
            qsoBtn = B(fieldTabs, "通联", "", () => ChangeGroup(1), "对方呼号、UTC、频率、模式与信号报告");
            stationBtn.Pill = qsoBtn.Pill = true;
            leftScroll = Pn(left); leftScroll.AutoScroll = true;

            stageTools = Pn(center); stageStatus = Pn(center);
            pageTabs = Pane(stageTools, 20, true);
            frontBtn = B(pageTabs, "正面", "", () => ShowSide(0, true), "切换到正面（Ctrl+1）");
            backBtn = B(pageTabs, "反面", "", () => ShowSide(1, true), "切换到反面（Ctrl+2）");
            frontBtn.Pill = backBtn.Pill = true;
            templateBtn = B(stageTools, "更换模板", "assets", ShowTemplates, "预览并更换正面、反面或两面模板");
            templateBtn.Pill = templateBtn.Border = true;
            historyTools = Pane(stageTools, 20);
            undoBtn = B(historyTools, "", "undo", () => MoveHistory(false), "撤销（Ctrl+Z）");
            redoBtn = B(historyTools, "", "redo", () => MoveHistory(true), "重做（Ctrl+Y）");
            undoBtn.Pill = redoBtn.Pill = true;
            insertTools = Pane(stageTools, 20);
            textBtn = B(insertTools, "文字", "text", AddText, "新增可移动文字");
            picBtn = B(insertTools, "图片", "image", () => ImportPicture(false), "插入照片、标志或签名图片");
            coverBtn = B(insertTools, "底图 ▾", "", ShowCoverTools, "更换底图，或裁切与拉伸已有底图");
            coverBtn.AccessibleName = "底图";
            libraryBtn = B(insertTools, "协会标志", "assets", ShowAssets, "插入无线电协会和活动标志");
            drawingBtn = B(insertTools, "绘图 / 模块", "draw", ShowDrawingMenu, "绘制线段、矩形、椭圆，或插入资料模块");
            drawingBtn.AccessibleName = "绘图和模块";
            textBtn.Pill = picBtn.Pill = coverBtn.Pill = libraryBtn.Pill = drawingBtn.Pill = true;

            view = new Canvas(); center.Controls.Add(view);
            view.Selected += SelectLayer;
            view.StartedChange += () => Checkpoint(null);
            view.FinishedChange += () => { card.Customized = true; RefreshInspector(false); MarkDirty(); };
            view.Drawn += layer => { if (CanAdd()) AddLayer(layer, true); };
            view.ViewChanged += ZoomLabel;
            view.EditSelected += () =>
            {
                var layer = Active;
                if (layer == null) return;
                if (layer.Kind == LayerKind.Picture && layer.Image != null) AdjustPicture(layer);
                if (layer.Kind == LayerKind.Text)
                {
                    if (showLayers) SetLayers(false);
                    activeText.Edit.Focus(); activeText.Edit.SelectAll();
                }
            };
            sideHint = L(stageStatus, "", 11, false, Ui.Quiet);
            guideBtn = B(stageStatus, "", "grid", () => { view.Guides = !view.Guides; guideBtn.Chosen = view.Guides; guideBtn.Invalidate(); view.Invalidate(); }, "参考线与 1 mm 吸附；拖动时按 Alt 暂时关闭吸附");
            guideBtn.Pill = guideBtn.Border = true;
            zoomTools = Pane(stageStatus, 20);
            minusBtn = B(zoomTools, "", "minus", () => view.StepZoom(1 / 1.22f), "缩小画布");
            zoomText = L(zoomTools, "100%", 12, true, Ui.Ink); zoomText.TextAlign = ContentAlignment.MiddleCenter;
            plusBtn = B(zoomTools, "", "add", () => view.StepZoom(1.22f), "放大画布");
            fitBtn = B(zoomTools, "", "fit", () => view.Fit(), "适合窗口；鼠标中键可平移");
            minusBtn.Pill = plusBtn.Pill = fitBtn.Pill = true;

            propertyTabs = Pane(right, 19, true);
            propertiesBtn = B(propertyTabs, "属性", "", () => SetLayers(false), "设置选中元素的字体、大小和位置");
            layersBtn = B(propertyTabs, "图层", "layers", () => SetLayers(true), "查看和选中当前面的图层");
            propertiesBtn.Pill = layersBtn.Pill = true;
            layerList = new ListBox { Font = Ui.F(13, false), BackColor = Ui.Surface, ForeColor = Ui.Ink, BorderStyle = BorderStyle.None, IntegralHeight = false, AccessibleName = "当前面图层", Visible = false };
            right.Controls.Add(layerList);
            layerList.SelectedIndexChanged += delegate { if (!sync && layerList.SelectedItem is Pick) SelectLayer(((Pick)layerList.SelectedItem).Id); };
            layerList.DoubleClick += delegate { if (layerList.SelectedItem != null) SetLayers(false); };
            projectStatus = L(footer, "", 11, true, Ui.Ink);
            tipsStatus = L(footer, "", 11, false, Ui.Quiet);
            storageBtn = B(footer, "存储位置", "", EditStorage, "选择台站资料和缓存目录");
            aboutBtn = B(footer, "用户手册", "help", Help, "查看操作说明和快捷键（F1）");
            storageBtn.Pill = aboutBtn.Pill = true;
        }

        private void BuildFields()
        {
            foreach (var f in Field.All)
            {
                string caption = f.Key.StartsWith("utc", StringComparison.Ordinal) ? "UTC 时间" : f.Key == "address" ? "邮寄地址" : f.Label;
                var fc = new FieldControl { Field = f, Caption = L(leftScroll, caption, 11, true, Ui.Quiet), Box = new InputBox(f.Label, Field.IsMultiline(f.Key)) };
                fields.Add(f.Key, fc); leftScroll.Controls.Add(fc.Box);
                if (f.Key == "confirm" || f.Key == "reply")
                {
                    fc.Choices = B(leftScroll, "", "chevron", () => ShowFieldChoices(fc), "选择" + f.Label);
                    fc.Choices.Border = true;
                }
                fc.Box.Edit.TextChanged += delegate
                {
                    if (sync) return;
                    string value = ReadFieldInput(fc.Box.Edit, fc.Field.Key);
                    if (card.Value(fc.Field.Key) == value) return;
                    Checkpoint(fc.Box.Edit);
                    card.Values[fc.Field.Key] = value;
                    WithSync(() => { var l = Active; if (l != null && l.Field == fc.Field.Key) activeText.Edit.Text = value; });
                    MarkDirty();
                };
                fc.Box.Edit.Leave += delegate { editToken = null; };
            }
            nowBtn = B(leftScroll, "日期与时间…", "", ShowClockMenu, "填入当前 UTC，或把北京时间换算成 UTC"); nowBtn.Border = true;
            profileBtn = B(leftScroll, "台站资料…", "", ManageStations, "保存或载入常用台站资料"); profileBtn.Border = true;
            logBtn = B(leftScroll, "导入 ADIF 日志…", "open", ImportLog, "从日志填入通联资料，或批量导出 PDF"); logBtn.Border = true;
            clearBtn = B(leftScroll, "清空台站资料", "", ClearFields, "清空当前资料栏，保留版式和图片");
        }

        private string ReadFieldInput(TextBox edit, string key)
        {
            string text = edit.Text;
            if (key != "postcode") return Files.Cut(text, Field.Limit(key));
            string digits = Field.PostalDigits(text);
            if (digits != text)
            {
                int caret = Field.PostalDigits(text.Substring(0, edit.SelectionStart)).Length;
                WithSync(() => { edit.Text = digits; edit.SelectionStart = caret; });
            }
            return digits;
        }
        private void ShowFieldChoices(FieldControl control)
        {
            if (control.Menu == null)
            {
                control.Menu = new StudioMenu();
                bool confirm = control.Field.Key == "confirm";
                var labels = confirm ? new[] { "留空", "QSO 双向通联", "SWL 确认收听", "EYEBALL 见面交换" } :
                    new[] { "留空", "PSE QSL 请回卡", "TNX QSL 谢谢回卡" };
                var values = confirm ? new[] { "", "QSO", "SWL", "EYEBALL" } : new[] { "", "PSE QSL", "TNX QSL" };
                for (int i = 0; i < labels.Length; i++)
                {
                    string value = values[i];
                    control.Menu.Items.Add(labels[i], null, (s, e) =>
                    {
                        editToken = null;
                        control.Box.Edit.Text = value.Replace("\n", "\r\n");
                        editToken = null;
                    });
                }
            }
            control.Menu.Show(control.Choices, new Point(0, control.Choices.Height + Ui.P(4)));
        }
        private void LayoutFields()
        {
            int width = Ui.ScrollContentWidth(leftScroll, 14);
            string backTemplate = card.TemplateOn(1);
            int layout = group == 0 ? 0 : Templates.IsBlank(backTemplate) ? -2 : 1 + Templates.Index(backTemplate);
            if (fieldWidth == width && fieldLayout == layout) return;
            fieldWidth = width; fieldLayout = layout;
            int oldScroll = -leftScroll.AutoScrollPosition.Y;
            leftScroll.AutoScrollPosition = Point.Empty;
            leftScroll.SuspendLayout();
            int x = 7, y = 7;
            string[] labels = group == 0 ? new[] { "own", "location", "operator", "grid", "cq|itu", "rig", "antenna", "power", "address", "email", "signature", "activity", "reference" }
                : new[] { "other", "date|utc", "mhz|mode", "rst", "confirm", "via", "reply", "note" };
            if (group == 1)
            {
                switch (backTemplate)
                {
                    case "bureau":
                        var rows = new List<string>(labels);
                        rows.InsertRange(4, new[] { "date2|utc2", "mhz2|mode2", "rst2", "date3|utc3", "mhz3|mode3", "rst3" });
                        labels = rows.ToArray();
                        break;
                    case "postal": labels = new[] { "other", "date|utc", "mhz|mode", "rst", "reply", "recipient" }; break;
                    case "portrait-postal":
                    case "postal-cn": labels = new[] { "other", "date|utc", "mhz|mode", "rst", "reply", "postcode", "recipient" }; break;
                    case "blank":
                    case "portrait-blank": labels = new[] { "other", "date|utc", "mhz|mode", "rst", "confirm", "via", "reply", "note", "recipient" }; break;
                    case "label": labels = new[] { "other", "date|utc", "mhz|mode", "rst", "via", "note" }; break;
                    case "satellite": labels = new[] { "other", "otherGrid", "sat", "date|utc", "mhz", "mhzRx", "mode|rst", "via", "note" }; break;
                    case "swl": labels = new[] { "other", "worked", "date|utc", "mhz|mode", "rst", "note" }; break;
                    case "eyeball": labels = new[] { "other", "date|utc", "venue" }; break;
                    case "callsign": labels = new[] { "other", "confirm", "date|utc", "mhz", "mode|rst", "via", "worked", "venue", "reply", "note" }; break;
                    case "handwritten": labels = new[] { "other", "date|utc", "mhz", "mode|rst", "via", "note" }; break;
                    case "contest": labels = new[] { "other", "date|utc", "mhz", "mode|rst", "stx|srx", "via", "note" }; break;
                    case "open-note": labels = new[] { "other", "date|utc", "mhz", "mode|rst", "reply", "note" }; break;
                    case "portrait-photo":
                    case "portrait-log": labels = new[] { "other", "date|utc", "mhz|mode", "rst", "reply", "via", "note" }; break;
                }
            }
            fields["other"].Caption.Text = group == 1 && backTemplate == "swl" ? "收听者呼号" : fields["other"].Field.Label;
            fields["mhz"].Caption.Text = backTemplate == "satellite" ? "上行频率（MHz）" :
                backTemplate == "callsign" || backTemplate == "handwritten" || backTemplate == "contest" || backTemplate == "open-note" ? fields["mhz"].Field.Label : "频率（MHz）";
            if (Templates.IsPortrait(backTemplate)) fields["mhz"].Caption.Text = "MHz / 频段";
            foreach (var item in fields.Values)
            { item.Caption.Visible = false; item.Box.Visible = false; if (item.Choices != null) item.Choices.Visible = false; }
            int tab = 0;
            profileBtn.Visible = group == 0;
            logBtn.Visible = nowBtn.Visible = group == 1;
            logBtn.Text = width < 145 ? "导入 ADIF…" : "导入 ADIF 日志…";
            var firstAction = group == 0 ? profileBtn : logBtn;
            Ui.Put(firstAction, x, y, width, 31); firstAction.TabIndex = tab++; y += 42;
            if (group == 1) { Ui.Put(nowBtn, x, y, width, 29); nowBtn.TabIndex = tab++; y += 38; }
            int dateWidth = FieldTextWidth("2000-12-31");
            int timeWidth = FieldTextWidth("00:00");
            int frequencyWidth = FieldTextWidth("145.850");
            int modeWidth = FieldTextWidth("MFSK");
            foreach (var row in labels)
            {
                var keys = row.Split('|');
                const int columnGap = 8;
                int editH = Field.IsMultiline(row) ? 74 : 31;
                int rowHeight = editH + 30;
                bool dates = keys.Length == 2 && keys[0].StartsWith("date", StringComparison.Ordinal);
                bool frequency = keys.Length == 2 && keys[0].StartsWith("mhz", StringComparison.Ordinal);
                int leftWidth = width;
                bool stacked = false;
                if (keys.Length == 2)
                {
                    var first = fields[keys[0]].Caption;
                    var second = fields[keys[1]].Caption;
                    int leftMinimum = Math.Max(Ui.TextWidth(first.Text, first.Font) + 3, dates ? dateWidth : frequency ? frequencyWidth : 0);
                    int rightMinimum = Math.Max(Ui.TextWidth(second.Text, second.Font) + 3, dates ? timeWidth : frequency ? modeWidth : 0);
                    stacked = width < leftMinimum + rightMinimum + columnGap;
                    if (!stacked)
                        leftWidth = Math.Min(width - columnGap - rightMinimum, Math.Max(leftMinimum, (width - columnGap) / 2));
                }
                for (int i = 0; i < keys.Length; i++)
                {
                    FieldControl f;
                    if (!fields.TryGetValue(keys[i], out f)) continue;
                    int left = x + (i == 0 || stacked ? 0 : leftWidth + columnGap);
                    int top = y + (stacked ? i * rowHeight : 0);
                    int w = keys.Length == 1 || stacked ? width : i == 0 ? leftWidth : width - leftWidth - columnGap;
                    f.Caption.Visible = true; f.Box.Visible = true;
                    f.Box.TabIndex = tab++;
                    Ui.Put(f.Caption, left, top, w, 19);
                    Ui.Put(f.Box, left, top + 20, w - (f.Choices == null ? 0 : 35), editH);
                    if (f.Choices != null)
                    {
                        f.Choices.Visible = true; f.Choices.TabIndex = tab++;
                        Ui.Put(f.Choices, left + w - 31, top + 20, 31, editH);
                    }
                }
                y += stacked ? 2 * rowHeight : rowHeight;
            }
            Ui.Put(clearBtn, x, y, width, 31); clearBtn.TabIndex = tab;
            leftScroll.AutoScrollMinSize = new Size(0, Ui.P(y + 41));
            leftScroll.ResumeLayout();
            leftScroll.AutoScrollPosition = new Point(0, oldScroll);
        }

        private static int FieldTextWidth(string sample)
        {
            return Ui.TextWidth(sample, Ui.F(13, false)) + 20;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (initialized && WindowState != FormWindowState.Minimized && ClientSize != laidOutSize)
                LayoutEditor();
        }
        private void LayoutEditor()
        {
            SuspendLayout();
            laidOutSize = ClientSize;
            int w = (int)Math.Round(ClientSize.Width / Ui.Scale), h = (int)Math.Round(ClientSize.Height / Ui.Scale);
            bool compact = w < 1040, narrow = w < 855;
            const int head = 61, foot = 36;
            int gap = narrow ? 7 : 12;
            int lw = narrow ? 178 : compact ? 206 : 230;
            int rw = narrow ? 196 : compact ? 217 : 234;
            int inner = w - gap * 2;
            int mid = Math.Max(120, inner - lw - rw - gap * 2);
            int body = Math.Max(120, h - head - foot);
            Ui.Put(header, gap, 9, inner, 44);
            const int fileButton = 69, materialButton = 139;
            int fileWidth = hasDocument ? 256 : 153;
            int materialX = inner - materialButton - (hasDocument ? 123 : 3);
            Ui.Put(fileTools, 0, 0, fileWidth, 44);
            Ui.Put(newBtn, 7, 4, fileButton, 33);
            Ui.Put(openBtn, 7 + fileButton, 4, fileButton, 33);
            Ui.Put(saveBtn, 7 + 2 * fileButton, 4, fileButton, 33);
            Ui.Put(moreBtn, 7 + 3 * fileButton, 4, 34, 33);
            Ui.Put(title, fileWidth + 8, 1, Math.Max(0, materialX - fileWidth - 16), 37);
            Ui.Put(materialsBtn, materialX, 2, materialButton, 37);
            Ui.Put(exportBtn, inner - 115, 2, 112, 37);
            Ui.Put(left, gap, head, lw, body);
            Ui.Put(center, lw + gap * 2, head, mid, body);
            Ui.Put(right, lw + mid + gap * 3, head, rw, body);
            Ui.Put(footer, gap, h - foot, inner, foot);
            Ui.Put(emptyWorkspace, gap, head + (body - 44) / 2, inner, 44);
            LayoutFooter(inner, foot);
            if (!hasDocument) { ResumeLayout(); return; }

            Ui.Put(templateTitle, 17, 8, lw - 34, 25);
            Ui.Put(fieldTabs, 12, 37, lw - 24, 37);
            Ui.Put(stationBtn, 6, 4, (lw - 36) / 2, 26); Ui.Put(qsoBtn, (lw - 24) / 2, 4, (lw - 36) / 2, 26);
            Ui.Put(leftScroll, 10, 75, lw - 22, body - 87);
            LayoutFields();

            LayoutStage(mid, body);

            Ui.Put(propertyTabs, 11, 9, rw - 22, 42);
            Ui.Put(propertiesBtn, 6, 4, (rw - 34) / 2, 31); Ui.Put(layersBtn, (rw - 22) / 2, 4, (rw - 34) / 2, 31);
            Ui.Put(layerPick, 17, 56, rw - 35, 29);
            Ui.Put(inspector, 9, 90, rw - 21, body - 103);
            Ui.Put(layerList, 16, 58, rw - 34, body - 73);
            LayoutInspector();
            ResumeLayout();
        }

        private void LayoutFooter(int inner, int foot)
        {
            int hintStart = Math.Min(280, inner / 3 + 11);
            Ui.Put(projectStatus, 8, 0, hintStart - 15, foot - 3);
            Ui.Put(tipsStatus, hintStart, 0, inner - hintStart - 200, foot - 3);
            Ui.Put(storageBtn, inner - 189, 1, 88, 30);
            Ui.Put(aboutBtn, inner - 99, 1, 96, 30);
        }

        private void UpdateWorkspace()
        {
            left.Visible = center.Visible = right.Visible = hasDocument;
            saveBtn.Visible = moreBtn.Visible = exportBtn.Visible = title.Visible = hasDocument;
            emptyWorkspace.Visible = !hasDocument;
        }

        private void LayoutStage(int mid, int body)
        {
            const int history = 65, inserts = 424, gap = 8, row = 44;
            const int commands = history + gap + inserts;
            int chooser = Math.Min(106, (mid - gap) / 2);
            int pages = Math.Min(126, mid - chooser - gap);
            bool stacked = mid < pages + chooser + commands + 2 * gap;
            bool narrowTools = mid < commands;
            bool historyFitsAbove = narrowTools && mid >= pages + chooser + history + 2 * gap;
            bool threeRows = narrowTools && !historyFitsAbove;
            int tools = row * (stacked ? (threeRows ? 3 : 2) : 1);
            Ui.Put(stageTools, 0, 0, mid, tools);
            Ui.Put(pageTabs, 0, 0, pages, 43);
            int tab = (pages - 12) / 2;
            Ui.Put(frontBtn, 6, 5, tab, 29); Ui.Put(backBtn, 6 + tab, 5, tab, 29);
            templateBtn.Text = chooser >= 88 ? "更换模板" : chooser >= 60 ? "模板" : "";
            Ui.Put(templateBtn, mid - chooser, 4, chooser, 33);

            int toolX = stacked ? (mid - commands) / 2 : pages + gap + (mid - pages - chooser - commands - 2 * gap) / 2;
            int toolY = stacked ? row : 0;
            int historyX = narrowTools ? (historyFitsAbove ? (pages + mid - chooser - history) / 2 : (mid - history) / 2) : toolX;
            Ui.Put(historyTools, historyX, historyFitsAbove ? 0 : toolY, history, 42);
            Ui.Put(undoBtn, 6, 4, 26, 31); Ui.Put(redoBtn, 33, 4, 26, 31);
            int insertWidth = Math.Min(mid, inserts);
            int insertX = narrowTools ? (mid - insertWidth) / 2 : toolX + history + gap;
            Ui.Put(insertTools, insertX, threeRows ? 2 * row : toolY, insertWidth, 42);
            bool iconsOnly = insertWidth < inserts;
            int item = iconsOnly ? (insertWidth - 12) / 5 : 68;
            textBtn.Text = iconsOnly ? "" : "文字"; picBtn.Text = iconsOnly ? "" : "图片";
            coverBtn.Text = iconsOnly ? "" : "底图 ▾"; libraryBtn.Text = iconsOnly ? "" : "协会标志";
            coverBtn.IconKey = iconsOnly ? "cover" : "";
            Ui.Put(textBtn, 6, 4, item, 31); Ui.Put(picBtn, 6 + item, 4, item, 31);
            int logoWidth = iconsOnly ? item : 96;
            Ui.Put(coverBtn, 6 + 2 * item, 4, item, 31); Ui.Put(libraryBtn, 6 + 3 * item, 4, logoWidth, 31);
            drawingBtn.Text = iconsOnly ? (item >= 68 ? "绘图" : "") : "绘图 / 模块";
            Ui.Put(drawingBtn, 6 + 3 * item + logoWidth, 4, insertWidth - 12 - 3 * item - logoWidth, 31);

            Ui.Put(view, 0, tools, mid, body - tools - 42);
            Ui.Put(stageStatus, 0, body - 42, mid, 41);
            int zoom = Math.Min(147, mid - 40), zoomLabel = Math.Min(45, zoom / 3);
            int z = (zoom - zoomLabel - 12) / 3;
            Ui.Put(zoomTools, mid - zoom, 0, zoom, 41);
            Ui.Put(guideBtn, mid - zoom - 40, 4, 34, 31);
            Ui.Put(minusBtn, 6, 4, z, 30); Ui.Put(zoomText, 6 + z, 4, zoomLabel, 30);
            Ui.Put(plusBtn, 6 + z + zoomLabel, 4, z, 30); Ui.Put(fitBtn, 6 + 2 * z + zoomLabel, 4, z, 30);
            Ui.Put(sideHint, 4, 1, mid - zoom - 50, 36);
        }

        private void SetLayers(bool visible)
        {
            showLayers = visible;
            layerPick.Visible = inspector.Visible = !visible;
            layerList.Visible = visible;
            propertiesBtn.Chosen = !visible; layersBtn.Chosen = visible;
            propertiesBtn.Invalidate(); layersBtn.Invalidate();
            RefreshInspector(true);
        }

        private void ShowTemplates()
        {
            TemplateAction action;
            using (var picker = new TemplateDialog(card.TemplateOn(side), side, card.HasCustomSize ? card.PrintSize : null))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                action = picker.Action;
                if (action == TemplateAction.Replace) SwitchTemplate(picker.TemplateIndex, picker.TargetSide, picker.BothSides);
            }
            if (action == TemplateAction.OpenSide) OpenSideTemplate();
            if (action == TemplateAction.SaveSide) SaveSideTemplate();
        }

        private Layer Active { get { return selected == null ? null : card.Find(selected); } }
        private bool Dirty { get { return card.Revision != saved; } }
        private void WithSync(Action action)
        { bool old = sync; sync = true; try { action(); } finally { sync = old; } }
        private void Checkpoint(object token)
        {
            if (token != null && editToken == token) return;
            undo.Add(card.Copy());
            if (undo.Count > 40) undo.RemoveAt(0);
            redo.Clear(); editToken = token; card.Revision = ++revision;
            UpdateHistory();
        }
        private void MoveHistory(bool forward)
        {
            var src = forward ? redo : undo;
            var dst = forward ? undo : redo;
            if (src.Count == 0) return;
            editToken = null;
            int width = card.Width, height = card.Height;
            dst.Add(card); card = src[src.Count - 1]; src.RemoveAt(src.Count - 1);
            if (width != card.Width || height != card.Height) view.Fit();
            if (Active == null) selected = null;
            RefreshAll(); ShowStatus(forward ? "已重做" : "已撤销");
        }
        private void UpdateHistory()
        { undoBtn.Enabled = undo.Count > 0; redoBtn.Enabled = redo.Count > 0; }
        private void MarkDirty()
        {
            if (card.Example && !card.IsDemo) card.Example = false;
            string name = string.IsNullOrEmpty(path) ? (materialEditor ? "加工设计" : card.IsDemo ? "示例卡片" : "未命名卡片") : Path.GetFileNameWithoutExtension(path);
            projectStatus.Text = Dirty ? "有未保存的修改" : (path == null ? "" : "已保存");
            title.Text = name + (Dirty ? "  *" : "");
            clearBtn.Text = card.IsDemo ? "清空示例资料" : group == 0 ? "清空台站资料" : "清空通联资料";
            view.InvalidateContent();
        }
        private void ShowStatus(string text) { tipsStatus.Text = text; }
        private void ZoomLabel() { zoomText.Text = (int)Math.Round(view.Zoom * 100) + "%"; }
        private void RefreshAll()
        {
            LayoutFields();
            WithSync(() =>
            {
                foreach (var item in fields.Values)
                {
                    string value = card.Value(item.Field.Key);
                    if (item.Box.Edit.Text != value) item.Box.Edit.Text = value;
                }
            });
            view.Card = card; view.Side = side; view.SelectedId = selected;
            tips.SetToolTip(templateBtn, "当前模板：" + Templates.Title(card.TemplateOn(side)));
            propertiesBtn.Chosen = !showLayers; layersBtn.Chosen = showLayers;
            frontBtn.Chosen = side == 0; backBtn.Chosen = side == 1;
            stationBtn.Chosen = group == 0; qsoBtn.Chosen = group == 1;
            frontBtn.Invalidate(); backBtn.Invalidate(); stationBtn.Invalidate(); qsoBtn.Invalidate();
            sideHint.Text = (side == 0 ? "正面" : "反面") + "  /  " + card.PrintSize;
            RefreshInspector(true); UpdateHistory(); ZoomLabel(); MarkDirty();
        }
        private void SelectLayer(string id)
        {
            if (selected == id) return;
            editToken = null;
            selected = id; view.SelectedId = selected;
            view.Invalidate(); RefreshInspector(false);
        }
        private void ShowSide(int newSide, bool matchForm)
        {
            if (side != newSide) { side = newSide; selected = null; editToken = null; SetDrawingTool(DrawingTool.Select); view.Fit(); }
            if (matchForm && group != newSide) { group = newSide; leftScroll.AutoScrollPosition = Point.Empty; }
            RefreshAll();
        }
        private void ChangeGroup(int newGroup)
        {
            if (group == newGroup) return;
            group = newGroup;
            leftScroll.AutoScrollPosition = Point.Empty;
            LayoutFields();
            stationBtn.Chosen = group == 0; qsoBtn.Chosen = group == 1;
            stationBtn.Invalidate(); qsoBtn.Invalidate();
            clearBtn.Text = card.IsDemo ? "清空示例资料" : group == 0 ? "清空台站资料" : "清空通联资料";
        }
        private void SwitchTemplate(int i, int targetSide, bool both)
        {
            string key = Templates.Keys[i];
            both = both || Templates.IsPortrait(key) != card.Portrait;
            if (card.TemplateOn(targetSide) == key && (!both || card.TemplateOn(1 - targetSide) == key)) return;
            string target = both ? "正反两面" : targetSide == 0 ? "正面" : "反面";
            if (card.Customized && MessageBox.Show(this, "将" + target + "换成「" + Templates.Names[i] + "」？\n所选卡面的图层和版式将被替换。按 Ctrl+Z 可撤销。", "更换模板", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            Checkpoint(null);
            var preset = Templates.Create(key);
            if (card.HasCustomSize) preset.Resize(card.Width, card.Height);
            if (targetSide == 0 || both)
            {
                var oldCover = card.Layers.Find(layer => layer.Side == 0 && layer.Cover);
                var newCover = preset.Layers.Find(layer => layer.Side == 0 && layer.Cover);
                if (oldCover != null && newCover != null && oldCover.Image != null && oldCover.Name != "示例照片")
                {
                    newCover.Image = oldCover.Image;
                    newCover.Name = oldCover.Name;
                }
            }
            if (both)
            {
                foreach (var field in Field.All) preset.Values[field.Key] = card.Value(field.Key);
                preset.Example = card.Example;
                preset.Revision = card.Revision;
                card = preset;
            }
            else
            {
                string backKey = card.TemplateOn(1);
                card.Layers.RemoveAll(layer => layer.Side == targetSide);
                card.Layers.AddRange(preset.Layers.FindAll(layer => layer.Side == targetSide));
                card.Background[targetSide] = preset.Background[targetSide];
                if (targetSide == 0) card.Template = key;
                else backKey = key;
                card.ReverseTemplate = backKey == card.Template ? null : backKey;
            }
            selected = null;
            if (side == targetSide) view.Fit();
            ShowSide(targetSide, true);
            ShowStatus(target + "已应用「" + Templates.Names[i] + "」");
        }
        private void FillUtc()
        {
            ApplyUtc(DateTime.UtcNow);
        }
        private void ApplyUtc(DateTime value)
        {
            Checkpoint(null);
            card.Values["date"] = value.ToString("yyyy-MM-dd", Files.Inv);
            card.Values["utc"] = value.ToString("HH:mm", Files.Inv);
            RefreshAll(); ShowStatus("已填入 " + card.Value("date") + " " + card.Value("utc") + " UTC");
        }
        private void ShowClockMenu()
        {
            if (clockMenu == null)
            {
                clockMenu = new StudioMenu();
                clockMenu.Items.Add("填入当前 UTC", null, (sender, e) => FillUtc());
                clockMenu.Items.Add("北京时间转 UTC…", null, (sender, e) =>
                {
                    using (var dialog = new BeijingDateDialog())
                        if (dialog.ShowDialog(this) == DialogResult.OK) ApplyUtc(dialog.Utc);
                });
            }
            clockMenu.Show(nowBtn, new Point(0, nowBtn.Height + Ui.P(4)));
        }
        private void ClearFields()
        {
            bool demo = card.IsDemo;
            if (!demo && MessageBox.Show(this, group == 0 ? "清空台站资料？" : "清空本卡的通联资料？台站资料将保留。", "清空资料", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            Checkpoint(null);
            if (demo) card.ClearDemoFields();
            else foreach (var f in Field.All)
                if (f.Station == (group == 0)) card.Values[f.Key] = "";
            RefreshAll(); ShowStatus(demo ? "已清空示例资料" : "已清空资料");
        }
    }
}
