using System;
using System.Collections.Generic;
using System.Text;

namespace FFXIV_GameSense
{
    internal static class Extensions
    {
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
    }
}
