using EFT;
using EFT.UI;
using JsonType;
using UnityEngine;
using System.Linq;
using Comfort.Common;
using UnityEngine.UI;
using EFT.Interactive;
using EFT.HealthSystem;
using System.Reflection;
using EFT.UI.Matchmaker;
using EFT.InventoryLogic;
using EFT.Communications;
using EFT.UI.BattleTimer;
using Aki.Custom.Airdrops;
using System.Threading.Tasks;

namespace ImmersiveRaids
{
    public class IRController : MonoBehaviour
    {
        float timer;
        float eventTimer;
        //float extractGearTimer;
        float timeToNextEvent = Random.Range(1800f, 3600f);
        bool exfilUIChanged = false;

        Player player
        { get => gameWorld.AllPlayers[0]; }

        GameWorld gameWorld 
        { get => Singleton<GameWorld>.Instance; }

        void Update()
        {
            RaidTime.inverted = MonoBehaviourSingleton<MenuUI>.Instance == null || MonoBehaviourSingleton<MenuUI>.Instance.MatchMakerSelectionLocationScreen == null
            ? RaidTime.inverted
            : !((EDateTime)typeof(MatchMakerSelectionLocationScreen).GetField("edateTime_0", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(MonoBehaviourSingleton<MenuUI>.Instance.MatchMakerSelectionLocationScreen) == EDateTime.CURR);

            if (!Ready()) 
            {
                timer = 0f;
                eventTimer = 0f;
                //extractGearTimer = 0f;
                return; 
            }

            timer += Time.deltaTime;
            if (Plugin.EnableEvents.Value) eventTimer += Time.deltaTime;
            //extractGearTimer += Time.deltaTime;

            if (timer >= 2700f)
            {
                QueueCleanup();
                timer = 0f;                                                                                                                                                                                                                    
            }
            if (eventTimer >= timeToNextEvent)
            {
                DoRandomEvent();
                eventTimer = 0f;
                timeToNextEvent = Random.Range(1800f, 3600f); // 30 min to hour
            }

            /*/
            if (extractGearTimer >= 3600f)
            {
                if (player.Location != "Factory" && player.Location != "Laboratory")
                    SendGearExtractCrate();
                extractGearTimer = 0f;
            }
            /**/

            if (EventExfilPatch.IsLockdown || EventExfilPatch.awaitDrop)
                if (!exfilUIChanged) 
                    ChangeExfilUI();
        }

        // moved from patch impacted performance too much
        async void ChangeExfilUI()
        {
            if (EventExfilPatch.IsLockdown || EventExfilPatch.awaitDrop)
            {
                Color red = new Color(0.8113f, 0.0376f, 0.0714f, 0.8627f);
                Color green = new Color(0.4863f, 0.7176f, 0.0157f, 0.8627f);
                RectTransform mainDescription = (RectTransform)typeof(ExtractionTimersPanel).GetField("_mainDescription", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(FindObjectOfType<ExtractionTimersPanel>());

                var text = mainDescription.gameObject.GetComponentInChildren<CustomTextMeshProUGUI>();
                var box = mainDescription.gameObject.GetComponentInChildren<Image>();

                text.text = EventExfilPatch.IsLockdown ? "Extraction unavailable" : EventExfilPatch.awaitDrop ? "Extracting gear - Exfils locked" : "Find an extraction point";
                box.color = red;

                foreach (ExitTimerPanel panel in FindObjectsOfType<ExitTimerPanel>())
                    panel.enabled = false;

                exfilUIChanged = true;

                while (EventExfilPatch.IsLockdown || EventExfilPatch.awaitDrop)
                    await Task.Yield();

                text.text = "Find an extraction point";
                box.color = green;

                foreach (ExitTimerPanel panel in FindObjectsOfType<ExitTimerPanel>())
                    panel.enabled = true;
                
                exfilUIChanged = false;
            }
        }

        async void QueueCleanup()
        {
            NotificationManagerClass.DisplayMessageNotification("Cleaning Gameworld in 30 seconds...", ENotificationDurationType.Long, ENotificationIconType.Alert);

            await Task.Delay(30000);

            if (Plugin.ActuallyCleanup.Value)
            {
                foreach (BotOwner bot in FindObjectsOfType<BotOwner>())
                {
                    if (!bot.HealthController.IsAlive && Vector3.Distance(player.Transform.position, bot.Transform.position) >= Plugin.DistToCleanup.Value) 
                        bot.gameObject.SetActive(false);
                }
            }

            if (player.Location != "Factory" && player.Location != "Laboratory")
            {
                gameWorld.gameObject.AddComponent<AirdropsManager>().isFlareDrop = true;
                NotificationManagerClass.DisplayMessageNotification("Incoming airdrop!", ENotificationDurationType.Long, ENotificationIconType.Default);
            }
        }

        void DoRandomEvent(bool skipFunny = false)
        {
            float rand = Random.Range(0, 5);

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
                //case 6:
                    //DoHuntedEvent();
                //break;
            }
        }

        /*/
        void DoHuntedEvent()
        {
            NotificationManagerClass.DisplayMessageNotification("Hunted Event: AI will now hunt you down for 10 minutes.", ENotificationDurationType.Long, ENotificationIconType.Alert);
        }
        /**/

        /*/
        void SendGearExtractCrate()
        {
            NotificationManagerClass.DisplayMessageNotification("An extraction airdrop is being deployed so you can secure your gear, you'll have 5 minutes after it's touched down to do so.", ENotificationDurationType.Long, ENotificationIconType.Default);
            AirdropBoxPatch.isExtractCrate = true;
            gameWorld.gameObject.AddComponent<AirdropsManager>().isFlareDrop = true;
        }
        /**/

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

            foreach (Switch pSwitch in FindObjectsOfType<Switch>())
            {
                typeof(Switch).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, null);
                typeof(Switch).GetMethod("Lock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, null);
            }

            foreach (LampController lamp in FindObjectsOfType<LampController>())
            {
                if (lamp.enabled == false) 
                { 
                    dontChangeOnEnd.Append(lamp); 
                    continue; 
                } 
                lamp.Switch(Turnable.EState.Off);
                lamp.enabled = false;
            }

            foreach (KeycardDoor door in FindObjectsOfType<KeycardDoor>())
            {
                typeof(KeycardDoor).GetMethod("Unlock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(door, null);
                AudioSource.PlayClipAtPoint(door.DeniedBeep, door.gameObject.transform.position);
            }

            NotificationManagerClass.DisplayMessageNotification("Blackout Event: All power switches and lights disabled for 10 minutes", ENotificationDurationType.Long, ENotificationIconType.Alert);

            await Task.Delay(600000);

            foreach(Switch pSwitch in FindObjectsOfType<Switch>())
            {
                typeof(Switch).GetMethod("Unlock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pSwitch, null);
            }

            foreach (LampController lamp in FindObjectsOfType<LampController>())
            {
                if (dontChangeOnEnd.Contains(lamp)) continue; 
                lamp.Switch(Turnable.EState.On);
                lamp.enabled = true;
            }

            foreach (KeycardDoor door in FindObjectsOfType<KeycardDoor>())
                await Task.Run(async () => 
                {
                    int timesToBeep = 3;
                    await Task.Delay(5000);

                    goto beep;

                    beep:
                        await Task.Delay(500);

                        if (timesToBeep == 0)
                            goto unlock;
                        
                        AudioSource.PlayClipAtPoint(door.DeniedBeep, door.gameObject.transform.position);
                        goto beep;

                    unlock:
                        typeof(KeycardDoor).GetMethod("Lock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(door, null);
                        AudioSource.PlayClipAtPoint(door.UnlockSound, door.gameObject.transform.position);
                        return;

                });

            NotificationManagerClass.DisplayMessageNotification("Blackout Event over", ENotificationDurationType.Long, ENotificationIconType.Quest);
        }

        public bool Ready() => gameWorld != null && gameWorld.AllPlayers != null && gameWorld.AllPlayers.Count > 0 && !(player is HideoutPlayer);
    }
}