using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using Splat;

namespace FFXIV_GameSense.UI
{
    /// <summary>
    /// Interaction logic for LogView.xaml
    /// </summary>
    public partial class LogView : Window
    {
        private Dictionary<LogLevel, Brush> LogLevelColors;
        public LogView()
        {
            InitializeComponent();
            var ComboBoxItems = new ComboBoxItem[LogLevelSelectComboBox.Items.Count];
            LogLevelSelectComboBox.Items.CopyTo(ComboBoxItems, 0);
            LogLevelColors = ComboBoxItems.ToDictionary(x => ((LogLevel)Convert.ToInt32(x.Tag)), y => y.Foreground);
        }

        public void AddLogLine(string text, LogLevel level)
        {
            if(level > (LogLevel)Properties.Settings.Default.LogLevel)
            {
                LogViewRTB.Dispatcher.Invoke(() =>
                {
                    while (LogViewRTB.Document.Blocks.Count > byte.MaxValue)
                        LogViewRTB.Document.Blocks.Remove(LogViewRTB.Document.Blocks.FirstBlock);
                    bool scrollToEnd = IsVerticalScrollOnBottom();
                    TextRange tr = new TextRange(LogViewRTB.Document.ContentEnd, LogViewRTB.Document.ContentEnd);
                    text = $"{DateTime.Now.ToString("HH:mm:ss")} {level.ToString()} {text}{Environment.NewLine}";
                    try
                    {
                        tr.Text = text;
                        tr.ApplyPropertyValue(TextElement.ForegroundProperty, LogLevelColors[level]);
                    }
                    catch (Exception) { }
                    if (scrollToEnd)
                        LogViewRTB.ScrollToEnd();
                });
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Visibility = Visibility.Hidden;
        }

        private bool IsVerticalScrollOnBottom()
        {
            // get the vertical scroll position
            double dVer = LogViewRTB.VerticalOffset;
            //get the vertical size of the scrollable content area
            double dViewport = LogViewRTB.ViewportHeight;
            //get the vertical size of the visible content area
            double dExtent = LogViewRTB.ExtentHeight;
            return dVer != 0 ? dVer + dViewport == dExtent : false;
        }
    }
}
