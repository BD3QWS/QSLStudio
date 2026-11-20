using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace QslStudio
{
    internal sealed class StorageLocations
    {
        internal readonly string DataRoot, CacheRoot;
        internal StorageLocations(string data, string cache)
        {
            DataRoot = FullPath(data);
            CacheRoot = FullPath(cache);
        }

        internal string Stations { get { return Path.Combine(DataRoot, "Stations"); } }
        internal string Logos { get { return LogoLibrary.Folder(CacheRoot); } }

        private static string FullPath(string path)
        {
            string value = Environment.ExpandEnvironmentVariables(path.Trim());
            string root = Path.GetPathRoot(value);
            if (string.IsNullOrEmpty(root) || root.Length < 3)
                throw new ArgumentException("请输入完整的文件夹路径，或点「浏览」。");
            string full = Path.GetFullPath(value);
            return full.Length == Path.GetPathRoot(full).Length ? full : full.TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    internal sealed class UserStorage
    {
        private readonly string preferences, preparedRoot;
        internal readonly StorageLocations Defaults;

        internal UserStorage(string preferences, StorageLocations defaults, string preparedRoot)
        {
            this.preferences = preferences;
            this.preparedRoot = preparedRoot;
            Defaults = defaults;
        }

        internal static UserStorage ForUser(string profile = "QSLStudio")
        {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), profile);
            string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), profile, "locations.xml");
            return new UserStorage(settings, new StorageLocations(data, Path.Combine(data, "Cache")), AppDomain.CurrentDomain.BaseDirectory);
        }

        private string GuideMarker { get { return Path.Combine(Path.GetDirectoryName(preferences), "guide-dismissed"); } }
        internal bool GuideDismissed { get { return File.Exists(GuideMarker); } }

        internal void DismissGuide()
        {
            string marker = GuideMarker;
            Directory.CreateDirectory(Path.GetDirectoryName(marker));
            using (File.Open(marker, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) { }
        }

        internal StorageLocations Read()
        {
            try
            {
                using (var file = File.OpenRead(preferences))
                using (var reader = XmlReader.Create(file, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 32768
                }))
                {
                    var root = XElement.Load(reader);
                    if (root.Name != "locations") return Defaults;
                    return new StorageLocations(Files.Attr(root, "data", Defaults.DataRoot), Files.Attr(root, "cache", Defaults.CacheRoot));
                }
            }
            catch (FileNotFoundException) { return Defaults; }
            catch (DirectoryNotFoundException) { return Defaults; }
            catch (XmlException) { return Defaults; }
            catch (ArgumentException) { return Defaults; }
        }

        internal void ClearApplicationData()
        {
            // Read the selected locations before removing the settings that identify them.
            var current = Read();
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectApplicationFiles(Defaults, files);
            if (!Same(current.Stations, Defaults.Stations) || !Same(current.Logos, Defaults.Logos))
                CollectApplicationFiles(current, files);

            string[] settings = { GuideMarker, preferences };
            foreach (string file in files)
                RequirePhysicalPath(file);
            foreach (string file in settings)
                if (File.Exists(file)) RequirePhysicalPath(file);

            foreach (string file in files)
                DeleteApplicationFile(file);
            // Keep the location settings available if a locked profile or cache needs a retry.
            foreach (string file in settings)
                DeleteApplicationFile(file);
        }

        private static void CollectApplicationFiles(StorageLocations locations, HashSet<string> files)
        {
            RequirePhysicalPath(locations.Stations);
            RequirePhysicalPath(locations.Logos);
            foreach (var profile in Stations.Read(locations.Stations))
                files.Add(profile.Path);
            foreach (var source in LogoLibrary.Catalog)
            {
                if (source.Original == null) continue;
                string cache = Path.Combine(locations.Logos, LogoLibrary.FileName(source));
                if (File.Exists(cache)) files.Add(cache);
            }
        }

        private static void DeleteApplicationFile(string file)
        {
            if (!File.Exists(file)) return;
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            File.Delete(file);
        }

        private static void RequirePhysicalPath(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((Directory.Exists(current) || File.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("应用资料路径含有重定向文件夹。");
        }

        internal void Change(StorageLocations next)
        {
            var before = Read();
            if (!Same(before.Stations, next.Stations))
            {
                EnsureWritable(next.Stations);
                CopyStations(before.Stations, next.Stations);
            }
            if (!Same(before.Logos, next.Logos))
            {
                EnsureWritable(next.Logos);
                CopyLogos(before.Logos, next.Logos);
                CopyLogos(LogoLibrary.Folder(preparedRoot), next.Logos);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(preferences));
            Files.Atomic(preferences, stream =>
                new XElement("locations", new XAttribute("data", next.DataRoot), new XAttribute("cache", next.CacheRoot)).Save(stream));
        }

        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        private static void EnsureWritable(string directory)
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, ".qsl-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose))
                file.WriteByte(0);
        }

        private static void CopyStations(string source, string target)
        {
            foreach (var profile in Stations.Read(source))
            {
                string name = Path.GetFileName(profile.Path);
                string destination = Path.Combine(target, name);
                if (File.Exists(destination))
                {
                    string version = Fingerprint(profile.Path);
                    if (version == Fingerprint(destination)) continue;
                    // Keep divergent copies and use a stable name so retries do not create more duplicates.
                    name = Path.GetFileNameWithoutExtension(name) + "-copy-" + version + ".xml";
                    destination = Path.Combine(target, name);
                }
                CopyNew(profile.Path, destination);
            }
        }

        private static string Fingerprint(string path)
        {
            using (var digest = SHA256.Create())
            using (var file = File.OpenRead(path))
                return BitConverter.ToString(digest.ComputeHash(file), 0, 12).Replace("-", "");
        }

        private static void CopyLogos(string source, string target)
        {
            if (Same(source, target)) return;
            foreach (var logo in LogoLibrary.Catalog)
            {
                if (logo.Original == null) continue;
                string name = LogoLibrary.FileName(logo);
                string file = Path.Combine(source, name);
                if (File.Exists(file)) CopyNew(file, Path.Combine(target, name));
            }
        }

        private static void CopyNew(string source, string target)
        {
            if (File.Exists(target)) return;
            try
            {
                Files.Atomic(target, output =>
                {
                    using (var input = File.OpenRead(source)) input.CopyTo(output);
                }, false);
            }
            catch (IOException)
            {
                if (!File.Exists(target)) throw;
            }
        }
    }

    internal sealed class StorageDialog : StudioDialog
    {
        private readonly UserStorage storage;
        private readonly InputBox data, cache;
        private readonly ButtonX apply, cancel, reset;
        private readonly Label status;
        private readonly Control[] inputs;
        private bool saving;

        private static int DialogWidth()
        {
            return Math.Min(610, Math.Max(374, (int)(Screen.PrimaryScreen.WorkingArea.Width / Ui.Scale) - 34));
        }

        internal StorageDialog(UserStorage storage) : base("存储位置", DialogWidth(), 262)
        {
            this.storage = storage;
            var current = storage.Read();
            int width = (int)Math.Round(ClientSize.Width / Ui.Scale);
            const int left = 20;
            int fieldWidth = width - 124;
            LabelAt(this, "台站资料", left, 13, fieldWidth, 23, 13, true, Ui.Ink);
            data = PathField("台站资料目录", current.DataRoot, fieldWidth, 40);
            var dataBrowse = ButtonAt(this, "浏览…", "", width - 94, 40, 74, 33, () => Browse(data, "选择台站资料目录"));
            dataBrowse.Border = true;
            LabelAt(this, "缓存（下载的协会标志）", left, 90, fieldWidth, 23, 13, true, Ui.Ink);
            cache = PathField("缓存目录", current.CacheRoot, fieldWidth, 117);
            var cacheBrowse = ButtonAt(this, "浏览…", "", width - 94, 117, 74, 33, () => Browse(cache, "选择缓存目录"));
            cacheBrowse.Border = true;

            status = LabelAt(this, "更改位置时会复制已有资料和标志。", left, 161, width - 40, 42, 12, false, Ui.Quiet);
            reset = ButtonAt(this, "恢复默认", "", left, 210, 91, 33, Reset);
            reset.Border = true;
            cancel = ButtonAt(this, "取消", "", width - 195, 210, 78, 33, () => DialogResult = DialogResult.Cancel);
            cancel.Border = true;
            apply = ButtonAt(this, "应用", "", width - 106, 210, 86, 33, Apply);
            apply.Primary = true;
            inputs = new Control[] { data, cache, dataBrowse, cacheBrowse, reset, cancel, apply };
            AcceptButton = apply;
            CancelButton = cancel;
            ActiveControl = data.Edit;
        }

        private InputBox PathField(string name, string value, int width, int y)
        {
            var field = new InputBox(name, false);
            field.Edit.Text = value;
            Controls.Add(field);
            Ui.Put(field, 20, y, width, 33);
            return field;
        }

        private void Browse(InputBox field, string description)
        {
            using (var d = new FolderBrowserDialog { Description = description, ShowNewFolderButton = true, RootFolder = Environment.SpecialFolder.Desktop })
            {
                if (Directory.Exists(field.Edit.Text)) d.SelectedPath = field.Edit.Text;
                if (d.ShowDialog(this) == DialogResult.OK) field.Edit.Text = d.SelectedPath;
            }
        }

        private void Reset()
        {
            data.Edit.Text = storage.Defaults.DataRoot;
            cache.Edit.Text = storage.Defaults.CacheRoot;
        }

        private void Apply()
        {
            StorageLocations next;
            try { next = new StorageLocations(data.Edit.Text, cache.Edit.Text); }
            catch (ArgumentException e) { status.Text = e.Message; return; }
            catch (NotSupportedException) { status.Text = "这个文件夹路径无效。请重新选择。"; return; }
            catch (PathTooLongException) { status.Text = "文件夹路径太长。请换个位置。"; return; }
            saving = UseWaitCursor = true;
            foreach (var control in inputs) control.Enabled = false;
            status.Text = "正在复制文件…";
            var worker = new BackgroundWorker();
            worker.DoWork += delegate { storage.Change(next); };
            worker.RunWorkerCompleted += (sender, e) =>
            {
                worker.Dispose();
                if (IsDisposed) return;
                saving = UseWaitCursor = false;
                if (e.Error == null) { DialogResult = DialogResult.OK; return; }
                status.Text = "无法更改位置。请检查文件夹是否可写。";
                foreach (var control in inputs) control.Enabled = true;
            };
            worker.RunWorkerAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (saving && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            base.OnFormClosing(e);
        }
    }
}
