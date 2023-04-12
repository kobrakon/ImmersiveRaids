using EFT;
using TMPro;
using System;
using EFT.UI;
using JsonType;
using EFT.UI.Map;
using HarmonyLib;
using System.Linq;
using EFT.Weather;
using UnityEngine;
using UnityEngine.UI;
using Comfort.Common;
using EFT.Interactive;
using Newtonsoft.Json;
using Aki.Common.Http;
using Aki.Common.Utils;
using EFT.UI.Matchmaker;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.BattleTimer;
using EFT.Communications;
using Aki.Custom.Airdrops;
using System.Threading.Tasks;
using System.Reflection.Emit;
using Aki.Reflection.Patching;
using Aki.Custom.Airdrops.Utils;
using System.Collections.Generic;
using Aki.Custom.Airdrops.Models;
using Aki.SinglePlayer.Utils.Progression;
using Aki.SinglePlayer.Patches.Progression;

#pragma warning disable IDE0051
namespace ImmersiveRaids
{
    public struct RaidTime
    {
        internal static bool inverted = false;
        private static DateTime mLastTime;
        private static DateTime lastTime
        { 
            get
            {
                if (mLastTime.Hour == DateTime.Now.AddHours(12).Hour)
                {
                    return mLastTime = inverseTime;
                } else return mLastTime = DateTime.Now;
            }
            set => mLastTime = value;
        }
        private static DateTime inverseTime
        {
            get
            {
                DateTime result = DateTime.Now.AddHours(12);
                return result.Day > DateTime.Now.Day 
                       ? result.AddDays(-1) 
                       : result.Day < DateTime.Now.Day 
                       ? result.AddDays(1) : result;
            }
        }

        public static DateTime GetCurrTime() => lastTime = DateTime.Now;
        public static DateTime GetInverseTime() => lastTime = inverseTime;
        public static DateTime GetDateTime() => inverted ? GetInverseTime() : GetCurrTime();
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
        static void Postfix(ref BotControllerClass __instance, int ___int_0) => __instance.BotSpawner.SetMaxBots(___int_0++);
    }

    public class GlobalsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(TarkovApplication).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static async void Postfix(TarkovApplication __instance)
        {
            while (__instance.GetClientBackEndSession() == null || __instance.GetClientBackEndSession().BackEndConfig == null)
                await Task.Yield();

            BackendConfigSettingsClass globals = __instance.GetClientBackEndSession().BackEndConfig.Config;
            globals.AllowSelectEntryPoint = true; // not on server L
        }
    }

    public class EnableEntryPointPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(EntryPointView).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);

        [PatchPrefix]
        static void Prefix(ref bool allowSelection) => allowSelection = true;
    }

    public class UIPanelPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(LocationConditionsPanel).GetMethod("method_0", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void Postfix(ref TextMeshProUGUI ____currentPhaseTime, ref TextMeshProUGUI ____nextPhaseTime)
        {
            try
            {
                ____nextPhaseTime.text = RaidTime.GetInverseTime().ToString("HH:mm:ss");
            }
            catch (Exception) { }
            finally { ____currentPhaseTime.text = RaidTime.GetCurrTime().ToString("HH:mm:ss"); }
        }
    }

    public class TimerUIPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(MainTimerPanel).GetMethod("SetTimerText", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPrefix]
        static void Prefix(ref MainTimerPanel __instance, ref TimeSpan timeSpan)
        {
            timeSpan = new TimeSpan(RaidTime.GetDateTime().Ticks);

            RectTransform mainDescription = (RectTransform)typeof(ExtractionTimersPanel).GetField("_mainDescription", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(UnityEngine.Object.FindObjectOfType<ExtractionTimersPanel>());

            var text = mainDescription.gameObject.GetComponentInChildren<CustomTextMeshProUGUI>();
            var box = mainDescription.gameObject.GetComponentInChildren<Image>();

            text.text = EventExfilPatch.IsLockdown ? "Extraction unavailable" : EventExfilPatch.awaitDrop ? "Extracting gear - Exfils locked" : "Find an extraction point";
            box.color = EventExfilPatch.IsLockdown || EventExfilPatch.awaitDrop ? new Color(0.8113f, 0.0376f, 0.0714f, 0.8627f) : new Color(0.4863f, 0.7176f, 0.0157f, 0.8627f);
        }
    }

    /*/ not needed cause server plugin
    public class ExitTimerUIPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(MainTimerPanel).GetMethod("UpdateTimer", BindingFlags.Instance | BindingFlags.NonPublic);

        // transpile as I need to edit in a really specific way
        // basically I still need the call base.UpdateTimer() and return afterward
        // Harmony has no injection for base and (__instance as TimerPanel) wont work as it will
        // call it on the MainTimerPanel instance anyway and since I have that patched it'll
        // cause an invocation loop and overload the stack
        [PatchTranspiler]
        static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            instructions.ExecuteForEach(inst =>
            {
                if (instructions.IndexOf(inst) > 1)
                    inst.opcode = OpCodes.Ret;
            });

            //
            output:
                IL_0000: ldarg.0
                IL_0001: call      instance void EFT.UI.BattleTimer.TimerPanel::UpdateTimer()
	            IL_0006: ret

                then a bunch of other ret codes cause the CLR will piss itself if you try to leave the IL without it
            //

            return instructions;
        }
    }/**/

    public class EventExfilPatch : ModulePatch
    {
        internal static bool IsLockdown = false;
        internal static bool awaitDrop = false;

        protected override MethodBase GetTargetMethod() => typeof(ExfiltrationRequirement).GetMethod("Met", BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void Postfix(Player player, ref bool __result)
        {
            if (player.IsYourPlayer)
            {
                if (IsLockdown || awaitDrop)
                {
                    NotificationManagerClass.DisplayMessageNotification(IsLockdown ? "Cannot extract during a lockdown" : "Your gear hasn't been extracted yet", ENotificationDurationType.Long, ENotificationIconType.Alert);
                    __result = false;
                }
                __result = true;
            }
            __result = true;
        }
    }

    // bleehhh
    public class EventExfilTipPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(ExfiltrationRequirement).GetMethod("GetLocalizedTip", BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void Postfix(ref string __result) => 
            __result = EventExfilPatch.IsLockdown ? "Cannot extract during a lockdown" : EventExfilPatch.awaitDrop ? "Your gear hasn't been extracted yet" : __result;
    }

    public class WeatherControllerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(WeatherController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void Postfix(ref WeatherController __instance) => __instance.WindController.CloudWindMultiplier = 1;
        // weather controller is weird and makes the clouds REALLY fast if game time is set to local time
    }

    // WIP!!!! NOT DONE!!!
    public class AirdropBoxPatch : ModulePatch
    {
        internal static bool isExtractCrate = false;
        internal static List<Item> gearToExtract = new List<Item>();
        internal static List<Item> gearToSend = new List<Item>();

        protected override MethodBase GetTargetMethod() => typeof(AirdropsManager).GetMethod("BuildLootContainer", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPrefix]
        static bool Prefix(ref ItemFactoryUtil ___factory, ref AirdropParametersModel ___airdropParameters, AirdropBox ___airdropBox)
        {
            if (!isExtractCrate) return true;
            ___factory.BuildContainer(___airdropBox.container);
            ___airdropParameters.AirdropAvailable = true;
            return false;
        }

        [PatchPostfix]
        static void Postfix(ref AirdropBox ___airdropBox, ref AirdropParametersModel ___airdropParameters)
        {
            if (!isExtractCrate) return;
            ___airdropBox.container.ItemOwner.MainStorage[0].RemoveAll(); // clear objects so crate empty
            AwaitThenGetBox(___airdropParameters, ___airdropBox.container);
        }

        static async void AwaitThenGetBox(AirdropParametersModel param, LootableContainer box)
        {
            if (!isExtractCrate) return;
            isExtractCrate = false;

            while (Vector3.Distance(box.transform.position, param.RandomAirdropPoint) > 3f)
            {
                await Task.Yield();
            }

            NotificationManagerClass.DisplayMessageNotification($"The extract crate has landed, secure your loot while you can", ENotificationDurationType.Long, ENotificationIconType.Default);

            EventExfilPatch.awaitDrop = true;

            await Task.Delay(300000);

            QueueLootSend(box);
        }

        static void QueueLootSend(LootableContainer instance)
        {
            NotificationManagerClass.DisplayMessageNotification("The extract crate is locked, and any gear within it is now secured and will be returned to your stash at the end of the raid, if possible.", ENotificationDurationType.Long, ENotificationIconType.Default);

            instance.ItemOwner.MainStorage[0].ContainedItems.ExecuteForEach(item => gearToExtract.Add(item.Key));
            typeof(LootableContainer).GetMethod("Lock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);

            gearToExtract.ExecuteForEach(item => 
            { 
                item.CurrentAddress = null; // fuck yourself 
                gearToSend.Add(item); 
            });

            gearToExtract.Clear();
            EventExfilPatch.awaitDrop = false;
            SendGear();
        }

        static void SendGear()
        {
            if (!gearToSend.Any())
                return;

            if (Plugin.Script.Ready() || Singleton<SessionResultPanel>.Instantiated)
                return;

            ISession session = UnityEngine.Object.FindObjectOfType<TarkovApplication>().GetClientBackEndSession();
            Profile profile = session.Profile;

            /*/while (profile.Inventory.Stash == null)
                await Task.Yield();

            foreach (Item i in gearToSend)
            {
                profile.Inventory.Stash.Grid.Add(i);
            }/**/

            UpdateProfileRequest req = new UpdateProfileRequest
            {
                player = profile
            };

            RequestHandler.PutJson("/ir/profile/update", Json.Serialize(req));

            gearToSend.Clear();

            Logger.LogInfo("IR : Added extract gear to stash");
        }

        struct UpdateProfileRequest
        {
            [JsonProperty("profile")] // go figure
            internal Profile player;
        }
    }

    public class FactoryTimePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(TarkovApplication).GetMethod("InternalStartGame", BindingFlags.Instance | BindingFlags.Public);

        [PatchPrefix]
        static void Prefix(ref string gameMap)
        {
            if (gameMap.Contains("factory")) gameMap = RaidTime.GetDateTime().Hour >= 22 || RaidTime.GetDateTime().Hour < 6 ? "factory4_night" : "factory4_day";
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