using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesCharacters;

namespace PatchTargetsCheck
{
    public static class Program
    {
        static readonly HashSet<string> InjectedNames = new HashSet<string> { "__instance", "__result", "__state" };

        public static int Main()
        {
            List<string> failures = new List<string>();
            List<Patches.Spec> specs = Patches.Specs();
            foreach (Patches.Spec spec in specs) CheckSpec(spec, failures);
            CheckSaveSite(specs.First(s => s.Label == "SaveSystem.SaveGame").Original, failures);

            foreach (string failure in failures) Console.WriteLine("FAIL  " + failure);
            Console.WriteLine(specs.Count + " patch targets checked, " + failures.Count + " problems");
            return failures.Count == 0 ? 0 : 1;
        }

        static void CheckSpec(Patches.Spec spec, List<string> failures)
        {
            if (spec.Original == null) { failures.Add(spec.Label + ": target not found"); return; }
            foreach (string handlerName in new[] { spec.Prefix, spec.Postfix, spec.Transpiler })
            {
                if (handlerName == null) continue;
                MethodInfo handler = typeof(Patches).GetMethod(handlerName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (handler == null) { failures.Add(spec.Label + ": handler " + handlerName + " missing"); continue; }
                if (handlerName == spec.Transpiler) continue;
                HashSet<string> originalNames = new HashSet<string>(spec.Original.GetParameters().Select(p => p.Name));
                foreach (ParameterInfo parameter in handler.GetParameters())
                {
                    string name = parameter.Name;
                    if (InjectedNames.Contains(name) || originalNames.Contains(name)) continue;
                    failures.Add(spec.Label + ": " + handlerName + " parameter '" + name + "' matches nothing in the original");
                }
            }
        }

        static void CheckSaveSite(MethodBase saveGame, List<string> failures)
        {
            if (saveGame == null) return;
            FieldInfo charType = typeof(Body).GetField("charType");
            FieldInfo character = typeof(SaveInfo).GetField("character");
            byte[] il = saveGame.GetMethodBody().GetILAsByteArray();
            int sites = 0;
            for (int i = 0; i + 9 < il.Length; i++)
            {
                if (il[i] != 0x7B || il[i + 5] != 0x7D) continue;
                FieldInfo loaded = saveGame.Module.ResolveField(BitConverter.ToInt32(il, i + 1));
                FieldInfo stored = saveGame.Module.ResolveField(BitConverter.ToInt32(il, i + 6));
                if (loaded == charType && stored == character) sites++;
            }
            Console.WriteLine("save site: ldfld charType directly followed by stfld character, matches = " + sites);
            if (sites != 1) failures.Add("SaveGame: expected exactly 1 charType to character site, found " + sites);
        }
    }
}
