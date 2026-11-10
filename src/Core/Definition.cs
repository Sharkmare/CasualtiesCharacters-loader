using System.Collections.Generic;

namespace CasualtiesCharacters.Core
{
    public sealed class StatDefinition
    {
        public string Name;
        public float Min;
        public float Max = 100f;
        public float Start;
        public float PerSecond;
        public bool Sync;
        public Dictionary<string, string> Label = new Dictionary<string, string>();
    }

    public sealed class PerkDefinition
    {
        public string Id;
        public bool ActiveAtStart = true;
        public float DurationSeconds;
        public Dictionary<string, string> Name = new Dictionary<string, string>();
        public Dictionary<string, string> Description = new Dictionary<string, string>();
        public Dictionary<string, float> Effects = new Dictionary<string, float>();
    }

    public sealed class Condition
    {
        public string Path;
        public string Op;
        public float Value;
    }

    public sealed class RuleAction
    {
        public string Verb;
        public string Target;
        public float Value;
        public bool PerSecond;
    }

    public sealed class BehaviourRule
    {
        public string Event;
        public List<Condition> When = new List<Condition>();
        public List<RuleAction> Do = new List<RuleAction>();
    }

    public sealed class CharacterDefinition
    {
        public string Key;
        public string PackId = "local";
        public string PackVersion = "0";
        public string SourceFolder;
        public int Id;
        public string FullKey { get { return PackId + "/" + Key; } }
        public string SkinPath
        {
            get
            {
                return string.IsNullOrEmpty(SkinFolder) || SourceFolder == null
                    ? null : System.IO.Path.Combine(SourceFolder, "skins", SkinFolder);
            }
        }
        public int RequestedId;
        public string Base = "experiment";
        public string SkinFolder;
        public Dictionary<string, string> Name = new Dictionary<string, string>();
        public Dictionary<string, string> Family = new Dictionary<string, string>();
        public Dictionary<string, string> Quote = new Dictionary<string, string>();
        public Dictionary<string, string> Quirks = new Dictionary<string, string>();
        public int[] Skills = new int[3];
        public float[] Multipliers = { 1f, 1f, 1f };
        public Dictionary<string, float> Numbers = new Dictionary<string, float>();
        public Dictionary<string, bool> Flags = new Dictionary<string, bool>();
        public Dictionary<string, StatDefinition> Stats = new Dictionary<string, StatDefinition>();
        public List<PerkDefinition> Perks = new List<PerkDefinition>();
        public List<BehaviourRule> Behaviours = new List<BehaviourRule>();
        public string Unlock = "always";
    }
}
