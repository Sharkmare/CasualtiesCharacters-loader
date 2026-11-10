using CasualtiesCharacters.Core;

namespace CasualtiesCharacters.Tests
{
    public static class MultiplayerPolicyTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("singleplayer is always on, with or without any Together build", () =>
            {
                Check.True(MultiplayerPolicy.CharactersActive(false, false), "no Together");
                Check.True(MultiplayerPolicy.CharactersActive(false, true), "fork present, no game running");
            });

            suite.Add("multiplayer is on only when the build with character support is present", () =>
            {
                Check.True(MultiplayerPolicy.CharactersActive(true, true), "fork");
                Check.True(!MultiplayerPolicy.CharactersActive(true, false), "any other build");
            });

            suite.Add("when inactive a custom slot becomes the first vanilla character and vanilla slots are untouched", () =>
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    Check.Equal(slot, MultiplayerPolicy.SpawnSlot(slot, false), "vanilla inactive " + slot);
                    Check.Equal(slot, MultiplayerPolicy.SpawnSlot(slot, true), "vanilla active " + slot);
                }
                foreach (int custom in new[] { 3, 4, 100, 254 })
                {
                    Check.Equal(0, MultiplayerPolicy.SpawnSlot(custom, false), "custom inactive " + custom);
                    Check.Equal(custom, MultiplayerPolicy.SpawnSlot(custom, true), "custom active " + custom);
                }
            });
        }
    }
}
