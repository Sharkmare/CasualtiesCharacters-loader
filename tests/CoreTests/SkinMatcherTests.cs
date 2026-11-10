using System;
using System.Collections.Generic;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters.Tests
{
    public static class SkinMatcherTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("skin matching reports matched, missing and ignored files", () =>
            {
                SkinMatcher.Report report = SkinMatcher.Match(new[] { "experimentHead.png", "experimentTail.PNG", "notes.txt" });
                Check.Equal(2, report.Matched.Count, "matched");
                Check.Equal(SkinMatcher.PartNames.Length - 2, report.Missing.Count, "missing");
                Check.Equal(1, report.Unknown.Count, "unknown");
                Check.True(report.Summary().StartsWith("matched 2 of 33"), report.Summary());
            });

            suite.Add("the part list has 33 distinct names", () =>
            {
                Check.Equal(33, SkinMatcher.PartNames.Length, "count");
                Check.Equal(33, new HashSet<string>(SkinMatcher.PartNames).Count, "distinct");
            });

            suite.Add("the body folder is found at depth 0 or 1 only", () =>
            {
                Check.Equal("Body", SkinMatcher.FindBodyFolder(new[] { "Body", "x/Body" }), "depth 0");
                Check.Equal("skin/Body", SkinMatcher.FindBodyFolder(new[] { "skin", "skin/Body" }), "depth 1");
                Check.True(SkinMatcher.FindBodyFolder(new[] { "a/b/Body" }) == null, "too deep");
            });
        }
    }
}
