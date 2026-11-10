using System.Collections.Generic;

namespace CasualtiesCharacters.Core
{
    public static class Vocabulary
    {
        public static readonly string[] BaseCharacters = { "experiment", "milky", "dune" };
        public static readonly string[] Events = { "spawn", "tick", "eat", "wake", "hurt", "perk" };
        public static readonly string[] Comparisons = { ">", ">=", "<", "<=", "==", "!=" };
        public static readonly string[] Verbs = { "set", "add", "mul", "grant", "revoke" };
        public static readonly string[] Unlocks = { "always", "afterBase" };

        public static readonly HashSet<string> BodyPathAllowlist = new HashSet<string>
        {
            "hunger", "thirst", "stamina", "energy", "temperature", "happiness",
            "sicknessAmount", "wetness", "shock", "adrenaline"
        };

        public static readonly HashSet<string> TraitNames = new HashSet<string>
        {
            "insulationMult", "hungerRateMult", "thirstRateMult", "moveForceMult", "staminaGainMult",
            "carryMult", "painGainMult", "bleedGainMult", "hearingLossGainMult", "heatThirstMult",
            "heatThreshold", "canSweat"
        };

        public static int BaseIndex(string baseName)
        {
            return System.Array.IndexOf(BaseCharacters, baseName);
        }
    }
}
