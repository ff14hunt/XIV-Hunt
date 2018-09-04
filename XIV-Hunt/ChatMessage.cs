using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XIVDB;
using XIVAPI;

namespace FFXIV_GameSense
{
    internal class ChatMessage
    {
        internal DateTime Timestamp { get; set; }
        private uint Epoch => Timestamp.ToEpoch();
        internal ChatChannel Channel { get; set; }
        internal ChatFilter Filter { get; set; }
        internal Sender Sender { get; set; }
        private byte[] Message { get; set; }
        internal string MessageString
        {
            get => Encoding.UTF8.GetString(Message);
            set => Message = Encoding.UTF8.GetBytes(value);
        }
        private const string possep = "<pos>";
        private static readonly Dictionary<string, byte[]> Tags = new Dictionary<string, byte[]>
        {
            { "<Emphasis>",  new byte[] { 0x02, 0x1A, 0x02, 0x02, 0x03 } },
            { "</Emphasis>",  new byte[] { 0x02, 0x1A, 0x02, 0x01, 0x03 } },
            { "<SoftHyphen/>", new byte[] { 0x02, 0x16, 0x01, 0x03 } },
            { "<Indent/>", new byte[] { 0x02, 0x1D, 0x01, 0x03 } },
            { "<22/>",  new byte[] { 0x02, 0x16, 0x01, 0x03 } }
        };
        private static readonly byte[] arrow = new byte[] { 0xEE, 0x82, 0xBB, 0x02, 0x13, 0x02, 0xEC, 0x03 };
        private static readonly byte[] HQChar = new byte[] { 0xEE, 0x80, 0xBC };
        private static readonly Dictionary<int, byte[]> RarityColors = new Dictionary<int, byte[]>
        {
            { 1, new byte[] { 0xF3, 0xF3, 0xF3 } },
            { 2, new byte[] { 0xC0, 0xFF, 0xC0 } },
            { 3, new byte[] { 0x59, 0x90, 0xFF } },
            { 4, new byte[] { 0xB3, 0x8C, 0xFF } },
            { 7, new byte[] { 0xFA, 0x89, 0xB6 } }
        };
        
        /// <summary>
        /// Default constructor. Sets timestamp to now and channel to Echo;
        /// </summary>
        internal ChatMessage()
        {
            Timestamp = DateTime.UtcNow;
            Channel = ChatChannel.Echo;
            Filter = ChatFilter.Unknown;
            Message = null;
        }

        /// <summary>
        /// First 4 bytes should be unix timestamp.
        /// 5th byte should be ChatChannel.
        /// 6th byte should be ChatFilter(unknown).
        /// 9th byte should be 0x3A, followed by sender name (UTF-8), followed by 0x3A.
        /// The rest is the message, including payloads.
        /// </summary>
        /// <param name="arr">Byte array should be longer than 10</param>
        internal ChatMessage(byte[] arr/*, ushort wid*/)
        {
            if (arr.Length < 10)
            {
                return;
            }
            Timestamp = UnixTimeStampToDateTime(BitConverter.ToUInt32(arr.Take(4).ToArray(), 0));
            Channel = (ChatChannel)arr[4];
            Filter = (ChatFilter)arr[5];//hmm
            Sender = new Sender(arr.Skip(9).Take(Array.FindIndex(arr.Skip(9).ToArray(), x => x == 0x3A)).ToArray()/*, wid*/);
            int pos = arr.Skip(9).ToList().IndexOf(0x3A) + 10;
            Message = arr.Skip(pos).ToArray();
        }

        private static DateTime UnixTimeStampToDateTime(uint unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeStamp);
        }

        internal byte[] ToArray()
        {
            List<byte> a = new List<byte>();
            a.AddRange(BitConverter.GetBytes(Epoch));
            a.Add((byte)Channel);
            a.Add((byte)Filter);
            a.AddRange(new byte[] { 0x00, 0x00 });
            a.Add(Convert.ToByte(':'));
            if (Sender != null && !string.IsNullOrEmpty(Sender.Name))
                a.AddRange(Sender.ToArray());//TODO: ommit world and link when apropriate?
            a.Add(Convert.ToByte(':'));
            if (Message != null)
                a.AddRange(ReplaceTags(Message));
            return a.ToArray();
        }

        internal static byte[] ReplaceTags(byte[] msg)
        {
            foreach (KeyValuePair<string, byte[]> kvp in Tags)
                msg = msg.ReplaceSequence(Encoding.UTF8.GetBytes(kvp.Key), kvp.Value);
            return msg;
        }

        internal static ChatMessage MakeItemChatMessage(Item Item, string prepend = "", string postpend = "", bool HQ = false)
        {
            ChatMessage cm = new ChatMessage();
            byte[] raritycolor = RarityColors.ContainsKey(Item.Rarity) ? RarityColors[Item.Rarity] : RarityColors.First().Value;
            byte[] ItemHeader1And2 = new byte[] { 0x02, 0x13, 0x06, 0xFE, 0xFF }.Concat(raritycolor).Concat(new byte[] { 0x03, 0x02, 0x27, 0x07, 0x03, 0xF2 }).ToArray();
            byte[] ItemHeaderEnd = new byte[] { 0x02, 0x01, 0x03 };
            byte[] end = new byte[] { 0x02, 0x27, 0x07, 0xCF, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03, 0x02, 0x13, 0x02, 0xEC, 0x03 };
            HQ = HQ && Item.CanBeHq;
            if (HQ)
            {
                ItemHeader1And2[ItemHeader1And2.Length - 1] = 0xF6;
                ItemHeader1And2[ItemHeader1And2.Length - 3]++;
                end = HQChar.Concat(end).ToArray();
                Item.Name += " ";
            }
            byte[] idba = BitConverter.GetBytes(HQ ? Item.ID + 1000000 : Item.ID).Reverse().SkipWhile(x=>x==0x00).ToArray();
            if (Item.ID <= byte.MaxValue)//Currencies
            {
                ItemHeader1And2 = ItemHeader1And2.Take(ItemHeader1And2.Count() - 1).ToArray();
                idba = new byte[] { ++idba[0], 0x02 };
                ItemHeaderEnd = ItemHeaderEnd.Skip(1).ToArray();
                ItemHeader1And2[11] -= 2;
            }
            else if (idba.Last() == 0x00)
            {
                ItemHeader1And2[ItemHeader1And2.Length - 1]--;
                idba = new byte[] { idba[0] };
                ItemHeader1And2[11]--;
            }
            ItemHeader1And2 = ItemHeader1And2.Concat(idba).ToArray();
            //                                            ?     ?     R     G     B
            var color = new byte[] { 0x02, 0x13, 0x06, 0xFE, 0xFF, 0xFF, 0x7B, 0x1A, 0x03 };
            if (Array.IndexOf(ItemHeader1And2, 0x00) > -1)
                throw new ArgumentException("ItemHeader contains 0x00. Params: " + Item.ID, nameof(Item));
            cm.Message = Encoding.UTF8.GetBytes(prepend).Concat(ItemHeader1And2).Concat(ItemHeaderEnd).Concat(color).Concat(arrow).Concat(Encoding.UTF8.GetBytes(Item.Name)).Concat(end).ToArray();
            if (!string.IsNullOrEmpty(postpend))
                cm.Message = cm.Message.Concat(Encoding.UTF8.GetBytes(postpend)).ToArray();
            return cm;
        }

        internal static ChatMessage MakePosChatMessage(string prepend, ushort zoneId, float x, float y, string postpend = "", ushort mapId = 0)
        {
            var cm = new ChatMessage();
            var pos = new byte[] { 0x02, 0x27, 0x12, 0x04 };
            var posZone = GameResources.GetMapMarkerZoneId(zoneId, mapId);
            var posX = CoordToFlagPosCoord(x);
            var posY = CoordToFlagPosCoord(y);
            //z does not appear to be used for the link; only for the text
            var posEnd = new byte[] { 0xFF, 0x01, 0x03 };//end +/ terminator
            pos = pos.Concat(posZone).Concat(posX).Concat(posY).Concat(posEnd).ToArray();
            pos[2] = Convert.ToByte(pos.Length - 3);
            //                                            ?     ?     R     G     B
            var color = new byte[] { 0x02, 0x13, 0x06, 0xFE, 0xFF, 0xA3, 0xEA, 0xF3, 0x03 };
            var end = new byte[] { 0x02, 0x27, 0x07, 0xCF, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03 };
            if (Array.IndexOf(posEnd, 0x00) > -1)
                throw new ArgumentException("posPost contains 0x00. Params: " + zoneId + " " + x + " " + y);

            if (prepend.Contains(possep))
            {
                string[] split = prepend.Split(new string[] { possep }, 2, StringSplitOptions.None);
                prepend = split.First();
                postpend = split.Last() + postpend;
            }
            else if (postpend.Contains(possep))
            {
                string[] split = postpend.Split(new string[] { possep }, 2, StringSplitOptions.None);
                prepend = prepend + split.First();
                postpend = split.Last();
            }

            cm.Message = Encoding.UTF8.GetBytes(prepend).Concat(pos).Concat(color).Concat(arrow).Concat(Encoding.UTF8.GetBytes(GameResources.GetZoneName(zoneId) + " ( " + Entity.GetXReadable(x, zoneId).ToString("0.0").Replace(',', '.') + "  , " + Entity.GetYReadable(y, zoneId).ToString("0.0").Replace(',', '.') + " )")).Concat(end).ToArray();
            if (!string.IsNullOrEmpty(postpend))
                cm.Message = cm.Message.Concat(Encoding.UTF8.GetBytes(postpend)).ToArray();

            return cm;
        }

        private static byte[] CoordToFlagPosCoord(float coordinate)
        {
            coordinate *= 1000;
            if (coordinate == 0f)//only if c > -0.256f && c < 0.256f, either way, default case on switch later on will take care of it
                coordinate++;
            byte[] t = BitConverter.GetBytes((int)coordinate);
            while (t.Last() == 0)//trim big-endian leading zeros
                Array.Resize(ref t, t.Length - 1);
            Array.Reverse(t);
            byte[] temp;
            switch (t.Length)
            {
                case 4: temp = new byte[] { 0xFE }; break;
                case 2: temp = new byte[] { 0xF2 }; break;
                case 3: temp = new byte[] { 0xF6 }; break;
                default: temp = new byte[] { 0xF2, 0x01 }; break;//must be minimum 3 bytes long
            }
            t = temp.Concat(t).ToArray();
            //0x00 prevents the game continuing reading the coordinate payload.
            //workaround
            for (int i = 0; i < t.Length; i++)
                if (t[i] == 0x00)
                    t[i]++;
            return t;
        }
    }

    public class Sender
    {
        public string Name { get; private set; }
        //public ushort WorldID { get; private set; }
        //public string WorldName => GameResources.GetWorldName(WorldID);
        //private static readonly byte[] WorldSign = new byte[] { 0x02, 0x12, 0x02, 0x59, 0x03 };
        private static readonly byte[] LinkStart = new byte[] { 0x02, 0x27 };
        private static readonly byte[] LinkEnd = new byte[] { 0x02, 0x27, 0x07, 0xCF, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03 };
        private static readonly byte[] LinkStartTemplate = new byte[] { 0x02, 0x27, 0x00, 0x01, 0x1F, 0x01, 0x01, 0xFF, 0x0B, 0x00 };

        public Sender(byte[] senderpart/*, ushort wid*/)
        {
            if (senderpart.IndexOf(LinkStart) == 0)
            {
                //first occurence is full name, second is display-as (full name / surabbr / forabbr / initials)
                Name = Encoding.UTF8.GetString(senderpart.Skip(9).Take(senderpart[8] - 1).ToArray());
                int nameend = senderpart.IndexOf(LinkEnd) + LinkEnd.Length;
                //if (nameend != senderpart.Length)
                //    WorldID = GameResources.GetWorldID(Encoding.UTF8.GetString(senderpart.Skip(nameend + WorldSign.Length).ToArray()));
                //else
                //    WorldID = wid;
            }
            else
                Name = Encoding.UTF8.GetString(senderpart);
        }

        internal byte[] ToArray(bool link = true, bool world = true)
        {
            List<byte> arr = new List<byte>();
            if (!string.IsNullOrWhiteSpace(Name))
            {
                byte[] n = Encoding.UTF8.GetBytes(Name);
                if (link)
                {
                    arr.AddRange(LinkStartTemplate);
                    arr.AddRange(n);
                    arr[2] = Convert.ToByte(n.Length + 8);
                    arr.Add(0x03);
                    arr[9] = Convert.ToByte(n.Length + 1);
                }
                arr.AddRange(n);
                arr.AddRange(LinkEnd);
            }
            //if (world)
            //{
            //    arr.AddRange(WorldSign);
            //    arr.AddRange(Encoding.UTF8.GetBytes(WorldName));
            //}
            return arr.ToArray();
        }
    }

    enum ChatFilter : byte
    {
        Unknown = 0x00,
    }

    internal enum ChatChannel : byte
    {
        Unknown = 0x00,
        MoTDorSA1 = 0x03,
        //MoTDorSA2 = 0x04,
        //MoTDorSA3 = 0x05,
        //MoTDorSA4 = 0x06,
        //MoTDorSA5 = 0x07,
        //MoTDorSA6 = 0x08,
        //MoTDorSA7 = 0x09,
        Say = 0x0A,
        Shout = 0x0B,
        Tell = 0x0C,
        Tell_Receive = 0x0D,
        Party = 0x0E,
        Alliance = 0x0F,
        Linkshell1 = 0x10,
        Linkshell2 = 0x11,
        Linkshell3 = 0x12,
        Linkshell4 = 0x13,
        Linkshell5 = 0x14,
        Linkshell6 = 0x15,
        Linkshell7 = 0x16,
        Linkshell8 = 0x17,
        FreeCompany = 0x18,
        NoviceNetwork = 0x1B,
        CustomEmote = 0x1C,
        StandardEmote = 0x1D,
        Yell = 0x1E,
        Actions = 0x2B,
        Echo = 0x38,
        SystemMessages = 0x39,//example: you dissolve a party, you invite 'player name' to party, player 'player name' joins the party, updating online status to away
        Defeats = 0x3A,
        Error = 0x3C,//example: unable to change gear
        NPCChat = 0x3E,
        ObtainsAndConverts = 0x40,
        ExperienceAndLevel = 0x41,
        ItemRolls = 0x45,
        PFRecruitmentNoficiation = 0x48, // Of the 38 parties currently recruiting, all match your search conditions.
        LoginsAndLogouts = 0xA9,
        BuffLossAndGains = 0xAE,
        EffectGains = 0xAF
    }
}
