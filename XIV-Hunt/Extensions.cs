using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;

namespace FFXIV_GameSense
{
    internal static class Extensions
    {
        public static string RemoveLineComments(this string i)
        {
            string lineComments = "//";
            var p = i.IndexOf(lineComments);
            if (p > -1)
                return i.Substring(0, p);
            else
                return i;
        }

        public static string RemoveBlockComments(this string i)
        {
            var blockComments = @"/\*(.*?)\*/";
            return Regex.Replace(i, blockComments, me =>
            {
                if (me.Value.StartsWith("/*") || me.Value.StartsWith("//"))
                    return me.Value.StartsWith("//") ? Environment.NewLine : "";
                return me.Value;
            }, RegexOptions.Singleline);
        }

        public static int IndexOfNth(this string input, string value, int startIndex, int nth)
        {
            if (nth < 1)
                throw new NotSupportedException("Param 'nth' must be greater than 0!");
            if (nth == 1)
                return input.IndexOf(value, startIndex);
            var idx = input.IndexOf(value, startIndex);
            if (idx == -1)
                return -1;
            return input.IndexOfNth(value, idx + 1, --nth);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static byte[] ReplaceSequence(this byte[] input, byte[] toRemove, byte[] replaceWith)
        {
            if (toRemove.Length == 0)
                return input;
            List<byte> result = new List<byte>();
            int i;
            for (i = 0; i <= input.Length - toRemove.Length; i++)
            {
                bool foundMatch = true;
                for (int j = 0; j < toRemove.Length; j++)
                {
                    if (input[i + j] != toRemove[j])
                    {
                        foundMatch = false;
                        break;
                    }
                }
                if (foundMatch)
                {
                    result.AddRange(replaceWith);
                    i += toRemove.Length - 1;
                }
                else
                {
                    result.Add(input[i]);
                }
            }
            for (; i < input.Length; i++)
            {
                result.Add(input[i]);
            }
            return result.ToArray();
        }

        public static string ReplaceAt(this string input, int index, char newChar)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            StringBuilder builder = new StringBuilder(input);
            builder[index] = newChar;
            return builder.ToString();
        }

        public static string Replace(this string input, IEnumerable<string> replaceThese, string replaceWith)
        {
            foreach (string s in replaceThese)
                input = input.Replace(s, replaceWith);
            return input;
        }

        public static string Remove(this string input, IEnumerable<string> removeThese)
        {
            return input.Replace(removeThese, string.Empty);
        }

        public static char Increment(this char c)
        {
            return Convert.ToChar(Convert.ToInt32(c) + 1);
        }

        public static char Decrement(this char c)
        {
            return Convert.ToChar(Convert.ToInt32(c) - 1);
        }

        public static string FirstLetterToUpperCase(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;
            char[] a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }

        public static uint ToEpoch(this DateTime d)
        {
            return Convert.ToUInt32(((DateTimeOffset)TimeZoneInfo.ConvertTimeToUtc(d)).ToUnixTimeSeconds());
        }

        public static bool IsWithin(this float val, int min, int max)
        {
            return val >= min && val <= max;
        }

        public static int IndexOf<T>(this IEnumerable<T> collection, IEnumerable<T> sequence)
        {
            var ccount = collection.Count();
            var scount = sequence.Count();
            if (scount > ccount)
                return -1;
            if (collection.Take(scount).SequenceEqual(sequence))
                return 0;
            int index = Enumerable.Range(1, ccount - scount + 1).FirstOrDefault(i => collection.Skip(i).Take(scount).SequenceEqual(sequence));
            if (index == 0)
                return -1;
            return index;
        }

        public static void MakeWindowUntransparent(this Window wnd)
        {
            if (!wnd.IsInitialized)
                throw new Exception("The extension method MakeWindowUntransparent can not be called prior to the window being initialized.");
            const int GwlExstyle = -20;
            const uint WsExLayered = 0x00080000;
            const int WsExTransparent = 0x00000020;
            IntPtr hwnd = new WindowInteropHelper(wnd).Handle;
            IntPtr ex_style = NativeMethods.GetWindowLongPtr3264(hwnd, GwlExstyle);
            NativeMethods.SetWindowLongPtr(hwnd, GwlExstyle, Convert.ToUInt32(ex_style.ToInt32() & ~WsExLayered & ~WsExTransparent));
        }
    }
}
