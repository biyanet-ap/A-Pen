using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HighPenSetup
{
    // Per-user installer / uninstaller. A-Pen.exe is embedded as a resource.
    // Run with /S for silent mode, /uninstall to remove.
    static class Program
    {
        const string AppName = "A-Pen"; // keep in sync with AppName in HighPen.cs
        const string Publisher = "Andy";
        const string Version = "1.0.0"; // keep in sync with AppVersion in HighPen.cs
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\A-Pen";

        static bool silent;

        // Installer text follows the Windows display language (Korean / Japanese / otherwise English).
        static readonly string Ui = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        static string L(string ko, string ja, string en) { return Ui == "ko" ? ko : (Ui == "ja" ? ja : en); }

        static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName); }
        }
        static string StartMenuLink
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"); }
        }
        static string DesktopLink
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"); }
        }

        [STAThread]
        static int Main(string[] args)
        {
            bool uninstall = Path.GetFileName(Assembly.GetExecutingAssembly().Location).Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase);
            foreach (string a in args)
            {
                if (a.Equals("/S", StringComparison.OrdinalIgnoreCase)) silent = true;
                else if (a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
            }
            try
            {
                return uninstall ? Uninstall() : Install();
            }
            catch (Exception ex)
            {
                Show((uninstall
                    ? L("제거 중 오류가 발생했습니다.", "アンインストール中にエラーが発生しました。", "An error occurred while uninstalling.")
                    : L("설치 중 오류가 발생했습니다.", "インストール中にエラーが発生しました。", "An error occurred while installing."))
                    + "\n\n" + ex.Message, MessageBoxIcon.Error);
                return 1;
            }
        }

        static void Show(string text, MessageBoxIcon icon)
        {
            if (!silent) MessageBox.Show(text, AppName, MessageBoxButtons.OK, icon);
        }

        static bool Ask(string text)
        {
            return silent || MessageBox.Show(text, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        static void KillApp()
        {
            foreach (Process p in Process.GetProcessesByName(AppName))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }
        }

        static void CreateLink(string path, string target, string workDir)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object link = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            Type lt = link.GetType();
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { workDir });
            lt.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { L("비대면 강의용 펜 앱", "オンライン授業用ペンアプリ", "Pen app for online lectures") });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }

        static int Install()
        {
            if (!Ask(string.Format(L("{0} 을(를) 설치할까요?\n\n설치 위치: {1}", "{0} をインストールしますか？\n\nインストール先: {1}", "Install {0}?\n\nLocation: {1}"), AppName + " " + Version, InstallDir))) return 0;

            KillApp();
            Directory.CreateDirectory(InstallDir);
            string exe = Path.Combine(InstallDir, AppName + ".exe");

            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(AppName + ".exe"))
            using (FileStream f = File.Create(exe))
                s.CopyTo(f);

            string uninstaller = Path.Combine(InstallDir, "Uninstall.exe");
            File.Copy(Assembly.GetExecutingAssembly().Location, uninstaller, true);

            CreateLink(StartMenuLink, exe, InstallDir);
            bool desktop = !silent && MessageBox.Show(L("바탕화면에 바로가기를 만들까요?", "デスクトップにショートカットを作成しますか？", "Create a desktop shortcut?"), AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            if (desktop) CreateLink(DesktopLink, exe, InstallDir);

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", Publisher);
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("DisplayIcon", exe);
                k.SetValue("UninstallString", "\"" + uninstaller + "\"");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }

            if (!silent && MessageBox.Show(L(
                "설치가 완료되었습니다.\n\n작업표시줄에 고정하려면 앱을 실행한 뒤 작업표시줄의 A-Pen 아이콘을 우클릭하고 '작업 표시줄에 고정'을 선택하세요.\n\n지금 실행할까요?",
                "インストールが完了しました。\n\nタスクバーにピン留めするには、アプリを起動してタスクバーの A-Pen アイコンを右クリックし、「タスクバーにピン留めする」を選んでください。\n\n今すぐ起動しますか？",
                "Installation complete.\n\nTo pin it to the taskbar, start the app, right-click the A-Pen icon on the taskbar and choose 'Pin to taskbar'.\n\nStart it now?"), AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(exe);
            return 0;
        }

        static int Uninstall()
        {
            if (!Ask(string.Format(L("{0} 을(를) 제거할까요?\n\n저장된 설정(%APPDATA%\\A-Pen)은 남겨 둡니다.", "{0} をアンインストールしますか？\n\n保存された設定 (%APPDATA%\\A-Pen) は残ります。", "Uninstall {0}?\n\nSaved settings (%APPDATA%\\A-Pen) are kept."), AppName))) return 0;

            KillApp();
            try { File.Delete(StartMenuLink); } catch { }
            try { File.Delete(DesktopLink); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            try { File.Delete(Path.Combine(InstallDir, AppName + ".exe")); } catch { }

            // Uninstall.exe is running, so remove it and the folder from a helper shortly after exit.
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                "/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + Path.Combine(InstallDir, "Uninstall.exe") + "\" & rmdir \"" + InstallDir + "\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);

            Show(L("제거가 완료되었습니다.", "アンインストールが完了しました。", "Uninstall complete."), MessageBoxIcon.Information);
            return 0;
        }
    }
}
