using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters
{
    public delegate void TraitHandler(Body body, CustomCharacter character, float value);

    public static class CharacterApi
    {
        internal static readonly Dictionary<string, TraitHandler> TraitHandlers = new Dictionary<string, TraitHandler>();

        public static void RegisterTrait(string name, TraitHandler handler)
        {
            TraitHandlers[name] = handler;
        }
    }

    public static class Registry
    {
        public const string LocalPackId = "local";

        public static readonly Dictionary<int, CharacterDefinition> ById = new Dictionary<int, CharacterDefinition>();
        static bool loaded;

        public static int HighestId { get; private set; }
        public static string Fingerprint { get; private set; }

        public static CharacterDefinition Get(int id)
        {
            CharacterDefinition definition;
            return ById.TryGetValue(id, out definition) ? definition : null;
        }

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            CatalogResult result;
            try
            {
                result = CharacterCatalog.Build(FindPacks(), Plugin.Version, CharacterApi.TraitHandlers.Keys);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError("character packs could not be read: " + exception.Message);
                return;
            }
            foreach (string warning in result.Warnings) Plugin.Log.LogWarning(warning);
            foreach (string error in result.Errors) Plugin.Log.LogError(error);
            foreach (CharacterDefinition definition in result.Definitions)
            {
                ById[definition.Id] = definition;
                if (definition.Id > HighestId) HighestId = definition.Id;
                Plugin.Log.LogInfo("character " + definition.Id + " = " + definition.FullKey + " (base " + definition.Base + ", pack version " + definition.PackVersion + ")");
            }
            Fingerprint = result.Fingerprint;
            Plugin.Log.LogInfo("character table fingerprint " + result.Fingerprint);
        }

        static List<PackSource> FindPacks()
        {
            List<PackSource> packs = new List<PackSource>();
            AddPack(packs, Plugin.PluginFolder, LocalPackId);
            foreach (string folder in Directory.GetDirectories(BepInEx.Paths.PluginPath))
            {
                if (string.Equals(Path.GetFullPath(folder), Path.GetFullPath(Plugin.PluginFolder), StringComparison.OrdinalIgnoreCase)) continue;
                AddPack(packs, folder, null);
            }
            return packs;
        }

        static void AddPack(List<PackSource> packs, string folder, string forcedId)
        {
            try
            {
                string charactersFolder = Path.Combine(folder, "characters");
                if (!Directory.Exists(charactersFolder)) return;
                string manifestPath = Path.Combine(folder, "pack.json");
                List<string> errors = new List<string>();
                PackInfo info = forcedId != null
                    ? new PackInfo { Id = forcedId, Name = forcedId, Folder = folder }
                    : PackManifest.Parse(File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null, folder, Path.GetFileName(folder), errors);
                foreach (string error in errors) Plugin.Log.LogError(error);
                if (info == null) return;
                PackSource source = new PackSource { Info = info };
                foreach (string file in Directory.GetFiles(charactersFolder, "*.json"))
                    source.Files.Add(new KeyValuePair<string, string>(Path.GetFileName(file), File.ReadAllText(file)));
                packs.Add(source);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError("pack folder " + folder + " skipped: " + exception.Message);
            }
        }
    }
}
