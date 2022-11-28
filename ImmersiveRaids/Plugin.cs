using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using EFT.Weather;

namespace ImmersiveRaids
{
    [BepInPlugin("com.kobrakon.immersiveraids", "Immersive Raids", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static GameObject Hook;
        internal static ManualLogSource logger;
        void Awake()
        {
            logger = Logger;
            Logger.LogInfo("Loading Immersive Raids");
            Hook = new GameObject("IR Object");
            Hook.AddComponent<IRController>();
            DontDestroyOnLoad(Hook);
            // not sure if all of these are really necessary but im not gonna gamble and find out
            new JSONTimePatch().Enable();
            new RaidTimerPatch().Enable();
            new GameWorldPatch().Enable();
            new UIPanelPatch().Enable();
            new TimerUIPatch().Enable();
            new EventExfilPatch().Enable();
            new WeatherControllerPatch().Enable();
        }
    }
}
