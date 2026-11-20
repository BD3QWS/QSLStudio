using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;

namespace QslStudio
{
    internal static class InstallMaintenance
    {
        private const string QaShortcut = "QSL Studio QA.lnk";

        internal static int Run(string command, string profile)
        {
            if (profile != "QSLStudio" && profile != "QSLStudioQA") return 2;
            try
            {
                if (command == "--clear-user-data")
                {
                    using (var self = Process.GetCurrentProcess())
                    {
                        var editors = Process.GetProcessesByName("QSLStudio");
                        try
                        {
                            foreach (var editor in editors)
                                if (editor.Id != self.Id && editor.SessionId == self.SessionId)
                                    return 3;
                        }
                        finally
                        {
                            foreach (var editor in editors) editor.Dispose();
                        }
                    }
                    UserStorage.ForUser(profile).ClearApplicationData();
                }
                else RemoveQaShortcuts(profile);
                return 0;
            }
            catch (IOException) { return 1; }
            catch (UnauthorizedAccessException) { return 1; }
            catch (COMException) { return 1; }
            catch (ArgumentException) { return 1; }
            catch (InvalidOperationException) { return 1; }
        }

        private static void RemoveQaShortcuts(string profile)
        {
            if (profile == "QSLStudioQA")
            {
                string privateShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), profile, "Shell");
                RemoveQaShortcut(Path.Combine(privateShell, "Desktop", QaShortcut));
                RemoveQaShortcut(Path.Combine(privateShell, "Programs", "QSL Studio QA", QaShortcut));
                return;
            }
            foreach (var folder in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory })
            {
                string directory = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(directory)) RemoveQaShortcut(Path.Combine(directory, QaShortcut));
            }
            foreach (var folder in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms })
            {
                string directory = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(directory)) continue;
                string group = Path.Combine(directory, "QSL Studio QA");
                if (RemoveQaShortcut(Path.Combine(group, QaShortcut)) && Directory.Exists(group) &&
                    Directory.GetFileSystemEntries(group).Length == 0)
                    Directory.Delete(group);
            }
        }

        private static bool RemoveQaShortcut(string path)
        {
            if (!File.Exists(path)) return false;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
            var link = new ShellLink();
            string target;
            try
            {
                ((IPersistFile)link).Load(path, 0);
                var raw = new StringBuilder(32768);
                if (((IShellLink)link).GetPath(raw, raw.Capacity, IntPtr.Zero, 4) != 0) return false;
                target = Environment.ExpandEnvironmentVariables(raw.ToString());
            }
            finally { Marshal.FinalReleaseComObject(link); }
            if (!string.Equals(Path.GetFileName(target), "QSLStudio.exe", StringComparison.OrdinalIgnoreCase))
                return false;
            string folder = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(folder)) return false;
            string localTestInstall = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "QSLStudioQA");
            if (!string.Equals(folder, localTestInstall, StringComparison.OrdinalIgnoreCase) &&
                !Regex.IsMatch(Path.GetFileName(folder), @"^(QA install|QSL Studio Path QA) [0-9a-f]{8}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return false;

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            File.Delete(path);
            SHChangeNotify(4, 5, path, IntPtr.Zero);
            return true;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(int eventId, uint flags, string path, IntPtr item2);

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLink
        {
            [PreserveSig]
            int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr file, uint flags);
        }
    }
}
