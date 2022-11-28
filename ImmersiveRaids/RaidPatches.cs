using EFT;
using TMPro;
using System;
using JsonType;
using EFT.Weather;
using UnityEngine.UI;
using Comfort.Common;
using EFT.Interactive;
using EFT.UI.Matchmaker;
using System.Reflection;
using EFT.UI.BattleTimer;
using EFT.Communications;
using Aki.Reflection.Patching;
using System.Threading.Tasks;

namespace ImmersiveRaids
{
    public class GameWorldPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod("OnGameStarted", BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void PostFix(GameWorld __instance)
        {
            __instance.GameDateTime.Reset(DateTime.Now, DateTime.Now, 1);
        }
    }

    public class JSONTimePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(LocationWeatherTime).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.HasThis, new Type[] { typeof(WeatherClass), typeof(float), typeof(string), typeof(string) }, null);

        [PatchPrefix]
        static void PreFix(ref string date, ref string time)
        {
            date = DateTime.Now.ToShortDateString();
            time = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    public class RaidTimerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(AbstractGame).GetMethod("UpdateExfiltrationUi", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void PostFix(ref AbstractGame __instance)
        {
            __instance.GameTimer.ChangeSessionTime(new TimeSpan(99999999999999));
        }
    }

    public class UIPanelPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(LocationConditionsPanel).GetMethod("method_0", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void PostFix(ref Toggle ____amTimeToggle, ref TextMeshProUGUI ____currentPhaseTime, ref TextMeshProUGUI ____nextPhaseTime)
        {
            ____amTimeToggle.gameObject.SetActive(false);
            ____currentPhaseTime.enabled = false;
            ____nextPhaseTime.text = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    public class TimerUIPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(TimerPanel).GetMethod("SetTimerText", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPrefix]
        static void PreFix(ref TimerPanel __instance, ref TimeSpan timeSpan)
        {
            if (__instance is MainTimerPanel)
            {
                timeSpan = new TimeSpan(DateTime.Now.Ticks);
            }
        }
    }

    public class EventExfilPatch : ModulePatch
    {
        public static bool IsLockdown = false;
        protected override MethodBase GetTargetMethod() => typeof(SharedExfiltrationPoint).GetMethod("HasMetRequirements", BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void PostFix(string profileId, ref bool __result)
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
            Logger.LogInfo("SHIT SHOULDA RUN");
            __instance.WindController.CloudWindMultiplier = 1;
        }
    }
}