using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Diagnostics;
using FFXIV_GameSense.Properties;
using System.Threading.Tasks;
using XIVDB;
using Microsoft.AspNet.SignalR.Client;
using System.Net.Http;
using System.Windows;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Documents;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace FFXIV_GameSense
{
    class FFXIVHunts : IDisposable
    {
        private static readonly Dictionary<ushort, List<ushort>> MapHunts = new Dictionary<ushort, List<ushort>>
        {
            { 134, new List<ushort>{ 2928,2945,2962 } },
            { 135, new List<ushort>{ 2929,2946,2963 } },
            { 137, new List<ushort>{ 2930,2947,2964 } },
            { 138, new List<ushort>{ 2931,2948,2965 } },
            { 139, new List<ushort>{ 2932,2949,2966 } },
            { 140, new List<ushort>{ 2923,2940,2957 } },
            { 141, new List<ushort>{ 2924,2941,2958 } },
            { 145, new List<ushort>{ 2925,2942,2959 } },
            { 146, new List<ushort>{ 2926,2943,2960 } },
            { 147, new List<ushort>{ 2927,2944,2961 } },//Northern Thanalan
            { 148, new List<ushort>{ 2919,2936,2953 } },
            { 152, new List<ushort>{ 2920,2937,2954 } },//East Shroud
            { 153, new List<ushort>{ 2921,2938,2955 } },
            { 154, new List<ushort>{ 2922,2939,2956 } },
            { 155, new List<ushort>{ 2934,2951,2968 } },//Coerthas Central Highlands
            { 156, new List<ushort>{ 2935,2952,2969 } },
            { 180, new List<ushort>{ 2933,2950,2967 } },
            { 397, new List<ushort>{ 4350,4351,4362,4363,4374 } },
            { 398, new List<ushort>{ 4352,4353,4364,4365,4375 } },//The Dravanian Forelands
            { 399, new List<ushort>{ 4354,4355,4366,4367,4376 } },
            { 400, new List<ushort>{ 4356,4357,4368,4369,4377 } },
            { 401, new List<ushort>{ 4358,4359,4370,4371,4378 } },
            { 402, new List<ushort>{ 4360,4361,4372,4373,4380 } },//Azyz Lla
            { 612, new List<ushort>{ 6008,6009,5990,5991,5987 } },//The Fringes
            { 620, new List<ushort>{ 6010,6011,5992,5993,5988 } },//The Peaks
            { 621, new List<ushort>{ 6012,6013,5994,5995,5989 } },//The Peaks
            { 613, new List<ushort>{ 6002,6003,5996,5997,5984 } },//Ruby Sea
            { 614, new List<ushort>{ 6004,6005,5998,5999,5985 } },//Yanxia
            { 622, new List<ushort>{ 6006,6007,6000,6001,5986 } },//Azim Steppe
        };
        internal List<Hunt> hunts = new List<Hunt>();
        private static List<FATEReport> fates = ConstructFATEReports();
        private static List<UInt32> HuntsPutInChat = new List<uint>();
        private static HubConnection hubConnection;
        private static IHubProxy hubProxy;
        internal static HttpClient http = new HttpClient();
        internal static bool joined = false;
        private static bool joining = false;
        private static bool connecting = false;
        private static ushort lastJoined, lastZone;
        internal const string baseUrl = "https://xivhunt.net/";//private const string baseUrl = "https://xivhunt.net/SignalR/HuntsHub";
        //internal const string baseUrl = "http://localhost:5000/";//private const string baseUrl = "http://localhost:5000/SignalR/HuntsHub";
        private const string VerifiedCharactersUrl = baseUrl + "Manage/VerifiedCharacters";
        private static DateTime ServerTimeUtc;

        internal static void LeaveGroup()
        {
            if (!joined)
                return;
            hubProxy.Invoke("LeaveGroup", lastJoined);
            Debug.WriteLine("Left " + GameResources.GetWorldName(lastJoined));
            joined = false;
        }

        internal FFXIVHunts()
        {
            //4.0
            for (ushort i = 6002; i < 6014; i++)
                hunts.Add(new Hunt(i, HuntRank.B));
            for (ushort i = 5990; i < 6002; i++)
                hunts.Add(new Hunt(i, HuntRank.A));
            for (ushort i = 5984; i < 5990; i++)
                hunts.Add(new Hunt(i, HuntRank.S));

            //3.0
            for (ushort i = 4350; i < 4362; i++)
                hunts.Add(new Hunt(i, HuntRank.B));
            for (ushort i = 4362; i < 4374; i++)
                hunts.Add(new Hunt(i, HuntRank.A));
            for (ushort i = 4374; i < 4381; i++)
                hunts.Add(new Hunt(i, HuntRank.S));
            hunts.RemoveAll(hunt => hunt.Id == 4379);//soul crystal?

            //2.0
            for (ushort i = 2919; i < 2936; i++)
                hunts.Add(new Hunt(i, HuntRank.B));
            for (ushort i = 2936; i < 2953; i++)
                hunts.Add(new Hunt(i, HuntRank.A));
            for (ushort i = 2953; i < 2970; i++)
                hunts.Add(new Hunt(i, HuntRank.S));

            if (hubConnection == null)
                hubConnection = new HubConnection(baseUrl);
            if (!string.IsNullOrWhiteSpace(Settings.Default.Cookies))
            {
                hubConnection.CookieContainer = (CookieContainer)ByteArrayToObject(Convert.FromBase64String(Settings.Default.Cookies));
            }

            if (hubProxy == null)
                hubProxy = hubConnection.CreateHubProxy("HuntsHub");
            hubConnection.StateChanged += HubConnection_StateChangedAsync;
            hubProxy.On<Hunt>("ReceiveHunt", hunt =>
            {
                Debug.WriteLine(string.Format(Resources.ReportReceived, GameResources.GetWorldName(hunt.WorldId), hunt.Name));
                if (PutInChat(hunt) && Settings.Default.FlashTaskbarIconOnHuntAndFATEs)
                    if (hunt.LastAlive)
                        NativeMethods.FlashTaskbarIcon(Program.mem.Process, 45, true);
                    else
                        NativeMethods.StopFlashWindowEx(Program.mem.Process);
            });
            hubProxy.On<FATEReport>("ReceiveFATE", fate =>
            {
                //Debug.WriteLine(string.Format(Resources.FATEReportReceived, GameResources.GetWorldName(fate.WorldId), fate.Name(true), fate.Progress));
                if (PutInChat(fate) && Settings.Default.FlashTaskbarIconOnHuntAndFATEs)
                    NativeMethods.FlashTaskbarIcon(Program.mem.Process);
            });
            _ = ConnectToGSHunt();
        }

        internal async void Connect()
        {
            if (hubConnection.State != ConnectionState.Connected && hubConnection.State != ConnectionState.Connecting)
                await ConnectToGSHunt();
            else if (!joined && hubConnection.State == ConnectionState.Connected)
                await JoinServerGroup();
        }

        private bool PutInChat(FATEReport fate)
        {
            int idx = fates.IndexOf(fate);
            if (idx == -1)
                return false;
            fates[idx].State = fate.State;
            fates[idx].StartTimeEpoch = fate.StartTimeEpoch;
            fates[idx].Duration = fate.Duration;
            fates[idx].Progress = fate.Progress;
            bool skipAnnounce = Settings.Default.NoAnnouncementsInContent && Program.mem.GetCurrentContentFinderCondition() > 0
                || (Math.Abs(fate.TimeRemaining.TotalHours) < 3 && fate.TimeRemaining.TotalMinutes < Settings.Default.FATEMinimumMinutesRemaining)
                || ((fate.State == FATEState.Preparation) ? fates[idx].lastPutInChat > Program.mem.GetServerUtcTime().AddMinutes(-10) : Math.Abs(fate.Progress - fates[idx].LastReportedProgress) < Settings.Default.FATEMinimumPercentInterval && Settings.Default.FATEMinimumPercentInterval > 0);
            if (FateNotifyCheck(fates[idx].ID) && fates[idx].lastPutInChat < Program.mem.GetServerUtcTime().AddMinutes(-Settings.Default.FATEInterval) && !fate.HasEnded && !skipAnnounce)
            {
                var cm = new ChatMessage();
                string postpend;
                if (fate.State == FATEState.Preparation)
                    postpend = Resources.PreparationState;
                else if (Math.Abs(fate.TimeRemaining.TotalHours) > 3)//StartTimeEpoch isn't set during the first few seconds
                    postpend = fate.Progress + "%";
                else
                    postpend = string.Format(Resources.FATEPrcTimeRemaining, fate.Progress, (int)fate.TimeRemaining.TotalMinutes, fate.TimeRemaining.Seconds.ToString("D2"));
                cm = ChatMessage.MakePosChatMessage(string.Format(Resources.FATEMsg, fates[idx].Name()), fate.ZoneID, fate.PosX, fate.PosY, " " + postpend);
                _ = Program.mem.WriteChatMessage(cm);
                CheckAndPlaySound(HuntRank.FATE);
                fates[idx].lastPutInChat = Program.mem.GetServerUtcTime();
                fates[idx].LastReportedProgress = fate.Progress;
                //if (fate.Progress > 99)
                //    fates[idx].lastReportedDead = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        private static List<FATEReport> ConstructFATEReports()
        {
            var l = new List<FATEReport>();
            foreach (FATE f in GameResources.GetFates())
            {
                l.Add(new FATEReport(f));
            }
            return l;
        }

        internal async Task LastKnownInfoForHunt(ushort id)
        {
            World world;
            string e;
            var r = await http.GetAsync(baseUrl + "api/worlds/" + Program.mem.GetServerId().ToString());
            if (r.IsSuccessStatusCode)
                e = await r.Content.ReadAsStringAsync();
            else
                return;
            world = JsonConvert.DeserializeObject<World>(e);
            Hunt result = world.Hunts.First(x => x.Id == id);
            if (result == null)
                return;
            var timeSinceLastReport = ServerTimeUtc.Subtract(result.LastReported);
            if (timeSinceLastReport < TimeSpan.Zero)
                timeSinceLastReport = TimeSpan.Zero;
            var cm = new ChatMessage();
            double TotalHours = Math.Floor(timeSinceLastReport.TotalHours);
            if (!result.LastAlive)
            {
                cm.Message = Encoding.UTF8.GetBytes(string.Format(Resources.LKIHuntKilled, result.Name));
                if (Resources.LKIHuntKilled.Contains("<time>"))//japanese case
                    cm.Message = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(cm.Message).Replace("<time>", string.Format(Resources.LKIHoursMinutes, TotalHours, timeSinceLastReport.Minutes)));
                else if (timeSinceLastReport.TotalDays > 90)
                    cm.Message = Encoding.UTF8.GetBytes(string.Format(Resources.LKIHuntNotReported, result.Name));
                else if (timeSinceLastReport.TotalHours > 72)
                    cm.Message = cm.Message.Concat(Encoding.UTF8.GetBytes(string.Format(Resources.LKIHours, TotalHours))).ToArray();
                else if (timeSinceLastReport.TotalHours < 1)
                    cm.Message = cm.Message.Concat(Encoding.UTF8.GetBytes(string.Format(Resources.LKIMinutes, Math.Floor(timeSinceLastReport.TotalMinutes)))).ToArray();
                else
                    cm.Message = cm.Message.Concat(Encoding.UTF8.GetBytes(string.Format(Resources.LKIHoursMinutes, TotalHours, timeSinceLastReport.Minutes))).ToArray();
                cm.Message = cm.Message.Concat(new byte[] { 0x00 }).ToArray();
            }
            else
            {
                var zid = GetZoneId(result.Id);
                cm = ChatMessage.MakePosChatMessage(string.Format(Resources.LKILastSeenAt, result.Name), zid, result.LastX, result.LastY, string.Format(Resources.LKIHoursMinutes, TotalHours, timeSinceLastReport.Minutes));
            }
            await Program.mem.WriteChatMessage(cm);
        }

        internal async Task LastKnownInfoForFATE(ushort id)
        {
            if (hubConnection.State != ConnectionState.Connected || !joined)
                return;
            FATE result = await hubProxy.Invoke<FATEReport>("QueryFATE", id);
            if (result == null)
                return;
            var timeSinceLastReport = ServerTimeUtc.Subtract(result.LastReported);
            if (timeSinceLastReport < TimeSpan.Zero)
                timeSinceLastReport = TimeSpan.Zero;
            ChatMessage cm = new ChatMessage();
            if (timeSinceLastReport.TotalDays > 90)
                cm.Message = Encoding.UTF8.GetBytes(string.Format(Resources.LKIHuntNotReported, result.Name()));
            else if (timeSinceLastReport.TotalHours > 100)
                cm.Message = Encoding.UTF8.GetBytes(string.Format(Resources.LKIFATEDays, result.Name(), Convert.ToUInt32(timeSinceLastReport.TotalDays)));
            else
                cm = ChatMessage.MakePosChatMessage(string.Format(Resources.LKIFATE, result.Name(), Math.Floor(timeSinceLastReport.TotalHours), timeSinceLastReport.Minutes), result.ZoneID, result.PosX, result.PosY);
            await Program.mem.WriteChatMessage(cm);
        }

        internal async void Check(FFXIVMemory mem)
        {
            if (hubConnection.State == ConnectionState.Disconnected)
                Connect();
            if (lastJoined != mem.GetServerId() && joined && !joining)
            {
                await hubProxy.Invoke("LeaveGroup", lastJoined);
                await JoinServerGroup();
            }
            ServerTimeUtc = mem.GetServerUtcTime();
            ushort thisZone = mem.GetZoneId();
            if (thisZone != lastZone && Settings.Default.OncePerHunt && Settings.Default.ForgetOnZoneChange)
            {
                HuntsPutInChat.Clear();
            }
            lastZone = thisZone;
            foreach (Combatant c in mem.Combatants.Where(c => c.Type == ObjectType.Monster))
            {
                if (((hunts.Exists(h => h.Id == c.ContentID && h.Rank == HuntRank.B))
                    || (hunts.Exists(h => h.Id == c.ContentID && h.Rank == HuntRank.A))
                    || (hunts.Exists(h => h.Id == c.ContentID && h.Rank == HuntRank.S)))
                    && GetZoneId(c.ContentID) == thisZone)
                {
                    ReportHunt(c);
                }
            }
            foreach (FATE f in mem.GetFateList())
            {
                if (f.ZoneID == thisZone)
                {
                    if (f.IsDataCenterShared() && PutInChat(new FATEReport(f) { WorldId = mem.GetServerId() }) && Settings.Default.FlashTaskbarIconOnHuntAndFATEs)
                        NativeMethods.FlashTaskbarIcon(Program.mem.Process);
                    else
                        ReportFate(f);
                }
            }
        }

        internal async Task RandomPositionForBNpc(ushort bnpcid)
        {
            EnemyObject Enemy;
            string e;
            var r = await http.GetAsync("https://api.xivdb.com/enemy/" + bnpcid);
            if (r.IsSuccessStatusCode)
                e = await r.Content.ReadAsStringAsync();
            else
                return;
            Enemy = JsonConvert.DeserializeObject<EnemyObject>(e);
            if (Enemy == null)
                return;
            //if (Enemy.Map_data.Points.All(x => x.App_data.Fate.Is_fate))
            //    return;//TODO: Redirect to FATE
            else if (Enemy.Map_data.Points.Any(x => x.App_data.Fate.Is_fate) && !Enemy.Map_data.Points.All(x => x.App_data.Fate.Is_fate))
            {   //Don't output FATE spawn points
                var temppoints = Enemy.Map_data.Points.ToList();
                temppoints.RemoveAll(x => x.App_data.Fate.Is_fate);
                Enemy.Map_data.Points = temppoints.ToArray();
            }
            var n = new Random().Next(0, Enemy.Map_data.Points.Length);
            var cm = ChatMessage.MakePosChatMessage(string.Format(Resources.LKICanBeFoundAt, GameResources.GetEnemyName(bnpcid, true)), GameResources.MapIdToZoneId(Enemy.Map_data.Points[n].Map_id), Enemy.Map_data.Points[n].App_position.Position.X, Enemy.Map_data.Points[n].App_position.Position.Y, mapId: Enemy.Map_data.Points[n].Map_id);
            await Program.mem.WriteChatMessage(cm);
        }

        internal static async Task<Item> LookupItemXIVDB(string itemname)
        {
            string e;
            int page = 1;
            string uri = "https://api.xivdb.com/search?string=" + WebUtility.UrlEncode(itemname) + "&one=items&strict=on&language=" + Thread.CurrentThread.CurrentUICulture.Name.Substring(0, 2) + "&page=";
            var results = new List<Item>();
            while (page == 1 ? true : results.Count != 0 && !results.Any(x => x.Name.Equals(itemname, StringComparison.CurrentCultureIgnoreCase)))
            {
                var r = await http.GetAsync(uri + page++);
                if (r.IsSuccessStatusCode)
                    e = await r.Content.ReadAsStringAsync();
                else
                    return null;
                results = JObject.Parse(e).SelectToken("items.results").ToObject<List<Item>>();
            }
            return results.SingleOrDefault(x => x.Name.Equals(itemname, StringComparison.InvariantCultureIgnoreCase));
        }

        private async void ReportFate(FATE f)
        {
            int idx = fates.FindIndex(h => h.ID == f.ID);
            if (idx < 0)
                return;
            else if (fates[idx].Progress != 100 && f.Progress > 99)
                ;//meh
            else if (fates[idx].LastReported > ServerTimeUtc.AddSeconds(-5))
                return;

            fates[idx].LastReported = ServerTimeUtc;
            //too pussy to use copy constructor
            fates[idx].Progress = f.Progress;
            fates[idx].PosX = f.PosX;
            fates[idx].PosZ = f.PosZ;
            fates[idx].PosY = f.PosY;
            fates[idx].ZoneID = f.ZoneID;
            fates[idx].Duration = f.Duration;
            fates[idx].StartTimeEpoch = f.StartTimeEpoch;
            fates[idx].State = f.State;
            fates[idx].ZoneID = f.ZoneID;

            try
            {
                if (hubConnection.State == ConnectionState.Connected && joined)
                    await hubProxy.Invoke("ReportFate", fates[idx]);
            }
            catch (Exception) { }
        }

        private static bool FateNotifyCheck(ushort id)
        {
            //Get first ID for the FATE with this name
            id = GameResources.GetFateId(GameResources.GetFateName(id, true));
            return Settings.Default.FATEs.Contains(id.ToString());
        }

        private async Task ConnectToGSHunt()
        {
            if (connecting)
                return;
            connecting = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            await TryConnect();
            connecting = false;
        }

        private async Task TryConnect()
        {
            if (hubConnection.CookieContainer == null || hubConnection.CookieContainer.Count == 0)
                hubConnection.CookieContainer = Application.Current.Dispatcher.Invoke(() => Login(Program.mem.GetServerId()));
            try
            {
                await hubConnection.Start();
            }
            catch (HttpClientException e)
            {
                if (e.Message.Contains("Unauthorized"))
                {
                    hubConnection.Stop();
                    hubConnection.CookieContainer = Application.Current.Dispatcher.Invoke(() => Login(Program.mem.GetServerId()));
                    await hubConnection.Start();
                }
            }
        }

        private async void HubConnection_StateChangedAsync(StateChange obj)
        {
            joined = false;
            if (obj.NewState != ConnectionState.Connected)
            {
                Program.w1.HuntConnectionTextBlock.Dispatcher.Invoke(new Action(() => Program.w1.HuntConnectionTextBlock.Text = obj.NewState.ToString()));
            }
            else if (obj.NewState == ConnectionState.Connected)
            {
                await JoinServerGroup();
            }
        }

        private async Task JoinServerGroup()
        {
            if (joined && hubConnection.State == ConnectionState.Connected || joining)
            {
                return;
            }
            joining = true;
            Program.w1.HuntConnectionTextBlock.Dispatcher.Invoke(new Action(() => Program.w1.HuntConnectionTextBlock.Text = Resources.FormReadingSID));
            ushort sid = Program.mem.GetServerId();
            Reporter r = new Reporter { WorldID = sid, Name = Program.mem.GetSelfCombatant().Name };
            Debug.WriteLine("Joining " + GameResources.GetWorldName(sid));
            if (await hubProxy.Invoke<bool>("JoinGroup", r))
                Program.w1.HuntConnectionTextBlock.Dispatcher.Invoke(new Action(() => Program.w1.HuntConnectionTextBlock.Text = string.Format(Resources.FormConnectedTo, GameResources.GetWorldName(sid))));
            else
                Program.w1.HuntConnectionTextBlock.Dispatcher.Invoke(new Action(() =>
                {
                    Program.w1.HuntConnectionTextBlock.Inlines.Clear();
                    Program.w1.HuntConnectionTextBlock.Inlines.Add(string.Format(Resources.FormFailedToJoin, $"{r.Name} ({GameResources.GetWorldName(sid)})").Replace(UI.LogInForm.XIVHuntNet, string.Empty));
                    var link = new Hyperlink(new Run(UI.LogInForm.XIVHuntNet)) { NavigateUri = new Uri(VerifiedCharactersUrl) };
                    link.RequestNavigate += UI.LogInForm.Link_RequestNavigate;
                    Program.w1.HuntConnectionTextBlock.Inlines.Add(link);
                }));
            joining = false;
            joined = true;
            lastJoined = sid;
            foreach (Hunt h in hunts)
                h.WorldId = sid;
            foreach (FATEReport f in fates)
                f.WorldId = sid;
        }

        private CookieContainer Login(ushort sid)
        {
            try
            {
                var lif = new UI.LogInForm(sid);
                if ((bool)lif.ShowDialog() && lif.receivedCookies.Count > 0)
                {
                    return lif.receivedCookies;
                }
            }
            catch (Exception) { }
            return null;
        }

        private bool PutInChat(Hunt hunt)
        {
            if (Settings.Default.NoAnnouncementsInContent && Program.mem.GetCurrentContentFinderCondition() > 0)
                return false;
            if ((hunt.IsARR() && hunt.Rank == HuntRank.B && Settings.Default.BARR && Settings.Default.notifyB)
                || (hunt.IsARR() && hunt.Rank == HuntRank.A && Settings.Default.AARR && Settings.Default.notifyA)
                || (hunt.IsARR() && hunt.Rank == HuntRank.S && Settings.Default.SARR && Settings.Default.notifyS)
                || (hunt.IsHW() && hunt.Rank == HuntRank.B && Settings.Default.BHW && Settings.Default.notifyB)
                || (hunt.IsHW() && hunt.Rank == HuntRank.A && Settings.Default.AHW && Settings.Default.notifyA)
                || (hunt.IsHW() && hunt.Rank == HuntRank.S && Settings.Default.SHW && Settings.Default.notifyS)
                || (hunt.IsSB() && hunt.Rank == HuntRank.B && Settings.Default.BSB && Settings.Default.notifyB)
                || (hunt.IsSB() && hunt.Rank == HuntRank.A && Settings.Default.ASB && Settings.Default.notifyA)
                || (hunt.IsSB() && hunt.Rank == HuntRank.S && Settings.Default.SSB && Settings.Default.notifyS)
                )
            {
                var idx = hunts.IndexOf(hunt);
                var cm = new ChatMessage();
                if (Settings.Default.OncePerHunt ? !HuntsPutInChat.Contains(hunt.OccurrenceID) : hunts[idx].lastPutInChat < Program.mem.GetServerUtcTime().AddMinutes(-Settings.Default.HuntInterval) && hunt.LastAlive /*&& hunts[idx].lastReportedDead < Program.mem.GetServerUtcTime().AddSeconds(-15)*/)
                {
                    cm = ChatMessage.MakePosChatMessage(string.Format(Resources.HuntMsg, hunt.Rank.ToString(), hunt.Name), GetZoneId(hunt.Id), hunt.LastX, hunt.LastY);
                    if (cm != null)
                    {
                        _ = Program.mem.WriteChatMessage(cm);
                        CheckAndPlaySound(hunt.Rank);
                        hunts[idx] = hunt;
                        HuntsPutInChat.Add(hunt.OccurrenceID);
                        hunts[idx].lastPutInChat = Program.mem.GetServerUtcTime();
                        return true;
                    }
                }
                else if (hunts[idx].lastReportedDead < ServerTimeUtc.AddSeconds(-12) && !hunt.LastAlive)
                {
                    cm = ChatMessage.MakePosChatMessage(string.Format(Resources.HuntMsg, hunt.Rank.ToString(), hunt.Name), GetZoneId(hunt.Id), hunt.LastX, hunt.LastY, Resources.HuntMsgKilled);
                    if (cm != null)
                    {
                        _ = Program.mem.WriteChatMessage(cm);
                        hunts[idx] = hunt;
                        hunts[idx].lastReportedDead = Program.mem.GetServerUtcTime();
                        return true;
                    }
                }
            }
            return false;
        }

        private static ushort GetZoneId(ushort huntId)
        {
            foreach (KeyValuePair<ushort, List<ushort>> m in MapHunts)
            {
                if (m.Value.Contains(huntId))
                    return m.Key;
            }
            return 0;
        }

        private void CheckAndPlaySound(HuntRank r)
        {
                if (r == HuntRank.S && Settings.Default.SPlaySound && Settings.Default.SBell != Resources.NoSoundAlert)
                    Program.w1.Ssp.Play();
                else if (r == HuntRank.A && Settings.Default.APlaySound && Settings.Default.ABell != Resources.NoSoundAlert)
                    Program.w1.Asp.Play();
                else if (r == HuntRank.B && Settings.Default.BPlaySound && Settings.Default.BBell != Resources.NoSoundAlert)
                    Program.w1.Bsp.Play();
                else if (r == HuntRank.FATE && Settings.Default.FATEPlaySound && Settings.Default.FATEBell != Resources.NoSoundAlert)
                    Program.w1.FATEsp.Play();
        }

        private async void ReportHunt(Combatant c)
        {
            int idx = hunts.FindIndex(h => h.Id == c.ContentID);
            if (hunts[idx].LastReported > ServerTimeUtc.AddSeconds(-5) && c.CurrentHP > 0)
                return;//no need to report this often
            //else if (!hunts[idx].LastAlive && hunts[idx].LastReported > DateTime.UtcNow.AddSeconds(-5))
            //    return;

            hunts[idx].LastReported = ServerTimeUtc;
            hunts[idx].LastX = c.PosX;
            hunts[idx].LastY = c.PosY;
            hunts[idx].OccurrenceID = c.ID;
            hunts[idx].LastAlive = (c.CurrentHP > 0) ? true : false;

            try
            {
                if (hubConnection.State == ConnectionState.Connected && joined)
                    await hubProxy.Invoke("ReportHunt", hunts[idx]);
            }
            catch (Exception) { }
        }

        public void Dispose()
        {
            LeaveGroup();
            hubConnection.StateChanged -= HubConnection_StateChangedAsync;
            hubConnection.Dispose();
        }

        private static Object ByteArrayToObject(byte[] arrBytes)
        {
            using (var memStream = new MemoryStream())
            {
                var binForm = new BinaryFormatter();
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                var obj = binForm.Deserialize(memStream);
                return obj;
            }
        }
    }

    class Hunt
    {
        [JsonProperty("wId")]
        internal ushort WorldId { get; set; }
        [JsonProperty]
        internal ushort Id { get; set; }
        [JsonProperty("r")]
        internal HuntRank Rank { get; set; }
        [JsonProperty]
        internal DateTime LastReported { get; set; }
        //[JsonProperty("i")]
        //internal byte instance { get; set; }
        internal DateTime lastPutInChat = DateTime.MinValue;
        internal DateTime lastReportedDead = DateTime.MinValue;
        [JsonProperty("x")]
        internal float LastX { get; set; }
        [JsonProperty("y")]
        internal float LastY { get; set; }
        [JsonProperty]
        internal bool LastAlive { get; set; }
        [JsonProperty]
        internal uint OccurrenceID { get; set; }
        internal string WorldName => GameResources.GetWorldName(WorldId);
        internal string Name => GameResources.GetEnemyName(Id);

        public Hunt() { }//necessary for SignalR receive

        internal Hunt(ushort _id, HuntRank r)
        {
            WorldId = Program.mem.GetServerId();
            Id = _id;
            Rank = r;
            LastReported = DateTime.MinValue;
        }

        internal bool IsARR()
        {
            return Id < 3000;
        }

        internal bool IsHW()
        {
            return Id > 3000 && Id < 5000;
        }

        internal bool IsSB()
        {
            return Id > 5000 && Id < 7000;
        }

        public override bool Equals(object obj)
        {
            var item = obj as Hunt;
            if (item == null)
            {
                return false;
            }
            return Id.Equals(item.Id) && WorldId.Equals(item.WorldId);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    class Reporter
    {
        public ushort WorldID { get; set; }
        public string Name { get; set; }
    }

    class FATEReport : FATE
    {
        [JsonProperty("wId")]
        public ushort WorldId { get; set; }
        //public byte Instance { get; set; }
        [JsonIgnore]
        public DateTime lastPutInChat = DateTime.MinValue;
        [JsonIgnore]
        public DateTime lastReportedDead = DateTime.MinValue;
        [JsonIgnore]
        public byte LastReportedProgress = byte.MaxValue;
        public FATEReport() : base()
        { }

        public FATEReport(FATE fate) : base(fate)
        { }


        public override bool Equals(object obj)
        {
            var item = obj as FATEReport;
            if (item == null)
            {
                return false;
            }
            return ID.Equals(item.ID) && WorldId.Equals(item.WorldId);
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
    }

    [JsonObject]
    class World
    {
        [JsonProperty("id")]
        internal ushort Id { get; set; }
        [JsonProperty("hunts")]
        internal List<Hunt> Hunts { get; set; }
    }

    enum HuntRank
    {
        B,
        A,
        S,
        FATE
    }
}