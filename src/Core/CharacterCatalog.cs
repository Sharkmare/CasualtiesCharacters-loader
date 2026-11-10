using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesCharacters.Core
{
    public sealed class PackSource
    {
        public PackInfo Info;
        public List<KeyValuePair<string, string>> Files = new List<KeyValuePair<string, string>>();
    }

    public sealed class CatalogResult
    {
        public List<CharacterDefinition> Definitions = new List<CharacterDefinition>();
        public List<string> Warnings = new List<string>();
        public List<string> Errors = new List<string>();
        public List<string> SkippedPacks = new List<string>();
        public string Fingerprint = "";
    }

    public static class CharacterCatalog
    {
        public static CatalogResult Build(IEnumerable<PackSource> sources, string loaderVersion, ICollection<string> extraTraitNames)
        {
            CatalogResult result = new CatalogResult();
            List<CharacterDefinition> accepted = new List<CharacterDefinition>();
            foreach (PackSource source in ChoosePacks(sources, loaderVersion, result))
            {
                try { accepted.AddRange(ReadPack(source, extraTraitNames, result).ToList()); }
                catch (Exception exception)
                {
                    result.Errors.Add("pack " + source.Info.Id + " failed and was skipped: " + exception.Message);
                    result.SkippedPacks.Add(source.Info.Id);
                }
            }
            Dictionary<string, int> idByKey = IdAllocator.Allocate(accepted);
            foreach (CharacterDefinition definition in accepted) definition.Id = idByKey[definition.FullKey];
            result.Definitions = accepted.OrderBy(d => d.Id).ToList();
            result.Fingerprint = IdAllocator.Fingerprint(result.Definitions);
            return result;
        }

        static List<PackSource> ChoosePacks(IEnumerable<PackSource> sources, string loaderVersion, CatalogResult result)
        {
            List<PackSource> chosen = new List<PackSource>();
            IEnumerable<PackSource> ordered = sources
                .OrderBy(s => s.Info.Id, StringComparer.Ordinal)
                .ThenByDescending(s => s.Info.Version, Comparer<string>.Create(VersionCompare.Compare))
                .ThenBy(s => s.Info.Folder ?? "", StringComparer.Ordinal);
            foreach (PackSource source in ordered)
            {
                if (chosen.Any(c => c.Info.Id == source.Info.Id))
                {
                    result.Warnings.Add("pack " + source.Info.Id + " is installed twice, the older copy was skipped");
                    result.SkippedPacks.Add(source.Info.Id);
                    continue;
                }
                if (VersionCompare.Compare(source.Info.MinLoaderVersion, loaderVersion) > 0)
                {
                    result.Warnings.Add("pack " + source.Info.Id + " needs the character loader " + source.Info.MinLoaderVersion + " or newer, skipped");
                    result.SkippedPacks.Add(source.Info.Id);
                    continue;
                }
                chosen.Add(source);
            }
            return chosen;
        }

        static IEnumerable<CharacterDefinition> ReadPack(PackSource source, ICollection<string> extraTraitNames, CatalogResult result)
        {
            HashSet<string> seenKeys = new HashSet<string>();
            foreach (KeyValuePair<string, string> file in source.Files.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                ParseResult parsed = CharacterParser.Parse(file.Value, source.Info.Id + "/" + file.Key, extraTraitNames);
                result.Warnings.AddRange(parsed.Warnings);
                result.Errors.AddRange(parsed.Errors);
                if (!parsed.Ok) continue;
                CharacterDefinition definition = parsed.Definition;
                if (!seenKeys.Add(definition.Key))
                {
                    result.Warnings.Add("pack " + source.Info.Id + " declares the character key '" + definition.Key + "' twice, the file " + file.Key + " was skipped");
                    continue;
                }
                definition.PackId = source.Info.Id;
                definition.PackVersion = source.Info.Version;
                definition.SourceFolder = source.Info.Folder;
                yield return definition;
            }
        }
    }
}
