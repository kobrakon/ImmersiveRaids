using EFT;
using System;
using UnityEngine;
using System.Linq;
using EFT.Weather;
using Comfort.Common;
using EFT.Interactive;
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
            if (!Ready()) { timer = 0f; eventTimer = 0f; return; }
            timer += Time.deltaTime;
            eventTimer += Time.deltaTime;
            if (timer >= 3600f)
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
            if (Singleton<BackendConfigSettingsClass>.Instance != null && Singleton<BackendConfigSettingsClass>.Instance.Health != null && Singleton<BackendConfigSettingsClass>.Instance.Health.Effects != null && Singleton<BackendConfigSettingsClass>.Instance.Health.Effects.Existence != null)
            {
                Singleton<BackendConfigSettingsClass>.Instance.Health.Effects.Existence.EnergyDamage = 0.75f;
                Singleton<BackendConfigSettingsClass>.Instance.Health.Effects.Existence.HydrationDamage = 0.75f;
            }
        }

        async void QueueCleanup()
        {
            NotificationManagerClass.DisplayMessageNotification("Cleaning Gameworld in 30 seconds...", ENotificationDurationType.Long, ENotificationIconType.Alert);

            await Task.Delay(30000);

            // stinky for statement
            for (int i = 0; i < gameWorld.AllPlayers.Count; i++) 
            {
                if (gameWorld.AllPlayers[i] == null) break;
                if (!gameWorld.AllPlayers[i].HealthController.IsAlive)
                {
                    Destroy(gameWorld.AllPlayers[i]);
                }
            }

            if (player.Location != "Factory" && player.Location != "Laboratory")
            {
                gameWorld.gameObject.AddComponent<AirdropsManager>().isFlareDrop = true;
                NotificationManagerClass.DisplayMessageNotification("Incoming airdrop!", ENotificationDurationType.Long, ENotificationIconType.Default);
            }
        }

        void DoRandomEvent()
        {
            float rand = UnityEngine.Random.Range(0, 4);

            switch (rand)
            {
                case 0:
                    DoArmorRepair();
                break;
                case 1:
                    //DoNoBatteriesEvent();
                    goto case 2;
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
            }
        }
        /*/
        void DoNoBatteriesEvent()
        {

        }/**/

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

            Array.ForEach(FindObjectsOfType<Switch>(), (Switch pSwitch) => 
            {
                pSwitch.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, new object[0]);
                pSwitch.GetType().GetMethod("Lock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, new object[0]);
            });
            Array.ForEach(FindObjectsOfType<LampController>(), (LampController lamp) => { if (lamp.Enabled == false) { dontChangeOnEnd.Append(lamp); return; } lamp.Switch(Turnable.EState.Off); });
            Array.ForEach(FindObjectsOfType<KeycardDoor>(), (KeycardDoor door) =>
            {
                typeof(KeycardDoor).GetMethod("Lock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(door, new object[0]);
                keys.Add(door, (string[])typeof(KeycardDoor).GetField("_additionalKeys", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(door));
                AudioSource.PlayClipAtPoint(door.DeniedBeep, door.gameObject.transform.position);
            });

            NotificationManagerClass.DisplayMessageNotification("Blackout Event: All power switches and lights disabled for 10 minutes", ENotificationDurationType.Long, ENotificationIconType.Alert);

            await Task.Delay(600000);

            Array.ForEach(FindObjectsOfType<Switch>(), (Switch pSwitch) => 
            {
                pSwitch.GetType().GetMethod("Unlock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, new object[0]);
            });
            Array.ForEach(FindObjectsOfType<LampController>(), (LampController lamp) => { if (dontChangeOnEnd.Contains(lamp)) return; lamp.Switch(Turnable.EState.On); });

            foreach (KeyValuePair<KeycardDoor, string[]> entry in keys)
            {
                typeof(KeycardDoor).GetField("_additionalKeys", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entry.Key, new object[] { entry.Value });
            }
            keys.Clear();
            NotificationManagerClass.DisplayMessageNotification("Blackout Event over", ENotificationDurationType.Long, ENotificationIconType.Quest);
        }

        bool Ready() => gameWorld != null && gameWorld.AllPlayers != null && gameWorld.AllPlayers.Count > 0 && !(player is HideoutPlayer);

        Player player
        { get => gameWorld.AllPlayers[0]; }

        GameWorld gameWorld 
        { get => Singleton<GameWorld>.Instance; }
    }
}