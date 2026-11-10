using System.Collections.Generic;
using System.Linq;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters.Tests
{
    public static class CatalogTests
    {
        const string Loader = "0.2.0";

        static string Character(string key, string name, string extra = "")
        {
            return "{\"key\":\"" + key + "\",\"name\":\"" + name + "\"" + extra + "}";
        }

        static PackSource Pack(string id, string version, string folder, params string[] characterJson)
        {
            PackSource source = new PackSource { Info = new PackInfo { Id = id, Version = version, Folder = folder } };
            for (int i = 0; i < characterJson.Length; i++)
                source.Files.Add(new KeyValuePair<string, string>("c" + i + ".json", characterJson[i]));
            return source;
        }

        static PackSource[] ThreePacks()
        {
            return new[]
            {
                Pack("alpha", "1.0.0", "/p/alpha", Character("hero", "Alpha Hero"), Character("side", "Alpha Side")),
                Pack("beta", "2.1.0", "/p/beta", Character("hero", "Beta Hero", ",\"skin\":\"look\"")),
                Pack("gamma", "0.3.0", "/p/gamma", Character("solo", "Gamma Solo", ",\"skin\":\"look\""))
            };
        }

        static Dictionary<string, int> IdsOf(CatalogResult result)
        {
            return result.Definitions.ToDictionary(d => d.FullKey, d => d.Id);
        }

        public static void Register(TestSuite suite)
        {
            suite.Add("the same installed set resolves to the same ids and fingerprint in every pack order", () =>
            {
                PackSource[] set = ThreePacks();
                CatalogResult reference = CharacterCatalog.Build(set, Loader, null);
                Check.Equal(4, reference.Definitions.Count, "all characters");
                foreach (IEnumerable<PackSource> order in IdAllocatorTests.Permutations(set))
                {
                    CatalogResult result = CharacterCatalog.Build(order, Loader, null);
                    Check.Equal(reference.Fingerprint, result.Fingerprint, "fingerprint");
                    foreach (KeyValuePair<string, int> pair in IdsOf(reference)) Check.Equal(pair.Value, IdsOf(result)[pair.Key], pair.Key);
                }
            });

            suite.Add("file order inside a pack does not change the ids", () =>
            {
                PackSource forward = Pack("alpha", "1", "/a", Character("one", "One"), Character("two", "Two"));
                PackSource backward = Pack("alpha", "1", "/a", Character("one", "One"), Character("two", "Two"));
                backward.Files.Reverse();
                Check.Equal(CharacterCatalog.Build(new[] { forward }, Loader, null).Fingerprint,
                    CharacterCatalog.Build(new[] { backward }, Loader, null).Fingerprint, "fingerprint");
            });

            suite.Add("two packs may declare the same character key and both load with their own text", () =>
            {
                CatalogResult result = CharacterCatalog.Build(ThreePacks(), Loader, null);
                CharacterDefinition alpha = result.Definitions.Single(d => d.FullKey == "alpha/hero");
                CharacterDefinition beta = result.Definitions.Single(d => d.FullKey == "beta/hero");
                Check.True(alpha.Id != beta.Id, "distinct ids");
                Check.Equal("Alpha Hero", alpha.Name["en"], "alpha text");
                Check.Equal("Beta Hero", beta.Name["en"], "beta text");
            });

            suite.Add("the same skin folder name in two packs points into each pack's own folder", () =>
            {
                CatalogResult result = CharacterCatalog.Build(ThreePacks(), Loader, null);
                string beta = result.Definitions.Single(d => d.FullKey == "beta/hero").SkinPath;
                string gamma = result.Definitions.Single(d => d.FullKey == "gamma/solo").SkinPath;
                Check.True(beta != gamma, "different paths");
                Check.True(beta.Replace('\\', '/').StartsWith("/p/beta/skins/"), beta);
                Check.True(gamma.Replace('\\', '/').StartsWith("/p/gamma/skins/"), gamma);
            });

            suite.Add("two packs requesting the same id: one keeps it, the order of loading does not matter", () =>
            {
                PackSource[] set =
                {
                    Pack("aaa", "1", "/a", Character("x", "X", ",\"id\":9")),
                    Pack("zzz", "1", "/z", Character("y", "Y", ",\"id\":9"))
                };
                foreach (IEnumerable<PackSource> order in IdAllocatorTests.Permutations(set))
                {
                    Dictionary<string, int> ids = IdsOf(CharacterCatalog.Build(order, Loader, null));
                    Check.Equal(9, ids["aaa/x"], "earlier pack id wins");
                    Check.True(ids["zzz/y"] != 9, "later moved");
                }
            });

            suite.Add("removing a pack does not shift the ids of the packs that remain", () =>
            {
                PackSource[] set = ThreePacks();
                Dictionary<string, int> full = IdsOf(CharacterCatalog.Build(set, Loader, null));
                Dictionary<string, int> withoutGamma = IdsOf(CharacterCatalog.Build(set.Take(2), Loader, null));
                foreach (KeyValuePair<string, int> pair in withoutGamma) Check.Equal(full[pair.Key], pair.Value, pair.Key);
            });

            suite.Add("the fingerprint changes when a pack version changes and when a pack is added", () =>
            {
                string reference = CharacterCatalog.Build(ThreePacks(), Loader, null).Fingerprint;
                PackSource[] bumped = ThreePacks();
                bumped[0].Info.Version = "1.0.1";
                Check.True(CharacterCatalog.Build(bumped, Loader, null).Fingerprint != reference, "version");
                Check.True(CharacterCatalog.Build(ThreePacks().Take(2), Loader, null).Fingerprint != reference, "added pack");
            });

            suite.Add("a pack installed twice keeps the newer copy whatever the folder order", () =>
            {
                PackSource older = Pack("alpha", "1.0.0", "/old", Character("hero", "Old"));
                PackSource newer = Pack("alpha", "1.2.0", "/new", Character("hero", "New"));
                foreach (IEnumerable<PackSource> order in IdAllocatorTests.Permutations(new[] { older, newer }))
                {
                    CatalogResult result = CharacterCatalog.Build(order, Loader, null);
                    Check.Equal(1, result.Definitions.Count, "one copy");
                    Check.Equal("New", result.Definitions[0].Name["en"], "newer wins");
                    Check.Equal(1, result.Warnings.Count, "warned once");
                }
            });

            suite.Add("a character key declared twice inside one pack keeps the first file", () =>
            {
                CatalogResult result = CharacterCatalog.Build(new[] { Pack("alpha", "1", "/a", Character("hero", "First"), Character("hero", "Second")) }, Loader, null);
                Check.Equal(1, result.Definitions.Count, "one");
                Check.Equal("First", result.Definitions[0].Name["en"], "first wins");
                Check.Equal(1, result.Warnings.Count, "warned");
            });

            suite.Add("a pack that needs a newer loader is skipped and the others still load", () =>
            {
                PackSource demanding = Pack("alpha", "1", "/a", Character("hero", "H"));
                demanding.Info.MinLoaderVersion = "9.0.0";
                CatalogResult result = CharacterCatalog.Build(new[] { demanding, Pack("beta", "1", "/b", Character("x", "X")) }, Loader, null);
                Check.Equal(1, result.Definitions.Count, "only beta");
                Check.Equal("beta", result.Definitions[0].PackId, "beta loaded");
                Check.True(result.SkippedPacks.Contains("alpha"), "alpha skipped");
            });

            suite.Add("a broken character file does not stop the pack's other files or other packs", () =>
            {
                PackSource broken = Pack("alpha", "1", "/a", "{not json", Character("good", "Good"), Character("nobase", "N", ",\"base\":\"robot\""));
                CatalogResult result = CharacterCatalog.Build(new[] { broken, Pack("beta", "1", "/b", Character("x", "X")) }, Loader, null);
                Check.Equal(2, result.Definitions.Count, "two survive");
                Check.Equal(2, result.Errors.Count, "two errors, each logged once");
            });

            suite.Add("a character with only a base and traits needs no skin", () =>
            {
                CatalogResult result = CharacterCatalog.Build(new[] { Pack("p", "1", "/p", Character("plain", "Plain", ",\"numbers\":{\"carryMult\":1.5}")) }, Loader, null);
                CharacterDefinition plain = result.Definitions.Single();
                Check.True(plain.SkinPath == null, "no skin path");
                Check.Equal(0, result.Warnings.Count, "no warnings");
                Check.Near(1.5f, plain.Numbers["carryMult"], "trait kept");
            });

            suite.Add("pack manifests: ids are validated, folder names are the fallback, versions compare numerically", () =>
            {
                List<string> errors = new List<string>();
                Check.Equal("my-pack", PackManifest.Parse(null, "/x", "My Pack", errors).Id, "fallback id");
                Check.True(PackManifest.Parse("{\"id\":\"Bad Id\"}", "/x", "f", errors) == null, "bad id refused");
                Check.True(PackManifest.Parse("{oops", "/x", "f", errors) == null, "bad json refused");
                Check.Equal(2, errors.Count, "errors recorded");
                Check.Equal("1.2.3", PackManifest.Parse("{\"id\":\"ok\",\"version\":\"1.2.3\"}", "/x", "f", errors).Version, "version");
                Check.True(VersionCompare.Compare("1.10.0", "1.9.9") > 0, "numeric not text");
                Check.True(VersionCompare.Compare("2", "2.0.0") == 0, "padding");
            });
        }
    }
}
