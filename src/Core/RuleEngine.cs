using System;

namespace CasualtiesCharacters.Core
{
    public interface IValueSource
    {
        bool TryGet(string path, out float value);
        bool TrySet(string path, float value);
        void SetPerk(string perkId, bool active);
    }

    public static class RuleEngine
    {
        public static int Run(CharacterDefinition definition, string eventName, IValueSource source, float dt)
        {
            int fired = 0;
            foreach (BehaviourRule rule in definition.Behaviours)
            {
                if (rule.Event != eventName) continue;
                if (!ConditionsHold(rule, source)) continue;
                foreach (RuleAction action in rule.Do) Apply(definition, action, source, dt);
                fired++;
            }
            return fired;
        }

        public static bool ConditionsHold(BehaviourRule rule, IValueSource source)
        {
            foreach (Condition condition in rule.When)
            {
                float current;
                if (!source.TryGet(condition.Path, out current)) return false;
                if (!Compare(current, condition.Op, condition.Value)) return false;
            }
            return true;
        }

        public static bool Compare(float left, string op, float right)
        {
            switch (op)
            {
                case ">": return left > right;
                case ">=": return left >= right;
                case "<": return left < right;
                case "<=": return left <= right;
                case "==": return Math.Abs(left - right) < 1e-6f;
                case "!=": return Math.Abs(left - right) >= 1e-6f;
                default: return false;
            }
        }

        static void Apply(CharacterDefinition definition, RuleAction action, IValueSource source, float dt)
        {
            if (action.Verb == "grant") { source.SetPerk(action.Target, true); return; }
            if (action.Verb == "revoke") { source.SetPerk(action.Target, false); return; }

            float amount = action.PerSecond ? action.Value * dt : action.Value;
            float current;
            if (!source.TryGet(action.Target, out current)) return;
            float next = action.Verb == "set" ? action.Value
                : action.Verb == "add" ? current + amount
                : current * (action.PerSecond ? 1f + (action.Value - 1f) * dt : action.Value);
            source.TrySet(action.Target, Clamp(definition, action.Target, next));
        }

        public static float Clamp(CharacterDefinition definition, string path, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            if (!path.StartsWith("stat.", StringComparison.Ordinal)) return value;
            StatDefinition stat;
            if (!definition.Stats.TryGetValue(path.Substring(5), out stat)) return value;
            return Math.Max(stat.Min, Math.Min(stat.Max, value));
        }
    }
}
