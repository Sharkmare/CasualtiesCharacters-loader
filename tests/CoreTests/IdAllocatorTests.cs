using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters.Tests
{
    public static class IdAllocatorTests
    {
        static CharacterDefinition Make(string pack, string key, int requested)
        {
            return new CharacterDefinition { PackId = pack, Key = key, RequestedId = requested };
        }

        public static void Register(TestSuite suite)
        {
            suite.Add("every id is in the custom range and unique", () =>
            {
                List<CharacterDefinition> all = Enumerable.Range(0, 100).Select(i => Make("p", "k" + i, 0)).ToList();
                Dictionary<string, int> ids = IdAllocator.Allocate(all);
                Check.Equal(100, ids.Values.Distinct().Count(), "unique");
                Check.True(ids.Values.All(id => id >= 3 && id <= 254), "range");
            });

            suite.Add("the same set gives the same ids in any input order", () =>
            {
                CharacterDefinition[] set = { Make("a", "one", 0), Make("b", "two", 0), Make("c", "three", 0), Make("a", "four", 0) };
                Dictionary<string, int> expected = IdAllocator.Allocate(set);
                foreach (IEnumerable<CharacterDefinition> order in Permutations(set))
                {
                    Dictionary<string, int> ids = IdAllocator.Allocate(order);
                    foreach (KeyValuePair<string, int> pair in expected) Check.Equal(pair.Value, ids[pair.Key], pair.Key);
                }
            });

            suite.Add("an id comes from the key, so adding or removing others does not move it", () =>
            {
                Dictionary<string, int> alone = IdAllocator.Allocate(new[] { Make("a", "one", 0) });
                Dictionary<string, int> crowd = IdAllocator.Allocate(new[] { Make("a", "one", 0), Make("b", "x", 0), Make("c", "y", 0) });
                Check.Equal(alone["a/one"], crowd["a/one"], "stable");
            });

            suite.Add("two requests for one id: the lower key keeps it and the other gets a derived id", () =>
            {
                Dictionary<string, int> ids = IdAllocator.Allocate(new[] { Make("zz", "late", 7), Make("aa", "early", 7) });
                Check.Equal(7, ids["aa/early"], "winner");
                Check.True(ids["zz/late"] != 7, "loser moved");
            });

            suite.Add("a hash collision moves only the later key, deterministically", () =>
            {
                string first = "pack/k0";
                CharacterDefinition a = Make("pack", "k0", 0);
                string collidingKey = null;
                uint slot = IdAllocator.Fnv1a(first) % 252;
                for (int i = 1; i < 100000 && collidingKey == null; i++)
                    if (IdAllocator.Fnv1a("pack/c" + i) % 252 == slot) collidingKey = "c" + i;
                CharacterDefinition b = Make("pack", collidingKey, 0);
                Dictionary<string, int> ids = IdAllocator.Allocate(new[] { b, a });
                Check.True(ids["pack/k0"] != ids["pack/" + collidingKey], "distinct");
                Check.Equal(3 + (int)slot, ids["pack/" + (string.CompareOrdinal("pack/k0", "pack/" + collidingKey) < 0 ? "k0" : collidingKey)], "earlier key keeps the hash slot");
            });

            suite.Add("the fingerprint depends on pack id, pack version, key, id and base", () =>
            {
                CharacterDefinition baseline = new CharacterDefinition { PackId = "a", PackVersion = "1", Key = "k", Id = 5, Base = "experiment" };
                string reference = IdAllocator.Fingerprint(new[] { baseline });
                Check.Equal(reference, IdAllocator.Fingerprint(new[] { baseline }), "stable");
                foreach (Action<CharacterDefinition> change in new Action<CharacterDefinition>[]
                {
                    d => d.PackId = "b", d => d.PackVersion = "2", d => d.Key = "j", d => d.Id = 6, d => d.Base = "milky"
                })
                {
                    CharacterDefinition changed = new CharacterDefinition { PackId = "a", PackVersion = "1", Key = "k", Id = 5, Base = "experiment" };
                    change(changed);
                    Check.True(IdAllocator.Fingerprint(new[] { changed }) != reference, "changed input must change the fingerprint");
                }
            });
        }

        public static IEnumerable<IEnumerable<T>> Permutations<T>(T[] items)
        {
            if (items.Length <= 1) { yield return items; yield break; }
            for (int i = 0; i < items.Length; i++)
            {
                T[] rest = items.Where((_, index) => index != i).ToArray();
                foreach (IEnumerable<T> tail in Permutations(rest)) yield return new[] { items[i] }.Concat(tail);
            }
        }
    }
}
