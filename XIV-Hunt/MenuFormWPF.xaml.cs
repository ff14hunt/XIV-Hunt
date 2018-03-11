using System;
using System.Windows;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Media;
using System.Windows.Controls;
using System.IO;
using FFXIV_GameSense.Properties;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Globalization;
using System.Threading;
using System.Reflection;
using System.Text;
using XIVDB;
using FFXIV_GameSense.MML;
using System.Text.RegularExpressions;
using static FFXIV_GameSense.FFXIVMemory;

namespace FFXIV_GameSense
{
    public partial class Window1 : Window, IDisposable
    {
        private DispatcherTimer dispatcherTimer1s;
        private FFXIVHunts hunts;
        internal SoundPlayer Ssp;
        internal SoundPlayer Asp;
        internal SoundPlayer Bsp;
        internal SoundPlayer FATEsp;
        private CheckBox currentCMPlacement;
        private System.Windows.Forms.NotifyIcon ni;
        private ProcessViewModel pvm;
        private static SettingsForm SettingsWindow;
        private CancellationTokenSource cts;
        private static bool WroteDRPop = false;

        public Window1()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(Settings.Default.LanguageCI);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(Settings.Default.LanguageCI);
            InitializeComponent();
            pvm = new ProcessViewModel();
            Closed += MenuForm_FormClosed;
            dispatcherTimer1s = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            dispatcherTimer1s.Tick += DispatcherTimer1s_Tick;
            dispatcherTimer1s.Start();
            CheckSoundStartup();
            ni = new System.Windows.Forms.NotifyIcon()
            {
                Icon = Properties.Resources.enemy,
                Visible = false,
                Text = Assembly.GetExecutingAssembly().GetName().Name + " " + Assembly.GetExecutingAssembly().GetName().Version
            };
            ni.Click += delegate (object sender, EventArgs args)
                {
                    Show();
                    Visibility = Visibility.Visible;
                    WindowState = WindowState.Normal;
                    ni.Visible = false;
                };
            DataContext = pvm;
            OtherFATEsCheckComboBox.Items.Filter += FilterPredicate;
            OtherFATEsCheckComboBox_ItemSelectionChanged(null, null);
            _ = PopulateFATEList();
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
            if (Settings.Default.StartMinimized)
            {
                WindowState = WindowState.Minimized;
                if (Settings.Default.MinimizeToTray)
                {
                    Visibility = Visibility.Hidden;
                    OnStateChanged(EventArgs.Empty);
                }
            }
        }

        private async Task PopulateFATEList()
        {
            foreach (FATE f in GameResources.GetFates().DistinctBy(x => x.Name(true)))
            {
                await OtherFATEsCheckComboBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var c = new CheckBox() { Content = f.Name(true) };
                    c.IsChecked = Settings.Default.FATEs.Contains(f.ID.ToString());
                    c.Unchecked += C_Unchecked;
                    c.Checked += C_Checked;
                    OtherFATEsCheckComboBox.Items.Add(c);
                }));
            }
        }

        private void C_Checked(object sender, RoutedEventArgs e)
        {
            Settings.Default.FATEs.Add(GameResources.GetFateId(((CheckBox)sender).Content.ToString()).ToString());
        }

        private void C_Unchecked(object sender, RoutedEventArgs e)
        {
            Settings.Default.FATEs.Remove(GameResources.GetFateId(((CheckBox)sender).Content.ToString()).ToString());
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && Settings.Default.MinimizeToTray)
            {
                ni.Visible = true;
                Hide();
            }
            base.OnStateChanged(e);
        }

        private void CheckSoundStartup()
        {
            var slist = new List<string> { Settings.Default.SBell, Settings.Default.ABell, Settings.Default.BBell, Settings.Default.FATEBell };
            for (int i = 0; i < 4; i++)
            {
                if (!string.IsNullOrEmpty(slist[i]) && !slist[i].Equals(Properties.Resources.NoSoundAlert) && File.Exists(slist[i]) && Path.GetExtension(slist[i]).ToLower().Equals(".wav"))
                {
                    switch (i)
                    {
                        case 0:
                            SetAlarmSound(SBell, slist[0]);
                            break;
                        case 1:
                            SetAlarmSound(ABell, slist[1]);
                            break;
                        case 2:
                            SetAlarmSound(BBell, slist[2]);
                            break;
                        case 3:
                            SetAlarmSound(FATEBell, slist[3]);
                            break;
                        default:
                            continue;
                    }
                }
                else
                {
                    switch (i)
                    {
                        case 0:
                            UnsetAlarmSound(SBell);
                            break;
                        case 1:
                            UnsetAlarmSound(ABell);
                            break;
                        case 2:
                            UnsetAlarmSound(BBell);
                            break;
                        case 3:
                            UnsetAlarmSound(FATEBell);
                            break;
                        default:
                            continue;
                    }
                }
            }
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is CheckBox text)
            {
                if (text.Content.ToString().IndexOf(OtherFATEsCheckComboBox.Text, StringComparison.CurrentCultureIgnoreCase) != -1)
                {
                    return true;
                }
                return false;
            }
            else
            {
                return false;
            }
        }

        private void DispatcherTimer1s_Tick(object sender, EventArgs e)
        {
            try
            {
                if (ProcessComboBox.SelectedIndex < 0)
                    ProcessComboBox.SelectedIndex = 0;
                int? psv = (int?)ProcessComboBox.SelectedValue;
                pvm.Refresh();
                if (psv > 0)
                    ProcessComboBox.SelectedValue = psv;
                if (ProcessComboBox.Items.Count > 1)
                    ProcessComboBox.IsEnabled = true;
                else
                {
                    ProcessComboBox.IsEnabled = false;
                }
            }
            catch (Exception) { }

#if DEBUG
            if (AnyProblems())
                return;
            HuntAndChatCheck();
#else
            try
            {
                if (AnyProblems())
                    return;
                HuntAndChatCheck();
                dispatcherTimer1s.Interval = TimeSpan.FromSeconds(1);
            }
            catch (Exception ex)
            {
                if (ex is MemoryScanException && dispatcherTimer1s.Interval.TotalSeconds < 5)
                    dispatcherTimer1s.Interval = TimeSpan.FromSeconds(dispatcherTimer1s.Interval.TotalSeconds + 1);
                Debug.WriteLine("Interval: " + dispatcherTimer1s.Interval.TotalSeconds + "s");
                //Program.WriteExceptionToErrorFile(ex);
            }
#endif
        }

        private bool AnyProblems()
        {
            if (Program.mem == null || !Program.mem.ValidateProcess() || Program.mem.GetSelfCombatant() == null || Program.mem.Process.Id != (int?)ProcessComboBox.SelectedValue)
            {
                HuntConnectionTextBlock.Text = string.Format(Properties.Resources.FormNoProcess, (Environment.Is64BitProcess) ? "ffxiv_dx11.exe" : "ffxiv.exe");
                if (ProcessComboBox.SelectedValue != null && FFXIVProcessHelper.GetFFXIVProcess((int)ProcessComboBox.SelectedValue) != null)
                {
                    if (Program.mem != null)
                        Program.mem.Dispose();
                    Program.mem = null;
                    Program.mem = new FFXIVMemory(FFXIVProcessHelper.GetFFXIVProcess((int)ProcessComboBox.SelectedValue));
                    PersistentNamedPipeServer.Restart();
                }
                FFXIVHunts.LeaveGroup();
                HuntNotifyGroupBox.IsEnabled = false;
                return true;
            }
            else
            {
                if (hunts == null && Program.mem != null && Program.mem.ValidateProcess())
                    hunts = new FFXIVHunts();
                hunts.Connect();
                HuntNotifyGroupBox.IsEnabled = true;
            }
            return false;
        }

        private void HuntAndChatCheck()
        {
            if (hunts != null)
            {
                hunts.Check(Program.mem);
                //User commands
                string LastCommand = Program.mem.GetLastFailedCommand();
                if (LastCommand.StartsWith("/hunt "))
                {
                    string huntSearchTerm = LastCommand.Substring(6);
                    if (GameResources.IsDailyHunt(huntSearchTerm, out ushort id))
                    {
                        Tuple<ushort, float, float> huntInfo = GameResources.GetDailyHuntInfo(id);
                        _ = Program.mem.WriteChatMessage(ChatMessage.MakePosChatMessage(string.Format(Properties.Resources.LKICanBeFoundAt, GameResources.GetEnemyName(id, true)), huntInfo.Item1, huntInfo.Item2, huntInfo.Item3));
                        Program.mem.WipeLastFailedCommand();
                    }
                    else if (hunts.hunts.Exists(x => x.Name.Equals(huntSearchTerm, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        _ = hunts.LastKnownInfoForHunt(hunts.hunts.First(x => x.Name.Equals(huntSearchTerm, StringComparison.CurrentCultureIgnoreCase)).Id);
                        Program.mem.WipeLastFailedCommand();
                    }
                    else if (GameResources.GetEnemyId(huntSearchTerm, out ushort bnpcid))
                    {
                        _ = hunts.RandomPositionForBNpc(bnpcid);
                        Program.mem.WipeLastFailedCommand();
                    }
                    else if(GameResources.GetFateId(huntSearchTerm, true) > 0)
                    {
                        _ = hunts.LastKnownInfoForFATE(GameResources.GetFateId(huntSearchTerm, true));
                        if (Settings.Default.TrackFATEAfterQuery)
                        {
                            var c = OtherFATEsCheckComboBox.Items.Cast<CheckBox>().Single(x => ((string)x.Content).Equals(huntSearchTerm, StringComparison.CurrentCultureIgnoreCase));
                            if (c != null)
                                c.IsChecked = true;
                            OtherFATEsCheckComboBox_ItemSelectionChanged(null, null);
                        }
                        Program.mem.WipeLastFailedCommand();
                    }
                    else
                    {
                        FFXIVHunts.LookupItemXIVDB(huntSearchTerm).ContinueWith(t =>
                        {
                            if (t.Result != null)
                            {
                                _ = Program.mem.WriteChatMessage(ChatMessage.MakeItemChatMessage(t.Result));
                            }
                        });
                        Program.mem.WipeLastFailedCommand();
                    }
                }
                if (LastCommand.StartsWith("/perform "))
                {
                    string request = LastCommand.Substring(9);
                    string nametxt = request.Trim();
                    if (!nametxt.EndsWith(".txt"))
                        nametxt += ".txt";
                    string namemml = request.Trim();
                    if (!namemml.EndsWith(".mml"))
                        namemml += ".mml";
                    string pathnametxt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nametxt);
                    string pathnamemml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, namemml);
                    if (File.Exists(pathnametxt))
                    {
                        StopPerformance();
                        var p = new Performance(string.Join(",", File.ReadAllLines(pathnametxt)));
                        if (p.Sheet.Count > 0)
                            _ = Program.mem.PlayPerformance(p, cts.Token);
                        else
                            TryMML(pathnametxt);
                        Program.mem.WipeLastFailedCommand();
                    }
                    else if(File.Exists(pathnamemml))
                    {
                        StopPerformance();
                        TryMML(pathnamemml);
                        Program.mem.WipeLastFailedCommand();
                    }
                }
                if (LastCommand.StartsWith("/performstop") && cts != null)
                {
                    cts.Cancel();
                    Program.mem.WipeLastFailedCommand();
                }
                if (LastCommand.StartsWith("/flag "))
                {
                    string[] coords = LastCommand.Substring(6).Split(',');
                    if (coords.Length > 1 && float.TryParse(coords[0], out float xR) && float.TryParse(coords[1], out float yR))
                    {
                        ushort zid = Program.mem.GetZoneId();
                        float x = Combatant.GetCoordFromReadable(xR, zid);
                        float y = Combatant.GetCoordFromReadable(yR, zid);
                        var cm = ChatMessage.MakePosChatMessage(string.Empty, zid, x, y);
                        _ = Program.mem.WriteChatMessage(cm);
                        Program.mem.WipeLastFailedCommand();
                    }
                }
                var cf = Program.mem.GetContentFinder();
                if (Settings.Default.notifyDutyRoulette && cf.State == ContentFinderState.Popped && cf.IsDutyRouletteQueued() && !WroteDRPop)//Pop and DR
                {
                    _ = Program.mem.WriteChatMessage(new ChatMessage { Message = Encoding.UTF8.GetBytes(string.Format(Properties.Resources.DutyRouletteResult, cf.InstanceContentName)) });
                    WroteDRPop = true;
                }
                else if (cf.State != ContentFinderState.Popped)
                    WroteDRPop = false;
            }
        }

        private void StopPerformance()
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
            cts = new CancellationTokenSource();
        }

        //Asumes file extension is already set to either .txt or .mml
        private void TryMML(string pathname)
        {
            var mml = new ImplementedPlayer();
            var mmls = File.ReadAllLines(pathname);
            for (int i = 0; i < mmls.Length; i++)
                mmls[i] = RemoveLineComments(mmls[i]);
            var fmml = RemoveBlockComments(string.Join(string.Empty, mmls));
            mml.Load(fmml);
            _ = Program.mem.PlayMML(mml, cts.Token);
        }

        private static string RemoveLineComments(string i)
        {
            string lineComments = "//";
            var p = i.IndexOf(lineComments);
            if (p > -1)
                return i.Substring(0, p);
            else
                return i;
        }

        private static string RemoveBlockComments(string i)
        {
            var blockComments = @"/\*(.*?)\*/";
            return Regex.Replace(i, blockComments, me => {
                if (me.Value.StartsWith("/*") || me.Value.StartsWith("//"))
                    return me.Value.StartsWith("//") ? Environment.NewLine : "";
                return me.Value;
            }, RegexOptions.Singleline);
        }

        private void MenuForm_FormClosed(object sender, EventArgs e)
        {
            dispatcherTimer1s.Stop();
            ni.Dispose();
            if (hunts != null)
            {
                FFXIVHunts.http.Dispose();
                hunts.Dispose();
            }
            if (Program.mem != null)
                Program.mem.Dispose();
            if (PersistentNamedPipeServer.Instance.IsConnected)
                PersistentNamedPipeServer.Instance.Disconnect();
            PersistentNamedPipeServer.Instance.Dispose();
            Settings.Default.Save();
            Environment.Exit(0);
        }

        private void HuntConnectionTextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((TextBlock)sender).Text.Contains("Disconnected"))
                hunts.Connect();
        }

        public void Dispose()
        {
            if (Ssp != null)
                Ssp.Dispose();
            if (Asp != null)
                Asp.Dispose();
            if (Bsp != null)
                Bsp.Dispose();
            if (FATEsp != null)
                FATEsp.Dispose();
            if (hunts != null)
                hunts.Dispose();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Title = Properties.Resources.FormSFDialogTitle,
                Filter = Properties.Resources.FormSFDialogFilter + " (*.wav)|*.wav",
                CheckFileExists = true,
                CheckPathExists = true
            };
            if (ofd.ShowDialog() == true)
            {
                if (!SetAlarmSound((CheckBox)sender, ofd.FileName))
                    ((CheckBox)sender).IsChecked = false;
            }
            else
                ((CheckBox)sender).IsChecked = false;
        }

        private bool SetAlarmSound(CheckBox r, string soundFile)
        {
            r.ToolTip = Path.GetFileName(soundFile);
            r.Opacity = 1;
            r.IsChecked = true;
            switch (r.Name)
            {
                case "SBell":
                    Ssp = new SoundPlayer(soundFile);
                    Settings.Default.SBell = soundFile;
                    return true;
                case "ABell":
                    Asp = new SoundPlayer(soundFile);
                    Settings.Default.ABell = soundFile;
                    return true;
                case "BBell":
                    Bsp = new SoundPlayer(soundFile);
                    Settings.Default.BBell = soundFile;
                    return true;
                case "FATEBell":
                    FATEsp = new SoundPlayer(soundFile);
                    Settings.Default.FATEBell = soundFile;
                    return true;
                default:
                    return false;
            }
        }

        private void UnsetAlarmSound(CheckBox r)
        {
            r.ToolTip = Properties.Resources.NoSoundAlert;
            r.Opacity = 0.25;
            r.IsChecked = false;
            switch (r.Name)
            {
                case "SBell":
                    if (Ssp != null) Ssp.Dispose();
                    Settings.Default.SBell = Properties.Resources.NoSoundAlert;
                    break;
                case "ABell":
                    if (Asp != null) Asp.Dispose();
                    Settings.Default.ABell = Properties.Resources.NoSoundAlert;
                    break;
                case "BBell":
                    if (Bsp != null) Bsp.Dispose();
                    Settings.Default.BBell = Properties.Resources.NoSoundAlert;
                    break;
                case "FATEBell":
                    if (FATEsp != null) FATEsp.Dispose();
                    Settings.Default.FATEBell = Properties.Resources.NoSoundAlert;
                    break;
                default:
                    return;
            }
        }

        private void SBell_Click(object sender, RoutedEventArgs e)
        {
            if (currentCMPlacement != null)
            {
                currentCMPlacement.IsChecked = false;
                currentCMPlacement = null;
            }
            if (((CheckBox)sender).Opacity == 1)
            {
                UnsetAlarmSound((CheckBox)sender);
            }
            else if (((CheckBox)sender).Opacity == 0.25)
            {
                ContextMenu cm = new ContextMenu();
                var mi1 = new MenuItem() { Header = Properties.Resources.FormSFCMNewAlert };
                mi1.Click += MenuItemClickCallCheckBox;
                cm.Items.Add(mi1);
                if (Settings.Default.SBell != Properties.Resources.NoSoundAlert /*&& Ssp.IsLoadCompleted*/)
                {
                    var miS = new MenuItem() { Header = Settings.Default.SBell };
                    miS.Click += MenuItemSoundSelected;
                    cm.Items.Add(miS);
                }
                if (Settings.Default.ABell != Properties.Resources.NoSoundAlert /*&& Asp.IsLoadCompleted*/)
                {
                    var miA = new MenuItem() { Header = Settings.Default.ABell };
                    miA.Click += MenuItemSoundSelected;
                    cm.Items.Add(miA);
                }
                if (Settings.Default.BBell != Properties.Resources.NoSoundAlert /*&& Bsp.IsLoadCompleted*/)
                {
                    var miB = new MenuItem() { Header = Settings.Default.BBell };
                    miB.Click += MenuItemSoundSelected;
                    cm.Items.Add(miB);
                }
                if (Settings.Default.FATEBell != Properties.Resources.NoSoundAlert /*&& FATEsp.IsLoadCompleted*/)
                {
                    var miFATE = new MenuItem() { Header = Settings.Default.FATEBell };
                    miFATE.Click += MenuItemSoundSelected;
                    cm.Items.Add(miFATE);
                }
                //Remove duplicates
                for (int i = 0; i < cm.Items.Count; i++)
                {
                    for (int j = 0; j < cm.Items.Count; j++)
                        if (i != j && ((MenuItem)cm.Items[i]).Header.ToString() == ((MenuItem)cm.Items[j]).Header.ToString())
                            cm.Items.RemoveAt(j);
                }

                if (cm.Items.Count < 2)
                    CheckBox_Checked(sender, e);
                else
                {
                    cm.PlacementTarget = (CheckBox)sender;
                    currentCMPlacement = (CheckBox)sender;
                    cm.Closed += Cm_Closed;
                    ((CheckBox)sender).ContextMenu = cm;
                    ((CheckBox)sender).ContextMenu.IsOpen = true;
                }
            }
        }

        private void Cm_Closed(object sender, RoutedEventArgs e)
        {
            if (currentCMPlacement != null)
                currentCMPlacement.IsChecked = false;
            currentCMPlacement = null;
        }

        private void MenuItemSoundSelected(object sender, RoutedEventArgs e)
        {
            var cb = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget as CheckBox;
            SetAlarmSound(cb, ((MenuItem)sender).Header.ToString());
            currentCMPlacement = null;
        }

        private void MenuItemClickCallCheckBox(object sender, RoutedEventArgs e)
        {
            var cb = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget as CheckBox;
            CheckBox_Checked(cb, new RoutedEventArgs());

        }

        private void SCheck_Checked(object sender, RoutedEventArgs e)
        {
            SBell.IsEnabled = true;
            if (!Settings.Default.SARR && !Settings.Default.SHW && !Settings.Default.SSB)
                Settings.Default.SSB = Settings.Default.SHW = Settings.Default.SARR = true;
        }

        private void ACheck_Checked(object sender, RoutedEventArgs e)
        {
            ABell.IsEnabled = true;
            if (!Settings.Default.AARR && !Settings.Default.AHW && !Settings.Default.ASB)
                Settings.Default.ASB = Settings.Default.AHW = Settings.Default.AARR = true;
        }

        private void BCheck_Checked(object sender, RoutedEventArgs e)
        {
            BBell.IsEnabled = true;
            if (!Settings.Default.BARR && !Settings.Default.BHW && !Settings.Default.BSB)
                Settings.Default.BSB = Settings.Default.BHW = Settings.Default.BARR = true;
        }

        private void FATECheck_Checked(object sender, RoutedEventArgs e)
        {
            FATEBell.IsEnabled = true;
        }

        private void SCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            UnsetAlarmSound(SBell);
            SBell.IsChecked = SBell.IsEnabled = false;
        }

        private void ACheck_Unchecked(object sender, RoutedEventArgs e)
        {
            UnsetAlarmSound(ABell);
            ABell.IsChecked = ABell.IsEnabled = false;
        }

        private void BCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            UnsetAlarmSound(BBell);
            BBell.IsChecked = BBell.IsEnabled = false;
        }

        private void FATECheck_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in OtherFATEsCheckComboBox.Items)
                if ((bool)cb.IsChecked)
                    return;
            UnsetAlarmSound(FATEBell);
            FATEBell.IsChecked = FATEBell.IsEnabled = false;
        }

        private void FilterCheckBoxOpacityUp(object sender, RoutedEventArgs e)
        {
            ((CheckBox)sender).Opacity = 1f;
        }

        private void FilterCheckBoxOpacityDown(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).Name.StartsWith("S") && (bool)!SARR.IsChecked && (bool)!SHW.IsChecked && (bool)!SSB.IsChecked)
                Settings.Default.notifyS = false;
            else if (((CheckBox)sender).Name.StartsWith("A") && (bool)!AARR.IsChecked && (bool)!AHW.IsChecked && (bool)!ASB.IsChecked)
                Settings.Default.notifyA = false;
            else if (((CheckBox)sender).Name.StartsWith("B") && (bool)!BARR.IsChecked && (bool)!BHW.IsChecked && (bool)!BSB.IsChecked)
                Settings.Default.notifyB = false;
            ((CheckBox)sender).Opacity = 0.35f;
        }

        private void UniformGrid_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (((UniformGrid)sender).IsEnabled)
                ((UniformGrid)sender).Opacity = 1f;
            else
                ((UniformGrid)sender).Opacity = 0.35f;
        }

        private void OtherFATEsCheckComboBox_ItemSelectionChanged(object sender, RoutedEventArgs e)
        {
            int selected = Settings.Default.FATEs.Count;
            if (selected == 1)
                SelectedFateCountTextBlock.Text = string.Format(Properties.Resources.FormFATESingle, selected);
            else
                SelectedFateCountTextBlock.Text = string.Format(Properties.Resources.FormFATEPlural, selected);
            if (selected == 0)
            {
                UnsetAlarmSound(FATEBell);
                FATEBell.IsChecked = FATEBell.IsEnabled = false;
                SelectedFateCountTextBlock.Text = string.Empty;
            }
            else if (selected > 0)
            {
                FATEBell.IsEnabled = true;
            }
        }

        private void OtherFATEsSearchTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter) || (e.Key == Key.Tab) || (e.Key == Key.Return))
            {
                OtherFATEsCheckComboBox.Items.Filter = null;
            }
            else if ((e.Key == Key.Down) || (e.Key == Key.Up))
            {
                OtherFATEsCheckComboBox.IsDropDownOpen = true;
            }
            else
            {
                OtherFATEsCheckComboBox.Items.Filter += FilterPredicate;
                OtherFATEsCheckComboBox.IsDropDownOpen = true;
            }
        }

        private void OtherFATEsCheckComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((ComboBox)sender).SelectedIndex = -1;
        }

        private void OtherFATEsCheckComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ((ComboBox)sender).IsDropDownOpen = true;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.Save();
            System.Windows.Forms.Application.Restart();
            Application.Current.Shutdown();
        }

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            if (SettingsWindow != null && SettingsWindow.IsVisible)
                SettingsWindow.Activate();
            else
            {
                SettingsWindow = new SettingsForm();
                SettingsWindow.Show();
            }

        }
    }

    public class ProcessViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Process> processes;
        public ProcessViewModel() => processes = new ObservableCollection<Process>(FFXIVProcessHelper.GetFFXIVProcessList());
        public void Refresh() => OnPropertyChanged("ProcessEntries");
        public ObservableCollection<Process> ProcessEntries => new ObservableCollection<Process>(FFXIVProcessHelper.GetFFXIVProcessList());
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
