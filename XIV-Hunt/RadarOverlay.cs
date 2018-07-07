using FFXIV_GameSense.Overlay;
using Overlay.NET.Common;
using Overlay.NET.Wpf;
using Process.NET.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OverlayWindow = Overlay.NET.Wpf.OverlayWindow;

namespace FFXIV_GameSense
{
    class RadarOverlay : WpfOverlayPlugin
    {
        // Used to limit update rates via timestamps 
        // This way we can avoid thread issues with wanting to delay updates
        private readonly TickEngine _tickEngine = new TickEngine();
        private DispatcherTimer dispatcher;
        private readonly CancellationToken ct;
        private IWindow _targetWindow;
        private bool _isDisposed;
        private bool _isSetup;
        private Point DragStart;
        private bool MouseDown = false;

        private readonly Dictionary<uint, EntityOverlayControl> drawMap = new Dictionary<uint, EntityOverlayControl>();
        private readonly Dictionary<float, EntityOverlayControl> miscDrawMap = new Dictionary<float, EntityOverlayControl>();
        private readonly List<uint> hoardsDiscovered = new List<uint>();

        public RadarOverlay(CancellationToken _ct)
        {
            ct = _ct;
        }

        public override void Enable()
        {
            _tickEngine.IsTicking = true;
            base.Enable();
        }

        internal void SetNewFrameRate()
        {
            dispatcher.Interval = _tickEngine.Interval = (1000 / Properties.Settings.Default.RadarMaxFrameRate).Milliseconds();
        }

        public override void Disable()
        {
            _tickEngine.IsTicking = false;
            base.Disable();
        }

        public override void Initialize(IWindow targetWindow)
        {
            // Set target window by calling the base method
            base.Initialize(targetWindow);
            _targetWindow = targetWindow;
            OverlayWindow = new OverlayWindow(targetWindow)
            {
                Title = GetType().Name,
                ShowInTaskbar = false,
            };
            
            OverlayWindow.MouseLeftButtonDown += OverlayWindow_MouseLeftButtonDown;
            OverlayWindow.MouseLeftButtonUp += OverlayWindow_MouseLeftButtonUp;
            OverlayWindow.MouseMove += OverlayWindow_MouseMove;
            OverlayWindow.SizeChanged += OverlayWindow_SizeChanged;
            //OverlayWindow.Background = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
            // Set up update interval and register events for the tick engine.
            _tickEngine.Interval = (1000 / Properties.Settings.Default.RadarMaxFrameRate).Milliseconds();
            _tickEngine.PreTick += OnPreTick;
            _tickEngine.Tick += OnTick;
            dispatcher = new DispatcherTimer();
            dispatcher.Tick += Dispatcher_Tick;
            dispatcher.Interval = _tickEngine.Interval;
            dispatcher.Start();
        }

        private void OverlayWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Properties.Settings.Default.RadarWindowSize = new System.Drawing.Size((int)e.NewSize.Width, (int)e.NewSize.Height);
            Properties.Settings.Default.Save();
        }

        private void Dispatcher_Tick(object sender, EventArgs e)
        {
            if(ct.IsCancellationRequested)
            {
                ((DispatcherTimer)sender).Stop();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            try
            {
                Update();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(RadarOverlay)}: {ex.ToString()}");
            }
        }

        private void OverlayWindow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if(MouseDown)
            {
                Point p = e.GetPosition(OverlayWindow);
                OverlayWindow.Left += p.X - DragStart.X;
                OverlayWindow.Top += p.Y - DragStart.Y;
            }
        }

        private void OverlayWindow_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (NativeMethods.GetWindowRect(_targetWindow.Handle, out NativeMethods.RECT trect))
            {
                Properties.Settings.Default.RadarWindowOffset = new System.Drawing.Point((int)(OverlayWindow.Left - trect.Left), (int)(OverlayWindow.Top - trect.Top));
                Properties.Settings.Default.Save();
            }
            MouseDown = false;
            OverlayWindow.ReleaseMouseCapture();
        }

        private void OverlayWindow_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MouseDown = true;
            DragStart = e.GetPosition(OverlayWindow);
            OverlayWindow.CaptureMouse();
        }

        void OnTick(object sender, EventArgs eventArgs)
        {
            // This will only be true if the target window is active
            // (or very recently has been, depends on your update rate)
            //if (OverlayWindow.IsVisible)
            //{
            //    OverlayWindow.Refresh();
            //}
        }

        void OnPreTick(object sender, EventArgs eventArgs)
        {
            // Only want to set them up once.
            if (!_isSetup)
            {
                SetUp();
                _isSetup = true;
            }

            // Ensure window is shown or hidden correctly prior to updating
            if ((TargetWindow.IsActivated && !OverlayWindow.IsVisible) || ApplicationIsActivated())
            {
                OverlayWindow.Show();
            }
            else if ((!TargetWindow.IsActivated && OverlayWindow.IsVisible))
            {
                OverlayWindow.Hide();
            }
        }

        /// <summary>Returns true if the current application has focus, false otherwise</summary>
        public static bool ApplicationIsActivated()
        {
            IntPtr activatedHandle = NativeMethods.GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
                return false;// No window is currently activated
            int procId = System.Diagnostics.Process.GetCurrentProcess().Id;
            NativeMethods.GetWindowThreadProcessId(activatedHandle, out int activeProcId);
            return activeProcId == procId;
        }

        internal void EnableResizeMode() => OverlayWindow.Dispatcher.Invoke(() => OverlayWindow.ResizeMode = ResizeMode.CanResizeWithGrip);

        internal void DisableResizeMode() => OverlayWindow.Dispatcher.Invoke(() => OverlayWindow.ResizeMode = ResizeMode.NoResize);

        internal void MakeClickable() => OverlayWindow.Dispatcher.Invoke(() => OverlayWindow?.MakeWindowUntransparent());

        internal void MakeClickthru() => OverlayWindow.Dispatcher.Invoke(() => OverlayWindow?.MakeWindowTransparent());

        public override void Update()
        {
            // Raises the events only when the given interval has
            // passed since the last event, so it is okay to call every frame
            _tickEngine.Pulse();
            if (!OverlayWindow.IsVisible)
                return;
            if(!MouseDown)
                FollowTargetWindow();
            Combatant self = Program.mem?.GetSelfCombatant();
            List<Combatant> clist = Program.mem?._getCombatantList();
            if (self != null && clist != null)
            {
                clist.RemoveAll(c => c.OwnerID == self.ID);
                foreach (uint ID in clist.Where(c => c.Type == ObjectType.PC).Select(x => x.ID).ToList())
                    clist.RemoveAll(c => c.OwnerID == ID);
                RemoveUnvantedCombatants(self, clist);

                //bool maximized = TargetWindow.Placement.ShowCmd == Process.NET.Native.Types.WindowStates.Maximize ? true : false;
                double centerY = OverlayWindow.Height / 2;
                double centerX = OverlayWindow.Width / 2;
                foreach (Combatant c in clist.Where(c => !c.Name.Equals("Trap")))
                {                               //ridiculous posx+posy as key, no idea what else to use
                    if (c.ID == 3758096384 && !miscDrawMap.ContainsKey(c.PosX + c.PosY))//for aetherytes, npcs, and other stuff;
                    {
                        miscDrawMap.Add(c.PosX + c.PosY, new EntityOverlayControl(c));
                        OverlayWindow.Add(miscDrawMap[c.PosX + c.PosY]);
                    }
                    else if (!drawMap.ContainsKey(c.ID))
                    {
                        drawMap.Add(c.ID, new EntityOverlayControl(c));
                        OverlayWindow.Add(drawMap[c.ID]);
                    }

                    //factors ?
                    double relativeToCenterY = (c.PosY - self.PosY) * 5;
                    double relativeToCenterX = (c.PosX - self.PosX) * 8.5;
                    if (drawMap.TryGetValue(self.ID, out EntityOverlayControl selfctrl))
                    {
                        relativeToCenterY += selfctrl.ActualHeight / 2;
                        relativeToCenterX -= selfctrl.ActualWidth / 2;
                    }
                    if (drawMap.ContainsKey(c.ID))
                    {
                        drawMap[c.ID].Update(c);
                        if (centerY + relativeToCenterY < 0)
                            Canvas.SetTop(drawMap[c.ID], 0);
                        else if (centerY + relativeToCenterY > OverlayWindow.Height - drawMap[c.ID].ActualHeight)
                            Canvas.SetTop(drawMap[c.ID], OverlayWindow.Height - drawMap[c.ID].ActualHeight);
                        else
                            Canvas.SetTop(drawMap[c.ID], centerY + relativeToCenterY);

                        if (centerX + relativeToCenterX < 0)
                            Canvas.SetLeft(drawMap[c.ID], 0);
                        else if (centerX + relativeToCenterX > OverlayWindow.Width - drawMap[c.ID].ActualWidth)
                            Canvas.SetLeft(drawMap[c.ID], OverlayWindow.Width - drawMap[c.ID].ActualWidth);
                        else
                            Canvas.SetLeft(drawMap[c.ID], centerX + relativeToCenterX);
                    }
                    else if (miscDrawMap.ContainsKey(c.PosX + c.PosY))
                    {
                        miscDrawMap[c.PosX + c.PosY].Update(c);
                        Canvas.SetTop(miscDrawMap[c.PosX + c.PosY], centerY + relativeToCenterY);
                        Canvas.SetLeft(miscDrawMap[c.PosX + c.PosY], centerX + relativeToCenterX);
                    }
                }
            }
            
            //cleanup
            foreach (KeyValuePair<uint, EntityOverlayControl> entry in drawMap.ToArray())
            {
                //Hide hoard/cairn, after Banded Coffer appeared... hmm Hoard will re-appear if going out of "sight" and in again
                if (entry.Value.GetName().Equals("Hoard!") && (clist?.Any(c => c.EventType == EventType.Banded) ?? false))
                {
                    entry.Value.Visibility = Visibility.Collapsed;
                    if (!hoardsDiscovered.Contains(entry.Key))
                        hoardsDiscovered.Add(entry.Key);
                    continue;
                }

                if (clist?.Exists(c => c.ID == entry.Key) ?? false)
                    continue;
                else
                {
                    entry.Value.Visibility = Visibility.Collapsed;
                    drawMap.Remove(entry.Key);
                }
            }
            foreach (KeyValuePair<float, EntityOverlayControl> entry in miscDrawMap.ToArray())
            {
                if (clist?.Exists(c => c.PosX + c.PosY == entry.Key) ?? false)
                    continue;
                else
                {
                    entry.Value.Visibility = Visibility.Collapsed;
                    miscDrawMap.Remove(entry.Key);
                }
            }
        }

        private void RemoveUnvantedCombatants(Combatant self, List<Combatant> clist)
        {
            if (!Properties.Settings.Default.displaySelf)
                clist.RemoveAll(c => c.ID == self.ID);
            if (!Properties.Settings.Default.displayMonsters)
                clist.RemoveAll(c => c.Type == ObjectType.Monster);
            if (!Properties.Settings.Default.displayTreasureCoffers)
                clist.RemoveAll(c => c.Type == ObjectType.Treasure);
            if (!Properties.Settings.Default.displayCairns)
                clist.RemoveAll(c => c.Type == ObjectType.EventObject && c.EventType != EventType.Silver && c.EventType != EventType.Gold);
            if (!Properties.Settings.Default.displayOtherPCs)
                clist.RemoveAll(c => c.Type == ObjectType.PC && c.ID != self.ID);
            if (!Properties.Settings.Default.displaySilverTreasureCoffers)
                clist.RemoveAll(c => c.Type == ObjectType.EventObject && c.EventType == EventType.Silver);
            if (!Properties.Settings.Default.displayGoldTreasureCoffers)
                clist.RemoveAll(c => c.Type == ObjectType.EventObject && c.EventType == EventType.Gold);
            if (clist.Any(c => c.Type == ObjectType.EventObject && c.EventType == EventType.Hoard && hoardsDiscovered.Contains(c.ID)))
                clist.RemoveAll(c => c.ID == hoardsDiscovered.Last());
        }

        private void FollowTargetWindow()
        {
            if (NativeMethods.GetWindowRect(_targetWindow.Handle, out NativeMethods.RECT trect))
            {
                OverlayWindow.Left = trect.Left + Properties.Settings.Default.RadarWindowOffset.X;
                OverlayWindow.Top = trect.Top + Properties.Settings.Default.RadarWindowOffset.Y;
                if(Properties.Settings.Default.RadarWindowSize.IsEmpty)
                {
                    OverlayWindow.Width = (trect.Right - trect.Left);
                    OverlayWindow.Height = (trect.Bottom - trect.Top);
                }
                else
                {
                    OverlayWindow.Width = Properties.Settings.Default.RadarWindowSize.Width;
                    OverlayWindow.Height = Properties.Settings.Default.RadarWindowSize.Height;
                }
            }
        }

        // Clear objects
        public override sealed void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (IsEnabled)
            {
                Disable();
            }

            //try
            //{
            //    OverlayWindow?.Dispatcher.Invoke(() => OverlayWindow?.Hide());
            //    OverlayWindow?.Dispatcher.Invoke(() => OverlayWindow?.Close());
            //}
            //catch (TaskCanceledException)
            //{
            //    Debug.WriteLine("Dispatcher Invoker canceled");
            //}
            OverlayWindow = null;
            _tickEngine.Stop();

            base.Dispose();
            _isDisposed = true;
        }

        ~RadarOverlay()
        {
            Dispose();
        }

        void SetUp()
        {
            if (!Properties.Settings.Default.RadarDisableResize)
                OverlayWindow.ResizeMode = ResizeMode.CanResizeWithGrip;
            if (!Properties.Settings.Default.RadarWindowSize.IsEmpty)
            {
                OverlayWindow.Width = Properties.Settings.Default.RadarWindowSize.Width;
                OverlayWindow.Height = Properties.Settings.Default.RadarWindowSize.Height;
            }
        }
    }
}
