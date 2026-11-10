using System.Runtime.CompilerServices;
using CasualtiesCharacters.Core;
using Together;

namespace CasualtiesCharacters
{
    static class ForkBridge
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Connect()
        {
            Registry.EnsureLoaded();
            foreach (var pair in Registry.ById)
            {
                string name = pair.Value.Name.ContainsKey("en") ? pair.Value.Name["en"] : pair.Value.Key;
                bool added = TogetherCharacters.Register((byte)pair.Key, name, (byte)Vocabulary.BaseIndex(pair.Value.Base));
                Plugin.Log.LogInfo("Together registration of " + pair.Key + " " + (added ? "ok" : "refused"));
            }
            TogetherCharacters.BodyBuilt += OnBodyBuilt;
            TogetherCharacters.CharacterChanged += OnCharacterChanged;
        }

        static void OnBodyBuilt(Body body, byte id, ScavPlayer player)
        {
            if (id >= 3) CustomCharacter.Attach(body, id);
        }

        static void OnCharacterChanged(ScavPlayer player, byte oldId, byte newId)
        {
            if (newId >= 3 && player != null && player.body != null) CustomCharacter.Attach(player.body, newId);
        }
    }
}
