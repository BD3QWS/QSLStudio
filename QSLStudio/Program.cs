using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;

[assembly: AssemblyTitle("QSL 卡片工坊")]
[assembly: AssemblyDescription("业余无线电双面通联确认卡编辑器")]
[assembly: AssemblyProduct("QSL 卡片工坊")]
[assembly: AssemblyVersion("1.10.5.0")]
[assembly: AssemblyFileVersion("1.10.5.0")]
[assembly: AssemblyInformationalVersion("1.10.5")]
[assembly: TargetFramework(".NETFramework,Version=v4.0", FrameworkDisplayName = ".NET Framework 4")]

namespace QslStudio
{
    internal static class Program
    {
        [STAThread]
        internal static void Main(string[] args)
        {
#if BUILD_PREVIEWS
            for (int i = 0; i < Templates.BuiltInCount; i++)
                for (int side = 0; side < 2; side++)
                    using (var preview = TemplateTile.RenderPreview(Templates.Keys[i], side))
                        preview.Save(System.IO.Path.Combine(args[0], TemplateTile.PreviewName(Templates.Keys[i], side)), System.Drawing.Imaging.ImageFormat.Jpeg);
#else
            if (args.Length == 2 && (args[0] == "--clear-user-data" || args[0] == "--clean-qa-shortcuts"))
            {
                Environment.ExitCode = InstallMaintenance.Run(args[0], args[1]);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (Environment.OSVersion.Version.Major >= 6)
            {
                try { System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072; }
                catch (NotSupportedException) { }
            }
            if (args.Length == 1 && args[0] == "--prepare-logos")
            {
                Environment.ExitCode = LogoLibrary.Prepare(AppDomain.CurrentDomain.BaseDirectory);
                return;
            }
            Ui.Init();
            var editor = new Editor(args.Length > 0 ? args[0] : null);
            if (args.Length == 0) editor.Shown += delegate { editor.ShowGettingStarted(); };
            Application.Run(editor);
#endif
        }
    }
}
