using System.Collections.Generic;
using System.IO;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters.Tests
{
    public sealed class DictionarySource : IValueSource
    {
        public Dictionary<string, float> Values = new Dictionary<string, float>();
        public Dictionary<string, bool> Perks = new Dictionary<string, bool>();

        public bool TryGet(string path, out float value) { return Values.TryGetValue(path, out value); }
        public bool TrySet(string path, float value) { Values[path] = value; return true; }
        public void SetPerk(string perkId, bool active) { Perks[perkId] = active; }
    }

    public static class RuleTests
    {
        static CharacterDefinition Sample()
        {
            string path = ParserTests.RepoFile("tests", "fixtures", "samplepack", "characters", "sample_scout.json");
            return CharacterParser.Parse(File.ReadAllText(path), "sample", null).Definition;
        }

        public static void Register(TestSuite suite)
        {
            suite.Add("wake rule fires only at full charge, resets it and grants the perk", () =>
            {
                CharacterDefinition sample = Sample();
                DictionarySource source = new DictionarySource();
                source.Values["stat.charge"] = 99f;
                Check.Equal(0, RuleEngine.Run(sample, "wake", source, 1f), "not yet");
                source.Values["stat.charge"] = 100f;
                Check.Equal(1, RuleEngine.Run(sample, "wake", source, 1f), "fires");
                Check.Near(0f, source.Values["stat.charge"], "reset");
                Check.True(source.Perks["boosted"], "granted");
            });

            suite.Add("per second actions scale with dt and respect conditions", () =>
            {
                CharacterDefinition sample = Sample();
                DictionarySource source = new DictionarySource();
                source.Values["body.temperature"] = 39f;
                source.Values["body.stamina"] = 50f;
                RuleEngine.Run(sample, "tick", source, 2f);
                Check.Near(49.2f, source.Values["body.stamina"], "drained");
                source.Values["body.temperature"] = 37f;
                RuleEngine.Run(sample, "tick", source, 2f);
                Check.Near(49.2f, source.Values["body.stamina"], "cool, unchanged");
            });

            suite.Add("stat writes are clamped to the stat range and nan becomes zero", () =>
            {
                CharacterDefinition sample = Sample();
                Check.Near(100f, RuleEngine.Clamp(sample, "stat.charge", 500f), "max");
                Check.Near(0f, RuleEngine.Clamp(sample, "stat.charge", -3f), "min");
                Check.Near(0f, RuleEngine.Clamp(sample, "stat.charge", float.NaN), "nan");
                Check.Near(500f, RuleEngine.Clamp(sample, "body.hunger", 500f), "body unclamped here");
            });

            suite.Add("a condition on a missing value is false instead of throwing", () =>
            {
                CharacterDefinition sample = Sample();
                Check.Equal(0, RuleEngine.Run(sample, "tick", new DictionarySource(), 1f), "none fired");
            });

            suite.Add("comparisons evaluate correctly", () =>
            {
                Check.True(RuleEngine.Compare(1, "<", 2) && RuleEngine.Compare(2, ">=", 2) && RuleEngine.Compare(2, "==", 2), "basic");
                Check.True(!RuleEngine.Compare(2, "!=", 2) && !RuleEngine.Compare(1, ">", 1) && !RuleEngine.Compare(1, "??", 1), "negatives");
            });
        }
    }
}
