using FFXIV_GameSense.Properties;
using Microsoft.Win32;
using NAudio.Wave;
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
        private RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        private string processpath = Assembly.GetExecutingAssembly().Location;
        public SettingsForm()
        {
            InitializeComponent();
            var cv = registryKey.GetValue(Program.AssemblyName.Name);
            if(App.IsSquirrelInstall())
            {
                var pn = processpath.Substring(processpath.LastIndexOf(@"\") + 1);
                processpath = processpath.Substring(0, processpath.Substring(0, processpath.LastIndexOf(@"\")).LastIndexOf(@"\") + 1) + "Update.exe --processStart " + pn;
            }

            if (cv != null && cv.Equals(processpath))
                StartWithWindowsCB.IsChecked = true;
            StartWithWindowsCB.Checked += StartWithWindowsCB_Checked;
            StartWithWindowsCB.Unchecked += StartWithWindowsCB_Unchecked;
            Closing += SettingsForm_Closing;
            if (!Directory.Exists(Settings.Default.PerformDirectory))
                PerformDirectoryTextBox.Text = string.Empty;
            RefreshAudioDevicesComboBox();
        }

        private void RefreshAudioDevicesComboBox()
        {
            for (int n = 0; n < WaveOut.DeviceCount; n++)
            {
                WaveOutCapabilities waveOutCapabilities = WaveOut.GetCapabilities(n);
                if(!AudioDevicesComboBox.Items.Contains(waveOutCapabilities.ProductName))
                    AudioDevicesComboBox.Items.Add(waveOutCapabilities.ProductName);
            }
            if (AudioDevicesComboBox.Items.Contains(Settings.Default.AudioDevice))
                AudioDevicesComboBox.SelectedItem = Settings.Default.AudioDevice;
        }

        private void SettingsForm_Closing(object sender, System.ComponentModel.CancelEventArgs e) => Settings.Default.Save();

        private void StartWithWindowsCB_Unchecked(object sender, RoutedEventArgs e) => registryKey.DeleteValue(Program.AssemblyName.Name, false);

        private void StartWithWindowsCB_Checked(object sender, RoutedEventArgs e) => registryKey.SetValue(Program.AssemblyName.Name, processpath);

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
                    Settings.Default.PerformDirectory = dialog.SelectedPath;
            }
        }

        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.Cookies = string.Empty;
            Updater.RestartApp();
        }

        private void ForgetPerformDirectoryButton_Click(object sender, RoutedEventArgs e) => Settings.Default.PerformDirectory = string.Empty;

        private void AudioDevicesComboBox_GotFocus(object sender, RoutedEventArgs e) => RefreshAudioDevicesComboBox();

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(SoundPlayer.WaveDevice != null)
                SoundPlayer.WaveDevice.Volume = (float)e.NewValue;
        }
    }
}
