using FFXIV_GameSense.Properties;
using Microsoft.Win32;
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
        private RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        private readonly string appName = Assembly.GetExecutingAssembly().GetName().Name;
        public SettingsForm()
        {
            InitializeComponent();
            var cv = registryKey.GetValue(appName);
            if (cv != null && cv.Equals(Assembly.GetExecutingAssembly().Location))
                StartWithWindowsCB.IsChecked = true;
            StartWithWindowsCB.Checked += StartWithWindowsCB_Checked;
            StartWithWindowsCB.Unchecked += StartWithWindowsCB_Unchecked;
            Closing += SettingsForm_Closing;
        }

        private void SettingsForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.Save();
        }

        private void StartWithWindowsCB_Unchecked(object sender, RoutedEventArgs e)
        {
            var cv = registryKey.GetValue(appName);
            if (cv != null)
                registryKey.DeleteValue(appName);
        }

        private void StartWithWindowsCB_Checked(object sender, RoutedEventArgs e)
        {
            registryKey.SetValue(appName, Assembly.GetExecutingAssembly().Location);
        }

        private void OncePerHuntCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            HuntInterval.IsEnabled = false;
        }

        private void OncePerHuntCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            HuntInterval.IsEnabled = true;
        }

        private void FATEPercentInterval_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textbox = sender as TextBox;
            if (int.TryParse(textbox.Text, out int value))
            {
                if (value > 100)
                    textbox.Text = "100";
                else if (value < 0)
                    textbox.Text = "0";
            }
        }

        //private void ClearCookiesButton_Click(object sender, RoutedEventArgs e)
        //{
        //    Settings.Default.Cookies = string.Empty;
        //    Settings.Default.Save();
        //}
    }
}
