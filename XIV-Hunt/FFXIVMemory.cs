using FFXIV_GameSense.MML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private CancellationTokenSource cts;
        internal static PersistentNamedPipeServer PNPS;
        internal List<Combatant> Combatants { get; private set; }
        internal object CombatantsLock => new object();

        private const string charmapSignature32 = "81f9ffff0000741781f958010000730f8b0c8d";
        private const string charmapSignature64 = "488b420848c1e8033da701000077248bc0488d0d";
        private const string targetSignature32 = "750E85D2750AB9";
        private const string targetSignature64 = "41bc000000e041bd01000000493bc47555488d0d";
        private const string zoneIdSignature32 = "a802752f8b4f04560fb735";
        private const string zoneIdSignature64 = "f64033020f85********83ff0175288b0d";
        private const string serverTimeSignature32 = "c20400558bec83ec0c53568b35";
        private const string serverTimeSignature64 = "4833c448898424d0040000488be9c644243000488b0d";
        private const string chatLogStartSignature32 = "8b45fc83e0f983c809ff750850ff35********e8********8b0d";
        private const string chatLogStartSignature64 = "e8********85c0740e488b0d********33D2E8********488b0d";
        private const string fateListSignature32 = "8bc8e8********8bf0eb**33f6ff75**8bcee8********8b15";
        private const string fateListSignature64 = "be********488bcbe8********4881c3********4883ee**75**488b05";
        private const string contentFinderConditionSignature32 = "e8********5f5e5dc2****0fb646**b9";
        private const string contentFinderConditionSignature64 = "440fb643**488d51**488d0d";
        private const string serverIdSignature32 = "e8********8d8e********e8********8d8e********e8********8d8e********e8********b9********e8********8d8e********e8********b9********e8********b9********e8********a1";
        private const string serverIdSignature64 = "e8********488d8b********e8********488d8b********e8********488d8b********e8********488d0d********e8********488d8b********e8********488d0d********e8********488d0d********e8********488b15";
        private const string lastFailedCommandSignature32 = "83f9**7c**5b5fb8";
        private const string lastFailedCommandSignature64 = "4183f8**7c**488d05";
        //private const int charmapOffset32 = 0;
        //private const int charmapOffset64 = 0;
        private const int targetOffset32 = 0x58;
        private const int targetOffset64 = 0;
        private const int contentFinderConditionOffset32 = 0xC4;
        private const int contentFinderConditionOffset64 = 0xF4;
        private const int currentContentFinderConditionOffset32 = 0x50;
        private const int currentContentFinderConditionOffset64 = 0x50;
        private const int lastFailedCommandOffset32 = 0x1B2;
        private const int lastFailedCommandOffset64 = 0x1C2;
        private static readonly int[] serverTimeOffset32 = { 0x14C0, 0x4, 0x644 };
        private static readonly int[] serverTimeOffset64 = { 0x1710, 0x8, 0x7D4 };
        private static readonly int[] chatLogStartOffset32 = { 0x18, 0x2C0, 0x0 };
        private static readonly int[] chatLogStartOffset64 = { 0x30, 0x3D8, 0x0 };
        private static readonly int[] chatLogTailOffset32 = { 0x18, 0x2C4 };
        private static readonly int[] chatLogTailOffset64 = { 0x30, 0x3E0 };
        private static readonly int[] serverIdOffset32 = { 0x29AC/*from ASM*/, 0x10, 0x174 };
        private static readonly int[] serverIdOffset64 = { 0x2D10/*from ASM*/, 0x18, 0x288 };
        private static readonly int[] fateListOffset32 = { 0x13A8, 0x0 };
        private static readonly int[] fateListOffset64 = { 0x16F8, 0x0 };
        private FFXIVClientMode _mode;

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

        public FFXIVMemory(Process process)
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

            Combatants = new List<Combatant>();
            cts = new CancellationTokenSource();
            _thread = new Thread(new ThreadStart(DoScanCombatants))
            {
                IsBackground = true
            };
            _thread.Start();
            if (PNPS == null)
                PNPS = new PersistentNamedPipeServer();
        }

        public void Dispose()
        {
            cts.Cancel();
            while (_thread.IsAlive)
                Task.Delay(10);
            Debug.WriteLine("FFXIVMemory Instance disposed");
        }

        private void DoScanCombatants()
        {
            List<Combatant> c;
            while (!cts.IsCancellationRequested)
            {
                Thread.Sleep(100);
                if (!ValidateProcess())
                {
                    Thread.Sleep(1000);
                    return;
                }

                c = _getCombatantList();
                lock (CombatantsLock)
                {
                    Combatants = c;
                }
            }
        }

        public enum FFXIVClientMode
        {
            Unknown = 0,
            FFXIV_32 = 1,
            FFXIV_64 = 2,
        }

        public Process Process { get; }

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
            string charmapSignature = (_mode == FFXIVClientMode.FFXIV_32) ? charmapSignature32 : charmapSignature64;
            string targetSignature = (_mode == FFXIVClientMode.FFXIV_32) ? targetSignature32 : targetSignature64;
            string zoneIdSignature = (_mode == FFXIVClientMode.FFXIV_32) ? zoneIdSignature32 : zoneIdSignature64;
            string serverTimeSignature = (_mode == FFXIVClientMode.FFXIV_32) ? serverTimeSignature32 : serverTimeSignature64;
            string chatLogStartSignature = (_mode == FFXIVClientMode.FFXIV_32) ? chatLogStartSignature32 : chatLogStartSignature64;
            string fateListSignature = (_mode == FFXIVClientMode.FFXIV_32) ? fateListSignature32 : fateListSignature64;
            string contentFinderConditionSignature = (_mode == FFXIVClientMode.FFXIV_32) ? contentFinderConditionSignature32 : contentFinderConditionSignature64;
            string serverIdSignature = (_mode == FFXIVClientMode.FFXIV_32) ? serverIdSignature32 : serverIdSignature64;
            string lastFailedCommandSignature = (_mode == FFXIVClientMode.FFXIV_32) ? lastFailedCommandSignature32 : lastFailedCommandSignature64;
            int[] serverTimeOffset = (_mode == FFXIVClientMode.FFXIV_32) ? serverTimeOffset32 : serverTimeOffset64;
            int[] chatLogStartOffset = (_mode == FFXIVClientMode.FFXIV_32) ? chatLogStartOffset32 : chatLogStartOffset64;
            int[] chatLogTailOffset = (_mode == FFXIVClientMode.FFXIV_32) ? chatLogTailOffset32 : chatLogTailOffset64;
            int[] serverIdOffset = (_mode == FFXIVClientMode.FFXIV_32) ? serverIdOffset32 : serverIdOffset64;
            int targetOffset = (_mode == FFXIVClientMode.FFXIV_32) ? targetOffset32 : targetOffset64;
            //int charmapOffset = (_mode == FFXIVClientMode.FFXIV_32) ? charmapOffset32 : charmapOffset64;
            int contentFinderConditionOffset = (_mode == FFXIVClientMode.FFXIV_32) ? contentFinderConditionOffset32 : contentFinderConditionOffset64;
            int currentContentFinderConditionOffset = (_mode == FFXIVClientMode.FFXIV_32) ? currentContentFinderConditionOffset32 : currentContentFinderConditionOffset64;
            int lastFailedCommandOffset = (_mode == FFXIVClientMode.FFXIV_32) ? lastFailedCommandOffset32 : lastFailedCommandOffset64;

            List<string> fail = new List<string>();

            bool bRIP = (_mode == FFXIVClientMode.FFXIV_32) ? false : true;

            // CHARMAP
            List<IntPtr> list = SigScan(charmapSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                charmapAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                charmapAddress = list[0] /*+ charmapOffset*/;
            }
            if (charmapAddress == IntPtr.Zero)
            {
                fail.Add(nameof(charmapAddress));
            }
            Combatant c = GetSelfCombatant();
            if (c == null)//No need scan for the remaining signatures
                throw new MemoryScanException(string.Format(Properties.Resources.FailedToSigScan, string.Join(",", fail)));

            // TARGET
            list = SigScan(targetSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                targetAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                targetAddress = list[0] + targetOffset;
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

            // SERVERID & CURRENTCONTENTFINDERCONDITION
            list = SigScan(serverIdSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                serverIdAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                //Meh
                var t = IntPtr.Add(list[0], serverIdOffset.First());
                serverIdAddress = ResolvePointerPath(t, serverIdOffset.Skip(1).ToArray());
                currentContentFinderConditionAddress = IntPtr.Add(t, currentContentFinderConditionOffset);
            }
            if (serverIdAddress == IntPtr.Zero)
            {
                fail.Add(nameof(serverIdAddress));
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

            if (c != null)
            {
                Debug.WriteLine("MyCharacter: '{0}' ({1})", c.Name, c.ID);
            }

            if (fail.Any())
            {
                throw new MemoryScanException(string.Format(Properties.Resources.FailedToSigScan, string.Join(",", fail)));
            }
            return !fail.Any();
        }

        internal void WipeLastFailedCommand(byte len = 62)
        {
            if (len > 62)
                len = 62;
            byte[] arr = new byte[len];
            NativeMethods.WriteProcessMemory(Process.Handle, lastFailedCommandAddress, arr, (uint)arr.Length, out uint lpNumberOfBytesWritten);
        }

        internal string GetLastFailedCommand() => GetStringFromBytes(GetByteArray(lastFailedCommandAddress, 70), 0, 70);

        internal ushort GetCurrentContentFinderCondition() => BitConverter.ToUInt16(GetByteArray(currentContentFinderConditionAddress, 2), 0);

        internal ContentFinder GetContentFinder()
        {
            var ba = GetByteArray(contentFinderConditionAddress, 0x100);
            return new ContentFinder
            {
                ContentFinderConditionID = BitConverter.ToUInt16(ba, 0),
                State = (ContentFinderState)ba[_mode == FFXIVClientMode.FFXIV_64 ? 0x71 : 0x69],
                RouletteID = ba[_mode == FFXIVClientMode.FFXIV_64 ? 0x76 : 0x6E],
            };
        }

        internal List<ChatMessage> ReadChatLogBackwards(int count)
        {
            var ChatLog = new List<ChatMessage>();
            //will overflow if chatlog contains 4+mil messages of max size
            ulong length = (_mode == FFXIVClientMode.FFXIV_64) ? GetUInt64(chatLogTailAddress) - (ulong)chatLogStartAddress.ToInt64() : GetUInt32(chatLogTailAddress) - (ulong)chatLogStartAddress.ToInt64();
            byte[] ws = GetByteArray(chatLogStartAddress, (uint)length);
            int currentStart = ws.Length;
            int currentEnd = ws.Length;
            while (currentStart > 0 && count > 0)
            {
                currentStart--;
                if (ws[currentStart] == 0x00 && ws[currentStart - 1] == 0x00)
                {
                    currentStart -= 7;
                    ChatLog.Add(new ChatMessage(ws.Skip(currentStart).Take(currentEnd - currentStart).ToArray()));
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
            ulong tail = (_mode == FFXIVClientMode.FFXIV_64) ? GetUInt64(chatLogTailAddress) : GetUInt32(chatLogTailAddress);
            NativeMethods.WriteProcessMemory(Process.Handle, (IntPtr)tail, msg, Convert.ToUInt32(msg.Length), out uint written);
            if (_mode == FFXIVClientMode.FFXIV_64)
                NativeMethods.WriteProcessMemory(Process.Handle, chatLogTailAddress, BitConverter.GetBytes(tail + Convert.ToUInt64(msg.Length)), sizeof(ulong), out written);
            else
                NativeMethods.WriteProcessMemory(Process.Handle, chatLogTailAddress, BitConverter.GetBytes(tail + Convert.ToUInt32(msg.Length)), sizeof(uint), out written);
            var SlashInstanceCommand = new PipeMessage { PID = Program.mem.Process.Id, Cmd = PMCommand.SlashInstance };
            PersistentNamedPipeServer.SendPipeMessage(SlashInstanceCommand);
        }

        //Something in here throws, on Win10, it gets "caught" but ... ?
        private async Task TryInject()
        {
            string dllfile = (_mode == FFXIVClientMode.FFXIV_32) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Hunt.dll") : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Hunt_x64.dll");
            byte[] ba = (_mode == FFXIVClientMode.FFXIV_32) ? Properties.Resources.Hunt : Properties.Resources.Hunt_x64;
            //not written already, or old version
            if (!File.Exists(dllfile) || !File.ReadAllBytes(dllfile).SequenceEqual(ba))
            {
                try
                {
                    File.WriteAllBytes(dllfile, ba);
                }
                catch (Exception) { }
            }
            if (!PersistentNamedPipeServer.IsConnected())
            {
                await NativeMethods.InjectDLL(Process.Handle, dllfile);
                for(int w = 0; !PersistentNamedPipeServer.IsConnected() && w < 1000; w += 100)
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
            ulong bytes = (_mode == FFXIVClientMode.FFXIV_64) ? GetUInt64(address) : GetUInt32(address);
            foreach (int offset in offsets)
            {
                address = IntPtr.Add((IntPtr)bytes, offset);
                bytes = (_mode == FFXIVClientMode.FFXIV_64) ? GetUInt64(address) : GetUInt32(address);
            }
            return address;
        }

        internal DateTime GetServerUtcTime()
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(GetUInt32(serverTimeAddress)).AddMilliseconds(GetUInt16(serverTimeAddress,4));
        }

        public Combatant GetTargetCombatant()
        {
            Combatant target = null;
            IntPtr address = IntPtr.Zero;

            byte[] source = GetByteArray(targetAddress, 128);
            unsafe
            {
                if (_mode == FFXIVClientMode.FFXIV_64)
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
            target = GetCombatantFromByteArray(source);
            return target;
        }

        public Combatant GetSelfCombatant()
        {
            Combatant self = null;
            IntPtr address = (_mode == FFXIVClientMode.FFXIV_64) ? (IntPtr)GetUInt64(charmapAddress) : (IntPtr)GetUInt32(charmapAddress);
            if (address.ToInt64() > 0)
            {
                byte[] source = GetByteArray(address, 0x3F40);
                self = GetCombatantFromByteArray(source);
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

        internal bool IsValidServerId()
        {
            ushort id = GetServerId();
            return id > 22 && id < 100;
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
            IntPtr liststart = ResolvePointerPath(fateListAddress, (_mode == FFXIVClientMode.FFXIV_64) ? fateListOffset64 : fateListOffset32);
            const byte maxFATEs = 8;
            List<IntPtr> fatePtrs = new List<IntPtr>(maxFATEs);
            List<FATE> fates = new List<FATE>(maxFATEs);
            var size = (_mode == FFXIVClientMode.FFXIV_64) ? 8 * maxFATEs : 4 * maxFATEs;

            for (int i = 0; i < size;)
            {
                IntPtr ptr = (_mode == FFXIVClientMode.FFXIV_64) ? (IntPtr)GetUInt64(liststart, i) : (IntPtr)GetUInt32(liststart, i);
                if (ptr.Equals(IntPtr.Zero))
                    break;
                fatePtrs.Add(ptr);
                i = (_mode == FFXIVClientMode.FFXIV_64) ? i + 8 : i + 4;
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
                Duration = BitConverter.ToInt16(ba, 0x28),
                ReadName = (_mode == FFXIVClientMode.FFXIV_64) ? GetStringFromBytes(ba, 0xE2) : GetStringFromBytes(ba, 0xAA),
                State = (_mode == FFXIVClientMode.FFXIV_64) ? (FATEState)ba[0x3AC] : (FATEState)ba[0x2F4],
                Progress = (_mode == FFXIVClientMode.FFXIV_64) ? ba[0x3B3] : ba[0x2FB],
                PosX = (_mode == FFXIVClientMode.FFXIV_64) ? BitConverter.ToSingle(ba, 0x400) : BitConverter.ToSingle(ba, 0x340),
                PosZ = (_mode == FFXIVClientMode.FFXIV_64) ? BitConverter.ToSingle(ba, 0x404) : BitConverter.ToSingle(ba, 0x344),
                PosY = (_mode == FFXIVClientMode.FFXIV_64) ? BitConverter.ToSingle(ba, 0x408) : BitConverter.ToSingle(ba, 0x348),
                ZoneID = (_mode == FFXIVClientMode.FFXIV_64) ? BitConverter.ToUInt16(ba, 0x624) : BitConverter.ToUInt16(ba, 0x4F4)
            };
            if (BitConverter.ToInt16(ba, 0x18) == 0 || f.Progress < 0 || f.Progress > 100)
                return null;
            else
                return f;
        }

        internal unsafe List<Combatant> _getCombatantList()
        {
            uint num = 344;
            List<Combatant> result = new List<Combatant>();

            uint sz = (_mode == FFXIVClientMode.FFXIV_64) ? (uint)8 : 4;
            byte[] source = GetByteArray(charmapAddress, sz * num);
            if (source == null || source.Length == 0) { return result; }

            for (int i = 0; i < num; i++)
            {
                IntPtr p;
                if (_mode == FFXIVClientMode.FFXIV_64)
                    fixed (byte* bp = source) p = new IntPtr(*(long*)&bp[i * sz]);
                else
                    fixed (byte* bp = source) p = new IntPtr(*(int*)&bp[i * sz]);

                if (!(p == IntPtr.Zero))
                {
                    byte[] c = GetByteArray(p, 0x25D0);
                    Combatant combatant = GetCombatantFromByteArray(c);
                    //skip
                    if (combatant.Type == ObjectType.Minion || combatant.Type == ObjectType.Furniture || combatant.Type == ObjectType.Gathering || combatant.Type == ObjectType.NPC || combatant.Type == ObjectType.LeyLines || combatant.Type == ObjectType.Retainer)
                        continue;
                    if (combatant.ID != 0 && combatant.ID != 3758096384u && !result.Exists((Combatant x) => x.ID == combatant.ID))
                    {
                        combatant.Order = i;
                        result.Add(combatant);
                    }
                }
            }
            return result;
        }

        internal unsafe Combatant GetCombatantFromByteArray(byte[] source)
        {
            int offset = 0;
            Combatant combatant = new Combatant();
            fixed (byte* p = source)
            {
                combatant.Name = GetStringFromBytes(source, 0x30);
                combatant.ID = *(uint*)&p[0x74];
                combatant.OwnerID = *(uint*)&p[0x84];
                if (combatant.OwnerID == 3758096384u)
                {
                    combatant.OwnerID = 0u;
                }
                combatant.Type = (ObjectType)p[0x8C];//0x8A
                combatant.EffectiveDistance = p[0x92];

                offset = 0xA0;
                combatant.PosX = *(float*)&p[offset];
                combatant.PosZ = *(float*)&p[offset + 4];
                combatant.PosY = *(float*)&p[offset + 8];
                combatant.Heading = *(float*)&p[offset + 16];


                if (combatant.Type == ObjectType.Monster)
                {
                    //if(*(uint*)&p[0xE4]==2149253119)//necessary?
                    combatant.FateID = *(uint*)&p[0xE8];
                    combatant.ContentID = (_mode == FFXIVClientMode.FFXIV_64) ? *(ushort*)&p[0x1694] : *(ushort*)&p[0x136C];
                }
                else
                    combatant.FateID = combatant.ContentID = 0;

                offset = (_mode == FFXIVClientMode.FFXIV_64) ? 0x1D8 : 0x1C8;
                combatant.TargetID = *(uint*)&p[offset];
                if (combatant.TargetID == 3758096384u)
                {
                    combatant.TargetID = (_mode == FFXIVClientMode.FFXIV_64) ? *(uint*)&p[0x990] : *(uint*)&p[0x9D8];
                }

                if (combatant.Type == ObjectType.PC || combatant.Type == ObjectType.Monster)
                {
                    offset = (_mode == FFXIVClientMode.FFXIV_64) ? 0x16B0 : 0x1388;
                    combatant.Job = (JobEnum)p[offset + 0x3E];
                    combatant.Level = p[offset + 0x40];
                    combatant.CurrentHP = *(uint*)&p[offset + 0x8];
                    combatant.MaxHP = *(uint*)&p[offset + 0xC];
                    combatant.CurrentMP = *(uint*)&p[offset + 0x10];
                    combatant.MaxMP = *(uint*)&p[offset + 0x14];
                    combatant.CurrentTP = *(ushort*)&p[offset + 0x18];
                    combatant.MaxTP = 1000;
                    combatant.CurrentGP = *(ushort*)&p[offset + 26];
                    combatant.MaxGP = *(ushort*)&p[offset + 28];
                    combatant.CurrentCP = *(ushort*)&p[offset + 30];
                    combatant.MaxCP = *(ushort*)&p[offset + 32];

                    offset = (_mode == FFXIVClientMode.FFXIV_64) ? offset + 0xB8 : offset + 0x94;
                    int countedStatusEffects = 0;
                    while (countedStatusEffects < 32)
                    {
                        Status status = new Status() { ID = *(short*)&p[offset] };
                        if (status.ID != 00)
                        {
                            status.Value = *(short*)&p[offset + 2];
                            status.Timer = *(float*)&p[offset + 4];
                            status.CasterId = *(uint*)&p[offset + 8];
                            combatant.StatusList.Add(status);
                        }
                        offset += 12;
                        countedStatusEffects++;
                    }
                }
                else
                {
                    combatant.CurrentHP =
                    combatant.MaxHP =
                    combatant.CurrentMP =
                    combatant.MaxMP =
                    combatant.MaxTP =
                    combatant.MaxGP =
                    combatant.CurrentGP =
                    combatant.CurrentCP =
                    combatant.CurrentTP = 0;
                }
            }
            return combatant;
        }

        internal async Task PlayPerformance(Performance p, CancellationToken cts)
        {
            if (!PersistentNamedPipeServer.IsConnected())
                await TryInject();
            await p.PlayAsync(Process.Id, cts);
        }

        internal async Task PlayMML(ImplementedPlayer p, CancellationToken cts)
        {
            p.Unmute();
            p.Play();
            if (!PersistentNamedPipeServer.IsConnected())
                await TryInject();
            Task.Run(async () =>
            {
                TimeSpan ts = new TimeSpan(0);
                foreach (var n in p.Tracks.First().notes)
                {
                    var w = (n.TimeSpan - ts);
                    if (w.TotalMilliseconds > 0)
                    {
                        Debug.WriteLine("Waiting for " + w.TotalMilliseconds + "ms");
                        await Task.Delay(w);
                    }
                    if (cts.IsCancellationRequested)
                        break;
                    PersistentNamedPipeServer.SendPipeMessage(new PipeMessage { PID = Process.Id, Cmd = PMCommand.PlayNote, Parameter = n.GetStep() });
                    ts = n.TimeSpan;
                }
            }).Start();
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

        private unsafe uint GetUInt16(IntPtr address, int offset = 0)
        {
            uint ret;
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
            IntPtr arg_05_0 = IntPtr.Zero;
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
            int moduleMemorySize = Process.MainModule.ModuleMemorySize;
            IntPtr baseAddress = Process.MainModule.BaseAddress;
            IntPtr intPtr = IntPtr.Add(baseAddress, moduleMemorySize);
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
                            else if (_mode == FFXIVClientMode.FFXIV_64)
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

        public static int FindPattern(byte[] source, byte[] pattern)
        {
            bool found = false;
            for (int i = 0; i < source.Length - pattern.Length; i++)
            {
                //see if it has pattern
                found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }

            return -1;
        }

        internal static IntPtr SigScan(byte[] toFind, Process p)
        {
            //Assuming this regionSize is big enough
            return SigScan(toFind, p, 0x7fff, 0x10000000);
        }

        internal static IntPtr SigScan(byte[] toFind, Process osup, int regionSize, int scanSize)
        {
            IntPtr startAddress = osup.MainModule.BaseAddress;
            IntPtr endAddress = startAddress + scanSize;

            IntPtr currentAddress = startAddress;
            int region = regionSize;

            byte[] buffer = new byte[region];

            while (currentAddress.ToInt64() < endAddress.ToInt64())
            {
                buffer = ReadBytes(osup, currentAddress, (IntPtr)region + toFind.Length);
                int index = FindPattern(buffer, toFind);
                if (index != -1)
                    return currentAddress + index;
                currentAddress += region;
            }
            return IntPtr.Zero;
        }

        public static byte[] ReadBytes(Process p, IntPtr address, IntPtr size)
        {
            byte[] ret = new byte[65536];
            IntPtr zero = IntPtr.Zero;
            NativeMethods.ReadProcessMemory(p.Handle, address, ret, size, ref zero);
            return ret;
        }

        static bool ByteArrayCompare(byte[] a1, byte[] a2)
        {
            return System.Collections.StructuralComparisons.StructuralEqualityComparer.Equals(a1, a2);
        }


    }
}
