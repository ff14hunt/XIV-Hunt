using FFXIV_GameSense.Properties;
using Squirrel;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace FFXIV_GameSense
{
    class Program
    {
        internal static FFXIVMemory mem;
        internal static Window1 w1;
        internal static AssemblyName AssemblyName = Assembly.GetExecutingAssembly().GetName();

        [STAThread]
        public static void Main(string[] args)
        {
#if !DEBUG
            //if (args.Length != 0 && args.Any(x=>x.Equals("werror", StringComparison.CurrentCultureIgnoreCase)))
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#endif
            if (ApplicationRunningHelper.AlreadyRunning())
            {
                Thread.Sleep(2000);
                if (ApplicationRunningHelper.AlreadyRunning())
                    return;
            }
            if (Settings.Default.CallUpgrade)
            {
                Updater.RestoreSettings();
                Settings.Default.Reload();
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

            Application app = new Application() { MainWindow = w1 = new Window1() };
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(w1);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteExceptionToErrorFile((Exception)e.ExceptionObject);
        }

        internal static void WriteExceptionToErrorFile(Exception ex)
        {
            File.AppendAllText(Path.Combine(Environment.CurrentDirectory, "error.txt"), DateTime.UtcNow + ":" + ex.GetType().ToString() + ":" + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
        }
    }
}
