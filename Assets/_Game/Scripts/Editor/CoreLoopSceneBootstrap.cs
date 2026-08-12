using System.IO;
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
using IdleGymBro.Meta;
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
        private const int MuscleTierCount = 6;
        private const string BackgroundArtFolder = "Assets/_Game/Art/Backgrounds/Placeholders";

        [MenuItem("IdleGymBro/Build Core Loop Scene")]
        public static void BuildCoreLoopScene()
        {
            // Import any on-disk assets that aren't yet in the AssetDatabase, so an
            // existing GameConfig.asset resolves to a real, referenceable asset.
            AssetDatabase.Refresh();

            // Must run before any MuscleTierData/CosmeticData assets are created below, since
            // those assets reference sprites this generates.
            PlaceholderArtGenerator.Generate();
            PlaceholderBackgroundGenerator.Generate();
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

            // MUST run before any GetOrCreate* below: those wire _icon via LoadIcon(), and an icon
            // whose .meta still describes the previous PNG yields NO sprite at all — the reference
            // would be written as null and the icon would just vanish from the UI.
            Debug.Log($"[CoreLoopSceneBootstrap] {ConfigureIconImporters()} icons ready.");

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
                GetOrCreateLocation("home", "Home Workout", 25, 1f, $"{BackgroundArtFolder}/bg_home.png"),
                GetOrCreateLocation("street", "Street Workout", 75, 2f, $"{BackgroundArtFolder}/bg_street.png"),
                GetOrCreateLocation("basic_gym", "Basic Gym", 160, 5f, $"{BackgroundArtFolder}/bg_basic_gym.png"),
                GetOrCreateLocation("hardcore_gym", "Hardcore Gym", 300, 12f, $"{BackgroundArtFolder}/bg_hardcore_gym.png"),
                GetOrCreateLocation("beach", "Venice Beach", 500, 30f, $"{BackgroundArtFolder}/bg_beach.png"),
                GetOrCreateLocation("olympia", "Mr. Olympia", 800, 75f, $"{BackgroundArtFolder}/bg_olympia.png"),
            };

            // Achievements (§12 retention). Type index = AchievementType declaration order:
            // TotalGainsEarned=0, RepsPerformed=1, UpgradesBought=2, LocationReached=3.
            var achievements = new AchievementData[]
            {
                GetOrCreateAchievement("first_grind", "First Grind", 0, 1000d, 500d),
                GetOrCreateAchievement("getting_swole", "Getting Swole", 0, 100000d, 25000d),
                GetOrCreateAchievement("rep_machine", "Rep Machine", 1, 500d, 2000d),
                GetOrCreateAchievement("gym_rat", "Gym Rat", 1, 5000d, 50000d),
                GetOrCreateAchievement("shopaholic", "Shopaholic", 2, 10d, 5000d),
                GetOrCreateAchievement("world_tour", "World Tour", 3, 2d, 100000d),
            };

            // Muscle tiers (data-driven; thresholds are lifetime TotalEarned, not balance).
            var tiers = new MuscleTierData[]
            {
                // headSpritePath is null: the painted tier bodies already include the head, so a
                // separate Head layer would draw a second face on top of the first. Pass a path
                // again only if head art is ever split back out into its own layer.
                GetOrCreateTier("tier1_skinny", 1, "Skinny", 0d, $"{CharacterArtFolder}/body_tier1.png", null),
                GetOrCreateTier("tier2_slim_fit", 2, "Slim Fit", 1000d, $"{CharacterArtFolder}/body_tier2.png", null),
                GetOrCreateTier("tier3_fit", 3, "Fit", 25000d, $"{CharacterArtFolder}/body_tier3.png", null),
                GetOrCreateTier("tier4_jacked", 4, "Jacked", 500000d, $"{CharacterArtFolder}/body_tier4.png", null),
                GetOrCreateTier("tier5_mass_monster", 5, "Mass Monster", 10000000d, $"{CharacterArtFolder}/body_tier5.png", null),
                GetOrCreateTier("tier6_enhanced", 6, "Enhanced", 500000000d, $"{CharacterArtFolder}/body_tier6.png", null),
            };

            // Must follow GetOrCreateTier: the bake reads each tier's frame arrays, which are only
            // wired a few lines above. Re-baking every build keeps the head offsets honest — swap an
            // animation frame for new art and the hair stops matching it until this runs again.
            Debug.Log($"[CoreLoopSceneBootstrap] frame anchors baked on {FrameAnchorBaker.Bake()} tier(s).");

            // Shorts are cut from each tier's own hips, so they must be regenerated whenever the
            // bodies change — and before the cosmetics below pick the per-tier sprites up.
            Debug.Log($"[CoreLoopSceneBootstrap] {ShortsGenerator.Generate()} shorts sprites ready.");

            // Default cosmetics (free, unlocked from the start; wardrobe/shop is post-MVP).
            var cosmetics = new CosmeticData[]
            {
                GetOrCreateCosmetic("hair_01", "Hair 1", CharacterLayer.Hair, $"{CharacterArtFolder}/hair_01.png", 0d),
                GetOrCreateCosmetic("hair_02", "Hair 2", CharacterLayer.Hair, $"{CharacterArtFolder}/hair_02.png", 0d),
                GetOrCreateCosmetic("hair_03", "Hair 3", CharacterLayer.Hair, $"{CharacterArtFolder}/hair_03.png", 0d),
                GetOrCreateCosmetic("beard_01", "Beard 1", CharacterLayer.Beard, $"{CharacterArtFolder}/beard_01.png", 0d),
                GetOrCreateCosmetic("beard_02", "Beard 2", CharacterLayer.Beard, $"{CharacterArtFolder}/beard_02.png", 0d),
                GetOrCreateCosmetic("shorts_01", "Shorts 1", CharacterLayer.Shorts, $"{CharacterArtFolder}/shorts_01.png", 0d),
                GetOrCreateCosmetic("shorts_02", "Shorts 2", CharacterLayer.Shorts, $"{CharacterArtFolder}/shorts_02.png", 0d),
                GetOrCreateCosmetic("shorts_03", "Shorts 3", CharacterLayer.Shorts, $"{CharacterArtFolder}/shorts_03.png", 0d),
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
            var periodicRewardManager = gameSystems.AddComponent<PeriodicRewardManager>();
            // Meta/retention: no _gameConfig field (drives off gameplay events) — excluded from self-check.
            var achievementManager = gameSystems.AddComponent<AchievementManager>();
            var dailyRewardManager = gameSystems.AddComponent<DailyRewardManager>();
            var prestigeManager = gameSystems.AddComponent<PrestigeManager>();
            // Wardrobe: no _gameConfig field (drives off CosmeticData assets) — excluded from self-check.
            var wardrobeManager = gameSystems.AddComponent<WardrobeManager>();

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
            AssignRef(periodicRewardManager, "_gameConfig", config);
            AssignRef(dailyRewardManager, "_gameConfig", config);
            AssignRef(prestigeManager, "_gameConfig", config);
            AssignArray(wardrobeManager, "_cosmetics", cosmetics);
            AssignArray(achievementManager, "_achievements", achievements);

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

            UiShapeGenerator.Generate();
            ConfigureUiKit();

            // Surfaces are generated white shapes, tinted by the palette. Nothing is baked into
            // the pixels — no border line, no gloss, no drop shadow — so a "flat" look really is
            // flat instead of flat-with-someone-else's-3D-underneath.
            // Fills and dimmers use a plain box: a rounded 9-sliced sprite stretched as a progress
            // fill would draw its own corners partway along the bar.
            Sprite panelSprite = UiShape("panel");
            Sprite buttonSprite = UiShape("panel_soft") ?? panelSprite;
            Sprite plainSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite uiSprite = panelSprite ?? plainSprite;

            // --- Location background (world-space, behind the character) ---
            // sortingOrder -100 draws beneath the character stack (whose lowest layer is -10);
            // LocationBackground swaps the sprite per LocationChangedEvent.
            var backgroundGo = new GameObject("LocationBackground");
            backgroundGo.transform.SetParent(root.transform, false);
            backgroundGo.transform.position = Vector3.zero;
            var bgRenderer = backgroundGo.AddComponent<SpriteRenderer>();
            bgRenderer.sortingOrder = -100;
            var locationBackground = backgroundGo.AddComponent<LocationBackground>();
            AssignRef(locationBackground, "_renderer", bgRenderer);
            AssignArray(locationBackground, "_locations", locations);

            // --- World-space character ---
            // Not UI: a SpriteRenderer layer stack positioned in front of the Main Camera, drawn
            // beneath the ScreenSpaceOverlay HUD canvas.
            var characterGo = new GameObject("Character");
            characterGo.transform.SetParent(root.transform, false);
            characterGo.transform.position = new Vector3(0f, -2.4f, 0f);
            characterGo.transform.localScale = new Vector3(3f, 3f, 1f);
            var builder = characterGo.AddComponent<CharacterBuilder>();

            // Replaces PlaceholderCharacter: idle breathing + the rep punch now live in one
            // component, because both drive the root transform and two writers fight each other.
            var characterAnimator = characterGo.AddComponent<CharacterAnimator>();
            AssignRef(characterAnimator, "_gameConfig", config);
            AssignRef(characterAnimator, "_characterBuilder", builder);
            AssignArray(builder, "_tiers", tiers);
            AssignRef(builder, "_blinkSprite", AssetDatabase.LoadAssetAtPath<Sprite>($"{CharacterArtFolder}/blink_01.png"));

            // Self-check: verify the asset reference actually serialized (asset refs are
            // more timing-sensitive in batchmode than scene-object refs). BoosterManager,
            // AudioManager, AdManager, LocationManager, AchievementManager and WardrobeManager
            // have no _gameConfig field, so they're intentionally excluded from this check.
            // Runs here rather than beside the system wiring because CharacterAnimator, which also
            // takes the config, only exists once the character has been built.
            var systems = new Component[] { gameManager, tickSystem, energySystem, currencyManager, tapController, saveSystem, passiveIncome, offlineEarnings, upgradeManager, periodicRewardManager, dailyRewardManager, prestigeManager, characterAnimator };
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

            // --- Top bar ---
            // A surface behind the readouts. Without it the counter is bare text floating on the
            // artwork: it competes with the background for contrast and stops being legible the
            // moment a location's background is bright.
            var topBar = CreateImage("TopBar", canvasGo.transform, uiSprite, PanelColor);
            SetRect(topBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(1020f, 320f));
            topBar.raycastTarget = false;

            var gainsIcon = CreateImage("GainsIcon", topBar.transform, LoadIcon("gains"), IconTint);
            SetRect(gainsIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(-196f, -80f), new Vector2(96f, 96f));
            gainsIcon.preserveAspect = true;
            gainsIcon.raycastTarget = false;

            // --- Gains text ---
            var gainsText = CreateText("GainsText", canvasGo.transform, "0", 80f, TextAlignmentOptions.Center);
            SetRect(gainsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(40f, -140f), new Vector2(620f, 110f));
            var gainsCounterJuice = gainsText.gameObject.AddComponent<GainsCounterJuice>();
            AssignRef(gainsCounterJuice, "_target", gainsText.rectTransform);

            // --- Passive income rate ---
            var passiveRateText = CreateText("PassiveRateText", canvasGo.transform, "0/s", 40f, TextAlignmentOptions.Center);
            SetRect(passiveRateText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(700f, 50f));

            // --- Energy bar ---
            var energyBarBg = CreateImage("EnergyBarBG", canvasGo.transform, uiSprite, new Color(0.06f, 0.07f, 0.10f, 0.95f));
            SetRect(energyBarBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(700f, 60f));

            var energyBar = CreateImage("EnergyBar", energyBarBg.transform, plainSprite, PositiveColor);
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
            MakeIconButton(openBtnImage, openLabel, "upgrades", AccentColor, 150f);
            SetRect(openLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 130f));

            // --- Booster buttons: left edge, stacked (docs/ui-layout.md "Boost: 2x tap" / "2x passive") ---
            var boosterBtnImage = CreateImage("BoosterButton_preworkout", canvasGo.transform, uiSprite, new Color(0.60f, 0.35f, 0.15f));
            SetRect(boosterBtnImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(130f, 80f), new Vector2(220f, 130f));
            var boosterButtonComponent = boosterBtnImage.gameObject.AddComponent<Button>();
            boosterButtonComponent.targetGraphic = boosterBtnImage;
            var boosterIcon = CreateBoosterIcon(boosterBtnImage.transform);
            var boosterLabel = CreateText("Label", boosterBtnImage.transform, string.Empty, 28f, TextAlignmentOptions.Center);
            SetRect(boosterLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(210f, 56f));

            var boosterButton = boosterBtnImage.gameObject.AddComponent<BoosterButton>();
            AssignRef(boosterButton, "_booster", boosters[0]);
            AssignRef(boosterButton, "_button", boosterButtonComponent);
            AssignRef(boosterButton, "_label", boosterLabel);
            AssignRef(boosterButton, "_icon", boosterIcon);
            StyleRoundButton(boosterBtnImage, boosterIcon, boosterLabel, new Color(0.62f, 0.38f, 0.16f, 0.96f), 140f, keepLabel: true);

            var proteinBtnImage = CreateImage("BoosterButton_protein_shake", canvasGo.transform, uiSprite, new Color(0.35f, 0.50f, 0.20f));
            SetRect(proteinBtnImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(130f, -80f), new Vector2(220f, 130f));
            var proteinButtonComponent = proteinBtnImage.gameObject.AddComponent<Button>();
            proteinButtonComponent.targetGraphic = proteinBtnImage;
            var proteinIcon = CreateBoosterIcon(proteinBtnImage.transform);
            var proteinLabel = CreateText("Label", proteinBtnImage.transform, string.Empty, 28f, TextAlignmentOptions.Center);
            SetRect(proteinLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(210f, 56f));

            var proteinButton = proteinBtnImage.gameObject.AddComponent<BoosterButton>();
            AssignRef(proteinButton, "_booster", boosters[1]);
            AssignRef(proteinButton, "_button", proteinButtonComponent);
            AssignRef(proteinButton, "_label", proteinLabel);
            AssignRef(proteinButton, "_icon", proteinIcon);
            StyleRoundButton(proteinBtnImage, proteinIcon, proteinLabel, new Color(0.30f, 0.52f, 0.26f, 0.96f), 140f, keepLabel: true);

            // Modal root (starts hidden via ModalToggle). Dimmer fills the screen and, being a
            // raycast target, both blocks clicks to the game and makes TapController skip taps.
            var modal = new GameObject("UpgradesModal", typeof(RectTransform));
            modal.transform.SetParent(canvasGo.transform, false);
            StretchFull(modal.GetComponent<RectTransform>());

            var dimmer = CreateImage("Dimmer", modal.transform, plainSprite, new Color(0f, 0f, 0f, 0.55f));
            StretchFull(dimmer.rectTransform);

            var backdropButton = dimmer.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None; // no hover tint on a fullscreen dimmer
            backdropButton.targetGraphic = dimmer;

            var window = CreateImage("Window", modal.transform, uiSprite, PanelColor);
            SetRect(window.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 980f));

            var modalTitle = CreateText("Title", window.transform, "UPGRADES", 60f, TextAlignmentOptions.Center);
            SetRect(modalTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(600f, 90f));

            var closeBtnImage = CreateImage("CloseButton", window.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(closeBtnImage.rectTransform, new Vector2(1f, 1f), new Vector2(-65f, -65f), new Vector2(90f, 90f));
            var closeButton = closeBtnImage.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeBtnImage;
            var closeLabel = CreateText("Label", closeBtnImage.transform, "X", 52f, TextAlignmentOptions.Center);
            StyleModal(window, modalTitle, closeBtnImage, closeLabel);
            SetRect(closeLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));

            // x1 / x10 switch. Sits above the list because it re-prices every row at once — putting
            // it on the rows would mean seven copies of the same state.
            var buyToggleImage = CreateImage("BuyMultiplier", window.transform, buttonSprite, ButtonColor);
            SetRect(buyToggleImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(260f, 84f));
            var buyToggleButton = buyToggleImage.gameObject.AddComponent<Button>();
            buyToggleButton.targetGraphic = buyToggleImage;
            var buyToggleLabel = CreateText("Label", buyToggleImage.transform, "BUY x1", 40f, TextAlignmentOptions.Center);
            SetRect(buyToggleLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 80f));
            var buyToggle = buyToggleImage.gameObject.AddComponent<BuyMultiplierToggle>();
            AssignRef(buyToggle, "_button", buyToggleButton);
            AssignRef(buyToggle, "_label", buyToggleLabel);
            AssignRef(buyToggle, "_surface", buyToggleImage);

            // Scrollable upgrade list: 6 muscle-group upgrades no longer fit as fixed-position
            // buttons, so the content grows vertically and the viewport clips/scrolls it.
            var scrollAreaGo = new GameObject("ScrollArea", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollAreaGo.transform.SetParent(window.transform, false);
            var scrollAreaRect = scrollAreaGo.GetComponent<RectTransform>();
            SetRect(scrollAreaRect, new Vector2(0.5f, 1f), new Vector2(0f, -600f), new Vector2(680f, 680f));
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
                btnImage.color = ButtonColor;

                var layoutElement = btnGo.GetComponent<LayoutElement>();
                layoutElement.preferredHeight = 140f;

                var button = btnGo.AddComponent<Button>();
                button.targetGraphic = btnImage;

                // Icon on the left, text in the remaining space — the row reads at a glance
                // instead of being one more block of prose.
                var rowIcon = CreateImage("Icon", btnGo.transform, null, new Color(1f, 1f, 1f, 0.92f));
                SetRect(rowIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(120f, 120f));
                rowIcon.preserveAspect = true;

                var buttonLabel = CreateText("Label", btnGo.transform, string.Empty, 34f, TextAlignmentOptions.Left);
                SetRect(buttonLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(500f, 120f));

                var upgradeButton = btnGo.AddComponent<UpgradeButton>();
                AssignRef(upgradeButton, "_upgrade", upgrades[i]);
                AssignRef(upgradeButton, "_button", button);
                AssignRef(upgradeButton, "_label", buttonLabel);
                AssignRef(upgradeButton, "_icon", rowIcon);
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
            MakeIconButton(settingsOpenImage, settingsOpenLabel, "settings", ButtonColor, 120f);
            StretchFull(settingsOpenLabel.rectTransform);

            // --- Periodic reward button (bottom-right per docs/ui-layout.md) ---
            var rewardImage = CreateImage("PeriodicRewardButton", canvasGo.transform, uiSprite, new Color(0.20f, 0.55f, 0.30f));
            SetRect(rewardImage.rectTransform, new Vector2(1f, 0f), new Vector2(-130f, 110f), new Vector2(220f, 130f));
            var rewardButton = rewardImage.gameObject.AddComponent<Button>();
            rewardButton.targetGraphic = rewardImage;
            var rewardLabel = CreateText("Label", rewardImage.transform, string.Empty, 28f, TextAlignmentOptions.Center);
            StretchFull(rewardLabel.rectTransform);
            MakeIconButton(rewardImage, rewardLabel, "reward", PositiveColor, 140f, keepLabel: true);

            var periodicRewardUi = rewardImage.gameObject.AddComponent<PeriodicRewardButton>();
            AssignRef(periodicRewardUi, "_button", rewardButton);
            AssignRef(periodicRewardUi, "_label", rewardLabel);

            var settingsModal = new GameObject("SettingsModal", typeof(RectTransform));
            settingsModal.transform.SetParent(canvasGo.transform, false);
            StretchFull(settingsModal.GetComponent<RectTransform>());

            var settingsDimmer = CreateImage("Dimmer", settingsModal.transform, plainSprite, new Color(0f, 0f, 0f, 0.55f));
            StretchFull(settingsDimmer.rectTransform);

            var settingsBackdropButton = settingsDimmer.gameObject.AddComponent<Button>();
            settingsBackdropButton.transition = Selectable.Transition.None; // no hover tint on a fullscreen dimmer
            settingsBackdropButton.targetGraphic = settingsDimmer;

            var settingsWindow = CreateImage("Window", settingsModal.transform, uiSprite, PanelColor);
            SetRect(settingsWindow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 420f));

            var settingsTitle = CreateText("Title", settingsWindow.transform, "SETTINGS", 48f, TextAlignmentOptions.Center);
            SetRect(settingsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(400f, 80f));

            var settingsCloseImage = CreateImage("CloseButton", settingsWindow.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(settingsCloseImage.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(80f, 80f));
            var settingsCloseButton = settingsCloseImage.gameObject.AddComponent<Button>();
            settingsCloseButton.targetGraphic = settingsCloseImage;
            var settingsCloseLabel = CreateText("Label", settingsCloseImage.transform, "X", 52f, TextAlignmentOptions.Center);
            StyleModal(settingsWindow, settingsTitle, settingsCloseImage, settingsCloseLabel);
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

            MakeIconButton(storyOpenImage, storyOpenLabel, "locations", ButtonColor, 140f, keepLabel: true);

            var storyProgressButton = storyOpenImage.gameObject.AddComponent<StoryProgressButton>();
            AssignRef(storyProgressButton, "_label", storyOpenLabel);

            var locationsModal = new GameObject("LocationsModal", typeof(RectTransform));
            locationsModal.transform.SetParent(canvasGo.transform, false);
            StretchFull(locationsModal.GetComponent<RectTransform>());

            var locationsDimmer = CreateImage("Dimmer", locationsModal.transform, plainSprite, new Color(0f, 0f, 0f, 0.55f));
            StretchFull(locationsDimmer.rectTransform);

            var locationsBackdropButton = locationsDimmer.gameObject.AddComponent<Button>();
            locationsBackdropButton.transition = Selectable.Transition.None; // no hover tint on a fullscreen dimmer
            locationsBackdropButton.targetGraphic = locationsDimmer;

            var locationsWindow = CreateImage("Window", locationsModal.transform, uiSprite, PanelColor);
            SetRect(locationsWindow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 900f));

            var locationsTitle = CreateText("Title", locationsWindow.transform, "LOCATIONS", 48f, TextAlignmentOptions.Center);
            SetRect(locationsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(500f, 80f));

            var locationsCloseImage = CreateImage("CloseButton", locationsWindow.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(locationsCloseImage.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(80f, 80f));
            var locationsCloseButton = locationsCloseImage.gameObject.AddComponent<Button>();
            locationsCloseButton.targetGraphic = locationsCloseImage;
            var locationsCloseLabel = CreateText("Label", locationsCloseImage.transform, "X", 52f, TextAlignmentOptions.Center);
            StyleModal(locationsWindow, locationsTitle, locationsCloseImage, locationsCloseLabel);
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

            // --- Achievements: "GOALS" open button (bottom-left per docs/ui-layout.md, with a
            // (N) claimable badge) + a modal listing achievements with a CLAIM ALL action. ---
            var goalsOpenImage = CreateImage("AchievementsButton", canvasGo.transform, uiSprite, new Color(0.45f, 0.25f, 0.45f));
            SetRect(goalsOpenImage.rectTransform, new Vector2(0f, 0f), new Vector2(130f, 110f), new Vector2(220f, 130f));
            var goalsOpenButton = goalsOpenImage.gameObject.AddComponent<Button>();
            goalsOpenButton.targetGraphic = goalsOpenImage;
            var goalsOpenLabel = CreateText("Label", goalsOpenImage.transform, "GOALS", 30f, TextAlignmentOptions.Center);
            MakeIconButton(goalsOpenImage, goalsOpenLabel, "achievements", ButtonColor, 140f, keepLabel: true);
            StretchFull(goalsOpenLabel.rectTransform);
            var achievementsButton = goalsOpenImage.gameObject.AddComponent<AchievementsButton>();
            AssignRef(achievementsButton, "_label", goalsOpenLabel);

            var goalsModal = new GameObject("AchievementsModal", typeof(RectTransform));
            goalsModal.transform.SetParent(canvasGo.transform, false);
            StretchFull(goalsModal.GetComponent<RectTransform>());

            var goalsDimmer = CreateImage("Dimmer", goalsModal.transform, plainSprite, new Color(0f, 0f, 0f, 0.55f));
            StretchFull(goalsDimmer.rectTransform);
            var goalsBackdropButton = goalsDimmer.gameObject.AddComponent<Button>();
            goalsBackdropButton.transition = Selectable.Transition.None;
            goalsBackdropButton.targetGraphic = goalsDimmer;

            var goalsWindow = CreateImage("Window", goalsModal.transform, uiSprite, PanelColor);
            SetRect(goalsWindow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 980f));

            var goalsTitle = CreateText("Title", goalsWindow.transform, "ACHIEVEMENTS", 44f, TextAlignmentOptions.Center);
            SetRect(goalsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 80f));

            var goalsCloseImage = CreateImage("CloseButton", goalsWindow.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(goalsCloseImage.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(80f, 80f));
            var goalsCloseButton = goalsCloseImage.gameObject.AddComponent<Button>();
            goalsCloseButton.targetGraphic = goalsCloseImage;
            var goalsCloseLabel = CreateText("Label", goalsCloseImage.transform, "X", 52f, TextAlignmentOptions.Center);
            StyleModal(goalsWindow, goalsTitle, goalsCloseImage, goalsCloseLabel);
            StretchFull(goalsCloseLabel.rectTransform);

            var goalsRowsGo = new GameObject("Rows", typeof(RectTransform));
            goalsRowsGo.transform.SetParent(goalsWindow.transform, false);
            var goalsRowsRect = goalsRowsGo.GetComponent<RectTransform>();
            SetRect(goalsRowsRect, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(680f, 620f));

            var claimAllImage = CreateImage("ClaimAllButton", goalsWindow.transform, uiSprite, new Color(0.20f, 0.70f, 0.30f));
            SetRect(claimAllImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(420f, 110f));
            var claimAllButton = claimAllImage.gameObject.AddComponent<Button>();
            claimAllButton.targetGraphic = claimAllImage;
            var claimAllLabel = CreateText("Label", claimAllImage.transform, "CLAIM ALL", 40f, TextAlignmentOptions.Center);
            StretchFull(claimAllLabel.rectTransform);

            var achievementsPanel = goalsWindow.gameObject.AddComponent<AchievementsPanel>();
            AssignRef(achievementsPanel, "_rowsContainer", goalsRowsRect);
            AssignRef(achievementsPanel, "_claimAllButton", claimAllButton);
            AssignRef(achievementsPanel, "_claimAllLabel", claimAllLabel);

            var goalsModalControllerGo = new GameObject("AchievementsModalController");
            goalsModalControllerGo.transform.SetParent(root.transform, false);
            var goalsModalToggle = goalsModalControllerGo.AddComponent<ModalToggle>();
            AssignRef(goalsModalToggle, "_panel", goalsModal);
            AssignRef(goalsModalToggle, "_openButton", goalsOpenButton);
            AssignRef(goalsModalToggle, "_closeButton", goalsCloseButton);
            AssignRef(goalsModalToggle, "_backdropButton", goalsBackdropButton);

            // --- Prestige: "NEW BULK" open button (top-left, under Story) + a confirm modal (§6). ---
            var prestigeOpenImage = CreateImage("PrestigeButton", canvasGo.transform, uiSprite, new Color(0.50f, 0.20f, 0.20f));
            SetRect(prestigeOpenImage.rectTransform, new Vector2(0f, 1f), new Vector2(130f, -240f), new Vector2(220f, 110f));
            var prestigeOpenButton = prestigeOpenImage.gameObject.AddComponent<Button>();
            prestigeOpenButton.targetGraphic = prestigeOpenImage;
            var prestigeOpenLabel = CreateText("Label", prestigeOpenImage.transform, "NEW BULK", 30f, TextAlignmentOptions.Center);
            MakeIconButton(prestigeOpenImage, prestigeOpenLabel, "prestige", DangerColor, 120f);
            StretchFull(prestigeOpenLabel.rectTransform);

            var prestigeModal = new GameObject("PrestigeModal", typeof(RectTransform));
            prestigeModal.transform.SetParent(canvasGo.transform, false);
            StretchFull(prestigeModal.GetComponent<RectTransform>());

            var prestigeDimmer = CreateImage("Dimmer", prestigeModal.transform, plainSprite, new Color(0f, 0f, 0f, 0.55f));
            StretchFull(prestigeDimmer.rectTransform);
            var prestigeBackdropButton = prestigeDimmer.gameObject.AddComponent<Button>();
            prestigeBackdropButton.transition = Selectable.Transition.None;
            prestigeBackdropButton.targetGraphic = prestigeDimmer;

            var prestigeWindow = CreateImage("Window", prestigeModal.transform, uiSprite, PanelColor);
            SetRect(prestigeWindow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 720f));

            var prestigeTitle = CreateText("Title", prestigeWindow.transform, "NEW BULK", 48f, TextAlignmentOptions.Center);
            SetRect(prestigeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(500f, 80f));

            var prestigeCloseImage = CreateImage("CloseButton", prestigeWindow.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(prestigeCloseImage.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(80f, 80f));
            var prestigeCloseButton = prestigeCloseImage.gameObject.AddComponent<Button>();
            prestigeCloseButton.targetGraphic = prestigeCloseImage;
            var prestigeCloseLabel = CreateText("Label", prestigeCloseImage.transform, "X", 52f, TextAlignmentOptions.Center);
            StyleModal(prestigeWindow, prestigeTitle, prestigeCloseImage, prestigeCloseLabel);
            StretchFull(prestigeCloseLabel.rectTransform);

            var prestigeInfo = CreateText("Info", prestigeWindow.transform, string.Empty, 34f, TextAlignmentOptions.Center);
            SetRect(prestigeInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(620f, 360f));

            var newBulkImage = CreateImage("NewBulkButton", prestigeWindow.transform, uiSprite, new Color(0.70f, 0.25f, 0.25f));
            SetRect(newBulkImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(420f, 110f));
            var newBulkButton = newBulkImage.gameObject.AddComponent<Button>();
            newBulkButton.targetGraphic = newBulkImage;
            var newBulkLabel = CreateText("Label", newBulkImage.transform, "NEW BULK", 40f, TextAlignmentOptions.Center);
            StretchFull(newBulkLabel.rectTransform);

            var prestigePanel = prestigeWindow.gameObject.AddComponent<PrestigePanel>();
            AssignRef(prestigePanel, "_infoText", prestigeInfo);
            AssignRef(prestigePanel, "_prestigeButton", newBulkButton);
            AssignRef(prestigePanel, "_prestigeLabel", newBulkLabel);

            var prestigeModalControllerGo = new GameObject("PrestigeModalController");
            prestigeModalControllerGo.transform.SetParent(root.transform, false);
            var prestigeModalToggle = prestigeModalControllerGo.AddComponent<ModalToggle>();
            AssignRef(prestigeModalToggle, "_panel", prestigeModal);
            AssignRef(prestigeModalToggle, "_openButton", prestigeOpenButton);
            AssignRef(prestigeModalToggle, "_closeButton", prestigeCloseButton);
            AssignRef(prestigeModalToggle, "_backdropButton", prestigeBackdropButton);

            // --- Wardrobe: "WARDROBE" open button (bottom-center) + a modal with per-layer NEXT cyclers. ---
            var wardrobeOpenImage = CreateImage("WardrobeButton", canvasGo.transform, uiSprite, new Color(0.35f, 0.30f, 0.50f));
            SetRect(wardrobeOpenImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(260f, 110f));
            var wardrobeOpenButton = wardrobeOpenImage.gameObject.AddComponent<Button>();
            wardrobeOpenButton.targetGraphic = wardrobeOpenImage;
            var wardrobeOpenLabel = CreateText("Label", wardrobeOpenImage.transform, "WARDROBE", 34f, TextAlignmentOptions.Center);
            MakeIconButton(wardrobeOpenImage, wardrobeOpenLabel, "wardrobe", ButtonColor, 130f);
            StretchFull(wardrobeOpenLabel.rectTransform);

            var wardrobeModal = new GameObject("WardrobeModal", typeof(RectTransform));
            wardrobeModal.transform.SetParent(canvasGo.transform, false);
            StretchFull(wardrobeModal.GetComponent<RectTransform>());

            // Bottom sheet, not a centred dialog: the character is a world-space sprite stack drawn
            // UNDER this overlay canvas, so a centred opaque window would hide the very thing the
            // player is customizing. The sheet stays below design-space y=-320 (the character spans
            // roughly -460..+403 at camera ortho size 5), and the dimmer is kept light so head,
            // hair and beard read clearly while cycling. Still a full-screen raycast target, so
            // backdrop-close and the TapController over-UI guard keep working.
            var wardrobeDimmer = CreateImage("Dimmer", wardrobeModal.transform, plainSprite, new Color(0f, 0f, 0f, 0.35f));
            StretchFull(wardrobeDimmer.rectTransform);
            var wardrobeBackdropButton = wardrobeDimmer.gameObject.AddComponent<Button>();
            wardrobeBackdropButton.transition = Selectable.Transition.None;
            wardrobeBackdropButton.targetGraphic = wardrobeDimmer;

            var wardrobeWindow = CreateImage("Window", wardrobeModal.transform, uiSprite, PanelColor);
            SetRect(wardrobeWindow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(720f, 620f));

            var wardrobeTitle = CreateText("Title", wardrobeWindow.transform, "WARDROBE", 48f, TextAlignmentOptions.Center);
            SetRect(wardrobeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(500f, 80f));

            var wardrobeCloseImage = CreateImage("CloseButton", wardrobeWindow.transform, uiSprite, new Color(0.55f, 0.20f, 0.20f));
            SetRect(wardrobeCloseImage.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(80f, 80f));
            var wardrobeCloseButton = wardrobeCloseImage.gameObject.AddComponent<Button>();
            wardrobeCloseButton.targetGraphic = wardrobeCloseImage;
            var wardrobeCloseLabel = CreateText("Label", wardrobeCloseImage.transform, "X", 52f, TextAlignmentOptions.Center);
            StyleModal(wardrobeWindow, wardrobeTitle, wardrobeCloseImage, wardrobeCloseLabel);
            StretchFull(wardrobeCloseLabel.rectTransform);

            var hairRow = CreateWardrobeRow(wardrobeWindow.transform, uiSprite, -190f);
            var beardRow = CreateWardrobeRow(wardrobeWindow.transform, uiSprite, -330f);
            var shortsRow = CreateWardrobeRow(wardrobeWindow.transform, uiSprite, -470f);

            var wardrobePanel = wardrobeWindow.gameObject.AddComponent<WardrobePanel>();
            AssignRef(wardrobePanel, "_hairLabel", hairRow.Label);
            AssignRef(wardrobePanel, "_hairNext", hairRow.NextButton);
            AssignRef(wardrobePanel, "_beardLabel", beardRow.Label);
            AssignRef(wardrobePanel, "_beardNext", beardRow.NextButton);
            AssignRef(wardrobePanel, "_shortsLabel", shortsRow.Label);
            AssignRef(wardrobePanel, "_shortsNext", shortsRow.NextButton);

            var wardrobeModalControllerGo = new GameObject("WardrobeModalController");
            wardrobeModalControllerGo.transform.SetParent(root.transform, false);
            var wardrobeModalToggle = wardrobeModalControllerGo.AddComponent<ModalToggle>();
            AssignRef(wardrobeModalToggle, "_panel", wardrobeModal);
            AssignRef(wardrobeModalToggle, "_openButton", wardrobeOpenButton);
            AssignRef(wardrobeModalToggle, "_closeButton", wardrobeCloseButton);
            AssignRef(wardrobeModalToggle, "_backdropButton", wardrobeBackdropButton);

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

            // --- Daily reward popup (shown on launch when today's streak reward is available) ---
            var dailyPopupGo = new GameObject("DailyRewardPopup");
            dailyPopupGo.transform.SetParent(canvasGo.transform, false);
            var dailyPopup = dailyPopupGo.AddComponent<DailyRewardPopup>();

            var dailyPanel = CreateImage("Panel", dailyPopupGo.transform, uiSprite, new Color(0f, 0f, 0f, 0.85f));
            StretchFull(dailyPanel.rectTransform);

            var dailyMessage = CreateText("Message", dailyPanel.transform, string.Empty, 52f, TextAlignmentOptions.Center);
            SetRect(dailyMessage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(900f, 360f));

            var dailyClaimImage = CreateImage("ClaimButton", dailyPanel.transform, uiSprite, new Color(0.20f, 0.80f, 0.35f));
            SetRect(dailyClaimImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(420f, 120f));
            var dailyClaimButton = dailyClaimImage.gameObject.AddComponent<Button>();
            dailyClaimButton.targetGraphic = dailyClaimImage;
            var dailyClaimLabel = CreateText("Label", dailyClaimImage.transform, "CLAIM", 44f, TextAlignmentOptions.Center);
            StretchFull(dailyClaimLabel.rectTransform);

            AssignRef(dailyPopup, "_panel", dailyPanel.gameObject);
            AssignRef(dailyPopup, "_messageText", dailyMessage);
            AssignRef(dailyPopup, "_claimButton", dailyClaimButton);

            dailyPanel.gameObject.SetActive(false);

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
                // Re-serialize so the .asset on disk carries EVERY tunable. Fields added to
                // GameConfig after the asset was last written are absent from its YAML and fall
                // back to the C# initializer — which silently makes balance live in code instead
                // of in the asset (§4: data-driven). This writes the in-memory values, so any
                // value the designer already tuned is preserved, and it is a no-op once written.
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
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
            so.FindProperty("_icon").objectReferenceValue = LoadIcon(id);
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            // Reload the canonical, imported instance so it serializes as an asset reference.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
        }

        // Booster face: icon on top, countdown/label underneath.
        private static Image CreateBoosterIcon(Transform parent)
        {
            var icon = CreateImage("Icon", parent, null, new Color(1f, 1f, 1f, 0.92f));
            SetRect(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(84f, 84f));
            icon.preserveAspect = true;
            return icon;
        }

        // --- Palette -------------------------------------------------------------------------
        // One place, six colours. Before this there were sixteen arbitrary per-button colours,
        // which is why the HUD read as a pile of unrelated rectangles instead of one interface.
        // Meaning is carried by SHAPE and ICON; colour only separates surface from action.
        // Dark and flat. Surfaces are nearly black so the painted character and background carry
        // the colour; only the accent is saturated, and it marks the ONE primary action on screen.
        // No gradients and no shadows anywhere — depth is communicated by contrast, not by fake 3D.
        // The surfaces now CARRY their colour in the art (generated pixel-art chrome), so they are
        // tinted white — multiplying dark slate pixels by a dark tint would crush them to black.
        // The named colours below are still used for things that are drawn, not textured: fills,
        // dimmers and state accents.
        private static readonly Color PanelColor = Color.white;
        private static readonly Color ButtonColor = Color.white;
        private static readonly Color AccentColor = new Color(0.16f, 0.51f, 0.96f, 1f);
        private static readonly Color PositiveColor = new Color(0.14f, 0.72f, 0.44f, 1f);
        private static readonly Color DangerColor = new Color(0.86f, 0.28f, 0.24f, 1f);
        private static readonly Color IconTint = new Color(0.98f, 0.99f, 1f, 0.97f);

        // Was a circle. The round discs were the last piece of untouched default-Unity look in a
        // game that is otherwise entirely pixel art, so the icon buttons now sit on the generated
        // pixel plate (square, 2px outline, warm accent corners) instead.
        private static Sprite CircleSprite => UiKit("plate_pixel") ?? UiShape("circle") ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // Turns a HUD button into a round button whose ICON IS THE BUTTON: circular surface, big
        // centred glyph, no text. Text-in-a-rectangle is what made the old HUD look like a debug
        // menu. Buttons whose label carries live information (a countdown, a badge, a percentage)
        // keep it — those are set with keepLabel, and the label sits under the glyph.
        private static void MakeIconButton(Image buttonImage, TMP_Text label, string iconId, Color tint, float diameter, bool keepLabel = false)
        {
            buttonImage.sprite = CircleSprite;
            buttonImage.color = tint;
            ApplySlicing(buttonImage);

            RectTransform rt = buttonImage.rectTransform;
            rt.sizeDelta = new Vector2(diameter, diameter);

            Sprite sprite = LoadIcon(iconId);

            if (sprite == null)
            {
                Debug.LogWarning($"[CoreLoopSceneBootstrap] Missing icon 'icon_{iconId}.png' — button keeps its text.");
                return;
            }

            var icon = CreateImage("Icon", buttonImage.transform, sprite, IconTint);
            LayOutIconButton(icon, label, diameter, keepLabel);
        }

        // Same round-button layout, but for buttons whose icon sprite is filled in at runtime from
        // a data asset (boosters) rather than looked up here by id.
        private static void StyleRoundButton(Image buttonImage, Image icon, TMP_Text label, Color tint, float diameter, bool keepLabel)
        {
            buttonImage.sprite = CircleSprite;
            buttonImage.color = tint;
            ApplySlicing(buttonImage);
            buttonImage.rectTransform.sizeDelta = new Vector2(diameter, diameter);

            LayOutIconButton(icon, label, diameter, keepLabel);
        }

        private static void LayOutIconButton(Image icon, TMP_Text label, float diameter, bool keepLabel)
        {
            // Bumped from 0.42/0.56: at the old fractions the pixel-art icons were a small mark
            // floating in a large disc and the motif was unreadable on a phone. The icons carry the
            // meaning here — the label under them is a caption, not the primary cue.
            float glyph = diameter * (keepLabel ? 0.56f : 0.72f);
            SetRect(icon.rectTransform, new Vector2(0.5f, keepLabel ? 0.68f : 0.5f), Vector2.zero, new Vector2(glyph, glyph));
            icon.color = IconTint;
            icon.preserveAspect = true;
            icon.raycastTarget = false; // the button underneath must still receive the click

            if (label == null)
            {
                return;
            }

            if (!keepLabel)
            {
                label.gameObject.SetActive(false);
                return;
            }

            label.fontSize = diameter * 0.16f;
            SetRect(label.rectTransform, new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(diameter * 0.92f, diameter * 0.34f));
        }

        // Gives a modal the same chrome everywhere: panelled window, a header strip behind the
        // title so it reads as a title bar rather than floating text, and a round close button
        // carrying the kit's cross glyph instead of a red box with the letter "X" in it.
        private static void StyleModal(Image window, TMP_Text title, Image closeButton, TMP_Text closeLabel)
        {
            window.sprite = UiShape("panel") ?? window.sprite;
            ApplySlicing(window);
            window.type = Image.Type.Sliced;
            window.color = PanelColor;

            float width = window.rectTransform.sizeDelta.x;

            var header = CreateImage("Header", window.transform, UiShape("panel_soft"), new Color(1f, 1f, 1f, 0.06f));
            header.type = Image.Type.Sliced;
            SetRect(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(width - 24f, 96f));
            header.raycastTarget = false;
            header.transform.SetAsFirstSibling(); // behind the title, which already exists

            if (title != null)
            {
                title.transform.SetAsLastSibling();
            }

            if (closeButton == null)
            {
                return;
            }

            closeButton.sprite = CircleSprite;
            ApplySlicing(closeButton);
            closeButton.type = Image.Type.Simple;
            closeButton.color = new Color(1f, 1f, 1f, 0.10f);
            closeButton.rectTransform.sizeDelta = new Vector2(76f, 76f);

            if (closeLabel != null)
            {
                closeLabel.gameObject.SetActive(false);
            }

            Sprite cross = UiKit("icon_cross");

            if (cross != null)
            {
                var glyph = CreateImage("Glyph", closeButton.transform, cross, IconTint);
                SetRect(glyph.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
                glyph.preserveAspect = true;
                glyph.raycastTarget = false;
            }
        }

        // Primary action inside a modal (BUY / CLAIM / MOVE UP / NEXT): the kit's raised button.
        private static void StyleActionButton(Image buttonImage, Color tint)
        {
            buttonImage.sprite = UiShape("panel_soft") ?? buttonImage.sprite;
            ApplySlicing(buttonImage);
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = tint;
        }

        private const string UiKitFolder = "Assets/_Game/Art/UI/Kit";

        // 9-slice borders for the UI kit. Without them a 192x64 sprite stretched across a 720px
        // modal smears its rounded corners and border into mush — the corners must stay fixed and
        // only the middle may stretch. The depth-gradient button gets a taller BOTTOM border so
        // its raised edge survives; that edge is what makes it read as a button and not a label.
        // Only the cross glyph is still used from the kit — the panels and buttons it shipped all
        // had a shadow or a border painted into the pixels, which is exactly what we removed.
        private static void ConfigureUiKit()
        {
            SetSpriteBorder($"{UiKitFolder}/icon_cross.png", Vector4.zero);

            // Generated pixel-art chrome (PixelLab create_ui_asset, styled from the character art).
            // The border is what makes it survive being stretched: 9-slicing keeps the outline and
            // the accent corners at their drawn size and repeats only the flat middle. Without it a
            // 232x168 panel blown up to a 760x980 modal would smear its 2px outline into a 10px
            // gradient — which is exactly how baked-pixel UI usually goes wrong.
            SetPixelUi("panel_pixel", new Vector4(18f, 18f, 18f, 18f));
            SetPixelUi("button_pixel", new Vector4(16f, 16f, 16f, 16f));
            SetPixelUi("plate_pixel", new Vector4(14f, 14f, 14f, 14f));
            SetPixelUi("tab_pixel", new Vector4(12f, 12f, 12f, 12f));
            SetPixelUi("bar_pixel", new Vector4(14f, 14f, 14f, 14f));
        }

        private static void SetPixelUi(string name, Vector4 border)
        {
            string path = $"{UiKitFolder}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogWarning($"[CoreLoopSceneBootstrap] Pixel UI sprite missing: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Point; // pixel art: never smooth it
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // Re-imports every icon with the settings the art depends on, because a .meta outlives the
        // PNG it describes. The icons this replaced were 512x512 in Multiple mode with a baked
        // sub-rect of (17,19,478,476); dropping a 64x64 pixel-art PNG into that slot leaves Unity
        // cropping a rectangle that lies entirely outside the new texture — every icon silently
        // renders as nothing while the wiring still looks correct.
        // Point filtering matters just as much: these are pixel art drawn at 64x64 and blown up to
        // ~72-96 px buttons, so bilinear would smear them into the mush the migration was undoing.
        private static int ConfigureIconImporters()
        {
            string absoluteFolder = Path.Combine(Application.dataPath, IconFolder.Substring("Assets/".Length));

            if (!Directory.Exists(absoluteFolder))
            {
                Debug.LogWarning($"[CoreLoopSceneBootstrap] Icon folder missing: {IconFolder}");
                return 0;
            }

            int configured = 0;

            foreach (string file in Directory.GetFiles(absoluteFolder, "*.png", SearchOption.TopDirectoryOnly))
            {
                string assetPath = $"{IconFolder}/{Path.GetFileName(file)}";
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (importer == null)
                {
                    Debug.LogWarning($"[CoreLoopSceneBootstrap] Could not get TextureImporter for {assetPath}.");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                configured++;
            }

            return configured;
        }

        private static void SetSpriteBorder(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogWarning($"[CoreLoopSceneBootstrap] UI kit sprite missing: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // Maps the old flat-shape names onto the generated pixel-art kit, so every call site that
        // asked for "panel"/"panel_soft" gets pixel chrome without being rewritten. The generated
        // shapes stay as the fallback (and are still what fills and dimmers use, since a 9-sliced
        // rounded sprite stretched as a progress bar would draw its own corners mid-bar).
        private static Sprite UiShape(string name)
        {
            switch (name)
            {
                case "panel":
                    return UiKit("panel_pixel") ?? RawShape(name);
                case "panel_soft":
                    return UiKit("button_pixel") ?? RawShape(name);
                default:
                    return RawShape(name);
            }
        }

        private static Sprite RawShape(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/UI/Shapes/{name}.png");
        }

        private static Sprite UiKit(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{UiKitFolder}/{name}.png");
        }

        private const string IconFolder = "Assets/_Game/Art/UI/Icons";

        // Icons are matched to data by id (upgrade "chest" -> icon_chest.png), so adding an
        // upgrade or booster only means dropping in a matching png — no wiring, no code.
        // Missing icons are fine: the UI just hides the image.
        private static Sprite LoadIcon(string id)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{IconFolder}/icon_{id}.png");
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
            so.FindProperty("_icon").objectReferenceValue = LoadIcon(id);
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            // Reload the canonical, imported instance so it serializes as an asset reference.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<BoosterData>(path);
        }

        private const string LocationsFolder = "Assets/_Game/Data/Locations";

        private static LocationData GetOrCreateLocation(string id, string displayName, int totalLevels, float multiplier, string backgroundSpritePath)
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
            so.FindProperty("_backgroundSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(backgroundSpritePath);
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            // Reload the canonical, imported instance so it serializes as an asset reference.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<LocationData>(path);
        }

        private const string AchievementsFolder = "Assets/_Game/Data/Achievements";

        private static AchievementData GetOrCreateAchievement(string id, string displayName, int typeDeclarationIndex, double threshold, double rewardGains)
        {
            if (!AssetDatabase.IsValidFolder(AchievementsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Achievements");
            }

            string path = $"{AchievementsFolder}/{id}.asset";
            var achievement = AssetDatabase.LoadAssetAtPath<AchievementData>(path);
            if (achievement == null)
            {
                achievement = ScriptableObject.CreateInstance<AchievementData>();
                AssetDatabase.CreateAsset(achievement, path);
            }

            var so = new SerializedObject(achievement);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            // enumValueIndex is the enum's DECLARATION-ORDER index, not its underlying int value.
            so.FindProperty("_type").enumValueIndex = typeDeclarationIndex;
            so.FindProperty("_threshold").doubleValue = threshold;
            so.FindProperty("_rewardGains").doubleValue = rewardGains;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AchievementData>(path);
        }

        // Discovers "<body>_<suffix>1.png", "_<suffix>2.png", … next to the tier's static body
        // sprite and fills the given frame array with them, stopping at the first gap.
        private static void AssignFrames(SerializedObject tierSo, string propertyName, string bodySpritePath, string suffix)
        {
            SerializedProperty frames = tierSo.FindProperty(propertyName);

            if (frames == null || string.IsNullOrEmpty(bodySpritePath))
            {
                return;
            }

            string withoutExtension = bodySpritePath.Substring(0, bodySpritePath.Length - ".png".Length);
            var found = new System.Collections.Generic.List<Sprite>();

            for (int i = 1; ; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{withoutExtension}_{suffix}{i}.png");

                if (sprite == null)
                {
                    break;
                }

                found.Add(sprite);
            }

            frames.arraySize = found.Count;

            for (int i = 0; i < found.Count; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            }
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
            // Null when the body art already contains the head (see call site).
            so.FindProperty("_headSprite").objectReferenceValue = string.IsNullOrEmpty(headSpritePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>(headSpritePath);

            // Clips: body_tierN_idle1..N (breathing) and _work1..N (curls), alongside the static
            // pose. Missing files are simply skipped, so a tier with no clip holds its static sprite.
            AssignFrames(so, "_idleFrames", bodySpritePath, "idle");
            AssignFrames(so, "_workoutFrames", bodySpritePath, "work");
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

            // Per-tier cuts, if ShortsGenerator produced any for this id. Hair and beard have none:
            // the skull is near enough the same size at every tier, while the hips are not.
            SerializedProperty tierSprites = so.FindProperty("_tierSprites");

            if (tierSprites != null)
            {
                var found = new System.Collections.Generic.List<Sprite>();

                for (int tier = 1; tier <= MuscleTierCount; tier++)
                {
                    found.Add(AssetDatabase.LoadAssetAtPath<Sprite>($"{CharacterArtFolder}/{id}_tier{tier}.png"));
                }

                bool any = found.Exists(s => s != null);
                tierSprites.arraySize = any ? found.Count : 0;

                for (int i = 0; any && i < found.Count; i++)
                {
                    tierSprites.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
                }
            }

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

        // Sprites assigned after CreateImage need the same treatment it applies: a bordered sprite
        // is 9-slice chrome and has to be drawn Sliced, or the outline stretches.
        private static void ApplySlicing(Image image)
        {
            if (image == null || image.sprite == null)
            {
                return;
            }

            bool sliced = image.sprite.border.sqrMagnitude > 0f;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;

            if (sliced)
            {
                image.pixelsPerUnitMultiplier = 1f;
            }
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;

            // A sprite that declares a 9-slice border is pixel-art chrome and MUST be drawn Sliced;
            // left on Simple the border is ignored and Unity stretches the whole bitmap, smearing a
            // 2px outline into a soft gradient at modal size. Sprites without a border (fills,
            // dimmers, icons) keep Simple, which is what they need.
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

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

        private struct WardrobeRow
        {
            public TMP_Text Label;
            public Button NextButton;
        }

        // One wardrobe row: a label (left) describing the equipped item + a NEXT button (right).
        private static WardrobeRow CreateWardrobeRow(Transform window, Sprite uiSprite, float y)
        {
            var label = CreateText("RowLabel", window, string.Empty, 34f, TextAlignmentOptions.MidlineLeft);
            SetRect(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(-110f, y), new Vector2(380f, 100f));

            var nextImage = CreateImage("NextButton", window, uiSprite, new Color(0.20f, 0.45f, 0.55f));
            SetRect(nextImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(190f, y), new Vector2(200f, 100f));
            var nextButton = nextImage.gameObject.AddComponent<Button>();
            nextButton.targetGraphic = nextImage;
            var nextLabel = CreateText("Label", nextImage.transform, "NEXT ▶", 34f, TextAlignmentOptions.Center);
            StretchFull(nextLabel.rectTransform);

            return new WardrobeRow { Label = label, NextButton = nextButton };
        }
    }
}
