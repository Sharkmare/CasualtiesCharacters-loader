using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CasualtiesCharacters.Core
{
    public static class IdAllocator
    {
        public const int FirstCustomId = 3;
        public const int LastCustomId = 254;
        const int SlotCount = LastCustomId - FirstCustomId + 1;

        public static Dictionary<string, int> Allocate(IEnumerable<CharacterDefinition> definitions)
        {
            List<CharacterDefinition> ordered = definitions.OrderBy(d => d.FullKey, StringComparer.Ordinal).ToList();
            Dictionary<string, int> idByKey = new Dictionary<string, int>();
            HashSet<int> taken = new HashSet<int>();
            foreach (CharacterDefinition definition in ordered)
            {
                int wanted = definition.RequestedId;
                if (wanted >= FirstCustomId && wanted <= LastCustomId && taken.Add(wanted))
                    idByKey[definition.FullKey] = wanted;
            }
            foreach (CharacterDefinition definition in ordered)
            {
                if (idByKey.ContainsKey(definition.FullKey)) continue;
                if (taken.Count >= SlotCount) throw new InvalidOperationException("out of custom character ids");
                int slot = (int)(Fnv1a(definition.FullKey) % (uint)SlotCount);
                while (taken.Contains(FirstCustomId + slot)) slot = (slot + 1) % SlotCount;
                taken.Add(FirstCustomId + slot);
                idByKey[definition.FullKey] = FirstCustomId + slot;
            }
            return idByKey;
        }

        public static uint Fnv1a(string text)
        {
            uint hash = 2166136261;
            foreach (byte value in Encoding.UTF8.GetBytes(text))
            {
                hash ^= value;
                hash *= 16777619;
            }
            return hash;
        }

        public static string Fingerprint(IEnumerable<CharacterDefinition> definitions)
        {
            string joined = string.Join("\n", definitions
                .OrderBy(d => d.Id).ThenBy(d => d.FullKey, StringComparer.Ordinal)
                .Select(d => d.PackId + "@" + d.PackVersion + "|" + d.Key + "|" + d.Id + "|" + d.Base));
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(joined)), 0, 6).Replace("-", "");
        }
    }
}
