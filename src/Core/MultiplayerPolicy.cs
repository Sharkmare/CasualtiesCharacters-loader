namespace CasualtiesCharacters.Core
{
    public static class MultiplayerPolicy
    {
        public const int FirstCustomSlot = 3;
        public const int FallbackVanillaSlot = 0;

        public static bool CharactersActive(bool multiplayerRunning, bool characterApiPresent)
        {
            return !multiplayerRunning || characterApiPresent;
        }

        public static int SpawnSlot(int requestedSlot, bool charactersActive)
        {
            if (requestedSlot < FirstCustomSlot) return requestedSlot;
            return charactersActive ? requestedSlot : FallbackVanillaSlot;
        }
    }
}
