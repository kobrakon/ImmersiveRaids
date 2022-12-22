using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using BepInEx.Configuration;

namespace ImmersiveRaids
{
    [BepInPlugin("com.kobrakon.immersiveraids", "Immersive Raids", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static GameObject Hook;
        internal static IRController Script;
        internal static ManualLogSource logger;
        internal static ConfigEntry<bool> InvertTime;
        internal static ConfigEntry<bool> EnableEvents;

        void Awake()
        {
            logger = Logger;
            Logger.LogInfo("Loading Immersive Raids");
            Hook = new GameObject("IR Object");
            Script = Hook.AddComponent<IRController>();
            DontDestroyOnLoad(Hook);
            InvertTime = Config.Bind("Raid Settings", "Invert Time", false, "Inverts raid time i.e. 8pm becomes 8am");
            EnableEvents = Config.Bind("Raid Settings", "Enable Dynamic Events", true, "Dictates whether the dynamic event timer should increment and allow events to run or not.\nNote that this DOES NOT stop events that are already running!");

            new JSONTimePatch().Enable();
            new RaidTimePatch().Enable();
            new GameWorldPatch().Enable();
            new UIPanelPatch().Enable();
            new TimerUIPatch().Enable();
            new EventExfilPatch().Enable();
            new WeatherControllerPatch().Enable();
            new BotDiedPatch().Enable();
            new ExistencePatch().Enable();
            new WatchPatch().Enable();
        }
    }
}
