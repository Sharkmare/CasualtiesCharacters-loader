using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters.PackCheck
{
    public static class Program
    {
        const string DefaultLoaderVersion = "0.2.0";

        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("usage: PackCheck <pack folder> [<pack folder> ...] [--loader <version>]");
                return 2;
            }
            string loader = DefaultLoaderVersion;
            List<string> folders = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--loader" && i + 1 < args.Length) loader = args[++i];
                else folders.Add(args[i]);
            }

            List<PackSource> packs = new List<PackSource>();
            List<string> problems = new List<string>();
            foreach (string folder in folders) packs.Add(ReadPack(Path.GetFullPath(folder), problems));
            CatalogResult result = CharacterCatalog.Build(packs.Where(p => p != null), loader, null);
            problems.AddRange(result.Errors);
            problems.AddRange(result.Warnings);
            problems.AddRange(CheckSkins(result.Definitions));

            foreach (CharacterDefinition definition in result.Definitions)
                Console.WriteLine("character " + definition.Id + " = " + definition.FullKey + " base " + definition.Base + (definition.SkinPath != null ? " skin " + definition.SkinFolder : " (no skin)"));
            Console.WriteLine("fingerprint " + result.Fingerprint);
            foreach (string problem in problems) Console.WriteLine("PROBLEM " + problem);
            Console.WriteLine(result.Definitions.Count + " character(s), " + problems.Count + " problem(s)");
            return problems.Count == 0 && result.Definitions.Count > 0 ? 0 : 1;
        }

        static PackSource ReadPack(string folder, List<string> problems)
        {
            string manifest = Path.Combine(folder, "pack.json");
            PackInfo info = PackManifest.Parse(File.Exists(manifest) ? File.ReadAllText(manifest) : null, folder, Path.GetFileName(folder), problems);
            if (info == null) return null;
            PackSource source = new PackSource { Info = info };
            string characters = Path.Combine(folder, "characters");
            if (!Directory.Exists(characters)) { problems.Add(folder + " has no characters folder"); return null; }
            foreach (string file in Directory.GetFiles(characters, "*.json"))
                source.Files.Add(new KeyValuePair<string, string>(Path.GetFileName(file), File.ReadAllText(file)));
            return source;
        }

        static IEnumerable<string> CheckSkins(IEnumerable<CharacterDefinition> definitions)
        {
            foreach (CharacterDefinition definition in definitions.Where(d => d.SkinPath != null))
            {
                if (!Directory.Exists(definition.SkinPath)) { yield return definition.FullKey + ": skin folder " + definition.SkinPath + " is missing"; continue; }
                List<string> relative = Directory.GetDirectories(definition.SkinPath, "*", SearchOption.AllDirectories)
                    .Select(d => d.Substring(definition.SkinPath.Length).TrimStart('\\', '/')).ToList();
                string body = SkinMatcher.FindBodyFolder(relative);
                if (body == null) { yield return definition.FullKey + ": no Body folder inside the skin"; continue; }
                SkinMatcher.Report report = SkinMatcher.Match(Directory.GetFiles(Path.Combine(definition.SkinPath, body), "*.png").Select(Path.GetFileName));
                if (report.Missing.Count > 0) yield return definition.FullKey + ": skin " + report.Summary();
            }
        }
    }
}
