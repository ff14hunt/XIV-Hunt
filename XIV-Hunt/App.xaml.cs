using FFXIV_GameSense.Properties;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using Splat;
using Squirrel;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace FFXIV_GameSense
{
    public partial class App : Application
    {
        internal const string AppID = "com.squirrel.XIVHunt.XIV-Hunt";

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            try { NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppID); } catch { }
            if (ApplicationRunningHelper.AlreadyRunning())
            {
                Thread.Sleep(2000);
                if (ApplicationRunningHelper.AlreadyRunning())
                    return;
            }

            bool isFirstInstall = RestoreSettings();

            if (IsSquirrelInstall())
            {
                SquirrelAwareApp.HandleEvents(onAppUpdate: v => Updater.OnAppUpdate(), onFirstRun: Updater.OnFirstRun);
                using (var cts = new CancellationTokenSource())
                {
                    var updateTask = Updater.Create(cts.Token);
                    updateTask.Start();
                    updateTask.Wait();
                }
            }

            MainWindow = new Window1();
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow.Show();

            base.OnStartup(e);
            if (isFirstInstall)
                Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(1000);
                    TryShowInstalledShortcutInfoToast();
                });

        }

        private static bool RestoreSettings()
        {
            bool isFirstInstall = false;
            try
            {
                if (Settings.Default.CallUpgrade)
                {
                    Updater.RestoreSettings();
                    Settings.Default.Reload();
                    isFirstInstall = Settings.Default.CallUpgrade;
                    Settings.Default.CallUpgrade = false;
                    Settings.Default.Save();
                }
            }
            catch (Exception ex)
            {
                WriteExceptionToErrorFile(new Exception("Failed to restore previous settings.", ex));
            }
            return isFirstInstall;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteExceptionToErrorFile((Exception)e.ExceptionObject);
        }

        internal static void WriteExceptionToErrorFile(Exception ex)
        {
            File.AppendAllText(Path.Combine(Environment.CurrentDirectory, "error.txt"), $"{DateTime.UtcNow} {ex.GetType().ToString()}:{ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
        }

        internal static bool IsSquirrelInstall()
        {
#if !DEBUG
            Assembly assembly = Assembly.GetEntryAssembly();
            string updateDotExe = Path.Combine(Path.GetDirectoryName(assembly.Location), "..", "Update.exe");
            return File.Exists(updateDotExe);
#else
            return false;
#endif
        }

        private static void TryShowInstalledShortcutInfoToast()
        {
            try
            {
                ShowInstalledShortcutInfoToast();
            }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShowInstalledShortcutInfoToast()
        {
            try
            {
                RegistryKey notificationReg = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\" + AppID, true);
                notificationReg.SetValue("ShowInActionCenter", 1, RegistryValueKind.DWord);
                notificationReg.Close();
                ToastContent toastContent = new ToastContent()
                {
                    Audio = new ToastAudio { Silent = true },
                    Visual = new ToastVisual
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                        {
                            new AdaptiveText()
                            {
                                Text = string.Format(FFXIV_GameSense.Properties.Resources.ToastNotificationAppInstalledShortcut, Program.AssemblyName.Name)
                            }
                        }
                        }
                    }
                };
                var doc = new XmlDocument();
                doc.LoadXml(toastContent.GetContent());
                var toast = new ToastNotification(doc)
                {
                    ExpirationTime = DateTimeOffset.Now.AddHours(6)
                };
                ToastNotificationManager.CreateToastNotifier(AppID).Show(toast);
            }
            catch (Exception ex) { LogHost.Default.WarnException("Could not show toast.", ex); }
        }
    }
}
