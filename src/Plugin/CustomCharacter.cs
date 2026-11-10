using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesCharacters.Core;
using UnityEngine;

namespace CasualtiesCharacters
{
    public sealed class CustomCharacter : MonoBehaviour
    {
        const float TickSeconds = 1f;

        public CharacterDefinition Definition;
        public int Id;
        public Body Body;
        public readonly Dictionary<string, float> Stats = new Dictionary<string, float>();
        public float LastDt = 1f;

        readonly Dictionary<string, float> activePerks = new Dictionary<string, float>();
        BodyValueSource valueSource;
        float baseMoveForce;
        float baseStaminaGain;
        float tickTimer;
        float lastWrittenMaxEncumbrance = -1f;
        float[] previousPain;
        float[] previousBleed;
        float previousHearingLoss;
        float previousSkinTotal = -1f;

        static readonly List<CustomCharacter> living = new List<CustomCharacter>();

        public static CustomCharacter OwnerOf(Skills skills)
        {
            foreach (CustomCharacter character in living)
                if (character != null && character.Body != null && character.Body.skills == skills) return character;
            return null;
        }

        void OnDestroy()
        {
            living.Remove(this);
            SkinApplier skin = GetComponent<SkinApplier>();
            if (skin != null) Destroy(skin);
        }

        public static CustomCharacter Of(Body body)
        {
            return body == null ? null : body.GetComponent<CustomCharacter>();
        }

        public static bool IsLocal(Body body)
        {
            return PlayerCamera.main != null && PlayerCamera.main.body == body;
        }

        public static CustomCharacter Attach(Body body, int id)
        {
            CharacterDefinition definition = Registry.Get(id);
            if (body == null || definition == null || !CharacterGate.Active) return null;
            CustomCharacter existing = Of(body);
            if (existing != null && existing.Id == id) return existing;
            if (existing != null) Destroy(existing);

            CustomCharacter character = body.gameObject.AddComponent<CustomCharacter>();
            character.Setup(body, id, definition);
            return character;
        }

        void Setup(Body body, int id, CharacterDefinition definition)
        {
            Body = body;
            Id = id;
            Definition = definition;
            valueSource = new BodyValueSource(this);
            living.Add(this);
            foreach (StatDefinition stat in definition.Stats.Values) Stats[stat.Name] = stat.Start;
            foreach (PerkDefinition perk in definition.Perks)
                if (perk.ActiveAtStart) activePerks[perk.Id] = perk.DurationSeconds;

            baseMoveForce = body.moveForce;
            baseStaminaGain = body.staminaGainMultiplier;
            body.skills.Setup(id);
            ApplyFieldTraits();
            gameObject.AddComponent<SkinApplier>().Configure(definition);
            foreach (KeyValuePair<string, float> number in definition.Numbers)
            {
                TraitHandler handler;
                if (CharacterApi.TraitHandlers.TryGetValue(number.Key, out handler)) handler(body, this, number.Value);
            }
            if (IsLocal(body)) RuleEngine.Run(definition, "spawn", valueSource, 0f);
            Plugin.Log.LogInfo("character " + id + " (" + definition.FullKey + ") attached to " + body.name);
        }

        public float Trait(string name, float neutral)
        {
            float value;
            if (!Definition.Numbers.TryGetValue(name, out value)) value = neutral;
            foreach (PerkDefinition perk in Definition.Perks)
            {
                float effect;
                if (activePerks.ContainsKey(perk.Id) && perk.Effects.TryGetValue(name, out effect)) value += effect;
            }
            return value;
        }

        public bool Flag(string name, bool neutral)
        {
            bool value;
            return Definition.Flags.TryGetValue(name, out value) ? value : neutral;
        }

        public bool IsPerkActive(string perkId)
        {
            return activePerks.ContainsKey(perkId);
        }

        public void SetPerk(string perkId, bool active)
        {
            PerkDefinition perk = Definition.Perks.Find(p => p.Id == perkId);
            if (perk == null) return;
            bool was = activePerks.ContainsKey(perkId);
            if (active) activePerks[perkId] = perk.DurationSeconds;
            else activePerks.Remove(perkId);
            if (was != active)
            {
                ApplyFieldTraits();
                Plugin.Debug("perk " + perkId + (active ? " on" : " off"));
            }
        }

        public void Fire(string eventName)
        {
            if (!IsLocal(Body)) return;
            RuleEngine.Run(Definition, eventName, valueSource, LastDt);
        }

        void ApplyFieldTraits()
        {
            Body.moveForce = baseMoveForce * Trait("moveForceMult", 1f);
            Body.staminaGainMultiplier = baseStaminaGain * Trait("staminaGainMult", 1f);
            if (Definition.Flags.ContainsKey("canSweat")) Body.canSweat = Flag("canSweat", Body.canSweat);
        }

        public void Tick(float dt)
        {
            if (!IsLocal(Body)) return;
            LastDt = dt;
            DriftStats(dt);
            ExpirePerks(dt);
            DampGains();
            ScaleCarryLimit();

            tickTimer += dt;
            if (tickTimer < TickSeconds) return;
            float elapsed = tickTimer;
            tickTimer = 0f;
            LastDt = elapsed;
            RuleEngine.Run(Definition, "tick", valueSource, elapsed);
            DetectHurt();
        }

        void DriftStats(float dt)
        {
            foreach (StatDefinition stat in Definition.Stats.Values)
            {
                if (stat.PerSecond == 0f) continue;
                Stats[stat.Name] = Math.Max(stat.Min, Math.Min(stat.Max, Stats[stat.Name] + stat.PerSecond * dt));
            }
        }

        void ExpirePerks(float dt)
        {
            List<string> expired = null;
            foreach (PerkDefinition perk in Definition.Perks)
            {
                float remaining;
                if (perk.DurationSeconds <= 0f || !activePerks.TryGetValue(perk.Id, out remaining)) continue;
                remaining -= dt;
                if (remaining <= 0f) (expired ?? (expired = new List<string>())).Add(perk.Id);
                else activePerks[perk.Id] = remaining;
            }
            if (expired == null) return;
            foreach (string perkId in expired) SetPerk(perkId, false);
        }

        void DampGains()
        {
            float painMultiplier = Trait("painGainMult", 1f);
            float bleedMultiplier = Trait("bleedGainMult", 1f);
            float hearingMultiplier = Trait("hearingLossGainMult", 1f);
            Limb[] limbs = Body.limbs;
            if (limbs != null)
            {
                if (previousPain == null || previousPain.Length != limbs.Length)
                {
                    previousPain = new float[limbs.Length];
                    previousBleed = new float[limbs.Length];
                    for (int i = 0; i < limbs.Length; i++)
                    {
                        previousPain[i] = limbs[i] != null ? limbs[i].pain : 0f;
                        previousBleed[i] = limbs[i] != null ? limbs[i].bleedAmount : 0f;
                    }
                }
                for (int i = 0; i < limbs.Length; i++)
                {
                    Limb limb = limbs[i];
                    if (limb == null) continue;
                    if (painMultiplier != 1f) limb.pain = TraitMath.DampenGain(previousPain[i], limb.pain, painMultiplier);
                    if (bleedMultiplier != 1f) limb.bleedAmount = TraitMath.DampenGain(previousBleed[i], limb.bleedAmount, bleedMultiplier);
                    previousPain[i] = limb.pain;
                    previousBleed[i] = limb.bleedAmount;
                }
            }
            if (hearingMultiplier != 1f) Body.hearingLoss = TraitMath.DampenGain(previousHearingLoss, Body.hearingLoss, hearingMultiplier);
            previousHearingLoss = Body.hearingLoss;
        }

        void ScaleCarryLimit()
        {
            float multiplier = Trait("carryMult", 1f);
            if (multiplier == 1f) return;
            float current = Body.maxEncumberance;
            if (Math.Abs(current - lastWrittenMaxEncumbrance) < 1e-5f) return;
            lastWrittenMaxEncumbrance = current * multiplier;
            Body.maxEncumberance = lastWrittenMaxEncumbrance;
        }

        void DetectHurt()
        {
            float total = 0f;
            foreach (Limb limb in Body.limbs) if (limb != null) total += limb.skinHealth;
            bool hurt = previousSkinTotal >= 0f && total < previousSkinTotal - 1f;
            previousSkinTotal = total;
            if (hurt) Fire("hurt");
        }
    }

    sealed class BodyValueSource : IValueSource
    {
        static readonly Dictionary<string, FieldInfo> fieldByName = new Dictionary<string, FieldInfo>();
        readonly CustomCharacter owner;

        public BodyValueSource(CustomCharacter owner)
        {
            this.owner = owner;
        }

        static FieldInfo Field(string name)
        {
            FieldInfo field;
            if (!fieldByName.TryGetValue(name, out field))
            {
                field = typeof(Body).GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && field.FieldType != typeof(float)) field = null;
                fieldByName[name] = field;
            }
            return field;
        }

        public bool TryGet(string path, out float value)
        {
            value = 0f;
            if (path == "time.dt") { value = owner.LastDt; return true; }
            int dot = path.IndexOf('.');
            string name = path.Substring(dot + 1);
            switch (path.Substring(0, dot))
            {
                case "stat": return owner.Stats.TryGetValue(name, out value);
                case "perk": value = owner.IsPerkActive(name) ? 1f : 0f; return true;
                case "body":
                    FieldInfo field = Field(name);
                    if (field == null) return false;
                    value = (float)field.GetValue(owner.Body);
                    return true;
                default: return false;
            }
        }

        public bool TrySet(string path, float value)
        {
            int dot = path.IndexOf('.');
            string name = path.Substring(dot + 1);
            switch (path.Substring(0, dot))
            {
                case "stat": owner.Stats[name] = value; return true;
                case "body":
                    FieldInfo field = Field(name);
                    if (field == null) return false;
                    field.SetValue(owner.Body, value);
                    return true;
                default: return false;
            }
        }

        public void SetPerk(string perkId, bool active)
        {
            owner.SetPerk(perkId, active);
        }
    }
}
