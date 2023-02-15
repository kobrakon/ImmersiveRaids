using EFT;
using TMPro;
using System;
using EFT.UI;
using JsonType;
using EFT.UI.Map;
using HarmonyLib;
using System.Linq;
using EFT.Weather;
using UnityEngine.UI;
using Comfort.Common;
using EFT.Interactive;
using Newtonsoft.Json;
using Aki.Common.Http;
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
using Aki.SinglePlayer.Models.Progression;

#pragma warning disable IDE0051
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
            Singleton<AbstractGame>.Instance.GameTimer.ChangeSessionTime(new TimeSpan(99999999999999)); // will literally make it so the game won't end for a couple months lel
        }
    }

    public class BotDiedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(BotControllerClass).GetMethod("BotDied", BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void Postfix(ref BotSpawnerClass ___gclass1415_0, int ___int_0) => ___gclass1415_0.SetMaxBots(___int_0++);
    }

    public class BotWaveLimitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(WaveInfo).GetConstructor(new Type[] { typeof(int), typeof(WildSpawnType), typeof(BotDifficulty) });

        [PatchPrefix]
        static void Prefix(ref int count) => count = 9999;
    }

    public class JSONTimePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(LocationWeatherTime).GetConstructor(new Type[] { typeof(WeatherClass), typeof(float), typeof(string), typeof(string) });

        [PatchPrefix]
        static void Prefix(ref string date, ref string time)
        {
            var raidTime = RaidTime.GetDateTime();
            date = raidTime.ToShortDateString();
            time = raidTime.ToString("HH:mm:ss");
        }
    }

    public class LocationTimePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(LocationSettingsClass.GClass1185).GetConstructor(new Type[0]);

        [PatchPostfix]
        static void Postfix(ref LocationSettingsClass.GClass1185 __instance) => __instance.exit_access_time = 9999999;
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
            globals.Health.Effects.Existence.EnergyDamage = 0.75f;
            globals.Health.Effects.Existence.HydrationDamage = 0.75f;
            globals.AllowSelectEntryPoint = true;
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
        static void Postfix(ref Toggle ____amTimeToggle, ref TextMeshProUGUI ____currentPhaseTime, ref TextMeshProUGUI ____nextPhaseTime)
        {
            var raidTime = RaidTime.GetDateTime();
            try
            {

                if (____amTimeToggle != null && ____currentPhaseTime != null)
                {
                    ____amTimeToggle.gameObject.SetActive(false);
                    ____currentPhaseTime.gameObject.SetActive(false);
                }
                ____nextPhaseTime.text = raidTime.ToString("HH:mm:ss");
            }
            catch (Exception) { }
            finally { ____currentPhaseTime.text = raidTime.ToString("HH:mm:ss"); }
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

    public class ExitTimerUIPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(MainTimerPanel).GetMethod("UpdateTimer", BindingFlags.Instance | BindingFlags.NonPublic);

        // transpile as I need to edit in a really specific way
        // basically I still need the call base.UpdateTimer() and return afterward
        // Harmony has no injection for base and (__instance as TimerPanel) wont work as it will
        // call it on the MainTimerPanel instance anyway and since I have that patched it'll
        // cause an invocation loop and overload the stack
        [PatchTranspiler]
        static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            instructions.ExecuteForEach(inst =>
            {
                if (instructions.IndexOf(inst) > 1)
                    inst.opcode = OpCodes.Ret;
            });

            /*/
            output:
                IL_0000: ldarg.0
                IL_0001: call      instance void EFT.UI.BattleTimer.TimerPanel::UpdateTimer()
	            IL_0006: ret

                then a bunch of other ret codes cause the CLR will piss itself if you try to leave the IL without it
            /**/

            return instructions;
        }
    }

    public class EventExfilPatch : ModulePatch
    {
        internal static bool IsLockdown = false;
        internal static bool awaitDrop = false;

        protected override MethodBase GetTargetMethod() => typeof(SharedExfiltrationPoint).GetMethod(nameof(SharedExfiltrationPoint.HasMetRequirements), BindingFlags.Instance | BindingFlags.Public);

        [PatchPostfix]
        static void Postfix(string profileId, ref bool __result)
        {
            if (profileId == Singleton<GameWorld>.Instance.AllPlayers[0].ProfileId)
            {
                __result = IsLockdown || awaitDrop ? false : __result;
                if (IsLockdown) NotificationManagerClass.DisplayMessageNotification("Cannot extract during a lockdown", ENotificationDurationType.Long, ENotificationIconType.Alert);
                if (awaitDrop) NotificationManagerClass.DisplayMessageNotification("Your gear hasn't been extracted yet", ENotificationDurationType.Long, ENotificationIconType.Alert);
            }
        }
    }

    public class WeatherControllerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(WeatherController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void Postfix(ref WeatherController __instance) => __instance.WindController.CloudWindMultiplier = 1;
        // weather controller is weird and makes the clouds REALLY fast if game time is set to local time
    }

    // WIP!!!! NOT DONE!!!          
    /*/public class AirdropBoxPatch : ModulePatch
    {
        internal static bool isExtractCrate = false;
        internal static List<Item> gearToExtract = new List<Item>();

        protected override MethodBase GetTargetMethod() => typeof(AirdropsManager).GetMethod("BuildLootContainer", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPrefix]
        static bool Prefix(ref ItemFactoryUtil ___factory, ref AirdropParametersModel ___airdropParameters, AirdropBox ___airdropBox)
        {
            if (!isExtractCrate) return true;
            ___factory.BuildContainer(___airdropBox.container);
            ___airdropParameters.containerBuilt = true;
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

            while (!param.parachuteComplete)
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
                StashPatch.gearToSend.Add(item); 
            });

            gearToExtract.Clear();
            EventExfilPatch.awaitDrop = false;
        }
    }

    public class StashPatch : ModulePatch
    {
        internal static object[] data = new object[0];
        internal static List<Item> gearToSend = new List<Item>();

        internal static JsonConverter[] converters
        { 
            get => typeof(OfflineSaveProfilePatch)
               .GetProperty("_defaultJsonConverters", BindingFlags.Static | BindingFlags.NonPublic)
               .GetValue(null); // i'll be takin that
        }

        protected override MethodBase GetTargetMethod() => typeof(TarkovApplication).GetMethod("method_43", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static async void Postfix()
        {
            if (!gearToSend.Any() || screen != EMenuType.MainMenu || !turnOn) return;

            if (Plugin.Script.Ready() || Singleton<SessionResultPanel>.Instantiated) return;

            ISession session = UnityEngine.Object.FindObjectOfType<TarkovApplication>().GetClientBackEndSession();
            Profile profile = session.Profile;

            while (profile.Inventory.Stash == null)
                await Task.Yield();

            foreach (Item i in gearToSend)
            {
                profile.Inventory.Stash.Grid.Add(i);
            }

            UpdateProfileRequest req = new UpdateProfileRequest
            {
                player = profile
            };

            RequestHandler.PutJson("/ir/profile/update", req.ToJson(converters.AddItem(new NotesJsonConverter()).ToArray()));

            gearToSend.Clear();

            Logger.LogInfo("IR : Added extract gear to stash");
        }


        struct UpdateProfileRequest
        {
            [JsonProperty("profile")] // go figure
            Profile player;
        }
    }

    public class DataPassthroughPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(TarkovApplication).GetMethod("method_43", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void Postfix(RaidSettings ____raidSettings, Result<ExitStatus, TimeSpan, ClientMetrics> result)
        {
            if (StashPatch.gearToSend.Any()) StashPatch.data = new object[] { ____raidSettings, result };
        }
    }/**/

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