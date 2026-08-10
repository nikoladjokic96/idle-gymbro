using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IdleGymBro.Character;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Economy;
using IdleGymBro.Gameplay;
using IdleGymBro.Meta;
using IdleGymBro.Progression;

namespace IdleGymBro.EditorTools
{
    // Headless runtime verification of the live system graph WITHOUT entering Play mode.
    //
    // Why reflection instead of Play mode: edit-mode AddComponent never fires Unity's lifecycle
    // callbacks, so this rig invokes Awake/OnEnable/Start explicitly. That is the point rather
    // than a workaround -- SaveSystem restores ISaveables in the arbitrary order FindObjectsByType
    // returns, and only an explicit driver can force BOTH orders and prove the state converges.
    //
    // Nothing here touches persistentDataPath: the save round-trip goes through the static
    // Serialize/Encrypt helpers in memory, so a developer's real save is never read or overwritten.
    //
    // Run: menu IdleGymBro -> Test Systems Runtime, or
    // -executeMethod IdleGymBro.EditorTools.SystemsSmokeTest.RunAll
    public static class SystemsSmokeTest
    {
        private const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";
        private const string UpgradesFolder = "Assets/_Game/Data/Upgrades";
        private const string LocationsFolder = "Assets/_Game/Data/Locations";
        private const string TiersFolder = "Assets/_Game/Data/MuscleTiers";
        private const string CosmeticsFolder = "Assets/_Game/Data/Cosmetics";
        private const string AchievementsFolder = "Assets/_Game/Data/Achievements";

        private static readonly List<string> _failures = new List<string>();
        private static int _checks;

        [MenuItem("IdleGymBro/Test Systems Runtime")]
        public static void RunAll()
        {
            _failures.Clear();
            _checks = 0;

            // Foreign GameSystems in an open scene would be picked up by the FindAnyObjectByType
            // calls inside UpgradeManager/LocationManager Start, so run against an empty scene.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[SystemsSmokeTest] Cancelled by user.");
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Each scenario is isolated: one throwing test must not hide the results of the rest.
            Run("T1 cold start", TestColdStart);
            Run("T2 upgrade purchase", TestUpgradePurchase);
            Run("T3 location advance", TestLocationAdvance);
            Run("T4 prestige resets run", TestPrestigeResetsRun);
            Run("T5 prestige no double grant", TestPrestigeCannotDoubleGrant);
            Run("T6 prestige save round-trip", TestPrestigeSurvivesSaveInBothRestoreOrders);
            Run("T7 wardrobe cycle", TestWardrobeCycleDrivesRenderer);
            Run("T8 wardrobe save round-trip", TestWardrobeSurvivesSave);
            Run("T9 wardrobe dangling id", TestWardrobeRecoversFromDanglingId);
            Run("T10 offline earnings scale", TestOfflineEarningsScaleWithEffectiveRate);
            Run("T11 achievements vs prestige", TestAchievementProgressSurvivesPrestige);
            Run("T12 idle clip per tier", TestEveryTierHasAnIdleClip);

            EventBus.Clear();

            if (_failures.Count == 0)
            {
                Debug.Log($"[SystemsSmokeTest] PASS — {_checks} checks, 0 failures.");
            }
            else
            {
                Debug.LogError($"[SystemsSmokeTest] FAIL — {_checks} checks, {_failures.Count} failures:\n  - " +
                    string.Join("\n  - ", _failures));
            }
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
            }
            catch (Exception e)
            {
                _checks++;
                _failures.Add($"{name} THREW: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                EventBus.Clear();
            }
        }

        // ---------------------------------------------------------------- tests

        private static void TestColdStart()
        {
            using var rig = Rig.Build();
            var probe = rig.Boot();

            CheckApprox(probe.GainsPerRep, rig.Config.GainsPerRep, 1e-9, "T1 cold-start gains/rep == config base");
            CheckApprox(probe.PassivePerSecond, rig.Config.BasePassiveGainsPerSecond, 1e-9, "T1 cold-start passive == config base");
            CheckApprox(rig.Energy.CurrentEnergy, rig.Config.MaxEnergy, 1e-4, "T1 cold-start energy full");
            Check(rig.Locations.CurrentIndex == 0, "T1 cold-start location index 0");
            Check(rig.Upgrades.TotalLevels == 0, "T1 cold-start no upgrade levels");
            CheckApprox(probe.PrestigeMultiplier, 1d, 1e-9, "T1 cold-start prestige multiplier 1x");
        }

        private static void TestUpgradePurchase()
        {
            using var rig = Rig.Build();
            var probe = rig.Boot();

            UpgradeData chest = rig.Upgrades.GetUpgrade("chest");
            if (!Check(chest != null, "T2 'chest' upgrade asset exists"))
            {
                return;
            }

            Grant(1e9);
            double gainsBefore = rig.Currency.TotalGains;
            double earnedBefore = rig.Currency.TotalEarned;
            double gprBefore = probe.GainsPerRep;
            double cost = rig.Upgrades.GetCost("chest");

            Check(rig.Upgrades.TryBuy("chest"), "T2 buy succeeds when affordable");
            CheckApprox(rig.Currency.TotalGains, gainsBefore - cost, 1e-6, "T2 cost deducted from balance");
            CheckApprox(rig.Currency.TotalEarned, earnedBefore, 1e-6, "T2 spending never reduces lifetime earned");
            Check(rig.Upgrades.GetLevel("chest") == 1, "T2 level incremented to 1");
            CheckApprox(probe.GainsPerRep, gprBefore + chest.EffectPerLevel, 1e-6, "T2 stat effect applied");

            // Second purchase must cost baseCost * growth^1.
            CheckApprox(rig.Upgrades.GetCost("chest"), chest.BaseCost * Math.Pow(chest.GrowthRate, 1), 1e-6,
                "T2 cost scales by growth rate");
        }

        private static void TestLocationAdvance()
        {
            using var rig = Rig.Build();
            var probe = rig.Boot();

            LocationData home = rig.Locations.GetLocation(0);
            LocationData next = rig.Locations.GetLocation(1);
            if (!Check(home != null && next != null, "T3 at least two locations configured"))
            {
                return;
            }

            Grant(1e15);
            BuyLevels(rig, "chest", home.TotalLevelsToComplete);

            Check(rig.Upgrades.TotalLevels >= home.TotalLevelsToComplete, "T3 reached location-1 level threshold");
            Check(probe.CanAdvanceLocation, "T3 progress reports advance available");
            Check(rig.Locations.TryAdvance(), "T3 TryAdvance succeeds at 100%");
            Check(rig.Locations.CurrentIndex == 1, "T3 moved to location 2");

            double expectedGpr = (rig.Config.GainsPerRep + rig.Upgrades.GetUpgrade("chest").EffectPerLevel * rig.Upgrades.GetLevel("chest"))
                * next.GlobalMultiplier;
            CheckApprox(probe.GainsPerRep, expectedGpr, Math.Abs(expectedGpr) * 1e-9 + 1e-6,
                "T3 location multiplier applied to gains/rep");
        }

        private static void TestPrestigeResetsRun()
        {
            using var rig = Rig.Build();
            var probe = rig.Boot();

            Grant(1e8); // respect = floor(factor * sqrt(1e8)) = 10000 at factor 1
            BuyLevels(rig, "chest", 3);

            double expectedRespect = Math.Floor(rig.Config.PrestigeRespectFactor * Math.Sqrt(rig.Currency.TotalEarned));
            Check(probe.CanPrestige, "T4 prestige available after a big run");
            CheckApprox(probe.PendingRespect, expectedRespect, 1e-6, "T4 pending respect == factor*sqrt(earned)");

            Check(rig.Prestige.DoPrestige(), "T4 DoPrestige succeeds");

            CheckApprox(rig.Currency.TotalGains, 0d, 1e-9, "T4 balance reset to 0");
            CheckApprox(rig.Currency.TotalEarned, 0d, 1e-9, "T4 lifetime earned reset to 0");
            Check(rig.Upgrades.TotalLevels == 0, "T4 upgrade levels wiped");
            Check(rig.Locations.CurrentIndex == 0, "T4 location back to first");
            CheckApprox(rig.Energy.CurrentEnergy, rig.Config.MaxEnergy, 1e-4, "T4 energy refilled");
            CheckApprox(probe.TotalRespect, expectedRespect, 1e-6, "T4 respect banked");

            double expectedMultiplier = 1d + expectedRespect * rig.Config.PrestigeMultiplierPerRespect;
            CheckApprox(probe.PrestigeMultiplier, expectedMultiplier, expectedMultiplier * 1e-9 + 1e-9,
                "T4 prestige multiplier == 1 + respect*perRespect");

            // The decisive ordering check: the LAST StatsChangedEvent of the prestige turn must
            // already carry the new multiplier, not the pre-prestige one.
            double expectedGpr = rig.Config.GainsPerRep * expectedMultiplier;
            CheckApprox(probe.GainsPerRep, expectedGpr, expectedGpr * 1e-9 + 1e-9,
                "T4 final stats carry the NEW prestige multiplier (no stale recompute)");
        }

        private static void TestPrestigeCannotDoubleGrant()
        {
            using var rig = Rig.Build();
            var probe = rig.Boot();

            Grant(1e8);
            Check(rig.Prestige.DoPrestige(), "T5 first prestige succeeds");
            double respectAfterFirst = probe.TotalRespect;

            Check(!rig.Prestige.DoPrestige(), "T5 immediate second prestige is rejected");
            CheckApprox(probe.TotalRespect, respectAfterFirst, 1e-9, "T5 respect not granted twice");
            Check(!probe.CanPrestige, "T5 panel reports prestige unavailable right after reset");
        }

        private static void TestPrestigeSurvivesSaveInBothRestoreOrders()
        {
            double gprPrestigeFirst;
            double gprPrestigeLast;
            SaveData reloaded;

            using (var rig = Rig.Build())
            {
                rig.Boot();
                Grant(1e8);
                BuyLevels(rig, "chest", 2);
                rig.Prestige.DoPrestige();
                Grant(5e4);
                BuyLevels(rig, "chest", 1);

                reloaded = RoundTrip(rig.Capture());
            }

            if (!Check(reloaded != null, "T6 save round-trip returned data"))
            {
                return;
            }

            using (var rig = Rig.Build())
            {
                var probe = rig.Boot();
                rig.Restore(reloaded, prestigeFirst: true);
                gprPrestigeFirst = probe.GainsPerRep;
                CheckApprox(probe.TotalRespect, reloaded.TotalRespect, 1e-9, "T6 respect restored");

                int savedChestLevel = reloaded.UpgradeLevels != null && reloaded.UpgradeLevels.TryGetValue("chest", out int lvl) ? lvl : -1;
                Check(savedChestLevel > 0 && rig.Upgrades.GetLevel("chest") == savedChestLevel,
                    "T6 upgrade levels restored", $"saved={savedChestLevel}, restored={rig.Upgrades.GetLevel("chest")}");
            }

            using (var rig = Rig.Build())
            {
                var probe = rig.Boot();
                rig.Restore(reloaded, prestigeFirst: false);
                gprPrestigeLast = probe.GainsPerRep;
            }

            CheckApprox(gprPrestigeLast, gprPrestigeFirst, Math.Abs(gprPrestigeFirst) * 1e-9 + 1e-9,
                "T6 restore converges regardless of ISaveable order");
            Check(gprPrestigeFirst > 1d, "T6 restored gains/rep reflects the banked prestige multiplier",
                $"got {gprPrestigeFirst:G17}");
        }

        private static void TestWardrobeCycleDrivesRenderer()
        {
            using var rig = Rig.Build();
            rig.Boot();

            List<CosmeticData> hairOptions = rig.Wardrobe.GetCosmeticsForLayer(CharacterLayer.Hair);
            if (!Check(hairOptions.Count >= 2, "T7 at least two hair cosmetics exist"))
            {
                return;
            }

            SpriteRenderer hairRenderer = rig.LayerRenderer(CharacterLayer.Hair);
            if (!Check(hairRenderer != null, "T7 character has a Hair layer renderer"))
            {
                return;
            }

            string before = rig.Wardrobe.GetEquippedId(CharacterLayer.Hair);
            Check(!string.IsNullOrEmpty(before), "T7 a default hair is equipped on boot");
            Check(hairRenderer.sprite != null, "T7 default hair sprite reached the renderer");

            rig.Wardrobe.CycleLayer(CharacterLayer.Hair);
            string after = rig.Wardrobe.GetEquippedId(CharacterLayer.Hair);

            Check(after != before, "T7 CycleLayer moves to a different hair");
            CosmeticData expected = hairOptions.First(c => c.Id == after);
            Check(hairRenderer.sprite == expected.Sprite, "T7 renderer shows the newly equipped sprite");

            // Full wrap must return to the starting option and never stall.
            for (int i = 1; i < hairOptions.Count; i++)
            {
                rig.Wardrobe.CycleLayer(CharacterLayer.Hair);
            }

            Check(rig.Wardrobe.GetEquippedId(CharacterLayer.Hair) == before, "T7 cycling wraps back to the first option");
        }

        private static void TestWardrobeSurvivesSave()
        {
            SaveData reloaded;
            string expectedHair;
            string expectedShorts;

            using (var rig = Rig.Build())
            {
                rig.Boot();
                rig.Wardrobe.CycleLayer(CharacterLayer.Hair);
                rig.Wardrobe.CycleLayer(CharacterLayer.Shorts);
                expectedHair = rig.Wardrobe.GetEquippedId(CharacterLayer.Hair);
                expectedShorts = rig.Wardrobe.GetEquippedId(CharacterLayer.Shorts);
                reloaded = RoundTrip(rig.Capture());
            }

            using (var rig = Rig.Build())
            {
                rig.Boot();
                rig.Restore(reloaded, prestigeFirst: true);

                Check(rig.Wardrobe.GetEquippedId(CharacterLayer.Hair) == expectedHair, "T8 hair choice survives save/load");
                Check(rig.Wardrobe.GetEquippedId(CharacterLayer.Shorts) == expectedShorts, "T8 shorts choice survives save/load");

                SpriteRenderer hairRenderer = rig.LayerRenderer(CharacterLayer.Hair);
                CosmeticData expected = rig.Wardrobe.GetCosmeticsForLayer(CharacterLayer.Hair).First(c => c.Id == expectedHair);
                Check(hairRenderer != null && hairRenderer.sprite == expected.Sprite, "T8 restored sprite re-applied to renderer");
            }
        }

        private static void TestWardrobeRecoversFromDanglingId()
        {
            using var rig = Rig.Build();
            rig.Boot();

            // Simulates a cosmetic asset renamed or removed after the player's save was written.
            var data = new SaveData
            {
                EquippedCosmetics = new Dictionary<string, string>
                {
                    { CharacterLayer.Hair.ToString(), "hair_that_no_longer_exists" }
                }
            };

            rig.Restore(data, prestigeFirst: true);

            string equipped = rig.Wardrobe.GetEquippedId(CharacterLayer.Hair);
            List<CosmeticData> options = rig.Wardrobe.GetCosmeticsForLayer(CharacterLayer.Hair);

            Check(options.Any(c => c.Id == equipped),
                "T9 dangling cosmetic id falls back to a real option",
                $"equipped='{equipped}'");

            SpriteRenderer hairRenderer = rig.LayerRenderer(CharacterLayer.Hair);
            Check(hairRenderer != null && hairRenderer.sprite != null,
                "T9 character is not left with an empty hair layer");

            // The player must not be stuck: cycling has to reach a valid option.
            rig.Wardrobe.CycleLayer(CharacterLayer.Hair);
            Check(options.Any(c => c.Id == rig.Wardrobe.GetEquippedId(CharacterLayer.Hair)),
                "T9 cycling from a dangling id lands on a real option");
        }

        private static void TestOfflineEarningsScaleWithEffectiveRate()
        {
            using var rig = Rig.Build();
            var probe = rig.Boot();

            Grant(1e12);
            BuyLevels(rig, "training_partner", 5);
            BuyLevels(rig, "gym_membership", 3);

            double effectiveRate = probe.PassivePerSecond;
            Check(effectiveRate > rig.Config.BasePassiveGainsPerSecond,
                "T10 passive rate actually grew past the config base");

            const double secondsAway = 3600d;
            double before = rig.Currency.TotalGains;
            probe.OfflineGrant = 0d;

            EventBus.Publish(new GameLoadedEvent(true, DateTime.UtcNow.Ticks - (long)(secondsAway * TimeSpan.TicksPerSecond)));

            double capped = Math.Min(secondsAway, rig.Config.OfflineCapSeconds);
            double expected = capped * effectiveRate * rig.Config.OfflineEfficiency;

            CheckApprox(probe.OfflineGrant, expected, expected * 1e-6 + 1e-6,
                "T10 offline earnings use the EFFECTIVE passive rate (upgrades/location/prestige)");
            CheckApprox(rig.Currency.TotalGains - before, expected, expected * 1e-6 + 1e-6,
                "T10 offline gains actually credited");
        }

        private static void TestAchievementProgressSurvivesPrestige()
        {
            using var rig = Rig.Build();
            rig.Boot();

            AchievementData earnedAchievement = null;

            for (int i = 0; i < rig.Achievements.Count; i++)
            {
                AchievementData candidate = rig.Achievements.GetData(i);
                if (candidate != null && candidate.Type == AchievementType.TotalGainsEarned)
                {
                    if (earnedAchievement == null || candidate.Threshold < earnedAchievement.Threshold)
                    {
                        earnedAchievement = candidate;
                    }
                }
            }

            if (!Check(earnedAchievement != null, "T11 a TotalGainsEarned achievement exists"))
            {
                return;
            }

            Grant(earnedAchievement.Threshold * 2d);
            Check(rig.Achievements.IsClaimable(earnedAchievement), "T11 achievement claimable after earning past threshold");

            rig.Prestige.DoPrestige();

            // Achievements are lifetime meta progression (CLAUDE.md §12): a prestige resets the RUN,
            // not the player's record. Losing it here would silently un-earn a completed goal.
            Check(rig.Achievements.IsClaimable(earnedAchievement),
                "T11 unclaimed achievement stays claimable across prestige",
                $"progress={rig.Achievements.GetProgress(earnedAchievement)} threshold={earnedAchievement.Threshold}");
        }

        // The idle animation is data, not code: a renamed or missing frame file silently degrades
        // the character back to a static pose with nothing failing loudly. This pins the wiring.
        private static void TestEveryTierHasAnIdleClip()
        {
            MuscleTierData[] tiers = LoadAll<MuscleTierData>(TiersFolder).OrderBy(t => t.TotalEarnedThreshold).ToArray();

            if (!Check(tiers.Length > 0, "T12 muscle tiers exist"))
            {
                return;
            }

            foreach (MuscleTierData tier in tiers)
            {
                Check(tier.BodySprite != null, $"T12 {tier.name}: has a static body sprite");
                Check(tier.IdleFrames != null && tier.IdleFrames.Length >= 1,
                    $"T12 {tier.name}: has idle frames",
                    $"got {(tier.IdleFrames == null ? 0 : tier.IdleFrames.Length)}");

                if (tier.IdleFrames == null)
                {
                    continue;
                }

                foreach (Sprite frame in tier.IdleFrames)
                {
                    Check(frame != null, $"T12 {tier.name}: no null frame in the clip");

                    // Every frame must share the static pose's canvas, or the character jumps
                    // between frames and the static cosmetic layers stop lining up.
                    if (frame != null && tier.BodySprite != null)
                    {
                        Check(Mathf.Approximately(frame.rect.width, tier.BodySprite.rect.width) &&
                              Mathf.Approximately(frame.rect.height, tier.BodySprite.rect.height),
                            $"T12 {tier.name}: frame '{frame.name}' matches the body canvas",
                            $"{frame.rect.width}x{frame.rect.height} vs {tier.BodySprite.rect.width}x{tier.BodySprite.rect.height}");

                        Check(Mathf.Approximately(frame.pixelsPerUnit, tier.BodySprite.pixelsPerUnit),
                            $"T12 {tier.name}: frame '{frame.name}' matches the body PPU",
                            $"{frame.pixelsPerUnit} vs {tier.BodySprite.pixelsPerUnit}");
                    }
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void Grant(double amount)
        {
            EventBus.Publish(new GainsEarnedEvent(amount));
        }

        private static void BuyLevels(Rig rig, string upgradeId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!rig.Upgrades.TryBuy(upgradeId))
                {
                    _failures.Add($"helper BuyLevels: '{upgradeId}' purchase {i + 1}/{count} failed (insufficient gains or missing asset)");
                    return;
                }
            }
        }

        private static SaveData RoundTrip(SaveData data)
        {
            return SaveSystem.Deserialize(SaveSystem.Decrypt(SaveSystem.Encrypt(SaveSystem.Serialize(data))));
        }

        private static bool Check(bool condition, string label, string detail = "")
        {
            _checks++;

            if (!condition)
            {
                _failures.Add(label + (string.IsNullOrEmpty(detail) ? string.Empty : "  [" + detail + "]"));
            }

            return condition;
        }

        private static void CheckApprox(double actual, double expected, double tolerance, string label)
        {
            Check(Math.Abs(actual - expected) <= tolerance, label, $"expected {expected:G17}, got {actual:G17}");
        }

        private static void Call(object target, string method)
        {
            MethodInfo mi = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            mi?.Invoke(target, null);
        }

        private static void SetField(object target, string field, object value)
        {
            FieldInfo fi = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (fi == null)
            {
                throw new InvalidOperationException($"SystemsSmokeTest: field '{field}' not found on {target.GetType().Name} (renamed?).");
            }

            fi.SetValue(target, value);
        }

        private static T[] LoadAll<T>(string folder) where T : ScriptableObject
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .ToArray();
        }

        // ---------------------------------------------------------------- probe

        // Captures the last value of every event the assertions care about, so tests read
        // published state (what the UI would show) rather than poking at private fields.
        private sealed class Probe : IDisposable
        {
            public double GainsPerRep = double.NaN;
            public double PassivePerSecond = double.NaN;
            public double PrestigeMultiplier = 1d;
            public double PendingRespect;
            public double TotalRespect;
            public bool CanPrestige;
            public bool CanAdvanceLocation;
            public double OfflineGrant;

            private readonly Action<StatsChangedEvent> _stats;
            private readonly Action<PassiveIncomeChangedEvent> _passive;
            private readonly Action<PrestigeMultiplierChangedEvent> _prestigeMul;
            private readonly Action<PrestigeStateChangedEvent> _prestigeState;
            private readonly Action<LocationProgressChangedEvent> _locationProgress;
            private readonly Action<OfflineProgressEvent> _offline;

            public Probe()
            {
                _stats = e => { GainsPerRep = e.GainsPerRep; PassivePerSecond = e.PassiveGainsPerSecond; };
                _passive = e => PassivePerSecond = e.GainsPerSecond;
                _prestigeMul = e => PrestigeMultiplier = e.Multiplier;
                _prestigeState = e => { CanPrestige = e.CanPrestige; PendingRespect = e.PendingRespect; TotalRespect = e.TotalRespect; };
                _locationProgress = e => CanAdvanceLocation = e.CanAdvance;
                _offline = e => OfflineGrant += e.GainsEarned;

                EventBus.Subscribe(_stats);
                EventBus.Subscribe(_passive);
                EventBus.Subscribe(_prestigeMul);
                EventBus.Subscribe(_prestigeState);
                EventBus.Subscribe(_locationProgress);
                EventBus.Subscribe(_offline);
            }

            public void Dispose()
            {
                EventBus.Unsubscribe(_stats);
                EventBus.Unsubscribe(_passive);
                EventBus.Unsubscribe(_prestigeMul);
                EventBus.Unsubscribe(_prestigeState);
                EventBus.Unsubscribe(_locationProgress);
                EventBus.Unsubscribe(_offline);
            }
        }

        // ---------------------------------------------------------------- rig

        private sealed class Rig : IDisposable
        {
            public GameConfig Config;
            public CurrencyManager Currency;
            public UpgradeManager Upgrades;
            public EnergySystem Energy;
            public PassiveIncomeSystem Passive;
            public LocationManager Locations;
            public PrestigeManager Prestige;
            public AchievementManager Achievements;
            public WardrobeManager Wardrobe;
            public CharacterBuilder Character;
            public OfflineEarningsSystem Offline;
            public DailyRewardManager Daily;

            private GameObject _root;
            private GameObject _characterRoot;
            private Probe _probe;
            private readonly List<MonoBehaviour> _all = new List<MonoBehaviour>();

            public static Rig Build()
            {
                EventBus.Clear();

                var rig = new Rig
                {
                    Config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath)
                };

                if (rig.Config == null)
                {
                    throw new InvalidOperationException("SystemsSmokeTest: GameConfig.asset not found at " + ConfigPath);
                }

                // Deliberately NO HideFlags: FindObjectsByType skips hidden objects, which would
                // leave UpgradeManager._currency / LocationManager._upgrades null (their Start uses
                // FindAnyObjectByType) and silently break every purchase in the suite.
                rig._root = new GameObject("SmokeRig_Systems");
                rig._characterRoot = new GameObject("SmokeRig_Character");

                rig.Currency = rig.Add<CurrencyManager>();
                rig.Upgrades = rig.Add<UpgradeManager>();
                rig.Energy = rig.Add<EnergySystem>();
                rig.Passive = rig.Add<PassiveIncomeSystem>();
                rig.Locations = rig.Add<LocationManager>();
                rig.Prestige = rig.Add<PrestigeManager>();
                rig.Achievements = rig.Add<AchievementManager>();
                rig.Wardrobe = rig.Add<WardrobeManager>();
                rig.Offline = rig.Add<OfflineEarningsSystem>();
                rig.Daily = rig.Add<DailyRewardManager>();

                // The character lives on its own object so its runtime-built Layer_* children
                // do not get mixed into the systems object.
                rig.Character = rig._characterRoot.AddComponent<CharacterBuilder>();
                rig._all.Add(rig.Character);

                foreach (MonoBehaviour mb in rig._all)
                {
                    if (mb.GetType().GetField("_gameConfig", BindingFlags.Instance | BindingFlags.NonPublic) != null)
                    {
                        SetField(mb, "_gameConfig", rig.Config);
                    }
                }

                SetField(rig.Upgrades, "_upgrades", LoadAll<UpgradeData>(UpgradesFolder));
                SetField(rig.Locations, "_locations", LoadAll<LocationData>(LocationsFolder)
                    .OrderBy(l => l.TotalLevelsToComplete).ToArray());
                SetField(rig.Character, "_tiers", LoadAll<MuscleTierData>(TiersFolder)
                    .OrderBy(t => t.TotalEarnedThreshold).ToArray());
                SetField(rig.Wardrobe, "_cosmetics", LoadAll<CosmeticData>(CosmeticsFolder)
                    .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray());
                SetField(rig.Achievements, "_achievements", LoadAll<AchievementData>(AchievementsFolder));

                return rig;
            }

            private T Add<T>() where T : MonoBehaviour
            {
                T component = _root.AddComponent<T>();
                _all.Add(component);
                return component;
            }

            // Drives Unity's lifecycle by hand. EventBus is cleared between Awake and OnEnable so
            // an editor that ever DID auto-invoke callbacks cannot leave duplicate subscriptions.
            public Probe Boot()
            {
                foreach (MonoBehaviour mb in _all)
                {
                    Call(mb, "Awake");
                }

                EventBus.Clear();

                foreach (MonoBehaviour mb in _all)
                {
                    Call(mb, "OnEnable");
                }

                _probe = new Probe();

                foreach (MonoBehaviour mb in _all)
                {
                    Call(mb, "Start");
                }

                return _probe;
            }

            public IEnumerable<ISaveable> Saveables => _all.OfType<ISaveable>();

            public SaveData Capture()
            {
                var data = new SaveData();

                foreach (ISaveable saveable in Saveables)
                {
                    saveable.CaptureState(data);
                }

                return data;
            }

            // SaveSystem restores in the arbitrary order FindObjectsByType returns; both extremes
            // are exercised so a convergence bug cannot hide behind one lucky ordering.
            public void Restore(SaveData data, bool prestigeFirst)
            {
                List<ISaveable> ordered = Saveables.ToList();

                ordered.Sort((a, b) =>
                {
                    int ra = a is PrestigeManager ? 0 : 1;
                    int rb = b is PrestigeManager ? 0 : 1;
                    return prestigeFirst ? ra.CompareTo(rb) : rb.CompareTo(ra);
                });

                foreach (ISaveable saveable in ordered)
                {
                    saveable.RestoreState(data);
                }
            }

            public SpriteRenderer LayerRenderer(CharacterLayer layer)
            {
                Transform child = _characterRoot.transform.Find("Layer_" + layer);
                return child != null ? child.GetComponent<SpriteRenderer>() : null;
            }

            public void Dispose()
            {
                _probe?.Dispose();

                foreach (MonoBehaviour mb in _all)
                {
                    Call(mb, "OnDisable");
                }

                if (_root != null)
                {
                    UnityEngine.Object.DestroyImmediate(_root);
                }

                if (_characterRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(_characterRoot);
                }

                EventBus.Clear();
            }
        }
    }
}
