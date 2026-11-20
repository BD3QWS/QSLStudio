using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace QslStudio
{
    internal enum TemplateAction { Replace, OpenSide, SaveSide }

    internal sealed class TemplateDialog : StudioDialog
    {
        private readonly TemplateTile[] options = new TemplateTile[Templates.BuiltInCount];
        private readonly Label detail, dimensions;
        private readonly ButtonX front, back, landscape, portrait;
        private readonly ButtonX apply, cancel;
        private readonly CheckBox applyBoth;
        private readonly Panel gallery;
        private readonly bool originalPortrait;
        private readonly bool pickProject;
        private readonly string customSize;
        private readonly int regularHeight;
        private bool bothPreference;
        private int landscapeIndex, portraitIndex = Templates.Index("portrait-photo");
        internal int TemplateIndex { get; private set; }
        internal int TargetSide { get; private set; }
        internal TemplateAction Action { get; private set; }
        internal bool BothSides { get { return applyBoth.Checked; } }
        private static int FitHeight()
        {
            int available = (int)(Screen.PrimaryScreen.WorkingArea.Height / Ui.Scale) - 48;
            return Math.Min(686, Math.Max(303, available));
        }
        internal TemplateDialog(string current, int side, string customSize = null, bool pickProject = false) :
            base(pickProject ? "选择卡片模板" : "更换模板", 608, FitHeight())
        {
            this.pickProject = pickProject;
            this.customSize = customSize;
            regularHeight = (int)Math.Round(ClientSize.Height / Ui.Scale);
            TemplateIndex = Templates.Index(current);
            if (Templates.IsLegacy(current)) TemplateIndex = 0;
            if (current == "portrait-blank") TemplateIndex = portraitIndex;
            originalPortrait = Templates.IsPortrait(Templates.Keys[TemplateIndex]);
            if (originalPortrait) portraitIndex = TemplateIndex;
            else landscapeIndex = TemplateIndex;
            LabelAt(this, "模板", 20, 13, 61, 33, 17, true, Ui.Ink);
            var file = new GlassPanel(20);
            Controls.Add(file); Ui.Put(file, 87, 9, 263, 42);
            file.Visible = !pickProject;
            ButtonAt(file, "打开单面…", "open", 6, 4, 108, 30, () => Finish(TemplateAction.OpenSide)).Pill = true;
            ButtonAt(file, side == 0 ? "保存当前正面…" : "保存当前反面…", "save", 115, 4, 142, 30, () => Finish(TemplateAction.SaveSide)).Pill = true;
            var faces = new GlassPanel(20, true);
            Controls.Add(faces); Ui.Put(faces, 412, 9, 180, 42);
            front = ButtonAt(faces, "预览正面", "", 6, 4, 84, 31, () => Preview(0));
            back = ButtonAt(faces, "预览反面", "", 90, 4, 84, 31, () => Preview(1));
            front.Pill = back.Pill = true;
            var directions = new GlassPanel(18, true);
            Controls.Add(directions); Ui.Put(directions, 16, 53, 148, 38);
            landscape = ButtonAt(directions, "横版", "", 6, 4, 68, 27, () => ShowOrientation(false));
            portrait = ButtonAt(directions, "竖版", "", 74, 4, 68, 27, () => ShowOrientation(true));
            landscape.Pill = portrait.Pill = true;
            dimensions = LabelAt(this, "", 177, 53, 388, 33, 12, false, Ui.Quiet);
            gallery = new SurfacePanel();
            gallery.AutoScroll = true;
            gallery.AccessibleName = "版式列表";
            Controls.Add(gallery);
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var option = new TemplateTile(Templates.Names[i], Templates.Keys[i], side);
                option.Click += delegate { Choose(index); };
                option.DoubleClick += delegate { Choose(index); DialogResult = DialogResult.OK; };
                options[i] = option;
                gallery.Controls.Add(option);
            }
            detail = LabelAt(this, "", 20, 0, 560, 37, 12, false, Ui.Quiet);
            applyBoth = new SurfaceCheckBox { Text = "同时换另一面", Font = Ui.F(12, false) };
            Controls.Add(applyBoth); applyBoth.Visible = !pickProject;
            cancel = ButtonAt(this, "取消", "", 394, 0, 81, 33, () => DialogResult = DialogResult.Cancel); cancel.Border = true;
            apply = ButtonAt(this, "", "", 483, 0, 105, 33, () => DialogResult = DialogResult.OK); apply.Primary = true;
            applyBoth.CheckedChanged += delegate
            {
                if (applyBoth.Enabled) bothPreference = applyBoth.Checked;
                UpdateApplyCaption();
            };
            Preview(side);
            ShowOrientation(originalPortrait);
            Shown += delegate { gallery.ScrollControlIntoView(options[TemplateIndex]); };
            CancelButton = cancel; AcceptButton = apply;
        }
        private void Finish(TemplateAction action)
        {
            Action = action;
            DialogResult = DialogResult.OK;
        }
        private void Preview(int side)
        {
            TargetSide = side;
            front.Chosen = side == 0;
            back.Chosen = side == 1;
            front.Invalidate(); back.Invalidate();
            foreach (var option in options) option.ShowSide(side);
            UpdateApplyCaption();
        }
        private void UpdateApplyCaption()
        {
            apply.Text = pickProject ? "使用模板" : applyBoth.Checked ? "替换两面" : TargetSide == 0 ? "替换正面" : "替换反面";
            apply.AccessibleName = apply.Text;
        }
        private void Choose(int index)
        {
            TemplateIndex = index;
            bool isPortrait = Templates.IsPortrait(Templates.Keys[index]);
            if (isPortrait) portraitIndex = index;
            else landscapeIndex = index;
            bool change = isPortrait != originalPortrait;
            detail.Text = Templates.Notes[index] + (change && !pickProject ? "\n此版式将应用到两面，保留已填写的资料。" : "");
            applyBoth.Enabled = false;
            applyBoth.Checked = change || bothPreference;
            applyBoth.Enabled = !change;
            UpdateApplyCaption();
            for (int i = 0; i < options.Length; i++) { options[i].Chosen = i == index; options[i].Invalidate(); }
        }

        private void ShowOrientation(bool showPortrait)
        {
            landscape.Chosen = !showPortrait; portrait.Chosen = showPortrait;
            landscape.Invalidate(); portrait.Invalidate();
            dimensions.Text = customSize == null ? Project.SizeLabel(showPortrait) : "按当前成品尺寸套用 · " + customSize;
            int dialogHeight = showPortrait ? Math.Min(regularHeight, 487) : regularHeight;
            SuspendLayout();
            ClientSize = new Size(ClientSize.Width, Ui.P(dialogHeight));
            if (Visible)
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                if (Bottom > area.Bottom) Top = Math.Max(area.Top, area.Bottom - Height);
            }
            Ui.Put(gallery, 13, 95, 582, dialogHeight - 205);
            Ui.Put(detail, 20, dialogHeight - 99, 560, 37);
            Ui.Put(applyBoth, 20, dialogHeight - 49, 200, 27);
            Ui.Put(cancel, 394, dialogHeight - 52, 81, 33);
            Ui.Put(apply, 483, dialogHeight - 52, 105, 33);
            gallery.AutoScrollPosition = Point.Empty;
            gallery.SuspendLayout();
            int count = 0, height = Math.Min(showPortrait ? 257 : 125, dialogHeight - 213);
            for (int i = 0; i < options.Length; i++)
            {
                bool show = Templates.IsPortrait(Templates.Keys[i]) == showPortrait;
                var option = options[i];
                if (show)
                {
                    Ui.Put(option, 3 + count % 3 * 187, 3 + count / 3 * (height + 8), 178, height);
                    count++;
                }
                option.Visible = show;
            }
            gallery.ResumeLayout();
            ResumeLayout();
            Choose(showPortrait ? portraitIndex : landscapeIndex);
            gallery.ScrollControlIntoView(options[TemplateIndex]);
        }
    }

    internal class StudioDialog : GlassForm
    {
        protected StudioDialog(string title, int width, int height)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; ShowInTaskbar = false; ShowIcon = false;
            MinimizeBox = MaximizeBox = false; AutoScaleMode = AutoScaleMode.None;
            Font = Ui.F(13, false);
            ClientSize = new Size(Ui.P(width), Ui.P(height));
        }
        protected Label LabelAt(Control parent, string text, int x, int y, int w, int h, int size, bool bold, Color color)
        {
            var l = Ui.Label(text, size, bold, color); parent.Controls.Add(l); Ui.Put(l, x, y, w, h); return l;
        }
        protected ButtonX ButtonAt(Control parent, string text, string icon, int x, int y, int w, int h, Action action)
        {
            var b = new ButtonX(text, icon); parent.Controls.Add(b); Ui.Put(b, x, y, w, h);
            b.Click += delegate { action(); }; return b;
        }
        protected void LinkAt(Control parent, string text, string address, int x, int y, int w)
        {
            var l = new SurfaceLink { Text = text, Font = Ui.F(12, true), LinkColor = Ui.Accent, ActiveLinkColor = Ui.Ink,
                TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, LinkBehavior = LinkBehavior.HoverUnderline };
            Ui.Put(l, x, y, w, 25); parent.Controls.Add(l);
            l.LinkClicked += delegate
            {
                try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }
                catch { MessageBox.Show(this, address, "无法打开浏览器", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
        }
    }

    internal sealed class BeijingDateDialog : StudioDialog
    {
        private readonly DateTimePicker date;
        internal DateTime Utc { get { return date.Value.AddHours(-8); } }

        internal BeijingDateDialog() : base("北京时间转 UTC", 396, 191)
        {
            LabelAt(this, "通联的北京时间（UTC+8）", 20, 13, 356, 28, 13, true, Ui.Ink);
            date = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd   HH:mm", ShowUpDown = true,
                Font = Ui.F(14, false), Value = DateTime.UtcNow.AddHours(8), AccessibleName = "通联的北京时间" };
            Controls.Add(date); Ui.Put(date, 20, 50, 356, 34);
            var preview = LabelAt(this, "", 20, 89, 356, 28, 12, false, Ui.Quiet);
            date.ValueChanged += (sender, e) => preview.Text = Utc.ToString("yyyy-MM-dd   HH:mm", Files.Inv) + " UTC";
            preview.Text = Utc.ToString("yyyy-MM-dd   HH:mm", Files.Inv) + " UTC";
            var close = ButtonAt(this, "取消", "", 166, 137, 86, 33, () => DialogResult = DialogResult.Cancel);
            var apply = ButtonAt(this, "填入日期与时间", "", 260, 137, 116, 33, () => DialogResult = DialogResult.OK);
            apply.Primary = true; CancelButton = close; AcceptButton = apply;
        }
    }

    internal sealed class ExportDialog : StudioDialog
    {
        private readonly ComboBox purpose, picker;
        private readonly RadioButton current, both;
        private readonly CheckBox bleed;
        private readonly Label formatLabel, hint, overprintHint;
        private readonly ButtonX next;
        internal bool MaterialRequested { get; private set; }
        internal bool OverprintRequested { get { return purpose.SelectedIndex == 2; } }
        internal int Format { get { return picker.SelectedIndex; } }
        internal bool Both { get { return both.Checked; } }
        internal bool Bleed { get { return !OverprintRequested && Format == 0 && bleed.Checked; } }
        internal bool Blank { get { return purpose.SelectedIndex == 0; } }

        internal ExportDialog(int side, Project card) : base("导出卡片", 468, 326)
        {
            LabelAt(this, "导出卡片", 22, 11, 423, 36, 22, true, Ui.Ink);
            LabelAt(this, card.PrintSize + "   /   300 DPI", 23, 48, 420, 23, 12, false, Ui.Quiet);
            LabelAt(this, "用途", 23, 81, 72, 29, 12, true, Ui.Ink);
            purpose = Picker("导出用途", 80, "印刷底版（通联栏留空）", "完整卡片（含通联资料）", "套印通联资料");
            formatLabel = LabelAt(this, "格式", 23, 124, 72, 29, 12, true, Ui.Ink);
            picker = Picker("文件格式", 123, "PDF（按成品尺寸）", "PNG", "JPG");

            both = new SurfaceRadioButton { Text = "正反两面", Font = Ui.F(13, true), Checked = true };
            current = new SurfaceRadioButton { Text = side == 0 ? "仅正面" : "仅反面", Font = Ui.F(13, false) };
            Controls.Add(both);
            Controls.Add(current);
            Ui.Put(both, 22, 164, 145, 28);
            Ui.Put(current, 177, 164, 215, 28);
            hint = LabelAt(this, "", 22, 194, 424, 24, 11, false, Ui.Quiet);
            bleed = new SurfaceCheckBox { Text = "预留 2 mm 出血（仅 PDF）", Font = Ui.F(12, false) };
            Controls.Add(bleed);
            Ui.Put(bleed, 22, 221, 423, 27);

            overprintHint = LabelAt(this, "将已填写的通联资料导出为同尺寸单面 PDF。可在预览中选择卡面，并校准套印位置。",
                23, 130, 414, 69, 13, false, Ui.Ink);
            if (card.IsDemo) LabelAt(this, "旧版样卡。发卡前请核对通联资料。", 22, 250, 423, 19, 11, true, Color.FromArgb(130, 72, 34));

            const int y = 271;
            ButtonAt(this, "PCB / 特殊材质…", "pcb", 22, y, 154, 36,
                () => { MaterialRequested = true; DialogResult = DialogResult.OK; }).Border = true;
            var cancel = ButtonAt(this, "取消", "", 224, y, 94, 36, () => DialogResult = DialogResult.Cancel);
            cancel.Border = true;
            next = ButtonAt(this, "保存文件…", "export", 327, y, 118, 36, () => DialogResult = DialogResult.OK);
            next.Primary = true;
            AcceptButton = next;
            CancelButton = cancel;
            both.CheckedChanged += delegate { UpdateOptions(); };
            purpose.SelectedIndexChanged += delegate { UpdateOptions(); };
            picker.SelectedIndexChanged += delegate { UpdateOptions(); };
            UpdateOptions();
        }

        private ComboBox Picker(string name, int y, params string[] entries)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                Font = Ui.F(13, false), AccessibleName = name };
            box.Items.AddRange(entries);
            Controls.Add(box);
            Ui.Put(box, 96, y, 349, 29);
            box.SelectedIndex = 0;
            return box;
        }

        private void UpdateOptions()
        {
            bool normal = !OverprintRequested;
            formatLabel.Visible = picker.Visible = both.Visible = current.Visible = hint.Visible = bleed.Visible = normal;
            bleed.Enabled = normal && Format == 0;
            overprintHint.Visible = !normal;
            next.Text = normal ? "保存文件…" : "套印预览…";
            hint.Text = !Both ? "导出当前卡面。" : Format == 0 ? "PDF 第 1 页为正面，第 2 页为反面。" : "分别保存正面和反面图片。";
        }
    }

    internal sealed class GettingStartedDialog : StudioDialog
    {
        private readonly CheckBox dismiss;
        internal bool Dismissed { get { return dismiss.Checked; } }

        internal GettingStartedDialog() : base("制作引导", 430, 171)
        {
            LabelAt(this, "用户手册提供新建卡片、送印和套印的操作说明。", 21, 15, 387, 45, 14, false, Ui.Ink);
            dismiss = new SurfaceCheckBox { Text = "下次不再提示", Font = Ui.F(12, false) };
            Controls.Add(dismiss);
            Ui.Put(dismiss, 21, 69, 386, 25);
            var later = ButtonAt(this, "稍后", "", 185, 116, 78, 34, () => DialogResult = DialogResult.Cancel);
            later.Border = true;
            var open = ButtonAt(this, "打开用户手册", "help", 272, 116, 136, 34, () => DialogResult = DialogResult.OK);
            open.Primary = true;
            AcceptButton = open;
            CancelButton = later;
            ActiveControl = open;
        }
    }

    internal sealed class AssetsDialog : StudioDialog
    {
        internal Art Logo;
        internal string AssetName;
        internal bool ImportLocal;
        private readonly Panel list;
        private readonly int rowWidth;
        private readonly ComboBox group;
        private readonly Label status;
        private readonly string cacheRoot, preparedRoot, stationCall;
        private readonly List<string> groups = new List<string> { "" };
        private readonly List<LogoEntry> rows = new List<LogoEntry>();
        private LogoEntry pendingInsert;
        private bool pendingCallsign;

        private sealed class LogoEntry
        {
            internal LogoSource Source;
            internal Panel Row;
            internal PictureBox Preview;
            internal Label PreviewState;
            internal ButtonX Insert;
            internal Art Art;
            internal bool Loading;
        }

        private static int FitHeight()
        {
            int available = (int)(Screen.PrimaryScreen.WorkingArea.Height / Ui.Scale) - 44;
            return Math.Min(568, Math.Max(295, available));
        }

        private static int FitWidth()
        {
            int available = (int)(Screen.PrimaryScreen.WorkingArea.Width / Ui.Scale) - 24;
            return Math.Min(636, Math.Max(550, available));
        }

        internal AssetsDialog(string cacheRoot, string preparedRoot = null, string stationCall = "") : base("协会和活动标志", FitWidth(), FitHeight())
        {
            this.cacheRoot = cacheRoot;
            this.preparedRoot = preparedRoot;
            this.stationCall = stationCall;
            int width = (int)Math.Round(ClientSize.Width / Ui.Scale);
            LabelAt(this, "协会和活动标志", 20, 10, width - 304, 29, 14, true, Ui.Ink);
            group = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                Font = Ui.F(12, false), AccessibleName = "筛选协会", DropDownWidth = Ui.P(270) };
            Controls.Add(group); Ui.Put(group, width - 270, 12, 245, 28);
            group.Items.Add("全部协会和活动");
            int height = (int)Math.Round(ClientSize.Height / Ui.Scale);
            var frame = new GlassPanel(16);
            Controls.Add(frame); Ui.Put(frame, 12, 45, width - 24, height - 100);
            list = new SurfacePanel { AutoScroll = true };
            frame.Controls.Add(list); Ui.Put(list, 8, 7, width - 40, height - 115);
            rowWidth = Ui.ScrollContentWidth(list, 6);
            foreach (string family in new[] { "CRAC", "CRSA", "ARRL", "JARL", "RSGB", "CDXA", "IOTA", "SOTA", "ARRAM", "URV-ABRA", "MARC" })
            {
                var variants = Array.FindAll(LogoLibrary.Catalog, source => source.Group == family);
                if (variants.Length == 0) continue;
                groups.Add(family);
                group.Items.Add(family + "  " + variants[0].Name);
                foreach (var source in variants) LogoRow(source);
            }
            status = LabelAt(this, "", 171, height - 44, width - 294, 32, 11, false, Ui.Quiet);
            var local = ButtonAt(this, "导入本地标志", "image", 20, height - 44, 144, 32,
                () => { ImportLocal = true; DialogResult = DialogResult.OK; });
            local.Border = true;
            var cancel = ButtonAt(this, "关闭", "", width - 115, height - 44, 89, 32, () => DialogResult = DialogResult.Cancel);
            cancel.Border = true;
            CancelButton = cancel;
            group.SelectedIndexChanged += delegate { Filter(); };
            group.SelectedIndex = 0;
            Shown += delegate
            {
                foreach (var entry in rows)
                    if (entry.Art == null && entry.Source.Preload) RequestLogo(entry, false);
            };
        }

        private void Filter()
        {
            string family = groups[group.SelectedIndex];
            list.AutoScrollPosition = Point.Empty;
            list.SuspendLayout();
            int y = 0;
            foreach (var entry in rows)
            {
                bool shown = family.Length == 0 || entry.Source.Group == family;
                entry.Row.Visible = shown;
                if (!shown) continue;
                Ui.Put(entry.Row, 0, y, rowWidth, 74);
                y += 74;
            }
            list.AutoScrollMinSize = new Size(0, Ui.P(y + 4));
            list.ResumeLayout();
        }

        private void LogoRow(LogoSource source)
        {
            var entry = new LogoEntry { Source = source, Row = new SurfacePanel() };
            rows.Add(entry);
            list.Controls.Add(entry.Row);
            var line = Ui.Panel(Ui.Line); entry.Row.Controls.Add(line); Ui.Put(line, 8, 73, rowWidth - 16, 1);
            entry.Preview = new PictureBox { BackColor = Ui.White, SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand, AccessibleName = source.Title + " 标志预览" };
            entry.Row.Controls.Add(entry.Preview); Ui.Put(entry.Preview, 8, 5, 66, 64);
            entry.Preview.Click += delegate { RequestLogo(entry, true); };
            entry.PreviewState = new Label { Text = "待下载", Font = Ui.F(11, false), ForeColor = Ui.Quiet,
                BackColor = Ui.Surface, TextAlign = ContentAlignment.MiddleCenter };
            entry.Row.Controls.Add(entry.PreviewState); Ui.Put(entry.PreviewState, 8, 5, 66, 64);
            entry.PreviewState.BringToFront();
            entry.PreviewState.Click += delegate { RequestLogo(entry, true); };
            bool round = source.Key == LogoLibrary.CracRound;
            int insertX = rowWidth - 104, badgeX = insertX - 100;
            int linkX = (round ? badgeX : insertX) - 51;
            int textWidth = linkX - 94;
            LabelAt(entry.Row, source.Title, 88, 9, textWidth, 24, 13, true, Ui.Ink);
            LabelAt(entry.Row, source.Caption, 88, 34, textWidth, 35, 11, false, Ui.Quiet);
            LinkAt(entry.Row, "来源", source.Policy, linkX, 24, 43);
            if (round)
                ButtonAt(entry.Row, "加呼号…", "", badgeX, 20, 90, 33, () => RequestLogo(entry, true, true)).Border = true;
            entry.Insert = ButtonAt(entry.Row, "下载并插入", "", insertX, 20, 94, 33, () => RequestLogo(entry, true));
            entry.Insert.Border = true;
            entry.Insert.AccessibleName = "下载并插入 " + source.Title;

            Art art = LogoLibrary.GetLocal(cacheRoot, source);
            if (art == null && source.Original != null && preparedRoot != null)
                art = LogoLibrary.GetLocal(preparedRoot, source);
            if (art != null) SetPreview(entry, art);
            else if (source.Original == null)
            {
                entry.PreviewState.Text = "文件缺失";
                SetInsertLabel(entry, "不可用");
                entry.Insert.Enabled = false;
            }
        }

        private static void SetPreview(LogoEntry entry, Art art)
        {
            entry.Art = art;
            entry.Preview.Image = art.Bitmap;
            entry.PreviewState.Hide();
            SetInsertLabel(entry, "插入");
        }

        private static void SetInsertLabel(LogoEntry entry, string text)
        {
            entry.Insert.Text = text;
            entry.Insert.AccessibleName = text + " " + entry.Source.Title;
        }

        private void UseLogo(LogoEntry entry, bool callsign)
        {
            if (callsign)
            {
                using (var badge = new CallsignBadgeDialog(entry.Art, stationCall))
                {
                    if (badge.ShowDialog(this) != DialogResult.OK) return;
                    Logo = badge.Badge;
                    AssetName = "CRAC 圆章 · " + badge.Callsign;
                }
            }
            else
            {
                Logo = entry.Art;
                AssetName = entry.Source.Title + " 标志";
            }
            DialogResult = DialogResult.OK;
        }

        private void RequestLogo(LogoEntry entry, bool insert, bool callsign = false)
        {
            if (entry.Art != null) { if (insert) UseLogo(entry, callsign); return; }
            if (entry.Source.Original == null) return;
            if (insert)
            {
                pendingInsert = entry;
                pendingCallsign = callsign;
                status.Text = "正在下载 " + entry.Source.Group + " 标志…";
            }
            if (entry.Loading) return;
            entry.Loading = true;
            entry.PreviewState.Text = "下载中";
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = LogoLibrary.FetchAndStore(cacheRoot, entry.Source); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                worker.Dispose();
                if (IsDisposed || Disposing || !Visible || Logo != null)
                {
                    if (e.Error == null && e.Result is Art) ((Art)e.Result).Dispose();
                    return;
                }
                entry.Loading = false;
                bool insertNow = pendingInsert == entry;
                if (insertNow) pendingInsert = null;
                if (e.Error != null)
                {
                    entry.PreviewState.Text = "下载失败";
                    SetInsertLabel(entry, "重试下载");
                    if (insertNow) status.Text = "下载失败。请重试或导入本地标志。";
                    return;
                }
                SetPreview(entry, (Art)e.Result);
                if (insertNow) UseLogo(entry, pendingCallsign);
            };
            worker.RunWorkerAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var entry in rows)
                    if (entry.Art != null && !ReferenceEquals(Logo, entry.Art))
                    {
                        entry.Preview.Image = null;
                        entry.Art.Dispose();
                        entry.Art = null;
                    }
            base.Dispose(disposing);
        }
    }

    internal sealed class CallsignBadgeDialog : StudioDialog
    {
        private readonly Art circle;
        private readonly InputBox call;
        private readonly PictureBox preview;
        private readonly ButtonX insert;
        private readonly Label hint;
        internal Art Badge { get; private set; }
        internal string Callsign { get { return CallsignBadge.Normalize(call.Edit.Text); } }

        private static int FitHeight()
        {
            int available = (int)(Screen.PrimaryScreen.WorkingArea.Height / Ui.Scale) - 38;
            return Math.Min(417, Math.Max(292, available));
        }

        internal CallsignBadgeDialog(Art circle, string stationCall) : base("CRAC 圆章加呼号", 382, FitHeight())
        {
            this.circle = circle;
            int h = (int)Math.Round(ClientSize.Height / Ui.Scale);
            int previewSize = h - 189;
            LabelAt(this, "CRAC 圆章加呼号", 21, 12, 338, 36, 19, true, Ui.Ink);
            preview = new PictureBox { BackColor = Ui.White, SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "圆章和呼号预览" };
            Controls.Add(preview); Ui.Put(preview, (382 - previewSize) / 2, 57, previewSize, previewSize);
            LabelAt(this, "本台呼号", 22, h - 122, 98, 26, 12, true, Ui.Ink);
            call = new InputBox("本台呼号", false);
            call.Edit.MaxLength = 16;
            call.Edit.CharacterCasing = CharacterCasing.Upper;
            call.Edit.Text = CallsignBadge.Normalize(stationCall);
            Controls.Add(call); Ui.Put(call, 119, h - 126, 240, 34);
            hint = LabelAt(this, "", 22, h - 86, 337, 22, 11, false, Ui.Quiet);
            var cancel = ButtonAt(this, "取消", "", 151, h - 50, 91, 34, () => DialogResult = DialogResult.Cancel);
            cancel.Border = true; CancelButton = cancel;
            insert = ButtonAt(this, "插入到卡片", "add", 248, h - 50, 111, 34, delegate
            {
                Badge = CallsignBadge.Encode((Bitmap)preview.Image);
                DialogResult = DialogResult.OK;
            });
            insert.Primary = true;
            AcceptButton = insert;
            call.Edit.TextChanged += delegate { UpdatePreview(); };
            ActiveControl = call.Edit;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            string callsign = Callsign;
            var before = preview.Image;
            preview.Image = CallsignBadge.Draw(circle, callsign);
            if (before != null) before.Dispose();
            insert.Enabled = CallsignBadge.Valid(callsign);
            hint.Text = insert.Enabled ? "个人呼号章" : "输入字母、数字或 /、-。";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && preview != null && preview.Image != null)
            {
                preview.Image.Dispose();
                preview.Image = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class HelpDialog : StudioDialog
    {
        internal const string ResourceName = "UserManual.txt";

        internal HelpDialog() : base("用户手册", FitWidth(), FitHeight())
        {
            int width = (int)Math.Round(ClientSize.Width / Ui.Scale);
            int height = (int)Math.Round(ClientSize.Height / Ui.Scale);
            var outline = new GlassPanel(14);
            var paper = new GlassPanel(14);
            Controls.Add(outline);
            Controls.Add(paper);
            Ui.Put(outline, 16, 16, 135, height - 77);
            Ui.Put(paper, 162, 16, width - 178, height - 77);

            var topics = new ListBox
            {
                Name = "manualTopics",
                AccessibleName = "手册目录",
                BackColor = Ui.Surface,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                Font = Ui.F(13, false),
                IntegralHeight = false,
                ItemHeight = Ui.P(30),
                TabIndex = 0
            };
            outline.Controls.Add(topics);
            Ui.Put(topics, 7, 8, 121, height - 93);
            topics.DrawItem += DrawTopic;

            var heading = LabelAt(paper, "", 18, 13, width - 218, 37, 21, true, Ui.Ink);
            var body = new RichTextBox
            {
                Name = "manualText",
                AccessibleName = "手册正文",
                BackColor = Ui.Surface,
                ForeColor = Ui.Ink,
                BorderStyle = BorderStyle.None,
                DetectUrls = false,
                Font = Ui.F(14, false),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                TabIndex = 0,
                WordWrap = true
            };
            paper.Controls.Add(body);
            Ui.Put(body, 18, 58, width - 216, height - 151);

            var chapters = new List<string>();
            using (var stream = typeof(HelpDialog).Assembly.GetManifestResourceStream(ResourceName))
            using (var reader = new StreamReader(stream))
            {
                string text = "\n" + reader.ReadToEnd().Replace("\r\n", "\n");
                foreach (string chapter in text.Split(new[] { "\n## " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int titleEnd = chapter.IndexOf('\n');
                    topics.Items.Add(chapter.Substring(0, titleEnd));
                    chapters.Add(chapter.Substring(titleEnd + 1).Trim());
                }
            }
            topics.SelectedIndexChanged += delegate
            {
                if (topics.SelectedIndex < 0) return;
                heading.Text = topics.SelectedItem.ToString();
                body.Text = chapters[topics.SelectedIndex];
                body.Select(0, 0);
                body.ScrollToCaret();
            };
            topics.SelectedIndex = 0;
            var close = ButtonAt(this, "关闭", "", width - 110, height - 47, 94, 33, Close);
            close.TabIndex = 2;
            close.Border = true;
            CancelButton = close;
            ActiveControl = topics;
        }

        private static int FitWidth()
        {
            int available = (int)(Screen.PrimaryScreen.WorkingArea.Width / Ui.Scale) - 32;
            return Math.Min(700, Math.Max(436, available));
        }

        private static int FitHeight()
        {
            int available = (int)(Screen.PrimaryScreen.WorkingArea.Height / Ui.Scale) - 42;
            return Math.Min(500, Math.Max(302, available));
        }

        private static void DrawTopic(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var topics = (ListBox)sender;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var bounds = e.Bounds;
            if (selected)
                Ui.Block(e.Graphics, RectangleF.Inflate(bounds, -1, -1), Color.FromArgb(219, 233, 252), Color.Transparent, Ui.P(7));
            else e.Graphics.FillRectangle(SystemBrushes.Window, bounds);
            bounds.Inflate(-Ui.P(8), 0);
            TextRenderer.DrawText(e.Graphics, topics.Items[e.Index].ToString(), e.Font, bounds, Ui.Ink,
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }
    }
}
