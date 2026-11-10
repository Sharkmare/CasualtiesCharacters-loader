using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CasualtiesCharacters.Core
{
    public sealed class ParseResult
    {
        public CharacterDefinition Definition;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public bool Ok { get { return Definition != null; } }
    }

    public static class CharacterParser
    {
        public static ParseResult Parse(string json, string sourceName, ICollection<string> extraTraitNames)
        {
            ParseResult result = new ParseResult();
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception exception)
            {
                result.Errors.Add(sourceName + ": not valid JSON: " + exception.Message);
                return result;
            }

            string key = (string)root["key"];
            if (string.IsNullOrWhiteSpace(key))
            {
                result.Errors.Add(sourceName + ": missing key");
                return result;
            }

            CharacterDefinition definition = new CharacterDefinition { Key = key };
            string where = sourceName + " (" + key + ")";

            definition.RequestedId = (int?)root["id"] ?? 0;
            if (definition.RequestedId != 0 && definition.RequestedId < 3)
            {
                result.Errors.Add(where + ": id " + definition.RequestedId + " is a vanilla id, use 3 or more or omit it");
                return result;
            }

            definition.Base = ((string)root["base"] ?? "experiment").ToLowerInvariant();
            if (Vocabulary.BaseIndex(definition.Base) < 0)
            {
                result.Errors.Add(where + ": base '" + definition.Base + "' is not experiment, milky or dune");
                return result;
            }

            definition.Unlock = (string)root["unlock"] ?? "always";
            if (Array.IndexOf(Vocabulary.Unlocks, definition.Unlock) < 0)
            {
                result.Warnings.Add(where + ": unlock '" + definition.Unlock + "' unknown, using always");
                definition.Unlock = "always";
            }

            definition.SkinFolder = (string)root["skin"];
            definition.Name = ReadText(root["name"]);
            definition.Family = ReadText(root["family"]);
            definition.Quote = ReadText(root["quote"]);
            definition.Quirks = ReadText(root["quirks"]);
            if (definition.Name.Count == 0)
            {
                result.Errors.Add(where + ": missing name");
                return result;
            }

            ReadSkills(root["skills"], definition, where, result);
            ReadMultipliers(root["multipliers"], definition, where, result);
            ReadNumbers(root["numbers"], definition, extraTraitNames, where, result);
            ReadFlags(root["flags"], definition);
            ReadStats(root["stats"], definition, where, result);
            ReadPerks(root["perks"], definition, extraTraitNames, where, result);
            ReadBehaviours(root["behaviours"], definition, where, result);

            result.Definition = definition;
            return result;
        }

        static Dictionary<string, string> ReadText(JToken token)
        {
            Dictionary<string, string> text = new Dictionary<string, string>();
            if (token == null) return text;
            if (token.Type == JTokenType.String)
            {
                text["en"] = (string)token;
                return text;
            }
            JObject map = token as JObject;
            if (map == null) return text;
            foreach (JProperty property in map.Properties())
                text[property.Name] = (string)property.Value;
            return text;
        }

        static void ReadSkills(JToken token, CharacterDefinition definition, string where, ParseResult result)
        {
            JArray array = token as JArray;
            if (array == null) return;
            if (array.Count != 3)
            {
                result.Warnings.Add(where + ": skills needs 3 numbers (STR, RES, INT), ignored");
                return;
            }
            for (int i = 0; i < 3; i++)
                definition.Skills[i] = Math.Max(0, Math.Min(30, (int)array[i]));
        }

        static void ReadMultipliers(JToken token, CharacterDefinition definition, string where, ParseResult result)
        {
            JArray array = token as JArray;
            if (array == null) return;
            if (array.Count != 3)
            {
                result.Warnings.Add(where + ": multipliers needs 3 numbers, ignored");
                return;
            }
            for (int i = 0; i < 3; i++)
                definition.Multipliers[i] = Math.Max(0f, Math.Min(10f, (float)array[i]));
        }

        static void ReadNumbers(JToken token, CharacterDefinition definition, ICollection<string> extraTraitNames, string where, ParseResult result)
        {
            JObject map = token as JObject;
            if (map == null) return;
            foreach (JProperty property in map.Properties())
            {
                definition.Numbers[property.Name] = (float)property.Value;
                WarnIfUnknownTrait(property.Name, extraTraitNames, where, result);
            }
        }

        static void ReadFlags(JToken token, CharacterDefinition definition)
        {
            JObject map = token as JObject;
            if (map == null) return;
            foreach (JProperty property in map.Properties())
                definition.Flags[property.Name] = (bool)property.Value;
        }

        static void WarnIfUnknownTrait(string name, ICollection<string> extraTraitNames, string where, ParseResult result)
        {
            if (Vocabulary.TraitNames.Contains(name)) return;
            if (extraTraitNames != null && extraTraitNames.Contains(name)) return;
            result.Warnings.Add(where + ": unknown trait '" + name + "' (ignored unless a pack registers it)");
        }

        static void ReadStats(JToken token, CharacterDefinition definition, string where, ParseResult result)
        {
            JObject map = token as JObject;
            if (map == null) return;
            foreach (JProperty property in map.Properties())
            {
                JObject body = property.Value as JObject;
                if (body == null) continue;
                StatDefinition stat = new StatDefinition { Name = property.Name };
                stat.Min = (float?)body["min"] ?? 0f;
                stat.Max = (float?)body["max"] ?? 100f;
                if (stat.Max < stat.Min)
                {
                    result.Warnings.Add(where + ": stat " + stat.Name + " has max below min, swapped");
                    float swap = stat.Min;
                    stat.Min = stat.Max;
                    stat.Max = swap;
                }
                stat.Start = Math.Max(stat.Min, Math.Min(stat.Max, (float?)body["start"] ?? stat.Min));
                stat.PerSecond = (float?)body["perSecond"] ?? 0f;
                stat.Sync = (bool?)body["sync"] ?? false;
                stat.Label = ReadText(body["label"]);
                definition.Stats[stat.Name] = stat;
            }
        }

        static void ReadPerks(JToken token, CharacterDefinition definition, ICollection<string> extraTraitNames, string where, ParseResult result)
        {
            JArray array = token as JArray;
            if (array == null) return;
            HashSet<string> seen = new HashSet<string>();
            foreach (JToken item in array)
            {
                string id = (string)item["id"];
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    result.Warnings.Add(where + ": perk with missing or duplicate id skipped");
                    continue;
                }
                PerkDefinition perk = new PerkDefinition { Id = id };
                perk.ActiveAtStart = (bool?)item["active"] ?? true;
                perk.DurationSeconds = Math.Max(0f, (float?)item["duration"] ?? 0f);
                perk.Name = ReadText(item["name"]);
                perk.Description = ReadText(item["desc"]);
                JObject effects = item["effects"] as JObject;
                if (effects != null)
                {
                    foreach (JProperty effect in effects.Properties())
                    {
                        perk.Effects[effect.Name] = (float)effect.Value;
                        WarnIfUnknownTrait(effect.Name, extraTraitNames, where + " perk " + id, result);
                    }
                }
                definition.Perks.Add(perk);
            }
        }

        static void ReadBehaviours(JToken token, CharacterDefinition definition, string where, ParseResult result)
        {
            JArray array = token as JArray;
            if (array == null) return;
            for (int index = 0; index < array.Count; index++)
            {
                string label = where + " behaviour " + index;
                BehaviourRule rule = ReadRule(array[index], definition, label, result);
                if (rule != null) definition.Behaviours.Add(rule);
            }
        }

        static BehaviourRule ReadRule(JToken item, CharacterDefinition definition, string label, ParseResult result)
        {
            string eventName = (string)item["on"];
            if (Array.IndexOf(Vocabulary.Events, eventName) < 0)
            {
                result.Warnings.Add(label + ": unknown event '" + eventName + "', rule skipped");
                return null;
            }
            BehaviourRule rule = new BehaviourRule { Event = eventName };
            JArray when = item["when"] as JArray;
            if (when != null)
            {
                foreach (JToken entry in when)
                {
                    Condition condition = new Condition { Path = (string)entry["path"], Op = (string)entry["op"], Value = (float?)entry["value"] ?? 0f };
                    string problem = CheckPath(condition.Path, definition);
                    if (problem == null && Array.IndexOf(Vocabulary.Comparisons, condition.Op) < 0) problem = "unknown comparison '" + condition.Op + "'";
                    if (problem != null)
                    {
                        result.Warnings.Add(label + ": " + problem + ", rule skipped");
                        return null;
                    }
                    rule.When.Add(condition);
                }
            }
            JArray actions = item["do"] as JArray;
            if (actions == null || actions.Count == 0)
            {
                result.Warnings.Add(label + ": no actions, rule skipped");
                return null;
            }
            foreach (JToken entry in actions)
            {
                RuleAction action = ReadAction(entry);
                string problem = CheckAction(action, definition);
                if (problem != null)
                {
                    result.Warnings.Add(label + ": " + problem + ", rule skipped");
                    return null;
                }
                rule.Do.Add(action);
            }
            return rule;
        }

        static RuleAction ReadAction(JToken entry)
        {
            foreach (string verb in Vocabulary.Verbs)
            {
                JToken target = entry[verb];
                if (target == null) continue;
                return new RuleAction
                {
                    Verb = verb,
                    Target = (string)target,
                    Value = (float?)entry["value"] ?? 0f,
                    PerSecond = (bool?)entry["perSecond"] ?? false
                };
            }
            return new RuleAction { Verb = null };
        }

        static string CheckAction(RuleAction action, CharacterDefinition definition)
        {
            if (action.Verb == null) return "action has no set, add, mul, grant or revoke";
            if (action.Verb == "grant" || action.Verb == "revoke")
                return definition.Perks.Exists(perk => perk.Id == action.Target) ? null : "unknown perk '" + action.Target + "'";
            return CheckPath(action.Target, definition);
        }

        public static string CheckPath(string path, CharacterDefinition definition)
        {
            if (string.IsNullOrEmpty(path)) return "empty path";
            if (path == "time.dt") return null;
            int dot = path.IndexOf('.');
            if (dot < 0) return "path '" + path + "' needs a prefix (body, stat, perk, time)";
            string prefix = path.Substring(0, dot);
            string name = path.Substring(dot + 1);
            if (prefix == "body") return Vocabulary.BodyPathAllowlist.Contains(name) ? null : "body field '" + name + "' is not allowed";
            if (prefix == "stat") return definition.Stats.ContainsKey(name) ? null : "unknown stat '" + name + "'";
            if (prefix == "perk") return definition.Perks.Exists(perk => perk.Id == name) ? null : "unknown perk '" + name + "'";
            return "unknown path prefix '" + prefix + "'";
        }
    }
}
