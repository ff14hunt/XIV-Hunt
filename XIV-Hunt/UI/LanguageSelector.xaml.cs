using FFXIV_GameSense.Properties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FFXIV_GameSense.UI
{
    /// <summary>
    /// Interaction logic for LanguageSelector.xaml
    /// </summary>
    public partial class LanguageSelector : Window
    {
        public LanguageSelector()
        {
            InitializeComponent();
            Title = string.Format(Properties.Resources.LanguageSelectorTitle, Program.AssemblyName.Name);
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(Settings.Default.LanguageCI);
            Title = string.Format(Properties.Resources.ResourceManager.GetString(nameof(Properties.Resources.LanguageSelectorTitle), culture), Program.AssemblyName.Name);
            InfoTextBlock.Text = Properties.Resources.ResourceManager.GetString(nameof(Properties.Resources.LanguageSelectorInfo), culture);
            Button.Content = Properties.Resources.ResourceManager.GetString(nameof(Properties.Resources.LanguageSelectorContinue), culture);
        }

        private void Button_Click(object sender, RoutedEventArgs e) => DialogResult = true;

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Button_Click(sender, e);
        }
    }
}
