using FFXIV_GameSense.Properties;
using Squirrel;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace FFXIV_GameSense
{
    class Program
    {
        internal static FFXIVMemory mem;
        internal static AssemblyName AssemblyName = Assembly.GetExecutingAssembly().GetName();

        [STAThread]
        public static void Main(string[] args)
        {
#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#endif
            if (ApplicationRunningHelper.AlreadyRunning())
            {
                Thread.Sleep(2000);
                if (ApplicationRunningHelper.AlreadyRunning())
                    return;
            }
            try
            {
                if (Settings.Default.CallUpgrade)
                {
                    Updater.RestoreSettings();
                    Settings.Default.Reload();
                }
            }catch(Exception e)
            {
                WriteExceptionToErrorFile(new Exception("Failed to restore previous settings.", e));
            }
#if !DEBUG
            SquirrelAwareApp.HandleEvents(onAppUpdate: v => Updater.OnAppUpdate(), onFirstRun: () => Updater.OnFirstRun());
            try { NativeMethods.SetCurrentProcessExplicitAppUserModelID("com.squirrel.XIVHunt.XIV-Hunt"); } catch { }
            using (var cts = new CancellationTokenSource())
            {
                var updateTask = Updater.Create(cts.Token);
                updateTask.Start();
                updateTask.Wait();
            }
#endif

            Application app = new Application { MainWindow = new Window1() };
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(app.MainWindow);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteExceptionToErrorFile((Exception)e.ExceptionObject);
        }

        internal static void WriteExceptionToErrorFile(Exception ex)
        {
            File.AppendAllText(Path.Combine(Environment.CurrentDirectory, "error.txt"), DateTime.UtcNow + " " + ex.GetType().ToString() + ":" + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
        }
    }
}
