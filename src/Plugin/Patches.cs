using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CasualtiesCharacters.Core;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesCharacters
{
    public static class Patches
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public sealed class Spec
        {
            public string Label;
            public MethodBase Original;
            public string Prefix;
            public string Postfix;
            public string Transpiler;
        }

        static MethodBase Method(Type type, string name, params Type[] parameters)
        {
            return parameters.Length == 0 ? type.GetMethod(name, Declared) : type.GetMethod(name, Declared, null, parameters, null);
        }

        static MethodBase Getter(Type type, string property)
        {
            PropertyInfo info = type.GetProperty(property, Declared);
            return info == null ? null : info.GetGetMethod(true);
        }

        public static List<Spec> Specs()
        {
            return new List<Spec>
            {
                new Spec { Label = "Skills.BaseSkills", Original = Method(typeof(Skills), "BaseSkills"), Prefix = "BaseSkillsPrefix" },
                new Spec { Label = "Skills.Setup", Original = Method(typeof(Skills), "Setup"), Postfix = "SkillsSetupPostfix" },
                new Spec { Label = "Skills.BaseMultipliers", Original = Method(typeof(Skills), "BaseMultipliers"), Prefix = "BaseMultipliersPrefix" },
                new Spec { Label = "Locale.GetString", Original = Method(typeof(Locale), "GetString"), Postfix = "GetStringPostfix" },
                new Spec { Label = "PlayerCamera.SpawnCharacter", Original = Method(typeof(PlayerCamera), "SpawnCharacter"), Prefix = "SpawnPrefix", Postfix = "SpawnPostfix" },
                new Spec { Label = "Body.GetTotalInsulation", Original = Method(typeof(Body), "GetTotalInsulation"), Postfix = "InsulationPostfix" },
                new Spec { Label = "Body.BaseHungerRate", Original = Getter(typeof(Body), "BaseHungerRate"), Postfix = "HungerPostfix" },
                new Spec { Label = "Body.BaseThirstRate", Original = Method(typeof(Body), "BaseThirstRate"), Postfix = "ThirstPostfix" },
                new Spec { Label = "Body.Update", Original = Method(typeof(Body), "Update"), Postfix = "UpdatePostfix" },
                new Spec { Label = "Body.WakeUp", Original = Method(typeof(Body), "WakeUp"), Postfix = "WakePostfix" },
                new Spec { Label = "Body.Eat", Original = Method(typeof(Body), "Eat", typeof(float), typeof(float), typeof(int)), Postfix = "EatPostfix" },
                new Spec { Label = "PreRunScript.Start", Original = Method(typeof(PreRunScript), "Start"), Postfix = "MenuStartPostfix" },
                new Spec { Label = "PreRunScript.HandleCharacterSelect", Original = Method(typeof(PreRunScript), "HandleCharacterSelect"), Postfix = "MenuColourPostfix" },
                new Spec { Label = "SaveSystem.SaveGame", Original = Method(typeof(SaveSystem), "SaveGame"), Transpiler = "SaveTranspiler" },
            };
        }

        public static void ApplyAll(Harmony harmony)
        {
            foreach (Spec spec in Specs()) Patch(harmony, spec);
        }

        static void Patch(Harmony harmony, Spec spec)
        {
            try
            {
                if (spec.Original == null) throw new InvalidOperationException("target method not found");
                harmony.Patch(spec.Original,
                    Handler(spec.Prefix), Handler(spec.Postfix), Handler(spec.Transpiler));
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError("patch " + spec.Label + " failed: " + exception.Message);
            }
        }

        static HarmonyMethod Handler(string name)
        {
            return name == null ? null : new HarmonyMethod(typeof(Patches).GetMethod(name, Declared));
        }

        static bool BaseSkillsPrefix(int chr, ref int[] __result)
        {
            if (chr < 3 || !CharacterGate.Active) return true;
            Registry.EnsureLoaded();
            CharacterDefinition definition = Registry.Get(chr);
            if (definition == null) return true;
            __result = (int[])definition.Skills.Clone();
            return false;
        }

        static bool BaseMultipliersPrefix(int chr, ref float[] __result)
        {
            if (chr < 3 || !CharacterGate.Active) return true;
            Registry.EnsureLoaded();
            CharacterDefinition definition = Registry.Get(chr);
            if (definition == null) return true;
            __result = (float[])definition.Multipliers.Clone();
            return false;
        }

        static void SkillsSetupPostfix(Skills __instance, int chr)
        {
            if (chr >= 3 || !CharacterGate.Active) return;
            CustomCharacter owner = CustomCharacter.OwnerOf(__instance);
            if (owner != null) __instance.Setup(owner.Id);
        }

        static void GetStringPostfix(string str, int type, ref string __result)
        {
            if (type != 3 || str == null || !str.StartsWith("char", StringComparison.Ordinal) || !CharacterGate.Active) return;
            int digits = 4;
            while (digits < str.Length && char.IsDigit(str[digits])) digits++;
            if (digits == 4) return;
            int id;
            if (!int.TryParse(str.Substring(4, digits - 4), out id) || id < 3) return;
            CharacterDefinition definition = Registry.Get(id);
            if (definition == null) return;
            string field = str.Substring(digits);
            string text = field == "name" ? Pick(definition.Name)
                : field == "desc" ? Join(Pick(definition.Family), Pick(definition.Quote))
                : field == "quirks" ? Pick(definition.Quirks) : null;
            if (text != null) __result = text;
        }

        static string Pick(Dictionary<string, string> text)
        {
            string value;
            if (text.TryGetValue("en", out value)) return value;
            foreach (string any in text.Values) return any;
            return "";
        }

        static string Join(string first, string second)
        {
            return first.Length > 0 && second.Length > 0 ? first + "\n\n" + second : first + second;
        }

        static void SpawnPrefix(ref int chr, out int __state)
        {
            __state = 0;
            if (chr < 3) return;
            chr = MultiplayerPolicy.SpawnSlot(chr, CharacterGate.Active);
            if (chr < 3) return;
            Registry.EnsureLoaded();
            CharacterDefinition definition = Registry.Get(chr);
            if (definition == null) { chr = 0; return; }
            __state = chr;
            chr = Vocabulary.BaseIndex(definition.Base);
        }

        static void SpawnPostfix(GameObject __result, int __state)
        {
            if (__state < 3 || __result == null) return;
            CustomCharacter.Attach(__result.GetComponentInChildren<Body>(true), __state);
        }

        static void InsulationPostfix(Body __instance, ref float __result)
        {
            CustomCharacter character = CustomCharacter.Of(__instance);
            if (character == null || !CharacterGate.Active) return;
            __result = TraitMath.ApplyInsulation(__result, character.Trait("insulationMult", 1f), 0.5f);
        }

        static void HungerPostfix(Body __instance, ref float __result)
        {
            CustomCharacter character = CustomCharacter.Of(__instance);
            if (character != null && CharacterGate.Active) __result *= character.Trait("hungerRateMult", 1f);
        }

        static void ThirstPostfix(Body __instance, ref float __result)
        {
            CustomCharacter character = CustomCharacter.Of(__instance);
            if (character == null || !CharacterGate.Active) return;
            __result *= character.Trait("thirstRateMult", 1f);
            __result *= TraitMath.HeatThirstFactor(__instance.temperature, character.Trait("heatThreshold", 37.5f), character.Trait("heatThirstMult", 1f));
        }

        static void UpdatePostfix(Body __instance)
        {
            CustomCharacter character = CustomCharacter.Of(__instance);
            if (character != null && CharacterGate.Active) character.Tick(Time.deltaTime);
        }

        static void WakePostfix(Body __instance)
        {
            CustomCharacter character = CustomCharacter.Of(__instance);
            if (character != null && CharacterGate.Active) character.Fire("wake");
        }

        static void EatPostfix(Body __instance)
        {
            CustomCharacter character = CustomCharacter.Of(__instance);
            if (character != null && CharacterGate.Active) character.Fire("eat");
        }

        static void MenuStartPostfix(PreRunScript __instance)
        {
            Registry.EnsureLoaded();
            try { CharacterMenu.Build(__instance); }
            catch (Exception exception) { Plugin.Log.LogError("character menu not built: " + exception); }
        }

        static void MenuColourPostfix(PreRunScript __instance)
        {
            CharacterMenu.Colour(__instance);
        }

        public static int SavedCharacter(int charType)
        {
            CustomCharacter character = PlayerCamera.main != null && CharacterGate.Active ? CustomCharacter.Of(PlayerCamera.main.body) : null;
            return character != null ? character.Id : charType;
        }

        public static IEnumerable<CodeInstruction> SaveTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo charTypeField = typeof(Body).GetField("charType");
            FieldInfo savedField = typeof(SaveInfo).GetField("character");
            MethodInfo hook = typeof(Patches).GetMethod("SavedCharacter");
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            int replaced = 0;
            for (int i = 0; i + 1 < list.Count; i++)
            {
                if (list[i].LoadsField(charTypeField) && list[i + 1].StoresField(savedField))
                {
                    list.Insert(i + 1, new CodeInstruction(OpCodes.Call, hook));
                    replaced++;
                    i++;
                }
            }
            if (replaced != 1) Plugin.Log.LogWarning("save patch matched " + replaced + " sites, custom characters will save as their base");
            return list;
        }
    }
}
