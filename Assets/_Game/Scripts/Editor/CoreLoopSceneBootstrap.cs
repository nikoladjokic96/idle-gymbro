using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Gameplay;
using IdleGymBro.Economy;
using IdleGymBro.Character;
using IdleGymBro.UI;
using IdleGymBro.Monetization;
using IdleGymBro.Progression;
using Object = UnityEngine.Object;

namespace IdleGymBro.EditorTools
{
    // Builds the playable core-loop scene entirely from code so the wiring between
    // systems, config, and HUD is reproducible without manual drag-and-drop in the
    // Inspector (and can be re-run headlessly via -executeMethod in CI).
    public static class CoreLoopSceneBootstrap
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
        private const string RootName = "CoreLoop";
        private const string CharacterArtFolder = "Assets/_Game/Art/Character/Placeholders";

        [MenuItem("IdleGymBro/Build Core Loop Scene")]
        public static void BuildCoreLoopScene()
        {
            // Import any on-disk assets that aren't yet in the AssetDatabase, so an
            // existing GameConfig.asset resolves to a real, referenceable asset.
            AssetDatabase.Refresh();

            // Must run before any MuscleTierData/CosmeticData assets are created below, since
            // those assets reference sprites this generates.
            PlaceholderArtGenerator.Generate();
            PlaceholderSfxGenerator.Generate();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Idempotency: destroy any previous build's root so re-running this method
            // never duplicates systems/UI in the scene.
            var old = GameObject.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            // Load the config AFTER OpenScene: opening a scene in Single mode invalidates
            // object references obtained beforehand, which nulled _gameConfig on assign.
            GameConfig config = GetOrCreateConfig();
            if (config == null)
            {
                Debug.LogError("[CoreLoopSceneBootstrap] Aborting: GameConfig asset could not be created/loaded.");
                return;
            }

            // Upgrades = muscle groups trained (§5 gym meme identity); consumables live as
            // boosters instead (see BoosterData below). Tune values in the .asset inspectors later.
            var upgrades = new UpgradeData[]
            {
                GetOrCreateUpgrade("chest", "Chest Day", StatType.GainsPerRep, 1d, 10d, 1.10f),
                GetOrCreateUpgrade("arms", "Arm Blaster", StatType.GainsPerRep, 2d, 60d, 1.11f),
                GetOrCreateUpgrade("back", "Back Attack", StatType.GainsPerRep, 5d, 350d, 1.12f),
                GetOrCreateUpgrade("abs", "Core Crusher", StatType.GainsPerRep, 8d, 900d, 1.125f),
                GetOrCreateUpgrade("legs", "Never Skip Leg Day", StatType.GainsPerRep, 12d, 2000d, 1.13f),
                GetOrCreateUpgrade("training_partner", "Training Partner", StatType.PassiveGainsPerSecond, 0.5d, 50d, 1.11f),
                GetOrCreateUpgrade("gym_membership", "Gym Membership", StatType.PassiveGainsPerSecond, 3d, 500d, 1.12f),
            };

            // Boosters (opt-in, rewarded-ad-flavored per §10; consumable-style temporary buffs).
            var boosters = new BoosterData[]
            {
                GetOrCreateBooster("preworkout", "Pre-Workout", 0 /* BoosterTarget.TapIncome */, 2f, 60f, 180f, true),
                GetOrCreateBooster("protein_shake", "Protein Shake", 1 /* BoosterTarget.PassiveIncome */, 2f, 60f, 180f, true),
            };

            // Locations (§9 story progression). Progress = total upgrade levels owned (summed
            // across ALL upgrades, order-independent) vs each location's cumulative target —
            // same pattern as muscle-tier thresholds. Ordered by TotalLevelsToComplete ascending.
            var locations = new LocationData[]
            {
                GetOrCreateLocation("home", "Home Workout", 25, 1f),
                GetOrCreateLocation("street", "Street Workout", 75, 2f),
                GetOrCreateLocation("basic_gym", "Basic Gym", 160, 5f),
                GetOrCreateLocation("hardcore_gym", "Hardcore Gym", 300, 12f),
                GetOrCreateLocation("beach", "Venice Beach", 500, 30f),
                GetOrCreateLocation("olympia", "Mr. Olympia", 800, 75f),
            };

            // Muscle tiers (data-driven; thresholds are lifetime TotalEarned, not balance).
            var tiers = new MuscleTierData[]
            {
                GetOrCreateTier("tier1_skinny", 1, "Skinny", 0d, $"{CharacterArtFolder}/body_tier1.png", $"{CharacterArtFolder}/head_01.png"),
                GetOrCreateTier("tier2_slim_fit", 2, "Slim Fit", 1000d, $"{CharacterArtFolder}/body_tier2.png", $"{CharacterArtFolder}/head_01.png"),
                GetOrCreateTier("tier3_fit", 3, "Fit", 25000d, $"{CharacterArtFolder}/body_tier3.png", $"{CharacterArtFolder}/head_01.png"),
                GetOrCreateTier("tier4_jacked", 4, "Jacked", 500000d, $"{CharacterArtFolder}/body_tier4.png", $"{CharacterArtFolder}/head_01.png"),
                GetOrCreateTier("tier5_mass_monster", 5, "Mass Monster", 10000000d, $"{CharacterArtFolder}/body_tier5.png", $"{CharacterArtFolder}/head_01.png"),
                GetOrCreateTier("tier6_enhanced", 6, "Enhanced", 500000000d, $"{CharacterArtFolder}/body_tier6.png", $"{CharacterArtFolder}/head_01.png"),
            };

            // Default cosmetics (free, unlocked from the start; wardrobe/shop is post-MVP).
            var cosmetics = new CosmeticData[]
            {
                GetOrCreateCosmetic("shorts_01", "Shorts", CharacterLayer.Shorts, $"{CharacterArtFolder}/shorts_01.png", 0d),
                GetOrCreateCosmetic("hair_01", "Hair", CharacterLayer.Hair, $"{CharacterArtFolder}/hair_01.png", 0d),
                GetOrCreateCosmetic("beard_01", "Beard", CharacterLayer.Beard, $"{CharacterArtFolder}/beard_01.png", 0d),
            };

            // Audio library referencing the placeholder clips generated above (data-driven per §16:
            // no clip paths live in AudioManager itself).
            AudioLibrary audioLibrary = GetOrCreateAudioLibrary();

            var root = new GameObject(RootName);

            // --- Systems ---
            var gameSystems = new GameObject("GameSystems");
            gameSystems.transform.SetParent(root.transform, false);

            var gameManager = gameSystems.AddComponent<GameManager>();
            var tickSystem = gameSystems.AddComponent<TickSystem>();
            var energySystem = gameSystems.AddComponent<EnergySystem>();
            var currencyManager = gameSystems.AddComponent<CurrencyManager>();
            var tapController = gameSystems.AddComponent<TapController>();
            var saveSystem = gameSystems.AddComponent<SaveSystem>();
            var passiveIncome = gameSystems.AddComponent<PassiveIncomeSystem>();
            var offlineEarnings = gameSystems.AddComponent<OfflineEarningsSystem>();
            var upgradeManager = gameSystems.AddComponent<UpgradeManager>();
            var boosterManager = gameSystems.AddComponent<BoosterManager>();
            // Story progression (§9): no _gameConfig field (drives purely off UpgradeManager.TotalLevels
            // via events), so — like BoosterManager/AudioManager/AdManager — excluded from the self-check below.
            var locationManager = gameSystems.AddComponent<LocationManager>();
            var audioSource = gameSystems.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            var audioManager = gameSystems.AddComponent<AudioManager>();
            // MOCK rewarded-ad provider (§10 opt-in monetization). No _gameConfig field, so —
            // like BoosterManager/AudioManager — it's excluded from the self-check below.
            var adManager = gameSystems.AddComponent<AdManager>();

            AssignRef(gameManager, "_gameConfig", config);
            AssignRef(tickSystem, "_gameConfig", config);
            AssignRef(energySystem, "_gameConfig", config);
            AssignRef(currencyManager, "_gameConfig", config);
            AssignRef(tapController, "_gameConfig", config);
            AssignRef(saveSystem, "_gameConfig", config);
            AssignRef(passiveIncome, "_gameConfig", config);
            AssignRef(offlineEarnings, "_gameConfig", config);
            AssignRef(upgradeManager, "_gameConfig", config);
            AssignArray(upgradeManager, "_upgrades", upgrades);
            AssignArray(boosterManager, "_boosters", boosters);
            AssignArray(locationManager, "_locations", locations);
            AssignRef(audioManager, "_library", audioLibrary);
            AssignRef(audioManager, "_source", audioSource);

            // Self-check: verify the asset reference actually serialized (asset refs are
            // more timing-sensitive in batchmode than scene-object refs). BoosterManager,
            // AudioManager, AdManager, and LocationManager have no _gameConfig field, so
            // they're intentionally excluded from this check.
            var systems = new Component[] { gameManager, tickSystem, energySystem, currencyManager, tapController, saveSystem, passiveIncome, offlineEarnings, upgradeManager };
            int wired = 0;
            foreach (var s in systems)
            {
                var check = new SerializedObject(s).FindProperty("_gameConfig");
                if (check != null && check.objectReferenceValue != null)
                {
                    wired++;
                }
            }
            Debug.Log($"[CoreLoopSceneBootstrap] _gameConfig wired on {wired}/{systems.Length} systems.");

            // --- Canvas ---
            var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root.transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            // Expand: pick the smaller scale so the full 1080x1920 design space always fits the
            // screen (portrait phone or landscape editor Game view) — modals can never overflow.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // EventSystem is REQUIRED for UI buttons (Upgrades / offline claim / close) to be
            // clickable. Uses the new Input System UI module with its default UI action maps.
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemGo.transform.SetParent(root.transform, false);
            var uiInputModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            uiInputModule.AssignDefaultActions();

            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // --- World-space character ---
            // Not UI: a SpriteRenderer layer stack positioned in front of the Main Camera, drawn
            // beneath the ScreenSpaceOverlay HUD canvas.
            var characterGo = new GameObject("Character");
            characterGo.transform.SetParent(root.transform, false);
            characterGo.transform.position = new Vector3(0f, -2.4f, 0f);
            characterGo.transform.localScale = new Vector3(3f, 3f, 1f);
            var builder = characterGo.AddComponent<CharacterBuilder>();
            characterGo.AddComponent<PlaceholderCharacter>();
            AssignArray(builder, "_tiers", tiers);
            AssignArray(builder, "_defaultCosmetics", cosmetics);

            // --- Gains text ---
            var gainsText = CreateText("GainsText", canvasGo.transform, "0", 80f, TextAlignmentOptions.Center);
            SetRect(gainsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(700f, 110f));
            var gainsCounterJuice = gainsText.gameObject.AddComponent<GainsCounterJuice>();
            AssignRef(gainsCounterJuice, "_target", gainsText.rectTransform);

            // --- Passive income rate ---
            var passiveRateText = CreateText("PassiveRateText", canvasGo.transform, "0/s", 40f, TextAlignmentOptions.Center);
            SetRect(passiveRateText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(700f, 50f));

            // --- Energy bar ---
            var energyBarBg = CreateImage("EnergyBarBG", canvasGo.transform, uiSprite, new Color(0.15f, 0.15f, 0.15f));
            SetRect(energyBarBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(700f, 60f));

            var energyBar = CreateImage("EnergyBar", energyBarBg.transform, uiSprite, new Color(0.20f, 0.80f, 0.35f));
            energyBar.type = Image.Type.Filled;
            energyBar.fillMethod = Image.FillMethod.Horizontal;
            energyBar.fillAmount = 1f;
            SetRect(energyBar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 60f));

            // Created after EnergyBar so the label draws on top of the fill.
            var energyText = CreateText("EnergyText", energyBarBg.transform, "100/100", 34f, TextAlignmentOptions.Center);
            SetRect(energyText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 60f));

            // EnergyBarSmoother is the sole writer of the fill's fillAmount (lerps toward the
            // latest EnergyChangedEvent); HudController no longer holds _energyFill below.
            var energyBarSmoother = energyBar.gameObject.AddComponent<EnergyBarSmoother>();
            AssignRef(energyBarSmoother, "_fill", energyBar);

            // --- Floating "+X" tap texts ---
            var floatingTextsGo = new GameObject("FloatingTexts", typeof(RectTransform));
            floatingTextsGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(floatingTextsGo.GetComponent<RectTransform>());
            var floatingTextSpawner = floatingTextsGo.AddComponent<FloatingTextSpawner>();
            AssignRef(floatingTextSpawner, "_spawnArea", floatingTextsGo.GetComponent<RectTransform>());

            // --- HUD controller ---
            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(root.transform, false);
            var hudController = hudGo.AddComponent<HudController>();

            AssignRef(hudController, "_gainsText", gainsText);
            AssignRef(hudController, "_energyText", energyText);
            AssignRef(hudController, "_passiveRateText", passiveRateText);

            // --- Tier-up banner: lives on the always-active HUDCanvas object since it only
            // deactivates its own text object, never itself. ---
            var tierUpText = CreateText("TierUpText", canvasGo.transform, string.Empty, 64f, TextAlignmentOptions.Center);
            SetRect(tierUpText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -460f), new Vector2(800f, 140f));
            var tierUpBanner = canvasGo.AddComponent<TierUpBanner>();
            AssignRef(tierUpBanner, "_text", tierUpText);

            // --- Upgrades: "UPGRADES" open button on the HUD + a modal with the upgrade buttons ---
            // Edges = buttons (docs/ui-layout.md): UPGRADES lives on the right-middle edge.
            var openBtnImage = CreateImage("UpgradesOpenButton", canvasGo.transform, uiSprite, new Color(0.18f, 0.30f, 0.45f));
            SetRect(openBtnImage.rectTransform, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(220f, 130f));
            var openButton = openBtnImage.gameObject.AddComponent<Button>();
            openButton.targetGraphic = openBtnImage;
            var openLabel = CreateText("Label", openBtnImage.transform, "UPGRADES", 36f, TextAlignmentOptions.Center);
            SetRect(openLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 130f));

            // --- Booster buttons: left edge, stacked (docs/ui-layout.md "Boost: 2x tap" / "2x passive") ---
            var boosterBtnImage = CreateImage("BoosterButton_preworkout", canvasGo.transform, uiSprite, new Color(0.60f, 0.35f, 0.15f));
            SetRect(boosterBtnImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(130f, 80f), new Vector2(220f, 130f));
            var boosterButtonComponent = boosterBtnImage.gameObject.AddComponent<Button>();
            boosterButtonComponent.targetGraphic = boosterBtnImage;
            var boosterLabel = CreateText("Label", boosterBtnImage.transform, string.Empty, 30f, TextAlignmentOptions.Center);
            StretchFull(boosterLabel.rectTransform);

            var boosterButton = boosterBtnImage.gameObject.AddComponent<BoosterButton>();
            AssignRef(boosterButton, "_booster", boosters[0]);
            AssignRef(boosterButton, "_button", boosterButtonComponent);
            AssignRef(boosterButton, "_label", boosterLabel);

            var proteinBtnImage = CreateImage("BoosterButton_protein_shake", canvasGo.transform, uiSprite, new Color(0.35f, 0.50f, 0.20f));
            SetRect(proteinBtnImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(130f, -80f), new Vector2(220f, 130f));
            var proteinButtonComponent = proteinBtnImage.gameObject.AddComponent<Button>();
            proteinButtonComponent.targetGraphic = proteinBtnImage;
            var proteinLabel = CreateText("Label", proteinBtnImage.transform, string.Empty, 30f, TextAlignmentOptions.Center);
            StretchFull(proteinLabel.rectTransform);

            var proteinButton = proteinBtnImage.gameObject.AddComponent<BoosterButton>();
            AssignRef(proteinButton, "_booster", boosters[1]);
            AssignRef(proteinButton, "_button", proteinButtonComponent);
            AssignRef(proteinButton, "_label", proteinLabel);

            // Modal root (starts hidden via ModalToggle). Dimmer fills the screen and, being a
            // raycast target, both blocks clicks to the game and makes TapController skip taps.
            var modal = new GameObject("UpgradesModal", typeof(RectTransform));
            modal.transform.SetParent(canvasGo.transform, false);
            StretchFull(modal.GetComponent<RectTransform>());

            var dimmer = CreateImage("Dimmer", modal.transform, uiSprite, new Color(0f, 0f, 0f, 0.75f));
            StretchFull(dimmer.rectTransform);

            var backdropButton = dimmer.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None; // no hover tint on a fullscreen dimmer
            backdropButton.targetGraphic = dimmer;

            var window = CreateImage("Window", modal.transform, uiSprite, new Color(0.12f, 0.14f, 0.18f, 1f));
            SetRect(window.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 980f));

            var modalTitle = CreateText("Title", window.transform, "UPGRADES", 60f, TextAlignmentOptions.Center);
            SetRect(modalTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(600f, 90f));

            var closeBtnImage = CreateImage("CloseButton", window.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(closeBtnImage.rectTransform, new Vector2(1f, 1f), new Vector2(-65f, -65f), new Vector2(90f, 90f));
            var closeButton = closeBtnImage.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeBtnImage;
            var closeLabel = CreateText("Label", closeBtnImage.transform, "X", 52f, TextAlignmentOptions.Center);
            SetRect(closeLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));

            // Scrollable upgrade list: 6 muscle-group upgrades no longer fit as fixed-position
            // buttons, so the content grows vertically and the viewport clips/scrolls it.
            var scrollAreaGo = new GameObject("ScrollArea", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollAreaGo.transform.SetParent(window.transform, false);
            var scrollAreaRect = scrollAreaGo.GetComponent<RectTransform>();
            SetRect(scrollAreaRect, new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(680f, 760f));
            var scrollAreaImage = scrollAreaGo.GetComponent<Image>();
            scrollAreaImage.sprite = uiSprite;
            scrollAreaImage.color = new Color(0.10f, 0.12f, 0.15f, 1f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollAreaGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layoutGroup = contentGo.GetComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(20, 20, 20, 20);
            layoutGroup.spacing = 20f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            // childControlHeight MUST be true or the per-button LayoutElement.preferredHeight
            // is never queried (buttons would silently render at the 100px RectTransform default).
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            var sizeFitter = contentGo.GetComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollAreaGo.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = scrollAreaRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            for (int i = 0; i < upgrades.Length; i++)
            {
                var btnGo = new GameObject("UpgradeBtn_" + upgrades[i].Id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                btnGo.transform.SetParent(contentGo.transform, false);

                var btnImage = btnGo.GetComponent<Image>();
                btnImage.sprite = uiSprite;
                btnImage.color = new Color(0.18f, 0.30f, 0.45f);

                var layoutElement = btnGo.GetComponent<LayoutElement>();
                layoutElement.preferredHeight = 140f;

                var button = btnGo.AddComponent<Button>();
                button.targetGraphic = btnImage;

                var buttonLabel = CreateText("Label", btnGo.transform, string.Empty, 34f, TextAlignmentOptions.Center);
                StretchFull(buttonLabel.rectTransform);

                var upgradeButton = btnGo.AddComponent<UpgradeButton>();
                AssignRef(upgradeButton, "_upgrade", upgrades[i]);
                AssignRef(upgradeButton, "_button", button);
                AssignRef(upgradeButton, "_label", buttonLabel);
            }

            var modalControllerGo = new GameObject("UpgradesModalController");
            modalControllerGo.transform.SetParent(root.transform, false);
            var modalToggle = modalControllerGo.AddComponent<ModalToggle>();
            AssignRef(modalToggle, "_panel", modal);
            AssignRef(modalToggle, "_openButton", openButton);
            AssignRef(modalToggle, "_closeButton", closeButton);
            AssignRef(modalToggle, "_backdropButton", backdropButton);

            // --- Settings: "SETTINGS" open button (top-right per docs/ui-layout.md) + a modal
            // with the sound mute toggle ---
            var settingsOpenImage = CreateImage("SettingsOpenButton", canvasGo.transform, uiSprite, new Color(0.30f, 0.30f, 0.35f));
            SetRect(settingsOpenImage.rectTransform, new Vector2(1f, 1f), new Vector2(-130f, -100f), new Vector2(220f, 130f));
            var settingsOpenButton = settingsOpenImage.gameObject.AddComponent<Button>();
            settingsOpenButton.targetGraphic = settingsOpenImage;
            var settingsOpenLabel = CreateText("Label", settingsOpenImage.transform, "SETTINGS", 34f, TextAlignmentOptions.Center);
            StretchFull(settingsOpenLabel.rectTransform);

            var settingsModal = new GameObject("SettingsModal", typeof(RectTransform));
            settingsModal.transform.SetParent(canvasGo.transform, false);
            StretchFull(settingsModal.GetComponent<RectTransform>());

            var settingsDimmer = CreateImage("Dimmer", settingsModal.transform, uiSprite, new Color(0f, 0f, 0f, 0.75f));
            StretchFull(settingsDimmer.rectTransform);

            var settingsBackdropButton = settingsDimmer.gameObject.AddComponent<Button>();
            settingsBackdropButton.transition = Selectable.Transition.None; // no hover tint on a fullscreen dimmer
            settingsBackdropButton.targetGraphic = settingsDimmer;

            var settingsWindow = CreateImage("Window", settingsModal.transform, uiSprite, new Color(0.12f, 0.14f, 0.18f, 1f));
            SetRect(settingsWindow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 420f));

            var settingsTitle = CreateText("Title", settingsWindow.transform, "SETTINGS", 48f, TextAlignmentOptions.Center);
            SetRect(settingsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(400f, 80f));

            var settingsCloseImage = CreateImage("CloseButton", settingsWindow.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(settingsCloseImage.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(80f, 80f));
            var settingsCloseButton = settingsCloseImage.gameObject.AddComponent<Button>();
            settingsCloseButton.targetGraphic = settingsCloseImage;
            var settingsCloseLabel = CreateText("Label", settingsCloseImage.transform, "X", 52f, TextAlignmentOptions.Center);
            StretchFull(settingsCloseLabel.rectTransform);

            var soundToggleImage = CreateImage("SoundToggle", settingsWindow.transform, uiSprite, new Color(0.18f, 0.30f, 0.45f));
            SetRect(soundToggleImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(420f, 110f));
            var soundToggleButton = soundToggleImage.gameObject.AddComponent<Button>();
            soundToggleButton.targetGraphic = soundToggleImage;
            var soundToggleLabel = CreateText("Label", soundToggleImage.transform, string.Empty, 36f, TextAlignmentOptions.Center);
            StretchFull(soundToggleLabel.rectTransform);

            var settingsPanel = settingsWindow.gameObject.AddComponent<SettingsPanel>();
            AssignRef(settingsPanel, "_soundToggleButton", soundToggleButton);
            AssignRef(settingsPanel, "_soundToggleLabel", soundToggleLabel);

            var settingsModalControllerGo = new GameObject("SettingsModalController");
            settingsModalControllerGo.transform.SetParent(root.transform, false);
            var settingsModalToggle = settingsModalControllerGo.AddComponent<ModalToggle>();
            AssignRef(settingsModalToggle, "_panel", settingsModal);
            AssignRef(settingsModalToggle, "_openButton", settingsOpenButton);
            AssignRef(settingsModalToggle, "_closeButton", settingsCloseButton);
            AssignRef(settingsModalToggle, "_backdropButton", settingsBackdropButton);

            // --- Locations / story progress: "Story" open button (top-left per docs/ui-layout.md)
            // + a modal listing every location with a MOVE UP action once the current one is 100%. ---
            var storyOpenImage = CreateImage("StoryProgressButton", canvasGo.transform, uiSprite, new Color(0.45f, 0.30f, 0.15f));
            SetRect(storyOpenImage.rectTransform, new Vector2(0f, 1f), new Vector2(130f, -100f), new Vector2(220f, 130f));
            var storyOpenButton = storyOpenImage.gameObject.AddComponent<Button>();
            storyOpenButton.targetGraphic = storyOpenImage;
            var storyOpenLabel = CreateText("Label", storyOpenImage.transform, string.Empty, 30f, TextAlignmentOptions.Center);
            StretchFull(storyOpenLabel.rectTransform);

            var storyProgressButton = storyOpenImage.gameObject.AddComponent<StoryProgressButton>();
            AssignRef(storyProgressButton, "_label", storyOpenLabel);

            var locationsModal = new GameObject("LocationsModal", typeof(RectTransform));
            locationsModal.transform.SetParent(canvasGo.transform, false);
            StretchFull(locationsModal.GetComponent<RectTransform>());

            var locationsDimmer = CreateImage("Dimmer", locationsModal.transform, uiSprite, new Color(0f, 0f, 0f, 0.75f));
            StretchFull(locationsDimmer.rectTransform);

            var locationsBackdropButton = locationsDimmer.gameObject.AddComponent<Button>();
            locationsBackdropButton.transition = Selectable.Transition.None; // no hover tint on a fullscreen dimmer
            locationsBackdropButton.targetGraphic = locationsDimmer;

            var locationsWindow = CreateImage("Window", locationsModal.transform, uiSprite, new Color(0.12f, 0.14f, 0.18f, 1f));
            SetRect(locationsWindow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 900f));

            var locationsTitle = CreateText("Title", locationsWindow.transform, "LOCATIONS", 48f, TextAlignmentOptions.Center);
            SetRect(locationsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(500f, 80f));

            var locationsCloseImage = CreateImage("CloseButton", locationsWindow.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(locationsCloseImage.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(80f, 80f));
            var locationsCloseButton = locationsCloseImage.gameObject.AddComponent<Button>();
            locationsCloseButton.targetGraphic = locationsCloseImage;
            var locationsCloseLabel = CreateText("Label", locationsCloseImage.transform, "X", 52f, TextAlignmentOptions.Center);
            StretchFull(locationsCloseLabel.rectTransform);

            var locationsRowsGo = new GameObject("Rows", typeof(RectTransform));
            locationsRowsGo.transform.SetParent(locationsWindow.transform, false);
            var locationsRowsRect = locationsRowsGo.GetComponent<RectTransform>();
            SetRect(locationsRowsRect, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(620f, 560f));

            var moveUpImage = CreateImage("MoveUpButton", locationsWindow.transform, uiSprite, new Color(0.20f, 0.70f, 0.30f));
            SetRect(moveUpImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(420f, 110f));
            var moveUpButton = moveUpImage.gameObject.AddComponent<Button>();
            moveUpButton.targetGraphic = moveUpImage;
            var moveUpLabel = CreateText("Label", moveUpImage.transform, "MOVE UP ▲", 40f, TextAlignmentOptions.Center);
            StretchFull(moveUpLabel.rectTransform);

            var locationsPanel = locationsWindow.gameObject.AddComponent<LocationsPanel>();
            AssignRef(locationsPanel, "_rowsContainer", locationsRowsRect);
            AssignRef(locationsPanel, "_moveUpButton", moveUpButton);
            AssignRef(locationsPanel, "_moveUpLabel", moveUpLabel);

            var locationsModalControllerGo = new GameObject("LocationsModalController");
            locationsModalControllerGo.transform.SetParent(root.transform, false);
            var locationsModalToggle = locationsModalControllerGo.AddComponent<ModalToggle>();
            AssignRef(locationsModalToggle, "_panel", locationsModal);
            AssignRef(locationsModalToggle, "_openButton", storyOpenButton);
            AssignRef(locationsModalToggle, "_closeButton", locationsCloseButton);
            AssignRef(locationsModalToggle, "_backdropButton", locationsBackdropButton);

            // --- Offline claim popup ---
            // Component lives on an always-active object; the panel it toggles is a child,
            // so hiding the panel never disables the component (which would kill OnEnable).
            var popupGo = new GameObject("OfflinePopup");
            popupGo.transform.SetParent(canvasGo.transform, false);
            var popup = popupGo.AddComponent<OfflineClaimPopup>();

            var panel = CreateImage("Panel", popupGo.transform, uiSprite, new Color(0f, 0f, 0f, 0.85f));
            StretchFull(panel.rectTransform);

            var offlineMessage = CreateText("Message", panel.transform, string.Empty, 48f, TextAlignmentOptions.Center);
            SetRect(offlineMessage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(900f, 320f));

            var claimBtnImage = CreateImage("ClaimButton", panel.transform, uiSprite, new Color(0.20f, 0.80f, 0.35f));
            SetRect(claimBtnImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(360f, 110f));
            var claimButton = claimBtnImage.gameObject.AddComponent<Button>();
            var claimLabel = CreateText("Label", claimBtnImage.transform, "OK", 44f, TextAlignmentOptions.Center);
            SetRect(claimLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 110f));

            // "Double via ad" (§10 opt-in rewarded): sits below the OK button, no overlap
            // (OK spans -195..-85, this spans -335..-225).
            var doubleBtnImage = CreateImage("DoubleButton", panel.transform, uiSprite, new Color(0.20f, 0.60f, 0.85f));
            SetRect(doubleBtnImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -280f), new Vector2(420f, 110f));
            var doubleButton = doubleBtnImage.gameObject.AddComponent<Button>();
            doubleButton.targetGraphic = doubleBtnImage;
            var doubleLabel = CreateText("Label", doubleBtnImage.transform, "DOUBLE IT ▶", 40f, TextAlignmentOptions.Center);
            SetRect(doubleLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 110f));

            AssignRef(popup, "_panel", panel.gameObject);
            AssignRef(popup, "_messageText", offlineMessage);
            AssignRef(popup, "_claimButton", claimButton);
            AssignRef(popup, "_doubleButton", doubleButton);

            // Hidden by default in the scene too (runtime Awake also hides it).
            panel.gameObject.SetActive(false);

            // --- Ad overlay: created LAST among canvas children so it renders on top of every
            // other UI (modals included) while a mock rewarded ad "plays". ---
            var adOverlay = CreateImage("AdOverlay", canvasGo.transform, uiSprite, new Color(0f, 0f, 0f, 0.92f));
            StretchFull(adOverlay.rectTransform);
            // raycastTarget stays true (Image default) so the overlay blocks input to everything beneath it.

            var adOverlayText = CreateText("Label", adOverlay.transform, "▶ AD PLAYING...", 64f, TextAlignmentOptions.Center);
            SetRect(adOverlayText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 160f));

            AssignRef(adManager, "_adOverlay", adOverlay.gameObject);

            // Hidden by default in the scene too (runtime Awake also hides it).
            adOverlay.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[CoreLoopSceneBootstrap] Scene built and saved.");
        }

        private static GameConfig GetOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            // Force a synchronous import and reload the canonical asset instance, so the
            // returned object has a registered GUID and serializes as an asset reference
            // (an in-memory CreateInstance object would serialize as {fileID: 0}).
            AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        }

        private const string AudioLibraryPath = "Assets/_Game/Data/AudioLibrary.asset";
        private const string SfxFolder = "Assets/_Game/Audio/Placeholders";

        private static AudioLibrary GetOrCreateAudioLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, AudioLibraryPath);
            }

            var so = new SerializedObject(library);
            so.FindProperty("_tapClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/tap.wav");
            so.FindProperty("_buyClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/buy.wav");
            so.FindProperty("_tierUpClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/tier_up.wav");
            so.FindProperty("_boosterClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/booster.wav");
            // _masterVolume intentionally left as-is (default from the SO / prior inspector tuning).
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            // Reload the canonical, imported instance so it serializes as an asset reference.
            AssetDatabase.ImportAsset(AudioLibraryPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioLibraryPath);
        }

        private const string UpgradesFolder = "Assets/_Game/Data/Upgrades";

        private static UpgradeData GetOrCreateUpgrade(string id, string displayName, StatType statType, double effectPerLevel, double baseCost, float growthRate)
        {
            if (!AssetDatabase.IsValidFolder(UpgradesFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Upgrades");
            }

            string path = $"{UpgradesFolder}/{id}.asset";
            var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
            if (upgrade == null)
            {
                upgrade = ScriptableObject.CreateInstance<UpgradeData>();
                AssetDatabase.CreateAsset(upgrade, path);
            }

            var so = new SerializedObject(upgrade);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_statType").enumValueIndex = (int)statType;
            so.FindProperty("_effectPerLevel").doubleValue = effectPerLevel;
            so.FindProperty("_baseCost").doubleValue = baseCost;
            so.FindProperty("_growthRate").floatValue = growthRate;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            // Reload the canonical, imported instance so it serializes as an asset reference.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
        }

        private const string BoostersFolder = "Assets/_Game/Data/Boosters";

        private static BoosterData GetOrCreateBooster(string id, string displayName, int targetEnumDeclarationIndex, float multiplier, float durationSeconds, float cooldownSeconds, bool requiresAd)
        {
            if (!AssetDatabase.IsValidFolder(BoostersFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Boosters");
            }

            string path = $"{BoostersFolder}/{id}.asset";
            var booster = AssetDatabase.LoadAssetAtPath<BoosterData>(path);
            if (booster == null)
            {
                booster = ScriptableObject.CreateInstance<BoosterData>();
                AssetDatabase.CreateAsset(booster, path);
            }

            var so = new SerializedObject(booster);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            // enumValueIndex is the enum's DECLARATION-ORDER index: BoosterTarget { TapIncome=0, PassiveIncome=1 }.
            so.FindProperty("_target").enumValueIndex = targetEnumDeclarationIndex;
            so.FindProperty("_multiplier").floatValue = multiplier;
            so.FindProperty("_durationSeconds").floatValue = durationSeconds;
            so.FindProperty("_cooldownSeconds").floatValue = cooldownSeconds;
            so.FindProperty("_requiresAd").boolValue = requiresAd;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            // Reload the canonical, imported instance so it serializes as an asset reference.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<BoosterData>(path);
        }

        private const string LocationsFolder = "Assets/_Game/Data/Locations";

        private static LocationData GetOrCreateLocation(string id, string displayName, int totalLevels, float multiplier)
        {
            if (!AssetDatabase.IsValidFolder(LocationsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Locations");
            }

            string path = $"{LocationsFolder}/{id}.asset";
            var location = AssetDatabase.LoadAssetAtPath<LocationData>(path);
            if (location == null)
            {
                location = ScriptableObject.CreateInstance<LocationData>();
                AssetDatabase.CreateAsset(location, path);
            }

            var so = new SerializedObject(location);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_totalLevelsToComplete").intValue = totalLevels;
            so.FindProperty("_globalMultiplier").floatValue = multiplier;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            // Reload the canonical, imported instance so it serializes as an asset reference.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<LocationData>(path);
        }

        private const string MuscleTiersFolder = "Assets/_Game/Data/MuscleTiers";

        private static MuscleTierData GetOrCreateTier(string fileName, int tier, string displayName, double threshold, string bodySpritePath, string headSpritePath)
        {
            if (!AssetDatabase.IsValidFolder(MuscleTiersFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data", "MuscleTiers");
            }

            string path = $"{MuscleTiersFolder}/{fileName}.asset";
            var tierAsset = AssetDatabase.LoadAssetAtPath<MuscleTierData>(path);
            if (tierAsset == null)
            {
                tierAsset = ScriptableObject.CreateInstance<MuscleTierData>();
                AssetDatabase.CreateAsset(tierAsset, path);
            }

            var so = new SerializedObject(tierAsset);
            so.FindProperty("_tier").intValue = tier;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_totalEarnedThreshold").doubleValue = threshold;
            so.FindProperty("_bodySprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(bodySpritePath);
            // Head is SHARED across tiers for the MVP: one sprite (head_01.png), all 6 tiers.
            so.FindProperty("_headSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(headSpritePath);
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            // Reload the canonical, imported instance so it serializes as an asset reference.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<MuscleTierData>(path);
        }

        private const string CosmeticsFolder = "Assets/_Game/Data/Cosmetics";

        private static CosmeticData GetOrCreateCosmetic(string id, string displayName, CharacterLayer layer, string spritePath, double cost)
        {
            if (!AssetDatabase.IsValidFolder(CosmeticsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Cosmetics");
            }

            string path = $"{CosmeticsFolder}/{id}.asset";
            var cosmetic = AssetDatabase.LoadAssetAtPath<CosmeticData>(path);
            if (cosmetic == null)
            {
                cosmetic = ScriptableObject.CreateInstance<CosmeticData>();
                AssetDatabase.CreateAsset(cosmetic, path);
            }

            var so = new SerializedObject(cosmetic);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            // enumValueIndex is the enum's DECLARATION-ORDER index, not its underlying int value.
            so.FindProperty("_layer").enumValueIndex = GetCharacterLayerDeclarationIndex(layer);
            so.FindProperty("_sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            so.FindProperty("_cost").doubleValue = cost;
            so.FindProperty("_unlockedByDefault").boolValue = true;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<CosmeticData>(path);
        }

        // CharacterLayer declaration order: Background, Body, Shorts, Shoes, Shirt, Arms, Head,
        // Beard, Hair, Accessory — this index must match that order, not the enum's int values.
        private static int GetCharacterLayerDeclarationIndex(CharacterLayer layer)
        {
            switch (layer)
            {
                case CharacterLayer.Background: return 0;
                case CharacterLayer.Body: return 1;
                case CharacterLayer.Shorts: return 2;
                case CharacterLayer.Shoes: return 3;
                case CharacterLayer.Shirt: return 4;
                case CharacterLayer.Arms: return 5;
                case CharacterLayer.Head: return 6;
                case CharacterLayer.Beard: return 7;
                case CharacterLayer.Hair: return 8;
                case CharacterLayer.Accessory: return 9;
                default: return 0;
            }
        }

        private static void AssignArray(Component c, string field, Object[] values)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogError($"[CoreLoopSceneBootstrap] Array field '{field}' not found on {c.GetType().Name}.");
                return;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedProperties();
        }

        // The fields this builds wires (_gameConfig, _gainsText, _energyText, _fill, _target...)
        // are private [SerializeField]s on existing runtime scripts we must not modify, so
        // they are assigned through SerializedObject rather than made public or reflected into.
        private static void AssignRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[CoreLoopSceneBootstrap] Field '{field}' not found on {c.GetType().Name}.");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;

            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;

            return tmp;
        }

        private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
