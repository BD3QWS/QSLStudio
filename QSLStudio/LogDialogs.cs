using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace QslStudio
{
    internal sealed class LogDialog : StudioDialog
    {
        private readonly Project project;
        private readonly ListView rows;
        private readonly Label countLabel;
        private readonly ButtonX batch;
        private int checkedCount;
        private bool changingChecks;
        internal Qso Selected { get; private set; }
        internal List<Qso> Batch { get; private set; }
        internal string BatchFile { get; private set; }

        private static int FitWidth() { return Math.Min(762, Math.Max(550, (int)(Screen.PrimaryScreen.WorkingArea.Width / Ui.Scale) - 42)); }
        private static int FitHeight() { return Math.Min(538, Math.Max(322, (int)(Screen.PrimaryScreen.WorkingArea.Height / Ui.Scale) - 48)); }

        internal LogDialog(Project project, AdifLog log) : base("导入 ADIF 通联日志", FitWidth(), FitHeight())
        {
            this.project = project;
            int w = (int)Math.Round(ClientSize.Width / Ui.Scale), h = (int)Math.Round(ClientSize.Height / Ui.Scale);
            LabelAt(this, "通联记录", 18, 11, w - 36, 28, 16, true, Ui.Ink);
            string own = project.StationCall;
            string status = log.Records.Count + " 条记录" + (log.Skipped == 0 ? "" : " · 跳过 " + log.Skipped + " 条无对方呼号的记录");
            status += own.Length == 0 ? " · 当前未填写本台呼号" : " · 本台 " + own;
            LabelAt(this, status, 18, 42, w - 36, 24, 11, false, Ui.Quiet);
            rows = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, MultiSelect = false,
                HideSelection = false, HeaderStyle = ColumnHeaderStyle.Nonclickable, Font = Ui.F(12, false), AccessibleName = "ADIF 通联记录" };
            Controls.Add(rows); Ui.Put(rows, 18, 76, w - 36, h - 177);
            int available = w - 56;
            rows.Columns.Add("对方呼号", Ui.P(available * .18f));
            rows.Columns.Add("日期（UTC）", Ui.P(available * .17f));
            rows.Columns.Add("时间", Ui.P(available * .11f));
            rows.Columns.Add("MHz / 频段", Ui.P(available * .14f));
            rows.Columns.Add("模式", Ui.P(available * .12f));
            rows.Columns.Add("RST", Ui.P(available * .09f));
            rows.Columns.Add("本台呼号", Ui.P(available * .18f));
            var items = new ListViewItem[log.Records.Count];
            for (int i = 0; i < items.Length; i++)
            {
                var record = log.Records[i];
                items[i] = new ListViewItem(new[] { record.Other, record.Date, record.Utc, record.Frequency.Length > 0 ? record.Frequency : record.Band,
                    record.Mode, record.Rst, record.Station }) { Tag = record };
            }
            rows.Items.AddRange(items);
            rows.Items[0].Selected = true;
            ButtonAt(this, "勾选全部", "", 18, h - 91, 96, 27, () => SetChecks(true));
            ButtonAt(this, "取消勾选", "", 121, h - 91, 96, 27, () => SetChecks(false));
            countLabel = LabelAt(this, "", 221, h - 91, w - 239, 27, 11, false, Ui.Quiet);
            var close = ButtonAt(this, "取消", "", 18, h - 49, 87, 33, () => DialogResult = DialogResult.Cancel);
            var single = ButtonAt(this, "载入所选记录", "", w - 308, h - 49, 131, 33, FillSelected); single.Border = true;
            batch = ButtonAt(this, "批量导出 PDF…", "", w - 167, h - 49, 149, 33, SelectBatch); batch.Primary = true;
            rows.ItemCheck += (sender, e) =>
            {
                if (changingChecks) return;
                if (e.CurrentValue == CheckState.Checked) checkedCount--;
                if (e.NewValue == CheckState.Checked) checkedCount++;
                UpdateChecks();
            };
            rows.SelectedIndexChanged += (sender, e) => single.Enabled = rows.SelectedItems.Count > 0;
            rows.DoubleClick += (sender, e) => FillSelected();
            UpdateChecks();
            AcceptButton = single; CancelButton = close;
        }

        private void UpdateChecks()
        {
            batch.Enabled = checkedCount > 0;
            countLabel.Text = checkedCount == 0 ? "选中一条记录可载入卡片。勾选多条可批量导出。" : "已勾选 " + checkedCount + " 条，每条生成两页";
        }
        private void SetChecks(bool value)
        {
            changingChecks = true;
            rows.BeginUpdate();
            try { foreach (ListViewItem item in rows.Items) item.Checked = value; }
            finally { rows.EndUpdate(); changingChecks = false; }
            checkedCount = value ? rows.Items.Count : 0;
            UpdateChecks();
        }
        private void FillSelected()
        {
            if (rows.SelectedItems.Count != 1) return;
            var record = (Qso)rows.SelectedItems[0].Tag;
            try { record.CheckStation(project.StationCall); }
            catch (System.IO.InvalidDataException e) { ShowProblem(e); return; }
            Selected = record;
            DialogResult = DialogResult.OK;
        }
        private void SelectBatch()
        {
            var records = new List<Qso>();
            foreach (ListViewItem item in rows.CheckedItems) records.Add((Qso)item.Tag);
            if (records.Count == 0) return;
            try { Qso.CheckBatch(project, records); }
            catch (System.IO.InvalidDataException e) { ShowProblem(e); return; }
            using (var d = new SaveFileDialog { Title = "保存双面卡片 PDF", Filter = "PDF 印刷文件 (*.pdf)|*.pdf",
                DefaultExt = "pdf", FileName = "QSL-批量.pdf", RestoreDirectory = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                BatchFile = d.FileName;
            }
            Batch = records;
            DialogResult = DialogResult.OK;
        }

        private void ShowProblem(Exception e)
        {
            MessageBox.Show(this, e.Message, "核对日志与台站资料", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    internal sealed class BatchDialog : StudioDialog
    {
        private readonly BackgroundWorker worker = new BackgroundWorker { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
        private readonly ButtonX cancel;
        private readonly Label status;
        private bool finished;
        internal Exception Failure { get; private set; }

        internal BatchDialog(Project project, List<Qso> rows, string file) : base("批量导出双面卡片", 437, 153)
        {
            int pages = rows.Count * 2;
            status = LabelAt(this, "正在导出 " + rows.Count + " 张双面卡片…", 20, 13, 398, 30, 13, true, Ui.Ink);
            var bar = new ProgressBar { Maximum = pages, AccessibleName = "导出进度" };
            Controls.Add(bar); Ui.Put(bar, 20, 53, 397, 13);
            cancel = ButtonAt(this, "取消导出", "", 306, 97, 110, 32, CancelExport); cancel.Border = true;
            CancelButton = cancel;
            worker.DoWork += (sender, e) =>
            {
                try
                {
                    CardOutput.SaveBatch(project, rows, file, completed =>
                    {
                        if (worker.CancellationPending) throw new OperationCanceledException();
                        worker.ReportProgress(completed);
                    });
                }
                catch (OperationCanceledException) { e.Cancel = true; }
            };
            worker.ProgressChanged += (sender, e) =>
            {
                bar.Value = e.ProgressPercentage;
                if (!worker.CancellationPending) status.Text = "已导出 " + e.ProgressPercentage + " / " + pages + " 页";
            };
            worker.RunWorkerCompleted += (sender, e) =>
            {
                finished = true;
                Failure = e.Error;
                DialogResult = e.Cancelled || Failure != null ? DialogResult.Cancel : DialogResult.OK;
            };
            Shown += (sender, e) => worker.RunWorkerAsync();
        }
        private void CancelExport()
        {
            if (!worker.IsBusy || worker.CancellationPending) return;
            worker.CancelAsync(); cancel.Enabled = false;
            status.Text = "正在取消…";
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!finished && worker.IsBusy) { CancelExport(); e.Cancel = true; }
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) worker.Dispose();
            base.Dispose(disposing);
        }
    }
}
