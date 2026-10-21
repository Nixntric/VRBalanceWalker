using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WiiBalanceWalker
{
    static class BalanceWalker
    {
        public static FormMain FormMain;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Eva.Init();
            // First launch starts in Aero; Retro only once unlocked.
            var theme = WiiBalanceWalker.Properties.Settings.Default.Theme;
            if (theme < 0 || theme >= Eva.Themes.Length || (theme == Eva.RetroIndex && !WiiBalanceWalker.Properties.Settings.Default.RetroUnlocked)) theme = Eva.AeroIndex;
            Eva.SetTheme(theme, null);

            var args = Environment.GetCommandLineArgs();
            int themeArg = -1;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--demo") FormMain.demoMode = true;
                if (args[i] == "--theme" && i + 1 < args.Length) themeArg = int.Parse(args[++i]);     // retint after load, not saved
                if (args[i] == "--themes" && i + 1 < args.Length) FormMain.shotThemes = args[++i].Split(',').Select(int.Parse).ToArray();
                if (args[i] == "--shot" && i + 1 < args.Length)
                {
                    FormMain.shotPath = args[++i];
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        FormMain.shotTimes = args[++i].Split(',').Select(int.Parse).ToArray();
                }
            }

            // Log anything unhandled next to the exe. WiimoteLib can throw on a garbled report from its own I/O thread;
            // app.config's legacyUnhandledExceptionPolicy keeps that from killing the program, this records it.
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogError("Unhandled", e.ExceptionObject as Exception);
            Application.ThreadException += (s, e) => LogError("UI thread", e.Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            FormMain = new WiiBalanceWalker.FormMain();
            if (themeArg >= 0) FormMain.SwitchTheme(themeArg, false);
            Application.Run(FormMain);
        }

        public static void LogError(string where, Exception ex)
        {
            try
            {
                var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wbw_errors.log");
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + where + ": " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
    }
}
