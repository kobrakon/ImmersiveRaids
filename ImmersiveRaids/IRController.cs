using EFT;
using System;
using UnityEngine;
using System.Linq;
using Comfort.Common;
using EFT.Interactive;
using EFT.HealthSystem;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.Communications;
using Aki.Custom.Airdrops;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ImmersiveRaids
{
    public class IRController : MonoBehaviour
    {
        float timer { get; set; }
        float eventTimer { get; set; }
        float timeToNextEvent = UnityEngine.Random.Range(1800f, 3600f);

        void Update()
        {
            if (!Ready()) 
            {
                timer = 0f;
                eventTimer = 0f;
                return; 
            }

            timer += Time.deltaTime;
            if (Plugin.EnableEvents.Value) eventTimer += Time.deltaTime;

            if (timer >= 2700f)
            {
                QueueCleanup();
                timer = 0f;                                                                                                                                                                                                                    
            }
            if (eventTimer >= timeToNextEvent)
            {
                DoRandomEvent();
                eventTimer = 0f;
                timeToNextEvent = UnityEngine.Random.Range(1800f, 3600f); // 30 min to hour
            }
        }

        async void QueueCleanup()
        {
            NotificationManagerClass.DisplayMessageNotification("Cleaning Gameworld in 30 seconds...", ENotificationDurationType.Long, ENotificationIconType.Alert);

            await Task.Delay(30000);

            foreach (BotOwner bot in FindObjectsOfType<BotOwner>())
            {
                if (!bot.HealthController.IsAlive) bot.gameObject.SetActive(false);
            }

            if (player.Location != "Factory" && player.Location != "Laboratory")
            {
                gameWorld.gameObject.AddComponent<AirdropsManager>().isFlareDrop = true;
                NotificationManagerClass.DisplayMessageNotification("Incoming airdrop!", ENotificationDurationType.Long, ENotificationIconType.Default);
            }
        }

        void DoRandomEvent(bool skipFunny = false)
        {
            float rand = UnityEngine.Random.Range(0, 5);

            switch (rand)
            {
                case 0:
                    DoArmorRepair();
                break;
                case 1:
                    if (skipFunny) DoRandomEvent();
                    DoFunny();
                break;
                case 2:
                    DoBlackoutEvent();
                break;
                case 3:
                    if (player.Location == "Factory" || player.Location == "Laboratory") DoRandomEvent();
                    DoAirdropEvent();
                break;
                case 4:
                    DoLockDownEvent();
                break;
                case 5:
                    ValueStruct health = player.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common);
                    if (health.Current != health.Maximum)
                    {
                        DoHealPlayer();
                        break;
                    } else DoRandomEvent();
                break;
            }
        }

        void DoHealPlayer()
        {
            NotificationManagerClass.DisplayMessageNotification("Heal Event: On your feet you ain't dead yet.");
            player.ActiveHealthController.RestoreFullHealth();
        }

        void DoArmorRepair()
        {
            NotificationManagerClass.DisplayMessageNotification("Armor Repair Event: All equipped armor repaired... nice!", ENotificationDurationType.Long, ENotificationIconType.Default);
            player.Profile.Inventory.GetAllEquipmentItems().ExecuteForEach((Item item) => 
            {
                if (item.GetItemComponent<ArmorComponent>() != null) item.GetItemComponent<RepairableComponent>().Durability = item.GetItemComponent<RepairableComponent>().MaxDurability;
            });
        }

        void DoAirdropEvent()
        {
            gameWorld.gameObject.AddComponent<AirdropsManager>().isFlareDrop = true;
            NotificationManagerClass.DisplayMessageNotification("Aidrop Event: Incoming Airdrop!", ENotificationDurationType.Long, ENotificationIconType.Default);
        }

        async void DoFunny()
        {
            NotificationManagerClass.DisplayMessageNotification("Heart Attack Event: Nice knowing ya, you've got 10 seconds", ENotificationDurationType.Long, ENotificationIconType.Alert);
            await Task.Delay(10000);
            NotificationManagerClass.DisplayMessageNotification("jk", ENotificationDurationType.Long, ENotificationIconType.Default);
            await Task.Delay(2000); DoRandomEvent(true);
        }

        async void DoLockDownEvent()
        {
            NotificationManagerClass.DisplayMessageNotification("Lockdown Event: All extracts are unavaliable for 15 minutes", ENotificationDurationType.Long, ENotificationIconType.Alert);
            EventExfilPatch.IsLockdown = true;
            await Task.Delay(900000);
            EventExfilPatch.IsLockdown = false;
            NotificationManagerClass.DisplayMessageNotification("Lockdown Event over", ENotificationDurationType.Long, ENotificationIconType.Quest);
        }

        async void DoBlackoutEvent()
        {
            LampController[] dontChangeOnEnd = new LampController[0];
            Dictionary<KeycardDoor, string[]> keys = new Dictionary<KeycardDoor, string[]>();

            foreach (Switch pSwitch in FindObjectsOfType<Switch>())
            {
                typeof(Switch).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, new object[0]);
                typeof(Switch).GetMethod("Lock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, new object[0]);
            }

            foreach (LampController lamp in FindObjectsOfType<LampController>())
            {
                if (lamp.Enabled == false) 
                { 
                    dontChangeOnEnd.Append(lamp); 
                    continue; 
                } 
                lamp.Switch(Turnable.EState.Off);
                lamp.gameObject.GetComponentInChildren<Light>().enabled = false;
            }

            foreach (KeycardDoor door in FindObjectsOfType<KeycardDoor>())
            {
                typeof(KeycardDoor).GetMethod("Lock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(door, new object[0]);
                keys.Add(door, (string[])typeof(KeycardDoor).GetField("_additionalKeys", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(door));
                AudioSource.PlayClipAtPoint(door.DeniedBeep, door.gameObject.transform.position);
            }

            NotificationManagerClass.DisplayMessageNotification("Blackout Event: All power switches and lights disabled for 10 minutes", ENotificationDurationType.Long, ENotificationIconType.Alert);

            await Task.Delay(600000);

            foreach(Switch pSwitch in FindObjectsOfType<Switch>())
            {
                typeof(Switch).GetMethod("Unlock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, new object[0]);
            }

            foreach (LampController lamp in FindObjectsOfType<LampController>())
            {
                if (dontChangeOnEnd.Contains(lamp)) return; 
                lamp.Switch(Turnable.EState.On);
                lamp.gameObject.GetComponentInChildren<Light>().enabled = true;
            }

            foreach (KeyValuePair<KeycardDoor, string[]> entry in keys)
            {
                typeof(KeycardDoor).GetField("_additionalKeys", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entry.Key, new object[] { entry.Value });
            }
            keys.Clear();
            NotificationManagerClass.DisplayMessageNotification("Blackout Event over", ENotificationDurationType.Long, ENotificationIconType.Quest);
        }   

        public bool Ready() => gameWorld != null && gameWorld.AllPlayers != null && gameWorld.AllPlayers.Count > 0 && !(player is HideoutPlayer);

        Player player
        { get => gameWorld.AllPlayers[0]; }

        GameWorld gameWorld 
        { get => Singleton<GameWorld>.Instance; }
    }
}
