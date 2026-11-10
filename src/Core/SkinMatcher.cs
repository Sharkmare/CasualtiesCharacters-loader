using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CasualtiesCharacters.Core
{
    public static class SkinMatcher
    {
        public static readonly string[] PartNames =
        {
            "experimentTail", "experimentFoot", "experimentUpTorso", "experimentUpArm", "experimentThigh",
            "experimentDownTorso", "experimentDownArm", "experimentCrus", "experimentHandB", "experimentHandF",
            "experimentNosebleed", "experimentHead", "experimentHeadBack", "experimentHeadBackMouth",
            "experimentHeadBackMouthMini", "experimentHeadDisfigured1", "experimentHeadDisfigured1Healed",
            "experimentHeadDisfigured2", "experimentHeadDisfigured2Healed", "experimentHeadDisfigured3",
            "experimentHeadDisfigured3Healed", "experimentEyeGoneHealed", "experimentEyeGone", "experimentEyeClosed",
            "experimentEyeScaredBack", "experimentEyeScared", "experimentEyeSadBack", "experimentEyeSad",
            "experimentEyeOpen", "experimentEyePanic", "experimentEyeLookBack", "experimentEyeHalfClosedBack",
            "experimentEyeHalfClosed"
        };

        public sealed class Report
        {
            public List<string> Matched = new List<string>();
            public List<string> Missing = new List<string>();
            public List<string> Unknown = new List<string>();

            public string Summary()
            {
                return "matched " + Matched.Count + " of " + PartNames.Length
                    + (Missing.Count > 0 ? ", missing: " + string.Join(",", Missing) : "")
                    + (Unknown.Count > 0 ? ", ignored files: " + string.Join(",", Unknown) : "");
            }
        }

        public static Report Match(IEnumerable<string> fileNames)
        {
            HashSet<string> available = new HashSet<string>(
                fileNames.Select(StripPng), StringComparer.OrdinalIgnoreCase);
            Report report = new Report();
            foreach (string part in PartNames)
                (available.Contains(part) ? report.Matched : report.Missing).Add(part);
            foreach (string name in available)
                if (!PartNames.Contains(name, StringComparer.OrdinalIgnoreCase)) report.Unknown.Add(name);
            return report;
        }

        public static string StripPng(string fileName)
        {
            string name = System.IO.Path.GetFileName(fileName);
            return name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
        }

        public static string CanonicalPart(string spriteName)
        {
            return PartNames.FirstOrDefault(part => string.Equals(part, spriteName, StringComparison.Ordinal));
        }

        public static string FindBodyFolder(IEnumerable<string> relativeDirectories)
        {
            List<string> directories = relativeDirectories.Select(d => d.Replace('\\', '/').Trim('/')).ToList();
            if (directories.Contains("Body")) return "Body";
            return directories.FirstOrDefault(d => d.Count(c => c == '/') == 1 && d.EndsWith("/Body", StringComparison.Ordinal));
        }
    }
}
