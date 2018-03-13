using System;
using System.Linq;
using FFXIV_GameSense.Properties;
using System.Globalization;
using FFXIV_GameSense;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;

namespace XIVDB
{
    class FATEName
    {
        public string NoTags { get; set; }
        public string WithTags { get; set; }
    }
    static class GameResources
    {
        private const string SquareBrauquetsRegex = @"\[(.*?)\]";
        private static readonly string[] lineEnding = new string[] { Environment.NewLine };
        private const string HtmlTagsRegex = "<.*?>";
        private static Dictionary<ushort, FATEName> FATENames = Resources.Fate.Split(lineEnding, StringSplitOptions.None).Skip(3).Where(x => ushort.TryParse(x.Split(',')[0], out ushort result)).Select(line => GetFateLine(line)).Where(line => !string.IsNullOrWhiteSpace(line[28].Trim('"'))).ToDictionary(line => ushort.Parse(line[0]), line => ParseFATEName(line[28]));
        private static Dictionary<ushort, ushort> CachedSizeFactors = new Dictionary<ushort, ushort>();
        private readonly static Dictionary<ushort, string> WorldNames = Resources.World.Split(lineEnding, StringSplitOptions.None).Skip(3).Where(x => ushort.TryParse(x.Split(',')[0], out ushort result)).Select(line => line.Split(',')).ToDictionary(line => ushort.Parse(line[0]), line => line[1].Trim('"'));

        internal static string GetContentFinderName(ushort id)
        {
            string[] lines = Resources.ContentFinderCondition.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries).Skip(3).ToArray();
            if (id > 0 && id < lines.Length)
            {
                return lines[id].Split(',')[3].Trim('"').FirstLetterToUpperCase();
            }
            return "Unknown duty " + id;
        }

        private static string[] GetFateLine(string line)
        {
            string[] li = line.Replace(", ", "##COMMA##").Split(',');
            for (int i = 0; i < li.Length; i++)
                li[i] = li[i].Replace("##COMMA##", ", ");
            return li;
        }

        internal static string GetFateName(ushort iD, bool stripTags = true)
        {
            if (FATENames.TryGetValue(iD, out FATEName name))
                return stripTags ? name.NoTags : name.WithTags;
            return "Unknown FATE: " + iD;
        }

        private static FATEName ParseFATEName(string name)
        {
            string fn = name.Trim('"');
            return new FATEName { WithTags = fn, NoTags = Regex.Replace(fn, HtmlTagsRegex, string.Empty) };//meh
        }

        internal static ushort GetFateId(string name, bool ignoreCase = false)
        {
            foreach (KeyValuePair<ushort, FATEName> f in FATENames)
                if (f.Value.NoTags.Equals(name, ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture))
                    return f.Key;
            return 0;
        }

        public static List<FATE> GetFates()
        {
            List<FATE> l = new List<FATE>();
            foreach (KeyValuePair<ushort, FATEName> f in FATENames)
            {
                if (f.Key == 122 || f.Key == 145 || f.Key == 151 || f.Key == 130 || f.Key == 173 || f.Key == 182)
                    continue;
                l.Add(new FATE { ID = f.Key });
            }
            return l;
        }

        public static string GetEnemyName(uint id, bool plural = false)
        {
            string[] lines = Resources.BNpcName.Split(lineEnding, StringSplitOptions.None).Skip(3).ToArray();
            if (id > lines.Length)
                return string.Empty;
            else
            {
                //JP does not have plural form
                bool jp = Thread.CurrentThread.CurrentUICulture.Name == "ja-JP";
                string result = lines[id].Split(',')[plural && !jp ? 3 : 1].Trim('"');
                //DE has [a], [p] tags... gramatical stuff; discard them
                if (Thread.CurrentThread.CurrentUICulture.Name == "de-DE")
                    result = Regex.Replace(result, SquareBrauquetsRegex, string.Empty);
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result);
            }
        }

        public static bool GetEnemyId(string Name, out ushort id)
        {
            int i = 0;
            string[] lines = Resources.BNpcName.Split(lineEnding, StringSplitOptions.None).Skip(3).ToArray();
            //JP does not have plural form
            bool jp = Thread.CurrentThread.CurrentUICulture.Name == "ja-JP";
            while (i < lines.Length - 1)
            {
                string singular = lines[i].Split(',')[1].Trim('"');
                string plural = lines[i].Split(',')[3].Trim('"');
                //DE has [a], [p] tags... gramatical stuff; discard them
                if (Thread.CurrentThread.CurrentUICulture.Name == "de-DE")
                {
                    singular = Regex.Replace(singular, SquareBrauquetsRegex, string.Empty);
                    plural = Regex.Replace(plural, SquareBrauquetsRegex, string.Empty);
                }
                //FR and DE plural forms seem to have a lot of <SoftHyphen/> tags that need to be discarded
                if (!jp)
                    plural = plural.Replace("<SoftHyphen/>", string.Empty);
                if (singular.Equals(Name, StringComparison.CurrentCultureIgnoreCase) || plural.Equals(Name, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (ushort.TryParse(lines[i].Split(',')[0], out ushort _id))
                    {
                        id = _id;
                        return true;
                    }
                }
                i++;
            }
            id = 0;
            return false;
        }

        public static string GetWorldName(ushort id)
        {
            if (WorldNames.TryGetValue(id, out string wn))
                return wn;
            return "Unknown World: " + id;
        }

        internal static string GetZoneName(uint zoneId)
        {
            int i = 0;
            string[] lines = Resources.TerritoryType.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries);
            while (i < lines.Length)
            {
                if (lines[i].Split(',')[0] == zoneId.ToString())
                    return lines[i].Split(',')[6].Trim('"');
                i++;
            }
            return "Unknown zoneID: " + zoneId;
        }

        internal static string GetMapCodeName(uint zoneId)
        {
            int i = 0;
            string[] lines = Resources.TerritoryType.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries);
            while (i < lines.Length)
            {
                if (lines[i].Split(',')[0] == zoneId.ToString())
                    return lines[i].Split(',')[1].Trim('"');
                i++;
            }
            return null;
        }

        internal static ushort GetZoneIdFromCodeName(string name)
        {
            int i = 0;
            string[] lines = Resources.TerritoryType.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries).Skip(3).ToArray();
            while (i < lines.Length)
            {
                if (lines[i].Split(',')[1].Trim('"').Equals(name))
                    return ushort.Parse(lines[i].Split(',')[0]);
                i++;
            }
            return 0;
        }

        public static ushort GetSizeFactor(ushort zoneId)
        {
            if (CachedSizeFactors.ContainsKey(zoneId))
                return CachedSizeFactors[zoneId];
            string[] lines = Resources.Map.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries);
            string name = GetMapCodeName(zoneId);
            int i = 0;
            while (i < lines.Length)
            {
                if (lines[i].Split(',')[15].Trim('"').Equals(name) && ushort.TryParse(lines[i].Split(',')[7], out ushort x))
                {
                    CachedSizeFactors.Add(zoneId, x);
                    return x;
                }
                i++;
            }
            return 0;
        }

        internal static ushort MapIdToZoneId(uint mapId)
        {
            string codename = Resources.Map.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries).Skip(3).ToArray()[mapId].Split(',')[15].Trim('"');
            return GetZoneIdFromCodeName(codename);
        }

        /// <summary>
        /// Returns the byte[] used, by the game, to identify which zone/subzone to open in the map
        /// </summary>
        /// <param name="zoneId"></param>
        /// <returns></returns>
        internal static byte[] GetMapMarkerZoneId(ushort zoneId, ushort subMapId = 0/*, string SubMapName=""*/)
        {
            List<byte> bl = BitConverter.GetBytes(zoneId).Reverse().ToList();
            Dictionary<ushort, string> Maps = GetMapIds(zoneId);
            if (subMapId != 0 && Maps.ContainsKey(subMapId))
            {
                bl.AddRange(BitConverter.GetBytes(subMapId).Reverse().ToList());
            }
            //else if (!string.IsNullOrWhiteSpace(SubMapName) && Maps.ContainsValue(SubMapName))
            //{
            //    bl.AddRange(BitConverter.GetBytes(Maps.FirstOrDefault(x => x.Value.Equals(SubMapName)).Key).Reverse().ToList());
            //}
            else
                bl.AddRange(BitConverter.GetBytes(Maps.Keys.Min()).Reverse().ToList());
            bl.RemoveAll(x => x == 0x00);
            switch (bl.Count)
            {
                case 2: bl.Insert(0, 0xF4); break;
                case 3: bl.Insert(0, 0xFC); break;
                case 4: bl.Insert(0, 0xFE); break;
            }
            return bl.ToArray();
        }

        private static Dictionary<ushort, string> GetMapIds(ushort zoneId)
        {
            Dictionary<ushort, string> d = new Dictionary<ushort, string>();
            string[] lines = Resources.Map.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries);
            string name = GetMapCodeName(zoneId);
            int i = 0;
            while (i < lines.Length)
            {
                string[] line = lines[i].Split(',');
                if (line[15].Trim('"') == name && ushort.TryParse(line[0], out ushort x))
                {
                    d.Add(x, line[12].Trim('"'));
                }
                i++;
            }
            return d;
        }

        internal static Tuple<ushort, float, float> GetDailyHuntInfo(ushort id)
        {
            int i = 0;
            string[] lines = Resources.DailyHunts.Split(lineEnding, StringSplitOptions.None);
            while (i < lines.Length)
            {
                if (lines[i].Split('|')[0].Equals(id.ToString()))
                {
                    return new Tuple<ushort, float, float>(ushort.Parse(lines[i].Split('|')[1]), float.Parse(lines[i].Split('|')[2]), float.Parse(lines[i].Split('|')[3]));
                }
                i++;
            }
            return new Tuple<ushort, float, float>(0, 0f, 0f);
        }

        private static ushort GetEnemyId(string huntSearchTerm)
        {
            int i = 0;
            string[] lines = Resources.BNpcName.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries);
            while (i < lines.Length)
            {
                if (lines[i].Split(',')[1].Trim('"').Equals(huntSearchTerm, StringComparison.CurrentCultureIgnoreCase) && ushort.TryParse(lines[i].Split(',')[0], out ushort id))
                {
                    return id;
                }
                i++;
            }
            return 0;
        }

        internal static bool IsDailyHunt(string huntSearchTerm, out ushort id)
        {
            id = GetEnemyId(huntSearchTerm.Trim());
            if (id == 0)
                return false;
            int i = 0;
            string[] lines = Resources.DailyHunts.Split(lineEnding, StringSplitOptions.None);
            while (i < lines.Length)
            {
                if (lines[i].Split('|')[0].Equals(id.ToString()))
                {
                    return true;
                }
                i++;
            }
            return false;
        }

        internal static List<Note> GetPerformanceNotes()
        {
            var l = new List<Note>();
            foreach (string n in Resources.Perform.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries).Skip(4))
            {
                var np = n.Split(',');
                l.Add(new Note { Id = (byte)(byte.Parse(np[0]) + 0x17), Name = np[2].Trim('"') });
            }
            return l;
        }
    }
}
