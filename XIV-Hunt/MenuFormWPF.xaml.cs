using FFXIV_GameSense.MML;
using FFXIV_GameSense.Properties;
using FFXIV_GameSense.UI;
using Microsoft.Win32;
using NAudio.Wave;
using Newtonsoft.Json;
using Splat;
using Squirrel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using XIVAPI;
using XIVDB;

namespace FFXIV_GameSense
{
    public partial class Window1 : Window, IDisposable
    {
        private DispatcherTimer dispatcherTimer1s;
        private FFXIVHunts hunts;
        internal Dictionary<HuntRank, MediaFoundationReader> sounds = new Dictionary<HuntRank, MediaFoundationReader>();
        private AlarmButton currentCMPlacement;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private ViewModel vm;
        private static SettingsForm SettingsWindow;
        private static LogView LogView = new LogView();
        private CancellationTokenSource cts;
        private static bool WroteDRPop = false;
        private static bool IconIsFlashing = false;

        public Window1()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(Settings.Default.LanguageCI);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(Settings.Default.LanguageCI);
            InitializeComponent();
            Logger logger = new Logger(LogView);
            Locator.CurrentMutable.RegisterConstant(logger, typeof(ILogger));
            Title = Program.AssemblyName.Name + " " + Program.AssemblyName.Version.ToString(3) + " - " + (Environment.Is64BitProcess ? 64 : 32) + "-Bit";
            vm = new ViewModel();
            Closed += MenuForm_FormClosed;
            Locator.CurrentMutable.RegisterLazySingleton(() => new XIVAPIClient());
            dispatcherTimer1s = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            dispatcherTimer1s.Tick += DispatcherTimer1s_Tick;
            dispatcherTimer1s.Start();
            CheckSoundStartup();
            trayIcon = new System.Windows.Forms.NotifyIcon()
            {
                Icon = Properties.Resources.enemy,
                Visible = false,
                Text = Title
            };
            trayIcon.Click += delegate (object sender, EventArgs args)
                {
                    Show();
                    Visibility = Visibility.Visible;
                    WindowState = WindowState.Normal;
                    trayIcon.Visible = false;
                };
            DataContext = vm;
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
            if (Settings.Default.StartMinimized)
            {
                WindowState = WindowState.Minimized;
                if (Settings.Default.MinimizeToTray)
                {
                    HideWindowAndShowTrayIcon();
                    base.OnStateChanged(EventArgs.Empty);
                }
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            HideWindowAndShowTrayIcon();
            base.OnStateChanged(e);
        }

        private void HideWindowAndShowTrayIcon()
        {
            if (WindowState == WindowState.Minimized && Settings.Default.MinimizeToTray)
            {
                trayIcon.Visible = true;
                Visibility = Visibility.Hidden;
                Hide();//necessary?
            }
        }

        private void CheckSoundStartup()
        {
            var slist = new string[] { Settings.Default.SBell, Settings.Default.ABell, Settings.Default.BBell, Settings.Default.FATEBell };
            for (int i = 0; i < slist.Length; i++)
            {
                if (!string.IsNullOrEmpty(slist[i]) && !slist[i].Equals(Properties.Resources.NoSoundAlert) && File.Exists(slist[i]))
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

        private void DispatcherTimer1s_Tick(object sender, EventArgs e)
        {
            if (ProcessComboBox.SelectedIndex < 0)
                ProcessComboBox.SelectedIndex = 0;
            int? psv = (int?)ProcessComboBox.SelectedValue;
            vm.Refresh();
            if (psv > 0)
                ProcessComboBox.SelectedValue = psv;
            if (ProcessComboBox.Items.Count > 1)
                ProcessComboBox.IsEnabled = true;
            else
            {
                ProcessComboBox.IsEnabled = false;
            }
#if DEBUG
            if (AnyProblems())
                return;
            HuntAndCFCheck();
#else
            try
            {
                if (AnyProblems())
                    return;
                HuntAndCFCheck();
                dispatcherTimer1s.Interval = TimeSpan.FromSeconds(1);
            }
            catch (Exception ex)
            {
                if (ex is FFXIVMemory.MemoryScanException && dispatcherTimer1s.Interval.TotalSeconds < 5)
                    dispatcherTimer1s.Interval = TimeSpan.FromSeconds(dispatcherTimer1s.Interval.TotalSeconds + 1);
                if (ex is FFXIVMemory.MemoryScanException)
                    LogHost.Default.InfoException(nameof(FFXIVMemory.MemoryScanException), ex);
                else if (ex is Win32Exception)
                    LogHost.Default.ErrorException("Could not read process memory. Possibly lacking privileges.", ex);
                else
                    LogHost.Default.WarnException("Unknown exception", ex);
            }
#endif
        }

        private bool AnyProblems()
        {
            if (Program.mem == null || !Program.mem.ValidateProcess() || Program.mem.GetSelfCombatant() == null || Program.mem.Process.Id != (int?)ProcessComboBox.SelectedValue)
            {
                HuntConnectionTextBlock.Text = string.Format(Properties.Resources.FormNoProcess, FFXIVProcessHelper.DX9ExeName + (Environment.Is64BitProcess ? "/"+FFXIVProcessHelper.DX11ExeName : string.Empty));
                if (ProcessComboBox.SelectedValue != null && FFXIVProcessHelper.GetFFXIVProcess((int)ProcessComboBox.SelectedValue) != null)
                {
                    if (Program.mem != null)
                    {
                        Program.mem.OnNewCommand -= ProcessChatCommand;
                        Program.mem.Dispose();
                        hunts?.LeaveGroup();
                    }
                    Program.mem = null;
                    Program.mem = new FFXIVMemory(FFXIVProcessHelper.GetFFXIVProcess((int)ProcessComboBox.SelectedValue));
                    Program.mem.OnNewCommand += ProcessChatCommand;
                    PersistentNamedPipeServer.Restart();
                }
                hunts?.LeaveGroup();
                HuntNotifyGroupBox.IsEnabled = false;
                return true;
            }
            else
            {
                if (hunts == null && Program.mem != null && Program.mem.ValidateProcess())
                    hunts = new FFXIVHunts(this);
                HuntNotifyGroupBox.IsEnabled = true;
            }
            return false;
        }

        internal void ProcessChatCommand(object sender, CommandEventArgs e)
        {
            LogHost.Default.Info($"[{nameof(ProcessChatCommand)}] New command: {e?.ToString()}");
            FFXIVMemory mem = sender is FFXIVMemory ? sender as FFXIVMemory : Program.mem;
            if (mem == null)
                return;
            if (e.Command == Command.Hunt && hunts != null)
            {
                if (GameResources.TryGetDailyHuntInfo(e.Parameter, out Tuple<ushort, ushort, float, float> hi))
                {
                    _ = mem.WriteChatMessage(ChatMessage.MakePosChatMessage(string.Format(Properties.Resources.LKICanBeFoundAt, GameResources.GetEnemyName(hi.Item1, true)), hi.Item2, hi.Item3, hi.Item4));
                }
                else if (hunts.hunts.Exists(x => x.Name.Equals(e.Parameter, StringComparison.OrdinalIgnoreCase)))
                {
                    _ = hunts.LastKnownInfoForHunt(hunts.hunts.First(x => x.Name.Equals(e.Parameter, StringComparison.OrdinalIgnoreCase)).Id);
                }
                else if (GameResources.GetEnemyId(e.Parameter, out ushort bnpcid))
                {
                    _ = hunts.RandomPositionForBNpc(bnpcid);
                }
                ushort fid = GameResources.GetFateId(e.Parameter, true);
                if (fid > 0)
                {
                    _ = hunts.LastKnownInfoForFATE(fid);
                    if (Settings.Default.TrackFATEAfterQuery)
                    {
                        vm.FATEEntries.SingleOrDefault(x => x.ID == fid).Announce = true;
                    }
                }
                else if (Enum.TryParse(e.Parameter.Split(' ').Last(), out HuntRank hr) && hr != HuntRank.FATE && GameResources.TryGetZoneID(e.Parameter.Substring(0, e.Parameter.Length - 2).Trim(), out ushort ZoneID) && FFXIVHunts.MapHunts.ContainsKey(ZoneID))
                {
                    foreach (ushort hid in FFXIVHunts.MapHunts[ZoneID].Where(x => hunts.HuntRankFor(x) == hr))
                        _ = hunts.LastKnownInfoForHunt(hid);
                }
                else if(!string.IsNullOrWhiteSpace(e.Parameter))
                {
                    string[] pwords = e.Parameter.Split(' ');
                    bool hqprefer = pwords.Last().Equals("HQ", StringComparison.OrdinalIgnoreCase);
                    Task.Factory.StartNew(async ()=> {
                        Item item = await Locator.CurrentMutable.GetService<XIVAPIClient>().SearchForItem(hqprefer ? string.Join(" ", pwords.Take(pwords.Length - 1)) : e.Parameter, hqprefer);
                        if(item != null)
                            await mem.WriteChatMessage(ChatMessage.MakeItemChatMessage(item, HQ: hqprefer));
                    });
                }
            }
            else if (e.Command == Command.Perform)
            {
                if (!Directory.Exists(Settings.Default.PerformDirectory))
                {
                    LogHost.Default.Error(Properties.Resources.PerformDirectoryNotExists);
                    _ = mem.WriteChatMessage(new ChatMessage { MessageString = Properties.Resources.PerformDirectoryNotExists });
                    return;
                }
                string nametxt = e.Parameter;
                if (!nametxt.EndsWith(".txt"))
                    nametxt += ".txt";
                string namemml = e.Parameter;
                if (!namemml.EndsWith(".mml"))
                    namemml += ".mml";
                string pathnametxt = Path.Combine(Settings.Default.PerformDirectory, nametxt);
                string pathnamemml = Path.Combine(Settings.Default.PerformDirectory, namemml);
                if (File.Exists(pathnametxt))
                {
                    StopPerformance();
                    var p = new Performance(string.Join(",", File.ReadAllLines(pathnametxt)));
                    if (p.Sheet.Count > 0)
                        _ = mem.PlayPerformance(p, cts.Token);
                    else
                        TryMML(pathnametxt);
                }
                else if (File.Exists(pathnamemml))
                {
                    StopPerformance();
                    TryMML(pathnamemml);
                }
                else
                    LogHost.Default.Error("Neither of these files were found:" + Environment.NewLine + pathnametxt + Environment.NewLine + pathnamemml);
            }
            else if (e.Command == Command.PerformStop && cts != null)
            {
                cts.Cancel();
            }
            else if (e.Command == Command.Flag)
            {
                string[] coords = e.Parameter.Split(',');
                if (coords.Length > 1 && float.TryParse(coords[0], out float xR) && float.TryParse(coords[1], out float yR))
                {
                    ushort zid = Program.mem.GetZoneId();
                    float x = Entity.GetCoordFromReadable(xR, zid);
                    float y = Entity.GetCoordFromReadable(yR, zid);
                    var cm = ChatMessage.MakePosChatMessage(string.Empty, zid, x, y);
                    _ = mem.WriteChatMessage(cm);
                }
            }
        }

        private void HuntAndCFCheck()
        {
            if (hunts != null)
            {
                hunts.Check(Program.mem);
                var cf = Program.mem.GetContentFinder();
                if (Settings.Default.FlashTaskbarIconOnDFPop && cf.State == ContentFinderState.Popped && !IconIsFlashing)
                {
                    NativeMethods.FlashTaskbarIcon(Program.mem.Process, 45);
                    IconIsFlashing = true;
                }
                if (Settings.Default.notifyDutyRoulette && cf.State == ContentFinderState.Popped && cf.IsDutyRouletteQueued() && !WroteDRPop)//Pop and DR
                {
                    _ = Program.mem.WriteChatMessage(new ChatMessage { MessageString = string.Format(Properties.Resources.DutyRouletteResult, cf.InstanceContentName) });
                    WroteDRPop = true;
                }
                else if (cf.State != ContentFinderState.Popped)
                {
                    if (IconIsFlashing)
                        NativeMethods.StopFlashWindowEx(Program.mem.Process);
                    IconIsFlashing = WroteDRPop = false;
                }
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
                mmls[i] = mmls[i].RemoveLineComments();
            var fmml = string.Join(string.Empty, mmls).RemoveBlockComments();
            mml.Load(fmml);
            _ = Program.mem.PlayMML(mml, cts.Token);
        }

        private void MenuForm_FormClosed(object sender, EventArgs e)
        {
            OverlayView.Dispose();
            Dispose();
            Environment.Exit(0);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(disposing)
            {
                dispatcherTimer1s.Stop();
                foreach (KeyValuePair<HuntRank, MediaFoundationReader> s in sounds)
                    if (s.Value != null)
                        s.Value.Dispose();
                if (hunts != null)
                    hunts.Dispose();
                sounds.Clear();
                if (cts!=null)
                    cts.Dispose();
                cts = null;

                trayIcon.Dispose();
                if (hunts != null)
                {
                    FFXIVHunts.Http.Dispose();
                    hunts.Dispose();
                }
                if (Program.mem != null)
                    Program.mem.Dispose();
                if (PersistentNamedPipeServer.Instance.IsConnected)
                    PersistentNamedPipeServer.Instance.Disconnect();
                PersistentNamedPipeServer.Instance.Dispose();
                Settings.Default.Save();
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Title = Properties.Resources.FormSFDialogTitle,
                Filter = Properties.Resources.FormSFDialogFilter + "|*.mp3;*.wav;*.wma;*.aiff;*.aac",
                CheckFileExists = true,
                CheckPathExists = true
            };
            if (ofd.ShowDialog() == true)
            {
                if (!SetAlarmSound((AlarmButton)sender, ofd.FileName))
                    ((AlarmButton)sender).SetOff();
            }
            else
                ((AlarmButton)sender).SetOff();
        }

        private bool SetAlarmSound(AlarmButton r, string soundFile)
        {
            r.ToolTip = Path.GetFileName(soundFile);
            r.SetOn();
            switch (r.Name)
            {
                case "SBell":
                    sounds[HuntRank.S] = new MediaFoundationReader(soundFile);
                    Settings.Default.SBell = soundFile;
                    return true;
                case "ABell":
                    sounds[HuntRank.A] = new MediaFoundationReader(soundFile);
                    Settings.Default.ABell = soundFile;
                    return true;
                case "BBell":
                    sounds[HuntRank.B] = new MediaFoundationReader(soundFile);
                    Settings.Default.BBell = soundFile;
                    return true;
                case "FATEBell":
                    sounds[HuntRank.FATE] = new MediaFoundationReader(soundFile);
                    Settings.Default.FATEBell = soundFile;
                    return true;
                default:
                    return false;
            }
        }

        private void UnsetAlarmSound(AlarmButton r)
        {
            r.ToolTip = Properties.Resources.NoSoundAlert;
            r.SetOff();
            MediaFoundationReader s;
            switch (r.Name)
            {
                case "SBell":
                    if (sounds.TryGetValue(HuntRank.S, out s))
                        sounds.Remove(HuntRank.S);
                    Settings.Default.SBell = Properties.Resources.NoSoundAlert;
                    break;
                case "ABell":
                    if (sounds.TryGetValue(HuntRank.A, out s))
                        sounds.Remove(HuntRank.A);
                    Settings.Default.ABell = Properties.Resources.NoSoundAlert;
                    break;
                case "BBell":
                    if (sounds.TryGetValue(HuntRank.B, out s))
                        sounds.Remove(HuntRank.B);
                    Settings.Default.BBell = Properties.Resources.NoSoundAlert;
                    break;
                case "FATEBell":
                    if (sounds.TryGetValue(HuntRank.FATE, out s))
                        sounds.Remove(HuntRank.FATE);
                    Settings.Default.FATEBell = Properties.Resources.NoSoundAlert;
                    break;
                default:
                    return;
            }
            if(s != null)
            {
                s.Position = s.Length;
                s.Dispose();
            }
        }

        private void SBell_Click(object sender, RoutedEventArgs e)
        {
            if (currentCMPlacement != null)
            {
                currentCMPlacement.SetOff();
                currentCMPlacement = null;
            }
            if (((AlarmButton)sender).IsOn())
            {
                UnsetAlarmSound((AlarmButton)sender);
            }
            else if (((AlarmButton)sender).IsEnabled)
            {
                ContextMenu cm = new ContextMenu();
                var mi1 = new MenuItem { Header = Properties.Resources.FormSFCMNewAlert };
                mi1.Click += MenuItemClickCallCheckBox;
                cm.Items.Add(mi1);
                if (Settings.Default.SBell != Properties.Resources.NoSoundAlert)
                {
                    var miS = new MenuItem { Header = Settings.Default.SBell };
                    miS.Click += MenuItemSoundSelected;
                    cm.Items.Add(miS);
                }
                if (Settings.Default.ABell != Properties.Resources.NoSoundAlert)
                {
                    var miA = new MenuItem { Header = Settings.Default.ABell };
                    miA.Click += MenuItemSoundSelected;
                    cm.Items.Add(miA);
                }
                if (Settings.Default.BBell != Properties.Resources.NoSoundAlert)
                {
                    var miB = new MenuItem { Header = Settings.Default.BBell };
                    miB.Click += MenuItemSoundSelected;
                    cm.Items.Add(miB);
                }
                if (Settings.Default.FATEBell != Properties.Resources.NoSoundAlert)
                {
                    var miFATE = new MenuItem { Header = Settings.Default.FATEBell };
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
                    cm.PlacementTarget = currentCMPlacement = (AlarmButton)sender;
                    cm.Closed += Cm_Closed;
                    ((AlarmButton)sender).ContextMenu = cm;
                    ((AlarmButton)sender).ContextMenu.IsOpen = true;
                }
            }
        }

        private void Cm_Closed(object sender, RoutedEventArgs e)
        {
            if (currentCMPlacement != null)
                currentCMPlacement.SetOff();
            currentCMPlacement = null;
        }

        private void MenuItemSoundSelected(object sender, RoutedEventArgs e)
        {
            var cb = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget as AlarmButton;
            SetAlarmSound(cb, ((MenuItem)sender).Header.ToString());
            currentCMPlacement = null;
        }

        private void MenuItemClickCallCheckBox(object sender, RoutedEventArgs e)
        {
            var cb = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget as AlarmButton;
            CheckBox_Checked(cb, new RoutedEventArgs());
        }

        private void SCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (!Settings.Default.SARR && !Settings.Default.SHW && !Settings.Default.SSB)
                Settings.Default.SSB = Settings.Default.SHW = Settings.Default.SARR = true;
        }

        private void ACheck_Checked(object sender, RoutedEventArgs e)
        {
            if (!Settings.Default.AARR && !Settings.Default.AHW && !Settings.Default.ASB)
                Settings.Default.ASB = Settings.Default.AHW = Settings.Default.AARR = true;
        }

        private void BCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (!Settings.Default.BARR && !Settings.Default.BHW && !Settings.Default.BSB)
                Settings.Default.BSB = Settings.Default.BHW = Settings.Default.BARR = true;
        }

        private void SCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            UnsetAlarmSound(SBell);
        }

        private void ACheck_Unchecked(object sender, RoutedEventArgs e)
        {
            UnsetAlarmSound(ABell);
        }

        private void BCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            UnsetAlarmSound(BBell);
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

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.Save();
            Updater.RestartApp();
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

        private void OpenLogViewer(object sender, RoutedEventArgs e)
        {
            if (LogView.IsVisible)
                LogView.Activate();
            else
                LogView.Show();
        }

        private void FATEBell_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is AlarmButton)
            {
                if (!(bool)e.NewValue && Settings.Default.FATEs.Count == 0)
                {
                    UnsetAlarmSound(((AlarmButton)sender));
                }
            }
        }

        private void FATEsListView_AllFATEsDeselected(object sender, EventArgs e) => FATEBell.IsEnabled = false;

        private void FATEsListView_FATESelected(object sender, EventArgs e) => FATEBell.IsEnabled = true;
    }

    public class ViewModel : INotifyPropertyChanged
    {
        private bool GotFATEZones = false;
        private bool IsFetchingZones = false;
        public ViewModel()
        {
            FATEEntries = new ObservableCollection<FATEListViewItem>();
            foreach (FATE f in GameResources.GetFates().DistinctBy(x => x.Name()))
                FATEEntries.Add(new FATEListViewItem(f));
        }

        public void Refresh()
        {
            OnPropertyChanged("ProcessEntries");
            if (FFXIVHunts.Joined && !GotFATEZones && !IsFetchingZones)
                _ = GetFATEZones();
        }

        public ObservableCollection<System.Diagnostics.Process> ProcessEntries => new ObservableCollection<System.Diagnostics.Process>(FFXIVProcessHelper.GetFFXIVProcessList());
        public ObservableCollection<FATEListViewItem> FATEEntries { get; }

        public bool FATEsAny => FATEEntries.Any(x => x.Announce);
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler PropertyChanged;

        private async Task GetFATEZones()
        {
            IsFetchingZones = true;
            string e;
            var r = await FFXIVHunts.Http.GetAsync(FFXIVHunts.baseUrl + "api/worlds/FATEIDZoneID/");
            if (r.IsSuccessStatusCode)
            {
                e = await r.Content.ReadAsStringAsync();
                var fateidzoneid = JsonConvert.DeserializeObject<Dictionary<ushort, ushort[]>>(e);
                await Task.Run(() =>
                {
                    foreach (FATEListViewItem i in FATEEntries.Where(x => fateidzoneid.ContainsKey(x.ID)))
                    {
                        i.Zones = string.Join(", ", fateidzoneid[i.ID].Distinct().Select(x => GameResources.GetZoneName(x)).Distinct());
                    }
                });
                GotFATEZones = true;
            }
            IsFetchingZones = false;
        }
    }
}
