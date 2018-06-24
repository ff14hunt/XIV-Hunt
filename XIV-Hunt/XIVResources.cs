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
    public class FATEInfo
    {
        private const string HtmlTagsRegex = "<.*?>";
        public byte ClassJobLevel { get; private set; }
        public string Name { get; private set; }
        public string NameWithTags { get; private set; }
        public string IconMap { get; private set; }
        public bool EurekaFate { get; private set; } = false;

        public FATEInfo(CsvParser csv)
        {
            NameWithTags = csv[nameof(Name)].Trim('"', ' ');
            ClassJobLevel = byte.Parse(csv[nameof(ClassJobLevel)]);
            Name = Regex.Replace(NameWithTags, HtmlTagsRegex, string.Empty);
            IconMap = csv["Icon{Map}"].Trim('"').Replace(".tex", ".png");
            if (csv.HasColum(nameof(EurekaFate)))
                EurekaFate = csv[nameof(EurekaFate)].Trim('"') == "1";
        }
    }

    static class GameResources
    {
        private const string SquareBrauquetsRegex = @"\[(.*?)\]";
        private static readonly string[] lineEnding = new string[] { Environment.NewLine };
        private static readonly Dictionary<ushort, FATEInfo> Fate = IndexFates();
        private static Dictionary<ushort, ushort> CachedSizeFactors = new Dictionary<ushort, ushort>();
        private static readonly Dictionary<ushort, string> WorldNames = Resources.World.Split(lineEnding, StringSplitOptions.None).Skip(3).Where(ValidWorld).Select(line => line.Split(',')).ToDictionary(line => ushort.Parse(line[0]), line => line[1].Trim('"'));
        private static readonly Dictionary<ushort, string> ContentFinderCondition = IndexContentFinderCondition();

        private static Dictionary<ushort, string> IndexContentFinderCondition()
        {
            string[] lines = Resources.ContentFinderCondition.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries);
            int namepos = Array.IndexOf(lines[1].Split(','), "InstanceContent");
            return lines.Skip(4).Select(x => x.Split(',')).Where(x => !string.IsNullOrWhiteSpace(x[namepos].Trim('"'))).ToDictionary(x => ushort.Parse(x[0]), x => x[namepos].Trim('"').FirstLetterToUpperCase());
        }

        private static Dictionary<ushort, FATEInfo> IndexFates()
        {
            Dictionary<ushort, FATEInfo> d = new Dictionary<ushort, FATEInfo>();
            CsvParser csv = new CsvParser(Resources.Fate);
            while (csv.Advance())
            {
                if (!string.IsNullOrWhiteSpace(csv["Name"].Trim('"')))
                    d.Add(ushort.Parse(csv["#"]), new FATEInfo(csv));
            }
            return d;
        }

        private static bool ValidWorld(string s)
        {
            string[] col = s.Split(',');
            return ushort.TryParse(col[0], out ushort _) && (col[4] == "True" && col[3].Trim('"') != "INVALID" || col[3].Trim('"') == "China" || col[3].Trim('"') == "Korea");
        }

        internal static bool IsValidWorldID(ushort id) => WorldNames.ContainsKey(id);

        internal static bool IsChineseWorld(ushort id) => id > 1023 && id < 1170;

        internal static bool IsKoreanWorld(ushort id) => id > 2074 && id < 2079;

        internal static string GetContentFinderName(ushort id)
        {
            if (ContentFinderCondition.TryGetValue(id, out string name))
                return name;
            return "Unknown duty: " + id;
        }

        internal static FATEInfo GetFATEInfo(ushort iD)
        {
            if (Fate.TryGetValue(iD, out FATEInfo fi))
                return fi;
            return null;
        }

        internal static ushort GetFateId(string name, bool ignoreCase = false)
        {
            foreach (KeyValuePair<ushort, FATEInfo> f in Fate)
                if (f.Value.Name.Equals(name, ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture))
                    return f.Key;
            return 0;
        }

        public static List<FATE> GetFates()
        {
            List<FATE> l = new List<FATE>();
            foreach (KeyValuePair<ushort, FATEInfo> f in Fate)
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
                //JP && CN does not have plural form
                plural = plural && !(Thread.CurrentThread.CurrentUICulture.Name == "ja-JP" || Thread.CurrentThread.CurrentUICulture.Name == "zh-CN" || Thread.CurrentThread.CurrentUICulture.Name == "ko-KR");
                string result = lines[id].Split(',')[plural ? 3 : 1].Trim('"');
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
            //JP & CN does not have plural form
            bool noPlural = !(Thread.CurrentThread.CurrentUICulture.Name == "ja-JP" || Thread.CurrentThread.CurrentUICulture.Name == "zh-CN" || Thread.CurrentThread.CurrentUICulture.Name == "ko-KR");
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
                if (!noPlural)
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

        internal static bool TryGetDailyHuntInfo(string huntSearchTerm, out Tuple<ushort, ushort, float, float> huntInfo)
        {
            huntInfo = new Tuple<ushort, ushort, float, float>(0, 0, 0, 0);
            ushort id = GetEnemyId(huntSearchTerm.Trim());
            if (id == 0)
                return false;
            int i = 0;
            string[] lines = Resources.DailyHunts.Split(lineEnding, StringSplitOptions.None);
            while (i < lines.Length)
            {
                if (lines[i].Split('|')[0].Equals(id.ToString()))
                {
                    string[] values = lines[i].Split('|');
                    huntInfo = new Tuple<ushort, ushort, float, float>(id, ushort.Parse(values[1]), float.Parse(values[2]), float.Parse(values[3]));
                    return true;
                }
                i++;
            }
            return false;
        }

        internal static bool TryGetZoneID(string ZoneName, out ushort ZoneID)
        {
            foreach (var line in Resources.TerritoryType.Split(lineEnding, StringSplitOptions.RemoveEmptyEntries).Skip(3))
            {
                if (line.Split(',')[6].Trim('"').Equals(ZoneName, StringComparison.CurrentCultureIgnoreCase))
                {
                    ZoneID = ushort.Parse(line.Split(',')[0]);
                    return true;
                }
            }
            ZoneID = 0;
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

    public class CsvParser
    {
        private const string LineSep = "\r\n";
        private readonly int colCount;
        private readonly Dictionary<string, int> columns = new Dictionary<string, int>();
        private List<string[]> Records;
        private int recordIterator = 0;
        public CsvParser(string csvstring)
        {
            string[] header = csvstring.Split(new string[] { LineSep }, StringSplitOptions.RemoveEmptyEntries)[1].Split(',');
            int i;
            for (i = 0; i < header.Length; i++)
                if (!string.IsNullOrWhiteSpace(header[i]))
                    columns.Add(header[i], i);
            colCount = i;
            Records = SplitRecords(csvstring.Substring(csvstring.IndexOfNth(LineSep, 0, 3) + LineSep.Length));
        }

        private List<string[]> SplitRecords(string recordsStr)
        {
            List<string[]> records = new List<string[]>();
            for (int valuestart = 0; valuestart < recordsStr.Length; valuestart++)
            {
                bool inQuote = false;
                string[] record = new string[colCount];
                int col = 0;
                for (int chariterator = valuestart + 1; chariterator < recordsStr.Length; chariterator++)
                {
                    if (!inQuote && recordsStr[chariterator] == '"')
                        inQuote = true;
                    else if (inQuote && (recordsStr.Substring(chariterator, 2) == "\"," || recordsStr.Substring(chariterator, 3) == "\"" + LineSep))
                        inQuote = false;
                    if (!inQuote && (recordsStr[chariterator] == ',' || recordsStr.Substring(chariterator, LineSep.Length) == LineSep))
                    {
                        record[col] = recordsStr.Substring(valuestart, chariterator - valuestart);
                        valuestart = chariterator + 1;
                        col++;
                        if (col == colCount)
                            break;
                        if (recordsStr.Substring(valuestart, LineSep.Length) == LineSep)
                        {
                            valuestart++;
                            break;
                        }
                    }
                }
                records.Add(record);
            }
            return records;
        }

        public bool Advance() => ++recordIterator < Records.Count;

        public string this[string colname] => Records.ElementAt(recordIterator)[columns[colname]];

        public bool HasColum(string colname) => columns.ContainsKey(colname);
    }
}
