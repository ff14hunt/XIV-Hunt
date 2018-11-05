using FFXIV_GameSense.Properties;
using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FFXIV_GameSense.UI.Controls
{
    /// <summary>
    /// Interaction logic for LanguageSelector.xaml
    /// </summary>
    public partial class LanguageSelector : UserControl
    {
        public LanguageSelector()
        {
            InitializeComponent();
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
            GotFocus += LanguageSelector_GotFocus;
        }

        private void LanguageSelector_GotFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(delegate ()
                {
                    LanguageComboBox.Focus();
                }));
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.Save();
            if(RestartOnChange)
                Updater.RestartApp();
        }

        public bool RestartOnChange
        {
            get { return (bool)GetValue(RestartOnChangeProperty); }
            set { SetValue(RestartOnChangeProperty, value); }
        }

        public static readonly DependencyProperty RestartOnChangeProperty = DependencyProperty.Register(nameof(RestartOnChange), typeof(bool), typeof(LanguageSelector), new PropertyMetadata(false));
    }
}
