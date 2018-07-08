using FFXIV_GameSense.Properties;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace FFXIV_GameSense
{
    /// <summary>
    /// Interaction logic for SettingsForm.xaml
    /// </summary>
    public partial class SettingsForm : Window
    {
        private RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        private readonly string appName = Assembly.GetExecutingAssembly().GetName().Name;
        private string processpath = Assembly.GetExecutingAssembly().Location;
        public SettingsForm()
        {
            InitializeComponent();
            var cv = registryKey.GetValue(appName);
#if !DEBUG
            var pn = processpath.Substring(processpath.LastIndexOf(@"\") + 1);
            processpath = processpath.Substring(0, processpath.Substring(0, processpath.LastIndexOf(@"\")).LastIndexOf(@"\") + 1) + "Update.exe --processStart " + pn;
#endif
            if (cv != null && cv.Equals(processpath))
                StartWithWindowsCB.IsChecked = true;
            StartWithWindowsCB.Checked += StartWithWindowsCB_Checked;
            StartWithWindowsCB.Unchecked += StartWithWindowsCB_Unchecked;
            Closing += SettingsForm_Closing;
            if (!Directory.Exists(Settings.Default.PerformDirectory))
                PerformDirectoryTextBox.Text = string.Empty;
        }

        private void SettingsForm_Closing(object sender, System.ComponentModel.CancelEventArgs e) => Settings.Default.Save();

        private void StartWithWindowsCB_Unchecked(object sender, RoutedEventArgs e) => registryKey.DeleteValue(appName, false);

        private void StartWithWindowsCB_Checked(object sender, RoutedEventArgs e) => registryKey.SetValue(appName, processpath);

        private void OncePerHuntCheckBox_Checked(object sender, RoutedEventArgs e) => HuntInterval.IsEnabled = false;

        private void OncePerHuntCheckBox_Unchecked(object sender, RoutedEventArgs e) => HuntInterval.IsEnabled = true;

        private void FATEPercentInterval_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textbox = sender as TextBox;
            if (int.TryParse(textbox.Text, out int value))
            {
                if (value > 100)
                    textbox.Text = 100.ToString();
                else if (value < 0)
                    textbox.Text = 0.ToString();
            }
        }

        private void PerformDirectoryTextBox_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && Directory.Exists(dialog.SelectedPath))
                    PerformDirectoryTextBox.Text = dialog.SelectedPath;
            }
        }

        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.Cookies = string.Empty;
            Updater.RestartApp();
        }
    }
}
