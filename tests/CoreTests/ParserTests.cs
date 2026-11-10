using System;
using System.IO;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters.Tests
{
    public static class ParserTests
    {
        public static string RepoFile(params string[] parts)
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, ".gitattributes"))) dir = Path.GetDirectoryName(dir);
            return Path.Combine(dir, Path.Combine(parts));
        }

        public static void Register(TestSuite suite)
        {
            suite.Add("the sample character file parses with no errors and no warnings", () =>
            {
                string path = RepoFile("tests", "fixtures", "samplepack", "characters", "sample_scout.json");
                ParseResult result = CharacterParser.Parse(File.ReadAllText(path), "sample", null);
                Check.True(result.Ok, string.Join(";", result.Errors));
                Check.Equal(0, result.Warnings.Count, string.Join(";", result.Warnings));
                Check.Equal(11, result.Definition.Skills[2], "INT");
                Check.Near(1.6f, result.Definition.Numbers["insulationMult"], "insulation");
                Check.True(result.Definition.Stats.ContainsKey("charge"), "stat");
                Check.Equal(2, result.Definition.Behaviours.Count, "rules");
            });

            suite.Add("invalid JSON, missing key and missing name are errors, not exceptions", () =>
            {
                Check.True(!CharacterParser.Parse("{nope", "a", null).Ok, "bad json");
                Check.True(!CharacterParser.Parse("{\"name\":\"x\"}", "a", null).Ok, "no key");
                Check.True(!CharacterParser.Parse("{\"key\":\"k\"}", "a", null).Ok, "no name");
            });

            suite.Add("vanilla ids and unknown bases are refused", () =>
            {
                Check.True(!CharacterParser.Parse("{\"key\":\"k\",\"name\":\"n\",\"id\":2}", "a", null).Ok, "id 2");
                Check.True(!CharacterParser.Parse("{\"key\":\"k\",\"name\":\"n\",\"base\":\"robot\"}", "a", null).Ok, "base");
            });

            suite.Add("a plain string name becomes the en text", () =>
            {
                ParseResult result = CharacterParser.Parse("{\"key\":\"k\",\"name\":\"Zed\"}", "a", null);
                Check.Equal("Zed", result.Definition.Name["en"], "name");
            });

            suite.Add("unknown trait names warn unless a pack registered them", () =>
            {
                string json = "{\"key\":\"k\",\"name\":\"n\",\"numbers\":{\"glowMult\":2}}";
                Check.Equal(1, CharacterParser.Parse(json, "a", null).Warnings.Count, "warns");
                Check.Equal(0, CharacterParser.Parse(json, "a", new[] { "glowMult" }).Warnings.Count, "registered");
            });

            suite.Add("skills are clamped and stat ranges are repaired", () =>
            {
                string json = "{\"key\":\"k\",\"name\":\"n\",\"skills\":[99,-4,5],\"stats\":{\"s\":{\"min\":10,\"max\":0,\"start\":50}}}";
                CharacterDefinition d = CharacterParser.Parse(json, "a", null).Definition;
                Check.Equal(30, d.Skills[0], "hi");
                Check.Equal(0, d.Skills[1], "lo");
                Check.Near(0f, d.Stats["s"].Min, "swapped min");
                Check.Near(10f, d.Stats["s"].Start, "start clamped");
            });

            suite.Add("perk duration is read and negative values become permanent", () =>
            {
                string json = "{\"key\":\"k\",\"name\":\"n\",\"perks\":[{\"id\":\"a\",\"duration\":30},{\"id\":\"b\",\"duration\":-5}]}";
                CharacterDefinition d = CharacterParser.Parse(json, "a", null).Definition;
                Check.Near(30f, d.Perks[0].DurationSeconds, "timed");
                Check.Near(0f, d.Perks[1].DurationSeconds, "permanent");
            });

            suite.Add("rules naming unknown paths, perks, events or fields are skipped with a warning", () =>
            {
                string json = "{\"key\":\"k\",\"name\":\"n\",\"behaviours\":["
                    + "{\"on\":\"tick\",\"when\":[{\"path\":\"stat.nope\",\"op\":\">\",\"value\":1}],\"do\":[{\"add\":\"body.hunger\",\"value\":1}]},"
                    + "{\"on\":\"explode\",\"do\":[{\"add\":\"body.hunger\",\"value\":1}]},"
                    + "{\"on\":\"tick\",\"do\":[{\"add\":\"body.limbs\",\"value\":1}]},"
                    + "{\"on\":\"tick\",\"do\":[{\"grant\":\"ghost\"}]},"
                    + "{\"on\":\"tick\",\"do\":[{\"add\":\"body.hunger\",\"value\":1}]}]}";
                ParseResult result = CharacterParser.Parse(json, "a", null);
                Check.Equal(1, result.Definition.Behaviours.Count, "kept");
                Check.Equal(4, result.Warnings.Count, string.Join(";", result.Warnings));
            });
        }
    }
}
