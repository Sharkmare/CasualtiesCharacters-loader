using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace CasualtiesCharacters.Core
{
    public sealed class PackInfo
    {
        public string Id;
        public string Name;
        public string Version = "0";
        public string MinLoaderVersion = "0";
        public string Folder;
    }

    public static class PackManifest
    {
        static readonly Regex ValidId = new Regex("^[a-z0-9][a-z0-9._-]{0,39}$");

        public static PackInfo Parse(string json, string folder, string folderName, List<string> errors)
        {
            PackInfo info = new PackInfo { Folder = folder };
            if (json == null)
            {
                info.Id = Sanitize(folderName);
                info.Name = folderName;
                if (!ValidId.IsMatch(info.Id))
                {
                    errors.Add("pack folder '" + folderName + "' has no usable id, add a pack.json");
                    return null;
                }
                return info;
            }
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception exception)
            {
                errors.Add(folderName + "/pack.json is not valid JSON: " + exception.Message);
                return null;
            }
            info.Id = ((string)root["id"] ?? "").Trim();
            if (!ValidId.IsMatch(info.Id))
            {
                errors.Add(folderName + "/pack.json: id '" + info.Id + "' must be lower case letters, digits, dot, dash or underscore (40 at most)");
                return null;
            }
            info.Name = (string)root["name"] ?? info.Id;
            info.Version = ((string)root["version"] ?? "0").Trim();
            info.MinLoaderVersion = ((string)root["minLoader"] ?? "0").Trim();
            return info;
        }

        public static string Sanitize(string folderName)
        {
            return Regex.Replace(folderName.ToLowerInvariant(), "[^a-z0-9._-]+", "-").Trim('-');
        }
    }

    public static class VersionCompare
    {
        public static int Compare(string left, string right)
        {
            int[] a = Parts(left), b = Parts(right);
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
                if (x != y) return x < y ? -1 : 1;
            }
            return 0;
        }

        static int[] Parts(string version)
        {
            List<int> parts = new List<int>();
            foreach (string piece in (version ?? "0").Split('.'))
            {
                string digits = Regex.Match(piece, "^[0-9]+").Value;
                int value;
                parts.Add(int.TryParse(digits, out value) ? value : 0);
            }
            return parts.ToArray();
        }
    }
}
