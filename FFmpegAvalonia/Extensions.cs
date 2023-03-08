using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            for (int i = 0; i < needleLength; i++)
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
        //extend FileInfo with NameWithoutExtension() -- use in ReadProfiles method of settings.cs

    }
}
