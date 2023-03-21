﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace ExtensionMethods
{
    public static class Extensions
    {
        public static bool EndsWith(this StringBuilder haystack, string needle)
        {
            var needleLength = needle.Length - 1;
            var haystackLength = haystack.Length - 1;
            if (haystackLength == 0)
            {
                return false;
            }
            for (int i = 0; i <= needleLength; i++)
            {
                if (haystack[haystackLength - i] != needle[needleLength - i])
                {
                    return false;
                }
            }
            return true;
        }
        private static bool ContainsOrdinal(this StringBuilder haystack, string needle)
        {
            int needleIndexLength = needle.Length - 1;
            int haystackIndexLength = haystack.Length - 1;
            if (haystack.Length == 0 || needle.Length == 0 || needle.Length > haystack.Length)
            {
                return false;
            }
            for (int i = 0; i <= haystackIndexLength && haystack.Length - i >= needleIndexLength; i++)
            {
                if (haystack[i] == needle[0])
                {
                    for (int j = 1; j <= needleIndexLength; j++)
                    {
                        if (haystackIndexLength < i + j)
                        {
                            return false;
                        }
                        else if (haystack[i + j] == needle[j])
                        {
                            continue;
                        }
                        else
                        {
                            i = i + j - 1;
                            goto CONTINUE_OUTER;
                        }
                    }
                    return true;
                }
                CONTINUE_OUTER:;
            }
            return false;
        }
        private static bool ContainsOrdinalIgnoreCase(this StringBuilder haystack, string needle)
        {
            int needleIndexLength = needle.Length - 1;
            int haystackIndexLength = haystack.Length - 1;
            if (haystack.Length == 0 || needle.Length == 0 || needle.Length > haystack.Length)
            {
                return false;
            }
            for (int i = 0; i <= haystackIndexLength && haystack.Length - i >= needleIndexLength; i++)
            {
                if (char.ToLower(haystack[i]) == needle[0])
                {
                    for (int j = 1; j <= needleIndexLength; j++)
                    {
                        if (haystackIndexLength < i + j)
                        {
                            return false;
                        }
                        else if (char.ToLower(haystack[i + j]) == needle[j])
                        {
                            continue;
                        }
                        else
                        {
                            i = i + j - 1;
                            goto CONTINUE_OUTER;
                        }
                    }
                    return true;
                }
            CONTINUE_OUTER:;
            }
            return false;
        }
        public static bool Contains(this StringBuilder haystack, string needle, StringComparison stringComparison)
        {
            return stringComparison switch
            {
                StringComparison.Ordinal => ContainsOrdinal(haystack, needle),
                StringComparison.OrdinalIgnoreCase => ContainsOrdinalIgnoreCase(haystack, needle.ToLower()),
                _ => throw new ArgumentException("Comparison rule not supported", nameof(stringComparison)),
            };
        }
        public static bool Contains(this StringBuilder haystack, string needle)
        {
            return haystack.ContainsOrdinal(needle);
        }
        public static StringBuilder ToLower(this StringBuilder sb)
        {
            StringBuilder returnSb = new();
            for (int i = 0; i < sb.Length - 1; i++)
            {
                returnSb.Append(char.ToLower(sb[i]));
            }
            return returnSb;
        }
        public static void TrimEnd(this StringBuilder sb, string str)
        {
            var strIndexLength = str.Length - 1;
            var sbIndexLength = sb.Length - 1;
            sb.Remove(sbIndexLength - strIndexLength, str.Length);
        }
        public static string ToStringTrimEnd(this StringBuilder sb, string trimEnd)
        {
            var strIndexLength = trimEnd.Length - 1;
            var sbIndexLength = sb.Length - 1;
            return sb.ToString(0, sbIndexLength - strIndexLength);
        }
        public static bool ParseToBool(this string str)
        {
            return str.ToLower() switch
            {
                "true" => true,
                "false" => false,
                "1" => true,
                "0" => false,
                _ => throw new Exception($"The string {str.ToLower()} could not be parsed to bool"),
            };
        }
        public static bool? TryParseToBool(this string str, out string returnStr)
        {
            returnStr = str;
            return str.ToLower() switch
            {
                "true" => true,
                "false" => false,
                "1" => true,
                "0" => false,
                _ => null,
            };
        }
        public static bool NullIsFalse(this bool? boolean)
        {
            return boolean switch
            {
                true => true,
                false => false,
                _ => false
            };
        }
        public static bool IsEven(this int val)
        {
            if (val % 2 == 0) return true; return false;
        }
        public static string Combine(this string[] stringArray)
        {
            StringBuilder sb = new();
            foreach (string str in stringArray)
            {
                sb.Append(str);
            }
            return sb.ToString();
        }
        public static void Rename(this FileInfo file, string newName)
        {
            if (file.Directory is not null)
            {
                file.MoveTo(Path.Combine(file.Directory.FullName, newName));
            }
        }
        public static string NameWithoutExtension(this FileInfo file)
        {
            return Path.GetFileNameWithoutExtension(file.FullName);
        }
        //extend FileInfo with NameWithoutExtension() -- use in ReadProfiles method of settings.cs

    }
}
