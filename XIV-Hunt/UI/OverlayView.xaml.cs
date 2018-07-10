using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Controls.Primitives;
using Process.NET;

namespace FFXIV_GameSense.UI
{
    /// <summary>
    /// Interaction logic for OverlayView.xaml
    /// </summary>
    public partial class OverlayView : UserControl, IDisposable
    {
        private Thread RadarOverlayThread;
        private RadarOverlay ro;
        private CancellationTokenSource cts;
        private bool disposedValue = false; // To detect redundant calls

        public OverlayView()
        {
            InitializeComponent();
            RadarMaxFrameRateTextBox.TextChanged += RadarMaxFrameRateTextBox_TextChanged;
            RadarBGOpacityTextBox.TextChanged += RadarBGOpacityTextBox_TextChanged;
        }

        private void RadarBGOpacityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textbox = sender as TextBox;
            if (byte.TryParse(textbox.Text, out byte value))
            {
                if (value > 100)
                    textbox.Text = 100.ToString();
                else if (value < byte.MinValue)
                    textbox.Text = byte.MinValue.ToString();
                ro?.SetBackgroundOpacity();
            }
            else
                textbox.Text = 0.ToString();
        }

        private void RadarMaxFrameRateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textbox = sender as TextBox;
            if (byte.TryParse(textbox.Text, out byte value))
            {
                if (value > 144)
                    textbox.Text = 144.ToString();
                else if (value < 1)
                    textbox.Text = 1.ToString();
                ro?.SetNewFrameRate();
            }
            else
                textbox.Text = 30.ToString();
        }

        private void _2DRadarToggle(object sender, RoutedEventArgs e)
        {
            ToggleButton b = (ToggleButton)sender;
            if (b.IsChecked ?? false)
            {
                cts = new CancellationTokenSource();
                RadarOverlayThread = new Thread(() =>
                {
                    ro = new RadarOverlay(cts.Token);
                    ProcessSharp ps = new ProcessSharp(Program.mem.Process.Id, Process.NET.Memory.MemoryType.Remote);
                    ro.Initialize(ps.WindowFactory.MainWindow);
                    ro.Enable();
                    System.Windows.Threading.Dispatcher.Run();
                });
                RadarOverlayThread.SetApartmentState(ApartmentState.STA);
                RadarOverlayThread.IsBackground = true;
                RadarOverlayThread.Start();
                Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(1000);
                    if (!cts.IsCancellationRequested && !Properties.Settings.Default.RadarEnableClickthru)
                        ro?.MakeClickable();
                });
            }
            else if (b.IsChecked != true)
            {
                cts?.Cancel();
                ro?.Dispose();
                ro = null;
            }
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (cts != null && !cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                        Thread.Sleep(1000 / Properties.Settings.Default.RadarMaxFrameRate * 2);
                    }
                    if(cts!=null)
                        cts.Dispose();
                    if(ro!=null)
                        ro.Dispose();
                }
                ro = null;
                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);

        private void ClickthruCheckBox_Checked(object sender, RoutedEventArgs e) => ro?.MakeClickthru();

        private void ClickthruCheckBox_Unchecked(object sender, RoutedEventArgs e) => ro?.MakeClickable();

        private void ResizeCheckBox_Checked_1(object sender, RoutedEventArgs e) => ro?.DisableResizeMode();

        private void ResizeCheckBox_Unchecked_1(object sender, RoutedEventArgs e) => ro?.EnableResizeMode();
    }
}
