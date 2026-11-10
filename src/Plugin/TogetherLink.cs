using System;
using System.Reflection;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters
{
    internal static class CharacterGate
    {
        public static bool CharacterApiPresent;
        public static Func<bool> MultiplayerRunning = () => false;

        public static bool Active
        {
            get { return MultiplayerPolicy.CharactersActive(MultiplayerRunning(), CharacterApiPresent); }
        }
    }

    internal static class TogetherLink
    {
        const string CharacterApiType = "Together.TogetherCharacters, CasualtiesMP";
        const string NetworkType = "Together.Net, CasualtiesMP";
        const string RunningProperty = "IsRunning";

        public static void Connect()
        {
            CharacterGate.CharacterApiPresent = Probe(() => Type.GetType(CharacterApiType) != null);
            CharacterGate.MultiplayerRunning = BindRunningFlag();
            if (CharacterGate.CharacterApiPresent)
            {
                Plugin.Log.LogInfo("Together with character support found, characters are available in multiplayer");
                ForkBridge.Connect();
            }
            else
            {
                Plugin.Log.LogInfo("Together without character support found, characters are available in singleplayer only");
            }
        }

        static Func<bool> BindRunningFlag()
        {
            try
            {
                Type net = Type.GetType(NetworkType);
                if (net == null) return () => false;
                PropertyInfo running = net.GetProperty(RunningProperty, BindingFlags.Public | BindingFlags.Static);
                if (running != null) return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), running.GetGetMethod());
                FieldInfo runningField = net.GetField(RunningProperty, BindingFlags.Public | BindingFlags.Static);
                if (runningField != null) return () => (bool)runningField.GetValue(null);
                return () => true;
            }
            catch (Exception)
            {
                return () => true;
            }
        }

        static bool Probe(Func<bool> check)
        {
            try { return check(); }
            catch (Exception) { return false; }
        }
    }
}
