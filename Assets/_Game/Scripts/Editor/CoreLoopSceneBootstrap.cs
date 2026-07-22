using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
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

        [MenuItem("IdleGymBro/Build Core Loop Scene")]
        public static void BuildCoreLoopScene()
        {
            // Import any on-disk assets that aren't yet in the AssetDatabase, so an
            // existing GameConfig.asset resolves to a real, referenceable asset.
            AssetDatabase.Refresh();

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

            var root = new GameObject(RootName);

            // --- Systems ---
            var gameSystems = new GameObject("GameSystems");
            gameSystems.transform.SetParent(root.transform, false);

            var gameManager = gameSystems.AddComponent<GameManager>();
            var tickSystem = gameSystems.AddComponent<TickSystem>();
            var energySystem = gameSystems.AddComponent<EnergySystem>();
            var currencyManager = gameSystems.AddComponent<CurrencyManager>();
            var tapController = gameSystems.AddComponent<TapController>();

            AssignRef(gameManager, "_gameConfig", config);
            AssignRef(tickSystem, "_gameConfig", config);
            AssignRef(energySystem, "_gameConfig", config);
            AssignRef(currencyManager, "_gameConfig", config);
            AssignRef(tapController, "_gameConfig", config);

            // Self-check: verify the asset reference actually serialized (asset refs are
            // more timing-sensitive in batchmode than scene-object refs).
            var systems = new Component[] { gameManager, tickSystem, energySystem, currencyManager, tapController };
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

            // No EventSystem needed here - input is read directly via the Input System
            // (TapController polls Pointer.current), not through UI button events.

            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // --- Placeholder character ---
            var placeholder = CreateImage("Placeholder", canvasGo.transform, uiSprite, new Color(0.90f, 0.49f, 0.13f));
            SetRect(placeholder.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 400f));
            placeholder.gameObject.AddComponent<PlaceholderCharacter>();

            // --- Gains text ---
            var gainsText = CreateText("GainsText", canvasGo.transform, "0", 80f, TextAlignmentOptions.Center);
            SetRect(gainsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(700f, 120f));

            // --- Energy bar ---
            var energyBarBg = CreateImage("EnergyBarBG", canvasGo.transform, uiSprite, new Color(0.15f, 0.15f, 0.15f));
            SetRect(energyBarBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(700f, 60f));

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
    }
}
