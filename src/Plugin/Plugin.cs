using System;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesCharacters
{
    [BepInPlugin(Guid, "CasualtiesCharacters", Version)]
    [BepInDependency("CasualtiesMP", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "hive.casualties.characters";
        public const string Version = "0.2.0";

        public static Plugin Instance;
        public static ManualLogSource Log;
        public static ConfigEntry<bool> DebugLogging;
        public static string PluginFolder;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            PluginFolder = Path.GetDirectoryName(Info.Location);
            DebugLogging = Config.Bind("Debug", "Enabled", false, "Log skin sprite names and every trait decision.");
            Patches.ApplyAll(new Harmony(Guid));
        }

        void Start()
        {
            Registry.EnsureLoaded();
            if (Chainloader.PluginInfos.ContainsKey("CasualtiesMP")) TryConnectTogether();
        }

        void TryConnectTogether()
        {
            try { TogetherLink.Connect(); }
            catch (Exception exception) { Log.LogWarning("Together bridge not connected: " + exception.Message); }
        }

        public static void Debug(string message)
        {
            if (DebugLogging != null && DebugLogging.Value) Log.LogInfo(message);
        }
    }
}
