using AutoUpdaterDotNET;
using FFXIV_GameSense.Properties;
using System;
using System.Diagnostics;
//using System.IO;
using System.Threading;
using System.Windows;

namespace FFXIV_GameSense
{
    class Program
    {
        internal static FFXIVMemory mem;
        internal static Window1 w1;

        [STAThread]
        public static void Main(string[] args)
        {
            //AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Process thisProc = Process.GetCurrentProcess();
            //If restarted by itself, give previous process 1sec to shutdown.
            if (NativeMethods.ParentProcessUtilities.GetParentProcess().ProcessName.Equals(thisProc.ProcessName))
                Thread.Sleep(1000);
            if (Process.GetProcessesByName(thisProc.ProcessName).Length > 1)
            {
                MessageBox.Show(Resources.AppIsAlreadyRunning);
                return;
            }
            if (Settings.Default.CallUpgrade)
            {
                Settings.Default.Upgrade();
                Settings.Default.CallUpgrade = false;
            }

            AutoUpdater.OpenDownloadPage = true;//
            if (!Environment.Is64BitProcess)
                AutoUpdater.Start("https://xivhunt.net/updates/xiv-huntx86.xml");
            else
                AutoUpdater.Start("https://xivhunt.net/updates/xiv-hunt.xml");

            Application app = new Application() { MainWindow = w1 = new Window1() };
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(w1);
        }

        //private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        //{
        //    WriteExceptionToErrorFile((Exception)e.ExceptionObject);
        //}

        //internal static void WriteExceptionToErrorFile(Exception ex)
        //{
        //    File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, "error.txt"), new string[] { ex.GetType().ToString() + ":", ex.Message, ex.StackTrace });
        //}
    }
}
