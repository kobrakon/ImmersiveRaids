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
        internal static ConfigEntry<bool> EnableEvents;
        internal static ConfigEntry<bool> ActuallyCleanup;
        internal static ConfigEntry<int> DistToCleanup;

        void Awake()
        {
            logger = Logger;
            Logger.LogInfo("Loading Immersive Raids");
            Hook = new GameObject("IR Object");
            Script = Hook.AddComponent<IRController>();
            DontDestroyOnLoad(Hook);
            EnableEvents = Config.Bind("Raid Settings", "Enable Dynamic Events", true, "Dictates whether the dynamic event timer should increment and allow events to run or not.\nNote that this DOES NOT stop events that are already running!");
            ActuallyCleanup = Config.Bind("Cleanup Settings", "Cleanup Bodies", true, "Do you want cleanup to clean bodies, or do you just want the airdrop?\nfreeloader");
            DistToCleanup = Config.Bind("Cleanup Settings", "Distance to Cleanup", 0, "How far away should bodies be for cleanup.\ni.e. 0 cleans entire map, 100 only cleans bodies 100m away.");

            new GameWorldPatch().Enable();
            new UIPanelPatch().Enable();
            new TimerUIPatch().Enable();
            new EventExfilPatch().Enable();
            new EventExfilTipPatch().Enable();
            new WeatherControllerPatch().Enable();
            new AirdropBoxPatch().Enable();
            new FactoryTimePatch().Enable();
            new BotDiedPatch().Enable();
            new GlobalsPatch().Enable();
            //new StashPatch().Enable();
            new WatchPatch().Enable();
            new EnableEntryPointPatch().Enable();
        }
    }
}
