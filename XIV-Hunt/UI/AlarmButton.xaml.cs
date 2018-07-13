using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace FFXIV_GameSense.UI
{
    /// <summary>
    /// Interaction logic for AlarmButton.xaml
    /// </summary>
    public partial class AlarmButton : UserControl
    {
        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(AlarmButton));

        public event RoutedEventHandler Click
        {
            add { AddHandler(ClickEvent, value); }
            remove { RemoveHandler(ClickEvent, value); }
        }

        private void RaiseClickEvent()
        {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(ClickEvent);
            RaiseEvent(newEventArgs);
        }

        private void OnClick() => RaiseClickEvent();

        public AlarmButton()
        {
            InitializeComponent();
            PreviewMouseLeftButtonUp += (sender, args) => OnClick();
            IsEnabledChanged += AlarmButton_IsEnabledChanged;
            DataContext = new AlarmButtonModel();
        }

        private void AlarmButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                Opacity = 1;
            else
                Opacity = .25;
        }

        public void SetOn() => ((AlarmButtonModel)(DataContext)).Icon = "/Resources/Images/sound_on.png";

        public void SetOff() => ((AlarmButtonModel)(DataContext)).Icon = "/Resources/Images/sound_off.png";

        public bool IsOn() => ((AlarmButtonModel)(DataContext)).Icon == "/Resources/Images/sound_on.png";
    }

    public class AlarmButtonModel : INotifyPropertyChanged
    {
        private string icon = "/Resources/Images/sound_off.png";
        public string Icon
        {
            get
            {
                return icon;
            }
            set
            {
                if (value != icon)
                {
                    icon = value;
                    OnPropertyChanged(nameof(Icon));
                }
            }
        } 

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
