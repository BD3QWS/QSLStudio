using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace QslStudio
{
    internal sealed class StationProfile
    {
        internal string Name, Path;
        internal readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        internal void Apply(Project project)
        {
            project.ClearDemoFields();
            foreach (var field in Field.All)
            {
                if (!field.Station) continue;
                string value;
                project.Values[field.Key] = Values.TryGetValue(field.Key, out value) ? value : "";
            }
        }
        public override string ToString() { return Name + " · " + Values["own"]; }
    }

    internal static class Stations
    {
        internal static List<StationProfile> Read(string folder)
        {
            var entries = new List<StationProfile>();
            if (!Directory.Exists(folder)) return entries;
            foreach (string path in Directory.GetFiles(folder, "*.xml"))
            {
                XElement root;
                try
                {
                    using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null, MaxCharactersInDocument = 32768 })) root = XElement.Load(reader);
                }
                catch (XmlException) { continue; }
                catch (FileNotFoundException) { continue; }
                if (root.Name != "qslstation" || Files.Attr(root, "version", "") != "1") continue;
                string name = Files.Cut(Files.Attr(root, "name", "").Trim(), 48);
                if (name.Length == 0) continue;
                var profile = new StationProfile { Name = name, Path = path };
                foreach (var element in root.Elements("value"))
                {
                    var field = Field.Find(Files.Attr(element, "key", ""));
                    if (field != null && field.Station) profile.Values[field.Key] = Files.Cut(element.Value, Field.Limit(field.Key));
                }
                if (profile.Values.ContainsKey("own") && !string.IsNullOrWhiteSpace(profile.Values["own"])) entries.Add(profile);
            }
            entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return entries;
        }

        internal static void Save(string folder, string name, Project current, StationProfile replace)
        {
            if (current.StationCall.Length == 0)
                throw new InvalidDataException("请先在「本台」输入实际使用的呼号，再保存台站资料。");
            name = Files.Cut(name.Trim(), 48);
            if (name.Length == 0) throw new InvalidDataException("请输入台站资料名称。");
            var clean = current.Copy();
            clean.ClearDemoFields();
            var root = new XElement("qslstation", new XAttribute("version", "1"), new XAttribute("name", name));
            foreach (var field in Field.All)
                if (field.Station) root.Add(new XElement("value", new XAttribute("key", field.Key), clean.Value(field.Key)));
            Directory.CreateDirectory(folder);
            string path = replace == null ? Path.Combine(folder, Guid.NewGuid().ToString("N") + ".xml") : replace.Path;
            Files.Atomic(path, stream => root.Save(stream));
        }
    }

    internal sealed class StationDialog : StudioDialog
    {
        internal StationProfile Selected { get; private set; }
        internal bool Saved { get; private set; }

        internal StationDialog(Project project, string folder, List<StationProfile> profiles) : base("台站资料", 464, 240)
        {
            LabelAt(this, "已保存的台站", 20, 14, 394, 27, 14, true, Ui.Ink);
            var list = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.F(13, false), AccessibleName = "选择台站资料" };
            Controls.Add(list); Ui.Put(list, 20, 48, 315, 33);
            var apply = ButtonAt(this, "填入本台", "", 345, 45, 99, 33, () => { Selected = list.SelectedItem as StationProfile; DialogResult = DialogResult.OK; });
            apply.Primary = true;
            apply.Enabled = profiles.Count > 0;
            if (profiles.Count > 0)
            {
                list.Items.AddRange(profiles.ToArray());
                list.SelectedIndex = 0;
            }
            else { list.Items.Add("暂无台站资料"); list.SelectedIndex = 0; list.Enabled = false; }
            LabelAt(this, "保存本台当前资料", 20, 106, 394, 28, 14, true, Ui.Ink);
            var name = new InputBox("台站资料名称", false);
            name.Edit.MaxLength = 48;
            name.Edit.Text = project.StationCall;
            Controls.Add(name); Ui.Put(name, 20, 140, 315, 33);
            var save = ButtonAt(this, "保存资料", "", 345, 140, 99, 33, () =>
            {
                string title = name.Edit.Text.Trim();
                var replace = profiles.Find(profile => profile.Name.Equals(title, StringComparison.CurrentCultureIgnoreCase));
                if (replace != null && MessageBox.Show(this, "更新「" + replace.Name + "」的台站资料？", "更新台站资料",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                try { Stations.Save(folder, title, project, replace); Saved = true; DialogResult = DialogResult.OK; }
                catch (Exception e) { MessageBox.Show(this, e.Message, "保存台站资料失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            });
            save.Border = true;
            var close = ButtonAt(this, "关闭", "", 355, 192, 89, 31, () => DialogResult = DialogResult.Cancel);
            CancelButton = close;
        }
    }
}
