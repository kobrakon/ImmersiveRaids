using EFT;
using TMPro;
using EFT.UI;
using System;
using JsonType;
using System.Linq;
using UnityEngine;
using EFT.Weather;
using UnityEngine.UI;
using Comfort.Common;
using EFT.Interactive;
using EFT.UI.Matchmaker;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.BattleTimer;
using EFT.Communications;
using Aki.Custom.Airdrops;
using System.Threading.Tasks;
using Aki.Reflection.Patching;
using Aki.Custom.Airdrops.Utils;
using System.Collections.Generic;
using Aki.Custom.Airdrops.Models;

namespace ImmersiveRaids
{
    public struct RaidTime
    {
        private static DateTime lastTime;
        public static DateTime GetDateTime() => 
            Plugin.InvertTime.Value ? 
            ShouldChange() ? 
            lastTime = DateTime.Now.AddHours(InverseTimeDiff()) : 
            lastTime.Hour == DateTime.Now.Hour ? 
            lastTime = DateTime.Now : 
            lastTime = DateTime.Now.AddHours(InverseTimeDiff()) :
            ShouldChange() ? 
            lastTime = DateTime.Now : 
            lastTime.Hour != DateTime.Now.Hour ? 
            lastTime = DateTime.Now.AddHours(InverseTimeDiff()) :
            lastTime = DateTime.Now;

        private static double InverseTimeDiff() => DateTime.Now.Hour >= 12 ? -12 : 12;
        private static bool ShouldChange() => !Plugin.Script.Ready();
    }

    public class GameWorldPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod("OnGameStarted", BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void Postfix(GameWorld __instance)
        {
            DateTime time = RaidTime.GetDateTime();
            __instance.GameDateTime.Reset(time, time, 1);
        }
    }

    public class BotDiedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(BotControllerClass).GetMethod("BotDied", BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void Postfix(ref BotSpawnerClass ___gclass1324_0, int ___int_0) => ___gclass1324_0.SetMaxBots(___int_0++);
    }

    public class ExistencePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GClass1926.GClass1927).GetConstructor(new Type[0]);

        [PatchPostfix]
        static void Postfix(ref GClass1926.GClass1927 __instance)
        {
            __instance.EnergyDamage = 0.75f;
            __instance.HydrationDamage = 0.75f;
        }
    }

    public class JSONTimePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(LocationWeatherTime).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.HasThis, new Type[] { typeof(WeatherClass), typeof(float), typeof(string), typeof(string) }, null);

        [PatchPrefix]
        static void Prefix(ref string date, ref string time)
        {
            var raidTime = RaidTime.GetDateTime();
            date = raidTime.ToShortDateString();
            time = raidTime.ToString("HH:mm:ss");
        }
    }

    public class RaidTimePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(AbstractGame).GetMethod("UpdateExfiltrationUi", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void Postfix(ref AbstractGame __instance)
        {
            __instance.GameTimer.ChangeSessionTime(new TimeSpan(99999999999999));
        }
    }

    public class UIPanelPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(LocationConditionsPanel).GetMethod("method_0", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void Postfix(ref Toggle ____amTimeToggle, ref TextMeshProUGUI ____currentPhaseTime, ref TextMeshProUGUI ____nextPhaseTime, bool ____takeFromCurrent)
        {
            var raidTime = RaidTime.GetDateTime();
            if (____amTimeToggle.gameObject != null && ____currentPhaseTime.gameObject != null)
            {
                ____amTimeToggle.gameObject.SetActive(false);
                ____currentPhaseTime.gameObject.SetActive(false);
            }
            ____nextPhaseTime.text = raidTime.ToString("HH:mm:ss");
            ____currentPhaseTime.text = raidTime.ToString("HH:mm:ss");
        }
    }

    public class TimerUIPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(TimerPanel).GetMethod("SetTimerText", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPrefix]
        static void Prefix(ref TimerPanel __instance, ref TimeSpan timeSpan)
        {
            if (__instance is MainTimerPanel)
            {
                timeSpan = new TimeSpan(RaidTime.GetDateTime().Ticks);
            }
        }
    }

    public class EventExfilPatch : ModulePatch
    {
        public static bool IsLockdown = false;
        protected override MethodBase GetTargetMethod() => typeof(SharedExfiltrationPoint).GetMethod("HasMetRequirements", BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void Postfix(string profileId, ref bool __result)
        {
            if (IsLockdown && profileId == Singleton<GameWorld>.Instance.AllPlayers[0].ProfileId)
            {
                NotificationManagerClass.DisplayMessageNotification("Cannot extract during a lockdown!", ENotificationDurationType.Default, ENotificationIconType.Alert);
                __result = false;
            }
        }
    }

    public class WeatherControllerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(WeatherController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void Postfix(ref WeatherController __instance)
        {
            __instance.WindController.CloudWindMultiplier = 1;
            // weather controller is weird and makes the clouds REALLY fast if game time is set to local time
        }
    }

    public class WatchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(Watch).GetProperty("DateTime_0", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true);

        [PatchPostfix]
        static void Postfix(ref DateTime __result)
        {
            __result = RaidTime.GetDateTime();
        }
    }
}
