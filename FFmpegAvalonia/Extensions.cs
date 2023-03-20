using System;
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
