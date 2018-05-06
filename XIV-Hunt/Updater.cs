using FFXIV_GameSense.Properties;
using Squirrel;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FFXIV_GameSense
{
    public class Updater
    {
        public static Task Create(CancellationToken token)
        {
            return new Task(() => { CheckAndApplyUpdates(); }, token, TaskCreationOptions.LongRunning);
        }

        private static void CheckAndApplyUpdates()
        {
            bool shouldRestart = false;
            try
            {
                using (var mgr = new UpdateManager(Settings.Default.UpdateLocation))
                {
                    var updateInfo = mgr.CheckForUpdate().Result;
                    if (updateInfo.ReleasesToApply.Any())
                    {
                        BackupSettings();
                        shouldRestart = true;
                        DeleteOldVersions();
                        mgr.UpdateApp().Wait();//y u no cleanup ლ(ಠ_ಠლ)
                    }
                }
            }
            catch (Exception) { }
            if (shouldRestart)
                UpdateManager.RestartApp();
        }

        internal static void OnAppUpdate()
        {
            using (var mgr = new UpdateManager(Settings.Default.UpdateLocation))
            {
                mgr.RemoveUninstallerRegistryEntry();
                mgr.CreateUninstallerRegistryEntry();
            }
        }

        internal static void OnFirstRun()
        {
            BackupLastStandaloneSettings();
            RestoreSettings();
            Settings.Default.Reload();
        }

        /// <summary>
        /// Make a backup of our settings.
        /// Used to persist settings across updates.
        /// </summary>
        private static void BackupSettings()
        {
            string settingsFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            string destination = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\last.config";
            File.Copy(settingsFile, destination, true);
        }

        private static void BackupLastStandaloneSettings()
        {
            string gsDir = Directory.GetParent(Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath)).Parent.FullName;
            if (!Directory.Exists(gsDir))
                return;
            DirectoryInfo di = new DirectoryInfo(gsDir);
            string mostrecent = di.EnumerateDirectories().OrderByDescending(x => x.CreationTime).First().FullName;
            di = new DirectoryInfo(mostrecent);
            string settings = Path.Combine(di.EnumerateDirectories().OrderByDescending(x => x.CreationTime).First().FullName, "user.config");
            if (File.Exists(settings))
            {
                string destination = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\last.config";
                File.Copy(settings, destination, true);
            }
        }

        private static void DeleteOldVersions()
        {
            DirectoryInfo appDir = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent;
            var olderDirs = appDir.EnumerateDirectories("app-*").OrderByDescending(x => x.CreationTimeUtc).Skip(2);
            foreach (DirectoryInfo oldDir in olderDirs)
                try
                {
                    oldDir.Delete(true);
                }
                catch (Exception) { }
            var packagesDir = Path.Combine(appDir.FullName, "packages");
            if (!Directory.Exists(packagesDir))
                return;
            DirectoryInfo packDir = new DirectoryInfo(packagesDir);
            var olderPackages = packDir.EnumerateFiles("*.nupkg").OrderByDescending(x => x.Name).Skip(4);
            foreach (var oldPack in olderPackages)
                try
                {
                    oldPack.Delete();
                }
                catch (Exception) { }
        }

        internal static void RestoreSettings()
        {
            //Restore settings after application update            
            string destFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            string sourceFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\last.config";
            // Check for settings that may be needed to restore
            if (!File.Exists(sourceFile))
            {
                return;
            }
            // Create directory as needed
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
            }
            catch (Exception) { }
            // Copy backup file in place 
            try
            {
                File.Copy(sourceFile, destFile, true);
            }
            catch (Exception) { }
            // Delete backup file
            try
            {
                File.Delete(sourceFile);
            }
            catch (Exception) { }
        }
    }
}
