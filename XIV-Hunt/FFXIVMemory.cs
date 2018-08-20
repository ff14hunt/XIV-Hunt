using FFXIV_GameSense.MML;
using Splat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIVDB;

namespace FFXIV_GameSense
{
    public class FFXIVMemory : IDisposable
    {
        [Serializable]
        public class MemoryScanException : Exception
        {
            public MemoryScanException(string message) : base(message) { }
        }

        private Thread _thread;
        private readonly CancellationTokenSource cts;
        internal List<Entity> Combatants { get; private set; }
        internal object CombatantsLock => new object();
        internal event EventHandler<CommandEventArgs> OnNewCommand = delegate { };
        private bool Is64Bit => _mode == FFXIVClientMode.FFXIV_64;
        private static readonly Dictionary<byte, Type> ObjectTypeMap = new Dictionary<byte, Type>
        {
            { 0x0, typeof(Entity) },
            { 0x1, typeof(PC) },
            { 0x2, typeof(Monster) },//BattleNPC
            { 0x3, typeof(NPC) },
            { 0x4, typeof(Treasure) },//bronze only
            { 0x5, typeof(Aetheryte) },
            { 0x6, typeof(Gathering) },
            { 0x7, typeof(EObject) },//EventOBject some furniture, silver&gold treasure coffers, hoards, FATE items etc...
            { 0x8, typeof(Mount) },
            { 0x9, typeof(Minion) },
            { 0xA, typeof(Retainer) },
            { 0xB, typeof(LeyLines) },//don't know what else this includes
            { 0xC, typeof(Furniture) },
        };

        private const string charmapSignature32 = "81f9ffff0000741781f958010000730f8b0c8d";
        private const string charmapSignature64 = "488b420848c1e8033da701000077248bc0488d0d";
        private const string targetSignature32 = "75**5fc746**********5ec3833d**********75**833d";
        private const string targetSignature64 = "5fc3483935********75**483935";
        private const string zoneIdSignature32 = "a802752f8b4f04560fb735";
        private const string zoneIdSignature64 = "e8********f30f108d********4c8d85********0fb705";
        private const string serverTimeSignature32 = "c20400558bec83ec0c53568b35";
        private const string serverTimeSignature64 = "4833c448898424d0040000488be9c644243000488b0d";
        private const string chatLogStartSignature32 = "8b45fc83e0f983c809ff750850ff35********e8********8b0d";
        private const string chatLogStartSignature64 = "e8********85c0740e488b0d********33D2E8********488b0d";
        private const string fateListSignature32 = "8bc8e8********8bf0eb**33f6ff75**8bcee8********8b15";
        private const string fateListSignature64 = "be********488bcbe8********4881c3********4883ee**75**488b05";
        private const string contentFinderConditionSignature32 = "e8********5f5e5dc2****0fb646**b9";
        private const string contentFinderConditionSignature64 = "440fb643**488d51**488d0d";
        private const string serverIdSignature32 = "8b15********85d274**8b028bcaff50**8b0d";
        private const string serverIdSignature64 = "e8********488bbc24********488b7424**488b0d";
        private const string lastFailedCommandSignature32 = "83f9**7c**5b5fb8";
        private const string lastFailedCommandSignature64 = "4183f8**7c**488d05";
        private const string currentContentFinderConditionSignature32 = "6a**b9********e8********393d";
        private const string currentContentFinderConditionSignature64 = "75**33d2488d0d********e8********48393d";
        private const int contentFinderConditionOffset32 = 0xC8;
        private const int contentFinderConditionOffset64 = 0xF4;
        private const int lastFailedCommandOffset32 = 0x1B2;
        private const int lastFailedCommandOffset64 = 0x1C2;
        private const int currentContentFinderConditionOffset32 = 0x8;
        private const int currentContentFinderConditionOffset64 = 0xC;
        private static readonly int[] serverTimeOffset32 = { 0x14C0, 0x4, 0x644 };
        private static readonly int[] serverTimeOffset64 = { 0x1710, 0x8, 0x7D4 };
        private static readonly int[] chatLogStartOffset32 = { 0x18, 0x2C0, 0x0 };
        private static readonly int[] chatLogStartOffset64 = { 0x30, 0x3D8, 0x0 };
        private static readonly int[] chatLogTailOffset32 = { 0x18, 0x2C4 };
        private static readonly int[] chatLogTailOffset64 = { 0x30, 0x3E0 };
        private static readonly int[] serverIdOffset32 = { 0x20, 0x174 };
        private static readonly int[] serverIdOffset64 = { 0x28, 0x288 };
        private static readonly int[] fateListOffset32 = { 0x13A8, 0x0 };
        private static readonly int[] fateListOffset64 = { 0x16F8, 0x0 };
        private readonly FFXIVClientMode _mode;
        private GameRegion region;
        private CombatantOffsets combatantOffsets;
        private ContentFinderOffsets contentFinderOffsets;

        private IntPtr charmapAddress = IntPtr.Zero;
        private IntPtr targetAddress = IntPtr.Zero;
        private IntPtr fateListAddress = IntPtr.Zero;
        private IntPtr zoneIdAddress = IntPtr.Zero;
        private IntPtr serverTimeAddress = IntPtr.Zero;
        private IntPtr chatLogStartAddress = IntPtr.Zero;
        private IntPtr chatLogTailAddress = IntPtr.Zero;
        private IntPtr serverIdAddress = IntPtr.Zero;
        private IntPtr contentFinderConditionAddress = IntPtr.Zero;
        private IntPtr currentContentFinderConditionAddress = IntPtr.Zero;
        private IntPtr lastFailedCommandAddress = IntPtr.Zero;

        //internal byte GetZoneInstance()
        //{
        //    if (_mode == FFXIVClientMode.FFXIV_32)
        //        return GetByteArray(IntPtr.Add(_process.MainModule.BaseAddress, zoneInstanceAddress32.ToInt32()), 1)[0];
        //    else
        //        return GetByteArray(IntPtr.Add(_process.MainModule.BaseAddress, zoneInstanceAddress64.ToInt32()), 1)[0];
        //}

        public FFXIVMemory(System.Diagnostics.Process process)
        {
            Process = process;
            if (process.ProcessName == "ffxiv")
            {
                _mode = FFXIVClientMode.FFXIV_32;
            }
            else if (process.ProcessName == "ffxiv_dx11")
            {
                _mode = FFXIVClientMode.FFXIV_64;
            }
            else
            {
                _mode = FFXIVClientMode.Unknown;
            }

            GetPointerAddress();

            Combatants = new List<Entity>();
            cts = new CancellationTokenSource();
            _thread = new Thread(new ThreadStart(ScanMemoryLoop))
            {
                IsBackground = true
            };
            _thread.Start();
        }

        public void Dispose()
        {
            Dispose(true);
            Debug.WriteLine("FFXIVMemory Instance disposed");
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                cts.Cancel();
                cts.Dispose();
                while (_thread.IsAlive)
                    Thread.Sleep(5);
            }
        }

        private void ScanMemoryLoop()
        {
            int interval = 50;
            uint cnt = uint.MinValue;
            while (!cts.IsCancellationRequested)
            {
                Thread.Sleep(interval);
                if (cnt % 10 == 0 && !ValidateProcess())
                {
                    Thread.Sleep(1000);
                    return;
                }
                ScanFailedCommand();
                if (cnt % 10 == 0)
                    ScanCombatants();

                if (cnt >= uint.MaxValue - 5)
                    cnt = uint.MinValue;
                else
                    cnt++;
            }
        }

        private void ScanFailedCommand()
        {
            string cmd = GetLastFailedCommand();
            if (string.IsNullOrWhiteSpace(cmd))
                return;
            WipeLastFailedCommand();
            if (cmd.StartsWith("/") && cmd.Length > 1 && Enum.TryParse(cmd.Split(' ')[0].Substring(1), true, out Command command))
            {
                CommandEventArgs cmdargs = new CommandEventArgs(command, cmd.Substring(cmd.IndexOf(' ') + 1).Trim());
                OnNewCommand(this, cmdargs);
            }
        }

        private void ScanCombatants()
        {
            List<Entity> c = _getCombatantList();
            lock (CombatantsLock)
            {
                Combatants = c;
            }
        }

        public enum FFXIVClientMode
        {
            Unknown = 0,
            FFXIV_32 = 1,
            FFXIV_64 = 2,
        }

        public System.Diagnostics.Process Process { get; }

        public bool ValidateProcess()
        {
            if (Process == null || Process.HasExited)
            {
                return false;
            }
            if (charmapAddress == IntPtr.Zero ||
                targetAddress == IntPtr.Zero ||
                serverIdAddress == IntPtr.Zero ||
                !IsValidServerId())
            {
                return GetPointerAddress();
            }
            return true;
        }

        private bool GetPointerAddress()
        {
            string charmapSignature = !Is64Bit ? charmapSignature32 : charmapSignature64;
            string targetSignature = !Is64Bit ? targetSignature32 : targetSignature64;
            string zoneIdSignature = !Is64Bit ? zoneIdSignature32 : zoneIdSignature64;
            string serverTimeSignature = !Is64Bit ? serverTimeSignature32 : serverTimeSignature64;
            string chatLogStartSignature = !Is64Bit ? chatLogStartSignature32 : chatLogStartSignature64;
            string fateListSignature = !Is64Bit ? fateListSignature32 : fateListSignature64;
            string contentFinderConditionSignature = !Is64Bit ? contentFinderConditionSignature32 : contentFinderConditionSignature64;
            string serverIdSignature = !Is64Bit ? serverIdSignature32 : serverIdSignature64;
            string lastFailedCommandSignature = !Is64Bit ? lastFailedCommandSignature32 : lastFailedCommandSignature64;
            string currentContentFinderConditionSignature = !Is64Bit ? currentContentFinderConditionSignature32 : currentContentFinderConditionSignature64;
            int[] serverTimeOffset = !Is64Bit ? serverTimeOffset32 : serverTimeOffset64;
            int[] chatLogStartOffset = !Is64Bit ? chatLogStartOffset32 : chatLogStartOffset64;
            int[] chatLogTailOffset = !Is64Bit ? chatLogTailOffset32 : chatLogTailOffset64;
            int[] serverIdOffset = !Is64Bit ? serverIdOffset32 : serverIdOffset64;
            int contentFinderConditionOffset = !Is64Bit ? contentFinderConditionOffset32 : contentFinderConditionOffset64;
            int currentContentFinderConditionOffset = !Is64Bit ? currentContentFinderConditionOffset32 : currentContentFinderConditionOffset64;
            int lastFailedCommandOffset = !Is64Bit ? lastFailedCommandOffset32 : lastFailedCommandOffset64;

            List<string> fail = new List<string>();

            bool bRIP = Is64Bit;

            // SERVERID
            List<IntPtr> list = SigScan(serverIdSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                serverIdAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                serverIdAddress = ResolvePointerPath(list[0], serverIdOffset);
            }
            if (serverIdAddress == IntPtr.Zero)
            {
                fail.Add(nameof(serverIdAddress));
            }

            string regionpostpend = $" (DX{(Is64Bit ? "11" : "9")}) game detected.";
            if (GameResources.IsChineseWorld(GetServerId()))
                region = GameRegion.Chinese;
            else if (GameResources.IsKoreanWorld(GetServerId()))
                region = GameRegion.Korean;
            else
                region = GameRegion.Global;

            combatantOffsets = new CombatantOffsets(Is64Bit, region);
            LogHost.Default.Info(region.ToString() + regionpostpend);
            if (region != GameRegion.Global && !Is64Bit)
                contentFinderConditionOffset -= 0x4;
            contentFinderOffsets = new ContentFinderOffsets(Is64Bit);

            // CHARMAP
            list = SigScan(charmapSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                charmapAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                charmapAddress = list[0];
            }
            if (charmapAddress == IntPtr.Zero)
            {
                fail.Add(nameof(charmapAddress));
            }

            // TARGET
            list = SigScan(targetSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                targetAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                targetAddress = list[0];
            }
            if (targetAddress == IntPtr.Zero)
            {
                fail.Add(nameof(targetAddress));
            }

            // ZONEID
            list = SigScan(zoneIdSignature, 0, bRIP);
            if (list.Count == 1)
            {
                zoneIdAddress = list[0];
            }
            if (zoneIdAddress == IntPtr.Zero)
            {
                fail.Add(nameof(zoneIdAddress));
            }

            // SERVERTIME
            list = SigScan(serverTimeSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                serverTimeAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                serverTimeAddress = ResolvePointerPath(list[0], serverTimeOffset);
            }
            if (serverTimeAddress == IntPtr.Zero)
            {
                fail.Add(nameof(serverTimeAddress));
            }

            // CHATLOGSTART
            list = SigScan(chatLogStartSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                chatLogStartAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                chatLogStartAddress = ResolvePointerPath(list[0], chatLogStartOffset);
                // CHATLOGTAIL
                chatLogTailAddress = ResolvePointerPath(list[0], chatLogTailOffset);
            }
            if (chatLogStartAddress == IntPtr.Zero)
            {
                fail.Add(nameof(chatLogStartAddress));
            }
            if (chatLogTailAddress == IntPtr.Zero)
            {
                fail.Add(nameof(chatLogTailAddress));
            }

            // FATELIST
            list = SigScan(fateListSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                fateListAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                fateListAddress = list[0];
            }
            if (fateListAddress == IntPtr.Zero)
            {
                fail.Add(nameof(fateListAddress));
            }

            // CURRENTCONTENFINDERCONDITION
            list = SigScan(currentContentFinderConditionSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                currentContentFinderConditionAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                currentContentFinderConditionAddress = list[0] + currentContentFinderConditionOffset;
            }
            if (currentContentFinderConditionAddress == IntPtr.Zero)
            {
                fail.Add(nameof(currentContentFinderConditionAddress));
            }

            // CONTENTFINDERCONDITION
            list = SigScan(contentFinderConditionSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                contentFinderConditionAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                contentFinderConditionAddress = list[0] + contentFinderConditionOffset;
            }
            if (contentFinderConditionAddress == IntPtr.Zero)
            {
                fail.Add(nameof(contentFinderConditionAddress));
            }

            // LASTFAILEDCOMMAND
            list = SigScan(lastFailedCommandSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                lastFailedCommandAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                lastFailedCommandAddress = list[0] + lastFailedCommandOffset;
            }
            if (lastFailedCommandAddress == IntPtr.Zero)
            {
                fail.Add(nameof(lastFailedCommandAddress));
            }

            Debug.WriteLine(nameof(charmapAddress) + ": 0x{0:X}", charmapAddress.ToInt64());
            Debug.WriteLine(nameof(targetAddress) + ": 0x{0:X}", targetAddress.ToInt64());
            Debug.WriteLine(nameof(zoneIdAddress) + ": 0x{0:X}", zoneIdAddress.ToInt64());
            Debug.WriteLine(nameof(chatLogStartAddress) + ": 0x{0:X}", chatLogStartAddress.ToInt64());
            Debug.WriteLine(nameof(chatLogTailAddress) + ": 0x{0:X}", chatLogTailAddress.ToInt64());
            Debug.WriteLine(nameof(fateListAddress) + ": 0x{0:X}", fateListAddress.ToInt64());
            Debug.WriteLine(nameof(serverIdAddress) + ": 0x{0:X}", serverIdAddress.ToInt64());
            Debug.WriteLine(nameof(serverTimeAddress) + ": 0x{0:X}", serverTimeAddress.ToInt64());
            Debug.WriteLine(nameof(contentFinderConditionAddress) + ": 0x{0:X}", contentFinderConditionAddress.ToInt64());
            Debug.WriteLine(nameof(currentContentFinderConditionAddress) + ": 0x{0:X}", currentContentFinderConditionAddress.ToInt64());
            Debug.WriteLine(nameof(lastFailedCommandAddress) + ": 0x{0:X}", lastFailedCommandAddress.ToInt64());

            Entity c = GetSelfCombatant();
            if (c == null)
                throw new MemoryScanException(string.Format(Properties.Resources.FailedToSigScan, nameof(charmapAddress)));
            else
                Debug.WriteLine("MyCharacter: '{0}' ({1})", c.Name, c.ID);

            if (fail.Any())
            {
                throw new MemoryScanException(string.Format(Properties.Resources.FailedToSigScan, string.Join(",", fail)));
            }
            return !fail.Any();
        }

        private void WipeLastFailedCommand(byte len = 62)
        {
            if (len > 62)
                len = 62;
            byte[] arr = new byte[len];
            NativeMethods.WriteProcessMemory(Process.Handle, lastFailedCommandAddress, arr, new IntPtr(arr.Length), out uint lpNumberOfBytesWritten);
        }

        private string GetLastFailedCommand() => GetStringFromBytes(GetByteArray(lastFailedCommandAddress, 70), 0, 70);

        internal ushort GetCurrentContentFinderCondition() => BitConverter.ToUInt16(GetByteArray(currentContentFinderConditionAddress, 2), 0);

        internal ContentFinder GetContentFinder()
        {
            var ba = GetByteArray(contentFinderConditionAddress, 0x100);
            return new ContentFinder
            {
                ContentFinderConditionID = BitConverter.ToUInt16(ba, 0),
                State = (ContentFinderState)ba[contentFinderOffsets.StateOffset],
                RouletteID = ba[contentFinderOffsets.RouletteIdOffset]
            };
        }

        internal List<ChatMessage> ReadChatLogBackwards(uint count = 1000, Predicate<ChatMessage> filter = null, Predicate<ChatMessage> stopOn = null)
        {
            var ChatLog = new List<ChatMessage>();
            ulong length = (Is64Bit ? GetUInt64(chatLogTailAddress) : GetUInt32(chatLogTailAddress)) - (ulong)chatLogStartAddress.ToInt64();
            byte[] ws = GetByteArray(chatLogStartAddress, (uint)length);
            int currentStart = ws.Length;
            int currentEnd = ws.Length;
            //ushort wid = GetServerId();
            while (currentStart > 0 && count > 0)
            {
                currentStart--;
                if (ws[currentStart] == 0x00 && ws[currentStart - 1] == 0x00)
                {
                    currentStart -= 7;
                    ChatMessage cm = new ChatMessage(ws.Skip(currentStart).Take(currentEnd - currentStart).ToArray()/*, wid*/);
                    if (stopOn != null && stopOn.Invoke(cm))
                        break;
                    if (filter == null || filter.Invoke(cm))
                        ChatLog.Add(cm);
                    currentEnd = currentStart;
                    count--;
                }
            }
            ChatLog.Reverse();
            return ChatLog;
        }

        private async Task WriteChatMessage(byte[] msg)
        {
            await TryInject();
            if (msg.Last() != 0x00)
                msg = msg.Concat(new byte[] { 0x00 }).ToArray();
            ulong tail = Is64Bit ? GetUInt64(chatLogTailAddress) : GetUInt32(chatLogTailAddress);
            NativeMethods.WriteProcessMemory(Process.Handle, (IntPtr)tail, msg, new IntPtr(msg.Length), out uint written);
            if (Is64Bit)
                NativeMethods.WriteProcessMemory(Process.Handle, chatLogTailAddress, BitConverter.GetBytes(tail + Convert.ToUInt64(msg.Length)), new IntPtr(sizeof(ulong)), out written);
            else
                NativeMethods.WriteProcessMemory(Process.Handle, chatLogTailAddress, BitConverter.GetBytes(tail + Convert.ToUInt32(msg.Length)), new IntPtr(sizeof(uint)), out written);
            var SlashInstanceCommand = new PipeMessage(Process.Id, PMCommand.SlashInstance);
            PersistentNamedPipeServer.SendPipeMessage(SlashInstanceCommand);
        }

        //Something in here throws, on Win10, it gets "caught" but ... ?
        private async Task TryInject()
        {
            string dllfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, !Is64Bit ? "Hunt.dll" : "Hunt_x64.dll");
            byte[] ba = !Is64Bit ? Properties.Resources.Hunt : Properties.Resources.Hunt_x64;
            //not written already, or old version
            if (!File.Exists(dllfile) || !File.ReadAllBytes(dllfile).SequenceEqual(ba))
            {
                try
                {
                    File.WriteAllBytes(dllfile, ba);
                }
                catch (Exception e) { LogHost.Default.InfoException(nameof(TryInject), e); }
            }
            if (!PersistentNamedPipeServer.Instance.IsConnected)
            {
                await NativeMethods.InjectDLL(Process, dllfile, !Is64Bit);
                for (int w = 0; !PersistentNamedPipeServer.Instance.IsConnected && w < 1000; w += 100)
                {
                    await Task.Delay(100);
                }
            }
        }

        internal async Task WriteChatMessage(ChatMessage cm)
        {
            cm.Timestamp = GetServerUtcTime();
            await WriteChatMessage(cm.ToArray());
        }

        //private static readonly uint[] _lookup32 = CreateLookup32();

        //private static uint[] CreateLookup32()
        //{
        //    var result = new uint[256];
        //    for (int i = 0; i < 256; i++)
        //    {
        //        string s = i.ToString("X2");
        //        result[i] = s[0] + ((uint)s[1] << 16);
        //    }
        //    return result;
        //}

        //private static string ByteArrayToHexViaLookup32(byte[] bytes)
        //{
        //    var lookup32 = _lookup32;
        //    var result = new char[bytes.Length * 2];
        //    for (int i = 0; i < bytes.Length; i++)
        //    {
        //        var val = lookup32[bytes[i]];
        //        result[2 * i] = (char)val;
        //        result[2 * i + 1] = (char)(val >> 16);
        //    }
        //    return new string(result);
        //}

        //internal int getPlaceNameId()
        //{
        //    return BitConverter.ToInt32(GetByteArray(placeNameIdAddress, 4), 0);
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address">Relative address of first/base pointer</param>
        /// <param name="offsets">Array with offsets</param>
        /// <returns>Final address</returns>
        private IntPtr ResolvePointerPath(IntPtr address, int[] offsets)
        {
            ulong bytes = Is64Bit ? GetUInt64(address) : GetUInt32(address);
            foreach (int offset in offsets)
            {
                address = IntPtr.Add((IntPtr)bytes, offset);
                bytes = Is64Bit ? GetUInt64(address) : GetUInt32(address);
            }
            return address;
        }

        internal DateTime GetServerUtcTime()
        {
            byte[] ba = GetByteArray(serverTimeAddress, 6);
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(BitConverter.ToUInt32(ba, 0)).AddMilliseconds(BitConverter.ToUInt16(ba, 4));
        }

        public Entity GetTargetCombatant()
        {
            Entity target = null;
            IntPtr address = IntPtr.Zero;

            byte[] source = GetByteArray(targetAddress, 128);
            unsafe
            {
                if (Is64Bit)
                {
                    fixed (byte* p = source) address = new IntPtr(*(long*)p);
                }
                else
                {
                    fixed (byte* p = source) address = new IntPtr(*(int*)p);
                }
            }
            if (address.ToInt64() <= 0)
            {
                return null;
            }

            source = GetByteArray(address, 0x3F40);
            target = GetEntityFromByteArray(source);
            return target;
        }

        public PC GetSelfCombatant()
        {
            PC self = null;
            IntPtr address = Is64Bit ? (IntPtr)GetUInt64(charmapAddress) : (IntPtr)GetUInt32(charmapAddress);
            if (address.ToInt64() > 0)
            {
                byte[] source = GetByteArray(address, 0x3F40);
                self = (PC)GetEntityFromByteArray(source);
            }
            return self;
        }

        private unsafe ulong GetUInt64(IntPtr address, int offset = 0)
        {
            ulong ret;
            var value = new byte[8];
            Peek(IntPtr.Add(address, offset), value);
            fixed (byte* p = &value[0]) ret = *(ulong*)p;
            return ret;
        }

        private bool IsValidServerId()
        {
            ushort id = GetServerId();
            if (!GameResources.IsValidWorldID(id))
            {
                LogHost.Default.Warn(id + " is not a valid server id");
                return false;
            }
            return true;
        }

        internal ushort GetServerId() => BitConverter.ToUInt16(GetByteArray(serverIdAddress, 2), 0);

        //public Combatant GetAnchorCombatant()
        //{
        //    Combatant self = null;
        //    int offset = _mode == FFXIVClientMode.FFXIV_64 ? 0x08 : 0x04;
        //    IntPtr address = (IntPtr)GetUInt32(targetAddress + offset);
        //    if (address.ToInt64() > 0)
        //    {
        //        byte[] source = GetByteArray(address, 0x3F40);
        //        self = GetCombatantFromByteArray(source);
        //    }
        //    return self;
        //}

        //public Combatant GetFocusCombatant()
        //{
        //    Combatant self = null;
        //    int offset = _mode == FFXIVClientMode.FFXIV_64 ? 0x78 : 0x44;
        //    IntPtr address = (IntPtr)GetUInt32(targetAddress + offset);
        //    if (address.ToInt64() > 0)
        //    {
        //        byte[] source = GetByteArray(address, 0x3F40);
        //        self = GetCombatantFromByteArray(source);
        //    }
        //    return self;
        //}

        //public Combatant GetHoverCombatant()
        //{
        //    Combatant self = null;
        //    int offset = _mode == FFXIVClientMode.FFXIV_64 ? 0x48 : 0x24;
        //    IntPtr address = (IntPtr)GetUInt32(targetAddress + offset);
        //    if (address.ToInt64() > 0)
        //    {
        //        byte[] source = GetByteArray(address, 0x3F40);
        //        self = GetCombatantFromByteArray(source);
        //    }
        //    return self;
        //}

        internal List<FATE> GetFateList()
        {
            IntPtr liststart = ResolvePointerPath(fateListAddress, Is64Bit ? fateListOffset64 : fateListOffset32);
            const byte maxFATEs = 8;
            List<IntPtr> fatePtrs = new List<IntPtr>(maxFATEs);
            List<FATE> fates = new List<FATE>(maxFATEs);
            var size = Is64Bit ? 8 * maxFATEs : 4 * maxFATEs;

            for (int i = 0; i < size;)
            {
                IntPtr ptr = Is64Bit ? (IntPtr)GetUInt64(liststart, i) : (IntPtr)GetUInt32(liststart, i);
                if (ptr.Equals(IntPtr.Zero))
                    break;
                fatePtrs.Add(ptr);
                i += Is64Bit ? 8 : 4;
            }

            var currentZone = GetZoneId();
            if (currentZone == 0)
                return fates;
            foreach (IntPtr ptr in fatePtrs.Distinct())
            {
                var f = GetFateFromByteArray(GetByteArray(ptr, 0x948));
                if (f == null)
                    break;
                if (!fates.Contains(f) && f.ZoneID == currentZone)
                    fates.Add(f);
            }
            return fates;
        }

        private FATE GetFateFromByteArray(byte[] ba)
        {
            FATE f = new FATE()
            {
                ID = BitConverter.ToUInt16(ba, 0x18),
                StartTimeEpoch = BitConverter.ToUInt32(ba, 0x20),
                Duration = BitConverter.ToUInt16(ba, 0x28),
                ReadName = GetStringFromBytes(ba, Is64Bit ? 0xE2 : 0xAA),
                State = (FATEState)ba[Is64Bit ? 0x3AC : 0x2F4],
                Progress = ba[Is64Bit ? 0x3B3 : 0x2FB],
                PosX = BitConverter.ToSingle(ba, Is64Bit ? 0x400 : 0x340),
                PosZ = BitConverter.ToSingle(ba, Is64Bit ? 0x404 : 0x344),
                PosY = BitConverter.ToSingle(ba, Is64Bit ? 0x408 : 0x348),
                ZoneID = BitConverter.ToUInt16(ba, Is64Bit ? 0x624 : 0x4F4)
            };
            if (f.ID == 0 || f.Progress < 0 || f.Progress > 100 || !f.PosX.IsWithin(-1024, 1024) || !f.PosY.IsWithin(-1024, 1024))
                return null;
            else
                return f;
        }

        internal unsafe List<Entity> _getCombatantList()
        {
            uint num = 344;
            List<Entity> result = new List<Entity>();

            uint sz = Is64Bit ? (uint)8 : 4;
            byte[] source = GetByteArray(charmapAddress, sz * num);
            if (source == null || source.Length == 0) { return result; }

            for (int i = 0; i < num; i++)
            {
                IntPtr p;
                if (Is64Bit)
                    fixed (byte* bp = source) p = new IntPtr(*(long*)&bp[i * sz]);
                else
                    fixed (byte* bp = source) p = new IntPtr(*(int*)&bp[i * sz]);

                if (!(p == IntPtr.Zero))
                {
                    byte[] c = GetByteArray(p, 0x25D0);
                    Entity entity = GetEntityFromByteArray(c);
                    //skip
                    if (entity is Minion || entity is Furniture || entity is Gathering || entity is NPC || entity is LeyLines || entity is Retainer)
                        continue;
                    if (entity.ID != 0 && entity.ID != 3758096384u && !result.Exists((Entity x) => x.ID == entity.ID))
                    {
                        entity.Order = i;
                        result.Add(entity);
                    }
                }
            }
            return result;
        }

        internal unsafe Entity GetEntityFromByteArray(byte[] source)
        {
            int offset = 0;
            Entity entity;
            fixed (byte* p = source)
            {
                entity = (Entity)Activator.CreateInstance(ObjectTypeMap[ObjectTypeMap.ContainsKey(p[combatantOffsets.Type]) ? p[combatantOffsets.Type] : (byte)0]);//alternatively a manually typed switch statement
                entity.Name = GetStringFromBytes(source, combatantOffsets.Name);
                entity.ID = *(uint*)&p[combatantOffsets.ID];
                entity.OwnerID = *(uint*)&p[combatantOffsets.OwnerID];
                if (entity.OwnerID == 3758096384u)
                {
                    entity.OwnerID = 0u;
                }

                entity.EffectiveDistance = p[combatantOffsets.EffectiveDistance];

                entity.PosX = *(float*)&p[combatantOffsets.PosX];
                entity.PosZ = *(float*)&p[combatantOffsets.PosZ];
                entity.PosY = *(float*)&p[combatantOffsets.PosY];
                entity.Heading = *(float*)&p[combatantOffsets.Heading];

                if (entity is EObject)
                {
                    IntPtr eventTypeAddr = Is64Bit ? *(IntPtr*)&p[combatantOffsets.EventType] : new IntPtr(*(int*)&p[combatantOffsets.EventType]);
                    ((EObject)entity).SubType = (EObjType)GetUInt16(eventTypeAddr, 4);
                    if (((EObject)entity).SubType == EObjType.CairnOfPassage || ((EObject)entity).SubType == EObjType.CairnOfReturn || ((EObject)entity).SubType == EObjType.BeaconOfPassage || ((EObject)entity).SubType == EObjType.BeaconOfReturn)
                        ((EObject)entity).CairnIsUnlocked = *(&p[combatantOffsets.CairnIsUnlocked]) == 0x04;
                }
                if (entity is Monster)
                {
                    //if(*(uint*)&p[0xE4]==2149253119)//necessary?
                    ((Monster)entity).FateID = *(uint*)&p[combatantOffsets.FateID];
                    ((Monster)entity).BNpcNameID = *(ushort*)&p[combatantOffsets.BNpcNameID];
                }

                entity.TargetID = *(uint*)&p[combatantOffsets.TargetID];
                if (entity.TargetID == 3758096384u)
                {
                    entity.TargetID = *(uint*)&p[combatantOffsets.TargetID2];
                }

                if (entity is Combatant)
                {
                    ((Combatant)entity).Job = (JobEnum)p[combatantOffsets.Job];
                    ((Combatant)entity).Level = p[combatantOffsets.Level];
                    ((Combatant)entity).CurrentHP = *(uint*)&p[combatantOffsets.CurrentHP];
                    ((Combatant)entity).MaxHP = *(uint*)&p[combatantOffsets.MaxHP];
                    ((Combatant)entity).CurrentMP = *(uint*)&p[combatantOffsets.CurrentMP];
                    ((Combatant)entity).MaxMP = *(uint*)&p[combatantOffsets.MaxMP];
                    ((Combatant)entity).CurrentTP = *(ushort*)&p[combatantOffsets.CurrentTP];
                    ((Combatant)entity).MaxTP = 1000;
                    ((Combatant)entity).CurrentGP = *(ushort*)&p[combatantOffsets.CurrentGP];
                    ((Combatant)entity).MaxGP = *(ushort*)&p[combatantOffsets.MaxGP];
                    ((Combatant)entity).CurrentCP = *(ushort*)&p[combatantOffsets.CurrentCP];
                    ((Combatant)entity).MaxCP = *(ushort*)&p[combatantOffsets.MaxCP];

                    offset = combatantOffsets.StatusEffectsStart;
                    int countedStatusEffects = 0;
                    while (countedStatusEffects < 32)
                    {
                        Status status = new Status { ID = *(short*)&p[offset] };
                        if (status.ID != 00)
                        {
                            status.Value = *(short*)&p[offset + 2];
                            status.Timer = *(float*)&p[offset + 4];
                            status.CasterId = *(uint*)&p[offset + 8];
                            (entity as Combatant).StatusList.Add(status);
                        }
                        offset += 12;
                        countedStatusEffects++;
                    }
                }
            }
            return entity;
        }

        internal async Task PlayPerformance(Performance p, CancellationToken performCancelationToken)
        {
            if (!PersistentNamedPipeServer.Instance.IsConnected)
                await TryInject();
            await p.PlayAsync(Process.Id, performCancelationToken);
        }

        internal async Task PlayMML(ImplementedPlayer p, CancellationToken performCancelationToken)
        {
            p.Unmute();
            p.Play();
            if (!PersistentNamedPipeServer.Instance.IsConnected)
                await TryInject();
            PipeMessage noteOff = new PipeMessage(Process.Id, PMCommand.PlayNote) { Parameter = 0 };
            foreach (var track in p.Tracks)
            {
                new Thread(() =>
                {
                    TimeSpan ts = new TimeSpan(0);
                    foreach (TextPlayer.Note note in track.notes)
                    {
                        TimeSpan w = note.TimeSpan - ts;
                        if (w.TotalMilliseconds > 0)
                        {
                            Thread.Sleep(w);
                        }
                        if (performCancelationToken.IsCancellationRequested)
                            return;
                        PersistentNamedPipeServer.SendPipeMessage(new PipeMessage(Process.Id, PMCommand.PlayNote) { Parameter = note.GetStep() });
                        Thread.Sleep(note.Length);
                        if(region == GameRegion.Global)
                            PersistentNamedPipeServer.SendPipeMessage(noteOff);
                        ts = note.TimeSpan + note.Length;
                    }
                })
                {
                    Priority = ThreadPriority.Highest,
                    IsBackground = true
                }.Start();
            }
        }

        private static string GetStringFromBytes(byte[] source, int offset = 0, int size = 256)
        {
            var bytes = new byte[size];
            Array.Copy(source, offset, bytes, 0, size);
            var realSize = 0;
            for (var i = 0; i < size; i++)
            {
                if (bytes[i] != 0)
                {
                    continue;
                }
                realSize = i;
                break;
            }
            Array.Resize(ref bytes, realSize);
            return Encoding.UTF8.GetString(bytes);
        }

        private bool Peek(IntPtr address, byte[] buffer)
        {
            IntPtr zero = IntPtr.Zero;
            IntPtr nSize = new IntPtr(buffer.Length);
            return NativeMethods.ReadProcessMemory(Process.Handle, address, buffer, nSize, ref zero);
        }

        public byte[] GetByteArray(IntPtr address, uint length)
        {
            var data = new byte[length];
            Peek(address, data);
            return data;
        }

        private unsafe int GetInt32(IntPtr address, int offset = 0)
        {
            int ret;
            var value = new byte[4];
            Peek(IntPtr.Add(address, offset), value);
            fixed (byte* p = &value[0]) ret = *(int*)p;
            return ret;
        }

        private unsafe uint GetUInt32(IntPtr address, int offset = 0)
        {
            uint ret;
            var value = new byte[4];
            Peek(IntPtr.Add(address, offset), value);
            fixed (byte* p = &value[0]) ret = *(uint*)p;
            return ret;
        }

        private unsafe ushort GetUInt16(IntPtr address, int offset = 0)
        {
            ushort ret;
            var value = new byte[2];
            Peek(IntPtr.Add(address, offset), value);
            fixed (byte* p = &value[0]) ret = *(ushort*)p;
            return ret;
        }

        /// <summary>
        /// Signature scan.
        /// Read data at address which follow matched with the pattern and return it as a pointer.
        /// </summary>
        /// <param name="pattern">byte pattern signature</param>
        /// <param name="offset">offset to read</param>
        /// <param name="bRIP">x64 rip relative addressing mode if true</param>
        /// <returns>the pointer addresses</returns>
        private List<IntPtr> SigScan(string pattern, int offset = 0, bool bRIP = false)
        {
            if (pattern == null || pattern.Length % 2 != 0)
            {
                return new List<IntPtr>();
            }

            byte?[] array = new byte?[pattern.Length / 2];
            for (int i = 0; i < pattern.Length / 2; i++)
            {
                string text = pattern.Substring(i * 2, 2);
                if (text == "**")
                {
                    array[i] = null;
                }
                else
                {
                    array[i] = new byte?(Convert.ToByte(text, 16));
                }
            }

            int num = 65536;
            IntPtr baseAddress = Process.MainModule.BaseAddress;
            IntPtr intPtr = IntPtr.Add(baseAddress, Process.MainModule.ModuleMemorySize);
            IntPtr intPtr2 = baseAddress;
            byte[] array2 = new byte[num];
            List<IntPtr> list = new List<IntPtr>();
            while (intPtr2.ToInt64() < intPtr.ToInt64())
            {
                IntPtr zero = IntPtr.Zero;
                IntPtr nSize = new IntPtr(num);
                if (IntPtr.Add(intPtr2, num).ToInt64() > intPtr.ToInt64())
                {
                    nSize = (IntPtr)(intPtr.ToInt64() - intPtr2.ToInt64());
                }
                if (NativeMethods.ReadProcessMemory(Process.Handle, intPtr2, array2, nSize, ref zero))
                {
                    int num2 = 0;
                    while (num2 < zero.ToInt64() - array.Length - offset - 4L + 1L)
                    {
                        int num3 = 0;
                        for (int j = 0; j < array.Length; j++)
                        {
                            if (!array[j].HasValue)
                            {
                                num3++;
                            }
                            else
                            {
                                if (array[j].Value != array2[num2 + j])
                                {
                                    break;
                                }
                                num3++;
                            }
                        }
                        if (num3 == array.Length)
                        {
                            IntPtr item;
                            if (bRIP)
                            {
                                item = new IntPtr(BitConverter.ToInt32(array2, num2 + array.Length + offset));
                                item = new IntPtr(intPtr2.ToInt64() + num2 + array.Length + 4L + item.ToInt64());
                            }
                            else if (Is64Bit)
                            {
                                item = new IntPtr(BitConverter.ToInt64(array2, num2 + array.Length + offset));
                                item = new IntPtr(item.ToInt64());
                            }
                            else
                            {
                                item = new IntPtr(BitConverter.ToInt32(array2, num2 + array.Length + offset));
                                item = new IntPtr(item.ToInt64());
                            }
                            list.Add(item);
                        }
                        num2++;
                    }
                }
                intPtr2 = IntPtr.Add(intPtr2, num);
            }
            return list;
        }

        public ushort GetZoneId() => BitConverter.ToUInt16(GetByteArray(zoneIdAddress, 2), 0);
    }

    internal class ContentFinderOffsets
    {
        internal int StateOffset { get; private set; }
        internal int RouletteIdOffset { get; private set; }

        public ContentFinderOffsets(bool Is64Bit)
        {
            StateOffset = Is64Bit ? 0x71 : 0x69;
            RouletteIdOffset = Is64Bit ? 0x76 : 0x6E;
        }
    }

    internal enum GameRegion
    {
        Global,
        Chinese,
        Korean
    }

    internal class CombatantOffsets
    {
        internal int Name { get; private set; }
        internal int ID { get; private set; }
        internal int OwnerID { get; private set; }
        internal int Type { get; private set; }
        internal int EffectiveDistance { get; private set; }
        internal int PosX { get; private set; }
        internal int PosZ { get; private set; }
        internal int PosY { get; private set; }
        internal int Heading { get; private set; }
        internal int FateID { get; private set; }
        internal int EventType { get; private set; }
        internal int CairnIsUnlocked { get; private set; }
        internal int BNpcNameID { get; private set; }
        internal int TargetID { get; private set; }
        internal int TargetID2 { get; private set; }
        internal int Job { get; private set; }
        internal int Level { get; private set; }
        internal int CurrentHP { get; private set; }
        internal int MaxHP { get; private set; }
        internal int CurrentMP { get; private set; }
        internal int MaxMP { get; private set; }
        internal int CurrentTP { get; private set; }
        internal int CurrentGP { get; private set; }
        internal int MaxGP { get; private set; }
        internal int CurrentCP { get; private set; }
        internal int MaxCP { get; private set; }
        internal int StatusEffectsStart { get; private set; }

        public CombatantOffsets(bool Is64Bit, GameRegion region)
        {
            Name = 0x30;
            ID = 0x74;
            OwnerID = 0x84;
            Type = 0x8C;
            EffectiveDistance = 0x92;
            PosX = 0xA0;
            PosZ = PosX + 0x4;
            PosY = PosZ + 0x4;
            Heading = PosY + 0x8;
            FateID = 0xE8;
            EventType = Is64Bit ? 0x190 : 0x180;
            CairnIsUnlocked = Is64Bit ? 0x1A2 : 0x18A;
            TargetID = Is64Bit ? 0x1D8 : 0x1C8;
            TargetID2 = Is64Bit ? 0x990 : 0x9D8;
            int offset;
            if (region == GameRegion.Chinese || region == GameRegion.Korean)
            {
                BNpcNameID = Is64Bit ? 0x1694 : 0x136C;
                offset = Is64Bit ? 0x16B0 : 0x1388;
                Job = offset + 0x3E;
                Level = offset + 0x40;
            }
            else
            {
                BNpcNameID = Is64Bit ? 0x16D8 : 0x1380;
                offset = Is64Bit ? 0x16F8 : 0x13A0;
                Job = offset + 0x40;
                Level = offset + 0x42;
            }
            CurrentHP = offset + 0x8;
            MaxHP = offset + 0xC;
            CurrentMP = offset + 0x10;
            MaxMP = offset + 0x14;
            CurrentTP = offset + 0x18;
            CurrentGP = offset + 0x1A;
            MaxGP = offset + 0x1C;
            CurrentCP = offset + 0x1E;
            MaxCP = offset + 0x20;
            if (region == GameRegion.Chinese || region == GameRegion.Korean)
                StatusEffectsStart = Is64Bit ? offset + 0xB8 : offset + 0x94;
            else
                StatusEffectsStart = Is64Bit ? offset + 0xC0 : offset + 0xA4;
        }
    }

    internal class CommandEventArgs : EventArgs
    {
        public Command Command { get; private set; }
        public string Parameter { get; private set; }

        public CommandEventArgs(Command cmd, string p)
        {
            Command = cmd;
            if (p.Equals('/' + cmd.ToString(), StringComparison.OrdinalIgnoreCase))
                Parameter = string.Empty;
            else
                Parameter = p;
        }

        public override string ToString() => "/" + Command.ToString() + " " + Parameter;
    }

    internal enum Command
    {
        Hunt,
        Perform,
        PerformStop,
        Flag
    }
}
