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

            // Placeholder upgrades (data-driven; tune values in the .asset inspectors later).
            var upgrades = new UpgradeData[]
            {
                GetOrCreateUpgrade("stronger_arms", "Stronger Arms", StatType.GainsPerRep, 1d, 10d, 1.10f),
                GetOrCreateUpgrade("protein_shake", "Protein Shake", StatType.GainsPerRep, 5d, 100d, 1.12f),
                GetOrCreateUpgrade("training_partner", "Training Partner", StatType.PassiveGainsPerSecond, 0.5d, 50d, 1.11f),
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

            // Self-check: verify the asset reference actually serialized (asset refs are
            // more timing-sensitive in batchmode than scene-object refs).
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

            // --- HUD controller ---
            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(root.transform, false);
            var hudController = hudGo.AddComponent<HudController>();

            AssignRef(hudController, "_gainsText", gainsText);
            AssignRef(hudController, "_energyFill", energyBar);
            AssignRef(hudController, "_energyText", energyText);
            AssignRef(hudController, "_passiveRateText", passiveRateText);

            // --- Upgrades: "UPGRADES" open button on the HUD + a modal with the upgrade buttons ---
            var openBtnImage = CreateImage("UpgradesOpenButton", canvasGo.transform, uiSprite, new Color(0.18f, 0.30f, 0.45f));
            SetRect(openBtnImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(460f, 130f));
            var openButton = openBtnImage.gameObject.AddComponent<Button>();
            openButton.targetGraphic = openBtnImage;
            var openLabel = CreateText("Label", openBtnImage.transform, "UPGRADES", 46f, TextAlignmentOptions.Center);
            SetRect(openLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 130f));

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

            for (int i = 0; i < upgrades.Length; i++)
            {
                var btnImage = CreateImage("UpgradeBtn_" + upgrades[i].Id, window.transform, uiSprite, new Color(0.18f, 0.30f, 0.45f));
                SetRect(btnImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f - i * 180f), new Vector2(640f, 150f));
                var button = btnImage.gameObject.AddComponent<Button>();
                button.targetGraphic = btnImage;
                var buttonLabel = CreateText("Label", btnImage.transform, string.Empty, 36f, TextAlignmentOptions.Center);
                SetRect(buttonLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 150f));

                var upgradeButton = btnImage.gameObject.AddComponent<UpgradeButton>();
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

            AssignRef(popup, "_panel", panel.gameObject);
            AssignRef(popup, "_messageText", offlineMessage);
            AssignRef(popup, "_claimButton", claimButton);

            // Hidden by default in the scene too (runtime Awake also hides it).
            panel.gameObject.SetActive(false);

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

        // The fields this builds wires (_gameConfig, _gainsText, _energyFill, _energyText)
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
