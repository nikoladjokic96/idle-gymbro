# Idle GymBro — Dev Log

> Detaljna istorija nalога: šta je rađeno, kako je verifikovano i koji su problemi
> usput rešeni. Kompaktan status živi u [CLAUDE.md §17](../CLAUDE.md#17-trenutni-status);
> ovde je puna priča. Najnoviji unosi na dnu.

---

## Poznati gotchas (naučeno na ovom projektu — proveri pre nego što ponovo udariš u isto)

| Gotcha | Rešenje |
|---|---|
| `.ps1` bez UTF-8 BOM → PowerShell 5.1 čita kao ANSI, lomi ć/š/ž i here-stringove | Snimaj skripte kao **UTF-8 with BOM** |
| Unity Hub CLI: `install` ne prepoznaje svežu LTS zakrpu | Dodaj `--changeset` (iz `ProjectVersion.txt` → `m_EditorVersionWithRevision`) |
| Unity Hub CLI vraća non-zero exit i posle uspešne instalacije | Ne veruj exit code-u — verifikuj `Unity.exe`/`AndroidPlayer` na disku |
| `EditorSceneManager.OpenScene(Single)` invalidira asset-reference učitane PRE poziva | Učitavaj assete POSLE OpenScene |
| Prvi batchmode posle dodavanja novih skripti ume samo da kompajlira (ne izvrši `-executeMethod`) | Pokreni batchmode ponovo — drugi run izvršava metodu |
| Editor i batchmode ne mogu istovremeno (project lock → "another Unity instance") | Zatvori editor pre batchmode; ili pokreni menu item u otvorenom editoru |
| UI dugmad ne reaguju na klik | Scena MORA imati `EventSystem` + `InputSystemUIInputModule` (+ `AssignDefaultActions()`) |
| `SHA256.HashData` ne postoji | Projekat je .NET Standard 2.1 → `SHA256.Create().ComputeHash()` |
| Portrait UI ogroman/isečen u landscape Game view | `CanvasScaler.screenMatchMode = Expand` (dizajn prostor 1080×1920 uvek staje) |
| TMP nema kao zaseban paket u Unity 6 | TextMeshPro dolazi unutar `com.unity.ugui` 2.0.0; Essentials resursi su commit-ovani |
| `FindObjectsByType`/`FindAnyObjectByType` ne vidi objekte sa `HideFlags.HideAndDontSave` | Test rig NE sme da koristi HideFlags — inače `_currency`/`_upgrades` ostanu null i sve tiho pada |
| Polje dodato u `GameConfig` posle poslednjeg upisa asseta ne postoji u `.asset` YAML-u | Unity zadrži C# field initializer → radi, ali balans živi u kodu; `SetDirty`+`SaveAssets` da se upiše |
| Jedan izuzetak u smoke-test paketu sakrije sve testove posle njega | Svaki scenario u svom `try/catch` + `EventBus.Clear()` u `finally` |
| Rebuild scene = ~14k linija git diff-a bez semantičke promene | Unity randomizuje lokalne fileID-jeve; verifikuj markerima iz loga, ne diff-om |

---

## Faza 0 — Setup

**NALOG #001** — Unity projekat (2D URP, 6000.0.79f1), folder struktura `_Game/…`, paketi
(URP, 2D feature, Input System, TMP, Newtonsoft), git + GitHub, core backbone:
`EventBus`, `TickSystem`, `TickEvent`, `GameConfig`, `GameManager` (Sonnet, review-ovan, kompajlira).

**Setup skripta popravke** (novi PC, commit `7d2f561`): UTF-8 BOM; `--changeset` za Hub CLI;
verifikacija instalacije na disku umesto exit code-a; `.claude/` u `.gitignore`;
Android SDK 34/35/36 + NDK r27c + OpenJDK + CMake sada idu automatski kroz `scripts/setup-dev-env.ps1`.

## Faza 1 — Core loop

**NALOG #002** (Sonnet, review Opus, batchmode kompajlira) — potpuno event-driven core loop.
Lanac: `TapController` (hold → `TapEvent` na `RepIntervalSeconds`) → `EnergySystem` (troši
`EnergyPerRep`, regen na `TickEvent`, publikuje `EnergyChangedEvent` + `RepPerformedEvent`) →
`CurrencyManager` (dodaje `GainsPerRep`, publikuje `GainsChangedEvent`).
- Tuning vrednosti u `GameConfig` (jedan SO za MVP; upgrade sistem kasnije razdvaja base od runtime).
- `GameManager` ima `[DefaultExecutionOrder(-1000)]` — `EventBus.Clear()` u `Awake` ide pre svih pretplata.
- Input: `Pointer.current.press.isPressed` (novi Input System), hold ceo ekran.

**NALOG #003** (Sonnet, review Opus) — vizuelni sloj: `UI/HudController` (event-driven, bez
ref-ova na sisteme), `UI/NumberFormatter` (K/M/B/T/aa..ae), `Character/PlaceholderCharacter`
(scale-punch na rep, coroutine — DOTween tek u Fazi 4).

**NALOG #004** (Sonnet + Opus debug/integracija) — scena iz koda: `Editor/CoreLoopSceneBootstrap`
(menu `IdleGymBro → Build Core Loop Scene` ili headless `-executeMethod`). Pravi `GameConfig.asset`,
sisteme, HUD, placeholder; sve reference kroz `SerializedObject`; idempotentan (briše stari root).
- **Gotcha rešen:** config se učitava POSLE `OpenScene(Single)` (inače `_gameConfig = {fileID:0}`).
- Self-check loguje `_gameConfig wired on N/N`.
- TMP Essentials + `GameConfig.asset` commit-ovani → radi out-of-the-box.

**NALOG #005** (Sonnet + Opus fix/integracija) — enkriptovan save/load.
- `Core/SaveSystem` `[DefaultExecutionOrder(1000)]`: autosave (`AutoSaveIntervalSeconds`, 30s),
  `OnApplicationPause(true)`, `OnApplicationQuit`. AES-CBC (ključ = SHA256 passphrase, IV prepend),
  Newtonsoft JSON, `persistentDataPath/gymbro.sav`. Korumpiran save → fresh, bez crash-a.
- `Core/ISaveable` na `CurrencyManager` + `EnergySystem`; restore publikuje evente da UI osveži.
- Opus fix: `SHA256.HashData` → `SHA256.Create().ComputeHash()` (.NET Standard 2.1).
- Verifikacija: `Editor/SaveSystemSmokeTest` headless — round-trip lossless + garbage odbačen = PASS.

## Faza 2 — Ekonomija

**NALOG #006** (Workflow: Sonnet implement; Opus review + integracija) — pasivni prihod + offline zarada.
- `Economy/PassiveIncomeSystem`: na `TickEvent` publikuje `GainsEarnedEvent(rate × dt)`;
  `PassiveIncomeChangedEvent` za HUD („X/s").
- `Economy/OfflineEarningsSystem`: na `GameLoadedEvent` (SaveSystem ga uvek publikuje posle load-a)
  računa `min(timeAway, OfflineCapSeconds) × rate × OfflineEfficiency`, grantuje kroz `GainsEarnedEvent`,
  obaveštava `OfflineProgressEvent` → `UI/OfflineClaimPopup`.
- Ordering: restore (save) → offline gains se dodaju na restore-ovan balans (EventBus je sinhron).
- `GameConfig` [Economy]: `BasePassiveGainsPerSecond` 1, `OfflineCapSeconds` 7200 (2h), `OfflineEfficiency` 0.5.
- Napomena: 3 adversarijalna review agenta pala na session-limit → Opus radio review ručno.

**NALOG #007** (agent implement; Opus review + integracija) — data-driven upgrade sistem.
- `Data/UpgradeData` (SO: id, displayName, statType, effectPerLevel, baseCost, growthRate, maxLevel)
  + `Data/StatType` (`GainsPerRep`, `PassiveGainsPerSecond`).
- `Economy/UpgradeManager` (`ISaveable`): `TryBuy` → cost `BaseCost × GrowthRate^level`, spend kroz
  `CurrencyManager.TrySpend`, `RecomputeAndPublish` → `StatsChangedEvent(base + Σ efekti)`.
  Sistemi keširaju efektivne vrednosti iz eventa (default = config base pre prvog eventa).
- `SaveData.UpgradeLevels` (Dictionary<string,int>); restore → recompute.
- `UI/UpgradeButton`: bind na jedan `UpgradeData`; refresh na Gains/UpgradePurchased/StatsChanged.
- 3 placeholder asseta u `Data/Upgrades/`: stronger_arms (+1 rep, 10, 1.10), protein_shake
  (+5 rep, 100, 1.12), training_partner (+0.5/s, 50, 1.11). Bootstrap ih kreira/ažurira.
- Verifikacija: batchmode `wired 9/9`; niz `_upgrades` (3 asset-ref) + 3 dugmeta ožičeni.

**Fix: EventSystem + upgrade modal + tap-over-UI** (commit `9acc85d`)
- Scena nije imala `EventSystem` → nijedno UI dugme nije primalo klik. Bootstrap sada pravi
  `EventSystem` + `InputSystemUIInputModule.AssignDefaultActions()`.
- Upgrade dugmad prebačena u modal: „UPGRADES" dugme na HUD-u otvara prozor (dimmer + naslov +
  3 dugmeta + „X"); `UI/ModalToggle` kontroliše open/close.
- `TapController` preskače tap kad je pokazivač nad UI-jem (`EventSystem.IsPointerOverGameObject`).

**Fix: UI skaliranje + modal layout**
- Koren: canvas dizajniran portrait 1080×1920, a `CanvasScaler` na default match-width →
  u landscape Game view vidljivo samo ~607 jedinica visine, modal (1040) prelazi ekran,
  „X" nedostupan, elementi jedan preko drugog.
- Rešenje: `screenMatchMode = Expand` (dizajn prostor uvek staje na ekran, bilo koji aspect);
  modal prozor 760×980 sa rasporedom od vrha prozora; klik na dimmer takođe zatvara
  (`ModalToggle._backdropButton`); HUD elementi razmaknuti.

## Faza 3 — Karakter

**NALOG #008** (Workflow: Sonnet implement + 2 Sonnet review lens-a + Sonnet fix/verify agenti; Fable arhitekta) — layered karakter sistem.
- `Data/CharacterLayer` enum (int vrednost = `sortingOrder`: Background −10 … Accessory 80, §7 depth order).
- `Data/MuscleTierData` (tier, displayName, **TotalEarnedThreshold**, bodySprite, headSprite) — 6 asseta
  (pragovi 0 / 1K / 25K / 500K / 10M / 500M, imena Skinny→Enhanced). `Data/CosmeticData`
  (id, layer, sprite, cost, unlockedByDefault) — 3 asseta (shorts/hair/beard, free).
- `Character/CharacterBuilder` — world-space (pozicija (0,−2.4), scale 3, ispod overlay HUD-a);
  child `Layer_*` SpriteRenderer-i se grade u `Awake` (bez scene wiring-a); tier po **`TotalEarned`**
  iz `GainsChangedEvent`; default kozmetika u `Start`; publikuje `MuscleTierChangedEvent`.
- **`CurrencyManager.TotalEarned`** (lifetime, `TrySpend` ga ne dira) + u `GainsChangedEvent` (2. polje)
  + `SaveData.TotalEarned` (restore: `max(TotalEarned, TotalGains)` migration guard).
- `Editor/PlaceholderArtGenerator` — generiše 10 PNG placeholder-a (6 tela sa rastućom siluetom,
  head/hair/beard/shorts) na 128×192, import: PPU 128, Point, BottomCenter — pravi art menja fajl 1:1.
- Bootstrap: poziva generator pre asseta; `GetOrCreateTier`/`GetOrCreateCosmetic` helperi;
  `enumValueIndex` = DEKLARACIONI indeks enuma, ne numerička vrednost (gotcha!).
- **Review nalazi (primenjeni):** (1) izbor tier-a bio zavisan od redosleda niza → best-threshold
  tracking, order-independent; (2) `_currentTierIndex` mutiran pre null guard-a + `CurrentTier` NRE
  → restrukturirano; (3) `head_01.png` generisan a nigde dodeljen → `_headSprite` ožičen na svih 6 tierova.
- Verifikacija (agent): batchmode bez `error CS`, `10 sprites generated`, `wired 9/9`,
  scena: `Character` objekat sa 6 tier + 3 cosmetic ref-a, stari UI placeholder uklonjen.
- Gotcha (batchmode iz agenta): harness relansira `Unity.exe` kao detached child — poll-uj PID
  umesto da veruješ povratku komandne linije.

**UI layout blueprint** — [`ui-layout.md`](ui-layout.md): korisnikov ciljni HUD raspored
(po uzoru na Medieval Idle Prayer): levo story-progress/boosti/offer, desno settings/buffovi/
upgrades/shop/periodic-claim, dole quests+event; svaki element mapiran na fazu.

## Faza 4 — MVP polish (početak)

**NALOG #009** (Workflow: 2 Sonnet implement + 2 Sonnet review lens-a + Sonnet verify; Fable arhitekta/fix) —
upgrade rework + booster sistem + HUD ivice.
- **Dizajn odluka (korisnik):** upgrades = mišićne grupe; konzumabilne stvari (protein, pre-workout)
  su BOOSTERI (privremeni buffovi), ne upgrades.
- Upgrades sada: chest „Chest Day", arms „Arm Blaster", back „Back Attack", legs „Never Skip Leg Day"
  (GainsPerRep) + training_partner, gym_membership (pasivno). `stronger_arms`/`protein_shake` obrisani
  (leveli iz starih save-ova se bezbedno ignorišu — recompute ide samo preko `_upgrades` niza).
- `Data/BoosterData` (id, target Tap/Passive, multiplier, duration, cooldown) + `Economy/BoosterManager`
  (TryActivate → active → cooldown; multiplikatori = proizvod aktivnih; publikuje
  `BoosterMultipliersChangedEvent` + `BoosterStateChangedEvent`). Stanje se NE persistuje (MVP).
- `CurrencyManager`/`PassiveIncomeSystem` množe efektivni prihod boosterom (§5: `base × boosterMultiplier`).
- `UI/BoosterButton` (ready „2x" / active countdown / cooldown). Prvi booster: **pre-workout** (2x tap, 60s, CD 180s).
- Upgrade modal: **ScrollRect lista** (6 dugmadi, VerticalLayoutGroup + ContentSizeFitter).
- HUD po blueprint-u: UPGRADES desna ivica sredina, boost dugme leva ivica.
- **Review nalazi (3, primenjeni):** BoosterButton.Start gazio inicijalni „2x" label (→ Awake);
  state eventi 10×/s bez potrebe (→ publikuj samo kad se prikazana sekunda promeni);
  `childControlHeight=false` čini `LayoutElement.preferredHeight` no-op (→ true; dugmad bi bila 100px).
- Verifikacija (agent): batchmode bez `error CS`, `wired 9/9`, preworkout asset tačan, tačno 6 upgrade
  asseta, scena: BoosterManager/_boosters + BoosterButton + ScrollRect/Content + 6 UpgradeButton-a — sve PASS.

**NALOG #010** (Sonnet implement + Sonnet verify; Fable review) — juice sloj, coroutine-based.
- **DOTween odložen:** nije dostupan kroz UPM ni OpenUPM (`no such package available`) — Asset Store
  import je manuelni editor korak. Juice implementiran preko coroutina (obrazac iz
  `PlaceholderCharacter.Punch`); ako DOTween kasnije uđe, menja se samo unutrašnjost efekata.
- Novi `TapGainsEvent(double Amount)` — CurrencyManager ga publikuje po uspešnom repu sa stvarno
  upisanim iznosom (posle booster multiplikatora); juice reaguje SAMO na tap, ne na pasivni trickle.
- `UI/FloatingTextSpawner` — pooled „+X" tekstovi (12, `raycastTarget=false`, pool ne raste; reclaim na disable).
- `UI/GainsCounterJuice` — scale-pop countera na tap. `UI/EnergyBarSmoother` — MoveTowards ka target fill-u;
  **jedini writer** fillAmount-a (HudController `_energyFill` namerno odžičen u bootstrap-u).
- `UI/TierUpBanner` — „TIER UP! {ime}" pop-in/hold/fade na `MuscleTierChangedEvent`; prvi (inicijalni)
  event se guta (`_initialTierSeen`).
- Verifikacija (agent): batchmode bez grešaka, sva 4 juice komponente ožičene u sceni, `_energyFill:{fileID:0}`
  potvrđeno namerno, `TapGainsEvent` definisan + publikovan tačno jednom — sve PASS.

**NALOG #011** (Sonnet implement + Sonnet verify; Fable review/fix) — zvuk + settings.
- `Editor/PlaceholderSfxGenerator` — 4 deterministička WAV placeholder-a (PCM16 mono 44.1kHz,
  ručni RIFF header): tap (50ms 880Hz), buy (2 tona), tier_up (arpeggio), booster (noise whoosh,
  fiksni seed 42 → commit bajtovi stabilni). Menu + headless.
- `Data/AudioLibrary` SO (4 clip slota + master volume) — pravi SFX menja .wav fajlove 1:1.
- `Core/AudioManager` — event→SFX: `TapGainsEvent`, `UpgradePurchasedEvent`,
  `MuscleTierChangedEvent` (guta inicijalni), `BoosterStateChangedEvent` (samo inactive→active
  tranzicija preko HashSet-a). Mute u PlayerPrefs (audio pref nije game progress).
- `UI/SettingsPanel` + SETTINGS dugme gore desno (po ui-layout.md) + settings modal (drugi
  `ModalToggle` — open/X/backdrop).
- **Verify uhvatio compile bug:** `Random` dvosmislen (`UnityEngine` vs `System`) u generatoru →
  `System.Random`. Gotcha: fajl sa `using UnityEngine;` + `using System;` mora da kvalifikuje Random.
- Verifikacija posle fix-a: batchmode PASS (0 error CS, 10 sprites + 4 clips generated, wired 9/9),
  WAV veličine tačne u bajt, AudioLibrary guid-ovi = .wav.meta guid-ovi, scena: AudioManager/_library/_source,
  SettingsPanel, oba ModalToggle-a kompletno ožičena — FULL PASS.

**NALOG #012** (Sonnet implement + Sonnet verify; Fable review) — mock monetizacija.
- **Odluka (korisnik):** realan LevelPlay/Unity IAP ide NA SAMOM KRAJU projekta; do tada mock
  iza istog javnog API-ja — `Monetization/AdManager.ShowRewarded(placement, Action onReward)`.
  Mock: fullscreen „▶ REKLAMA..." overlay (blokira input) ~1s pa reward; `OnDisable` cleanup
  (overlay ne sme da ostane zaglavljen); `IsShowingAd` guard.
- `BoosterData.RequiresAd` (data-driven) — oba boostera ad-gated; „▶ " prefiks na ready labelu.
  NOVI booster: protein_shake (2x passive 60s / CD 180s) — drugi levi slot po ui-layout.md.
- Offline popup: „UDVOSTRUČI ▶" — mock reklama → drugi `GainsEarnedEvent(amount)`;
  `_pendingDoubleAmount` se nuluje PRE reklame (nema double-claim-a klikom u nizu).
- Verifikacija: batchmode PASS prvi run; oba booster asseta `_requiresAd:1`; scena: AdManager→AdOverlay,
  2 BoosterButton-a → 2 RAZLIČITA asset guid-a, popup `_doubleButton` — sve verifikovano guid/fileID-jem.

**NALOG #013** (Fable direktno — mali data/string pass) — engleski + abs modul.
- **Pravilo (korisnik, kodifikovano u §11):** SAV in-game tekst na ENGLESKOM (globalna publika,
  gym meme identitet); srpski za docs/komunikaciju. Zamenjeno: offline poruka („Your gymbro kept
  training while you were away"), „DOUBLE IT ▶", „▶ AD PLAYING...".
- Upgrade moduli (korisnik): **chest / arms / back / abs / legs** + Training Partner (+ Gym Membership
  zadržan kao drugi pasivni). Novi: abs „Core Crusher" (8/level, base 900, growth 1.125). 7 dugmadi u scroll listi.
- U §17 dodate **„Smernice za nastavak"** — prioriteti, ustaljeni putevi dodavanja, tačne
  verifikacione komande — pisano za buduće sesije (Opus/Sonnet) iz hladnog starta.
- Verifikacija: batchmode svi markeri, abs.asset tačan, 7 UpgradeButton-a, 0 srpskih stringova u sceni.

## Faza 6 — Progresija

**NALOG #014** (Sonnet implement + Sonnet verify; Fable review) — lokacije/story progres.
- `Data/LocationData` (id, displayName, `TotalLevelsToComplete` — KUMULATIVNI prag ukupnih upgrade
  nivoa, isti obrazac kao muscle tiers; `GlobalMultiplier` [Min 1]). 6 asseta: Home Workout 25/1x,
  Street Workout 75/2x, Basic Gym 160/5x, Hardcore Gym 300/12x, Venice Beach 500/30x, Mr. Olympia 800/75x.
- `Progression/LocationManager` (`ISaveable` — `CurrentLocationIndex`): progres =
  `(TotalLevels − prevPrag) / (prag − prevPrag)`; `TryAdvance()` na 100% (ručni „MOVE UP ▲" — svesna
  proslava, ne auto); publikuje `LocationProgressChanged/LocationChanged/LocationMultiplierChangedEvent`.
- `UpgradeManager`: `TotalLevels` property + kešira `_locationMultiplier` iz eventa i množi gpr/pps u
  `RecomputeAndPublish` — bez direktnih manager referenci; restore ordering konvergira (oba redosleda
  završe istim StatsChangedEvent lancem).
- UI: `StoryProgressButton` gore levo („{Location}\n{XX}%" + „▲" kad može dalje) otvara Locations modal
  (3. `ModalToggle`): runtime-built redovi `[DONE]/>/[LOCKED]` + MOVE UP dugme. AdOverlay ostao poslednji
  (topmost) canvas child.
- Verifikacija (agent): batchmode PASS prvi run; 6 location asseta tačnih vrednosti; scena — LocationManager
  `_locations` 6/6 u redosledu, 3× ModalToggle svi ref-ovi, AdOverlay poslednje dete — sve PASS.

## Faza 7 — Meta/retencija (SOLO — pod-agenti na account spend-limitu)

> Od #016 nadalje agenti padaju na „monthly spend limit"; Fable radi direktno u glavnoj petlji,
> verifikacija batchmode-om. Naloga su namerno mali i nezavisno commit-ovani (ako limit udari
> usred posla, prethodni je već siguran).

**NALOG #016** — Periodic Reward (dole desno): `Meta/PeriodicRewardManager` (time chest — na
`PeriodicRewardIntervalSeconds`=900 spremna nagrada = kеširani passive rate × `PeriodicRewardSeconds`=300;
throttle na celu sekundu; NE persistuje se — offline zarada pokriva odsustvo) + `PeriodicRewardStateChangedEvent`
+ `UI/PeriodicRewardButton` (M:SS → „COLLECT +X"). Self-check 9/9 → **10/10**.

**NALOG #017** — Achievements (dole levo): `Data/AchievementData`+`AchievementType`
(TotalGainsEarned/RepsPerformed/UpgradesBought/LocationReached), `Meta/AchievementManager` (`ISaveable`;
counteri reps/upgrades/maxLocation + claimed set persistovani, TotalEarned iz `GainsChangedEvent`),
6 asseta, `UI/AchievementsButton` (GOALS + „(N)" badge) + `AchievementsPanel` (runtime redovi + CLAIM ALL),
**4. ModalToggle**. `SaveData` +4 polja. HUD sada kompletan po `ui-layout.md`.

**NALOG #018** — Daily Reward (streak): `Meta/DailyRewardManager` (`ISaveable`; UTC dan =
`Ticks/TicksPerDay`; nagrada = passive rate × `DailyRewardSeconds` × streakDay clamp na cycle;
preskočen dan → reset na 1; evaluira na `GameLoadedEvent` posle restore-a) + `DailyRewardAvailableEvent`
+ `UI/DailyRewardPopup` (na startu). `SaveData`: LastDailyClaimDay, DailyStreak.

**NALOG #019** — Prestige („New Bulk", §6) — NAJINVAZIVNIJI (dira 4 sistema); ⚠️ **compile-only
verifikacija, TREBA PLAYTEST.**
- `Progression/PrestigeManager` (`ISaveable`: `_totalRespect`): respect = floor(factor×√TotalEarned),
  multiplier = 1 + respect×perRespect; `CanPrestige` = pending ≥ `PrestigeMinRespect`.
  `DoPrestige`: += respect → publish `PrestigeEvent` + `PrestigeMultiplierChangedEvent`.
- Reset handleri na `PrestigeEvent`: CurrencyManager (Gains/Earned=0 → `GainsChangedEvent(0,0)`),
  UpgradeManager (`_levels.Clear` + recompute), EnergySystem (energy=max), LocationManager (index=0 → `PublishAll`).
- `UpgradeManager` kešira `_prestigeMultiplier` i množi `gpr/pps *= _locationMultiplier * _prestigeMultiplier`.
  **Ordering bezbedan:** idempotentni `RecomputeAndPublish` + finalni eksplicitni `PrestigeMultiplierChangedEvent`
  (posle svih PrestigeEvent handlera) → poslednji recompute uvek tačan bez obzira na redosled handlera.
- `UI/PrestigePanel` (multiplier/respect/pending + NEW BULK dugme, interactable=CanPrestige) + NEW BULK
  open dugme (top-left ispod Story) + **5. ModalToggle**. `SaveData.TotalRespect`.
- **Poznata mrlja:** „TotalGainsEarned" achievementi se resetuju uz TotalEarned na prestige
  (reps/upgrades ne, jer AchievementManager ne sluša PrestigeEvent) — nedosledno; balansirati kasnije.
- Verifikacija: batchmode wired 12/12, 0 error CS; scena — PrestigeManager+panel+5×ModalToggle ožičeni,
  4 reset handlera prisutna. **Runtime NEtestiran** (spend-limit).

> **Svi roadmap sistemi (Faze 0–7) implementirani.** Ostaje: realan LevelPlay/IAP (na kraju),
> pravi art/animacije (čeka assete), i **playtest + balans tuning** (prioritet — brojke su u `.asset`/GameConfig).

**NALOG #020** — Wardrobe/kustomizacija (§8). ⚠️ compile-only verifikacija (spend-limit).
- `Character/CosmeticEvents.CosmeticEquippedEvent(CharacterLayer, Sprite)`.
- `Character/WardrobeManager` (`ISaveable`): `_cosmetics[]`, equipped `Dictionary<CharacterLayer,string>`;
  `Equip(id)`/`CycleLayer(layer)` publikuju event; `EnsureDefaults` (prvi po sloju); `PublishAll` na Start/restore.
  `SaveData.EquippedCosmetics` (Dictionary<string,string>, ključ = layer.ToString()).
- **`CharacterBuilder` refaktorisan:** izbačen `_defaultCosmetics`; sada samo sluša `CosmeticEquippedEvent`
  i menja `_renderers[layer].sprite`. Tier logika netaknuta. (Bootstrap ne dodeljuje više `_defaultCosmetics`.)
- 8 `CosmeticData` asseta (hair_01/02/03, beard_01/02, shorts_01/02/03) + 5 novih placeholder-a
  (`PlaceholderArtGenerator` sada 15 sprites, sa baked labelama HAIR2/HAIR3/BEARD2/SHORTS2/SHORTS3).
- `UI/WardrobePanel` (3 reda: Hair/Beard/Shorts, „NEXT ▶" cycler) + WARDROBE dugme (dole centar) + **6. ModalToggle**.
- Verifikacija: batchmode `15 sprites generated`, `wired 12/12`, 8 kozmetika, WardrobeManager `_cosmetics` 8/8,
  panel refs, 6× ModalToggle, `_defaultCosmetics` potpuno uklonjen. Runtime NEtestiran.

## Faza 8 — Runtime verifikacija (zatvaranje „compile-only" duga)

**NALOG #021** — runtime harness + 6 popravki koje je otkrio. **Ovim prestaje „compile-only" era:**
#019/#020 su sada stvarno izvršeni, ne samo kompajlirani.

- **`Editor/SystemsSmokeTest`** — headless runtime verifikacija BEZ Play mode-a. Edit-mode
  `AddComponent` ne okida Unity lifecycle, pa rig zove `Awake`/`OnEnable`/`Start` eksplicitno
  (refleksijom). To nije zaobilaznica nego poenta: `SaveSystem` restore-uje `ISaveable`-ove
  redosledom koji `FindObjectsByType` vrati (ARBITRARAN), a samo eksplicitni drajver može da
  iznudi OBA redosleda i dokaže da stanje konvergira. **Ne dira `persistentDataPath`** —
  round-trip ide kroz statičke `Serialize`/`Encrypt` helpere u memoriji, pa se pravi save
  developera nikad ne čita ni ne prepisuje. 11 scenarija / **58 provera**.
- **Negativna kontrola (bitno):** testovi su pušteni i nad NEpopravljenim kodom (`git stash` samo
  runtime fajlova) → tačno T9/T10/T11 padaju, ostali prolaze. Test koji prolazi i pre i posle ne
  dokazuje ništa; ovim je dokazano da mere pravu stvar.

**Popravke (F1 i F6 su nađene čitanjem koda, F2–F4 pod-agent „wardrobe" lens):**
- **F1 (KRITIČNO) — offline zarada nikad nije skalirala.** `OfflineEarningsSystem` je množio
  `_gameConfig.BasePassiveGainsPerSecond` (konstanta 1/s) umesto efektivnog rate-a, pa su
  upgrade-ovi, lokacijski i prestige multiplikator bili potpuno ignorisani — suprotno §5 formuli.
  Izmereno testom: **1.800 umesto 22.500 (12,5× manje)**, a jaz raste bez granice kroz progresiju.
  Ovo je razbijalo idle stub igre i obesmišljavalo „Spot me bro" x2 offline monetizaciju (§10).
  Fix: kešira rate iz `PassiveIncomeChangedEvent` (restore svih ISaveable-a se dešava PRE
  `GameLoadedEvent`, pa je keš već post-upgrade kad offline računica krene).
- **F2 (major) — dangling kozmetički id.** `WardrobeManager.RestoreState` je primao id koji više
  ne postoji; `EnsureDefaults` ga nije mogao popraviti (ključ već postoji), `PublishAll` ga tiho
  preskoči → lik nosi hair_01 a UI piše `hair_99`, i autosave ga ovekovečava. Prvi klik na NEXT
  ne daje vidljivu promenu. Fix: id koji se ne rezolvira se ODBACUJE, pa ga `EnsureDefaults` vrati
  na validnu opciju. **Očekuj ovo kad pravi art zameni placeholder-e.**
- **F3 (major) — wardrobe modal je skrivao lika.** Centrirani neprozirni prozor + 75% dimmer preko
  celog ekrana, a lik je world-space sprite ISPOD overlay canvas-a → menjaš kozmetiku i ne vidiš
  ništa osim teksta. Fix: **bottom sheet** (720×620, anchor bottom, y=330) + dimmer 0.75→0.35;
  lik zauzima design y ≈ −460..+403 (kamera ortho 5), sheet stoji ispod −320 → glava/kosa/brada
  ostaju čiste. Dimmer i dalje full-screen raycast target (backdrop-close + tap-over-UI guard rade).
- **F4 (minor)** — `WardrobePanel` je prikazivao sirov asset id; `CosmeticData.DisplayName` je bio
  autorovan na svih 8 asseta i nigde čitan. + `Refresh()` u `OnEnable` (panel je unsubscribe-ovan
  dok je modal zatvoren, a `Start` ide samo jednom).
- **F5 (workflow) — `GameConfig.asset` je imao samo 6 od 17 tunable-a.** Ostalih 11 (offline,
  periodic, daily, prestige) postojalo je SAMO kao C# inicijalizatori: Unity za polja kojih nema
  u YAML-u zadrži vrednost iz field initializer-a, pa je igra radila — ali balans je de facto
  živeo u kodu, što ruši §4 princip #1 i blokira dogovoreni sledeći prioritet (balans tuning).
  Fix: `GetOrCreateConfig` radi `SetDirty` + `SaveAssets` na postojećem assetu → upisuje se
  in-memory stanje, pa se **već tuniranje vrednosti ČUVAJU** (nije overwrite), a idempotentno je.
- **F6 — achievements su gubili progres na prestige.** (Mrlja prijavljena u #019.) `TotalGainsEarned`
  se čitao iz `CurrencyManager.TotalEarned` koji je per-run; posle „New Bulk" progres pada na 0 i
  nezatražen završen cilj se tiho ODzavršava (test: progress=0 vs threshold=1000). Achievements su
  trajni rekordi (§12). Fix: `_lifetimeEarned` akumuliran iz DELTI (pad = re-baseline, nikad
  oduzimanje) + `SaveData.AchievementLifetimeEarned`; `_lastSeenRunEarned` se baseline-uje na
  `data.TotalEarned` pa oba redosleda restore-a konvergiraju. Migracija: stari save (0) se seed-uje
  iz `TotalEarned`.

## Faza 3 (nastavak) — pravi art za lika

**NALOG #022** — hand-painted art kroz `game-assets-enhancement` skill + fal.ai. **Menja zaključanu
odluku §2** (pixel art → hand-painted cartoon) — svesna odluka korisnika.

- **Instalacija skill-a:** korisnik je kloniran sa `\~/.claude/skills/...` — backslash je sprečio
  ekspanziju tilde, pa je završio u `F:\~\.claude\skills\`. Premešten u `C:\Users\nikol\.claude\skills\`.
- **Pipeline (validiran u sve tri tačke pre masovnog trošenja kredita):** `fal-ai/nano-banana-pro`
  (`aspect_ratio: "2:3"`, chroma-green pozadina) → `pixelcut/background-removal` → transparentan PNG.
- **Registracija slojeva — ključni trik:** svi asseti su **editi jednog anchor-a** (tier3).
  Edit čuva kompoziciju, pa svih 14 fajlova izađe kao **identičnih 848×1264** sa istom visinom
  glave i istom linijom stopala → slojevi se poklapaju **po konstrukciji**, bez ručnog poravnavanja.
  Prompt eksplicitno zakucava: „do NOT rescale, move or rotate; top of head and soles stay on the same line".
- **`Editor/CosmeticLayerExtractor`** — model ne ume pouzdano da nacrta „samo kosu, u tačnoj poziciji",
  ali ume da EDITUJE referencu. Zato se sloj dobija kao **razlika varijante i baze** unutar zadatog
  vertikalnog pojasa (kosa 0.00–0.15, brada 0.10–0.21, šorc 0.40–0.62). Pojas postoji jer edit
  usput blago precrta i lice — bez njega „duh" obrva/osmeha uđe u sloj kose.
- **14 naslikanih asseta:** body_tier1–6, hair_01–03, beard_01–02, shorts_01–03. Pozadine (6) ostaju placeholderi.

**Popravljeno usput (nađeno tokom integracije):**
- 🔴 **Generatori placeholder-a su bezuslovno prepisivali sve na SVAKI rebuild scene.** Pošto je
  rebuild obavezan deo workflow-a, prvi `BuildCoreLoopScene` posle ubacivanja pravog arta bi ga
  nepovratno obrisao — a `asset-checklist.md` je baš to i preporučivao. Sada `File.Exists` guard u
  sva tri generatora (art/pozadine/SFX) + zaseban `DANGER — Regenerate…` menu sa potvrdom.
  Log marker promenjen: `N generated.` → `N ready (x generated, y kept)`.
- **PPU se izvodi iz visine teksture** (`height / 1.5f`) umesto hardkodovanih 128. Placeholder
  (192px → PPU 128) i naslikani art (1264px → PPU 842.667) zauzimaju istih 1.5 world jedinica →
  zamena arta bilo koje rezolucije ne menja veličinu lika i ne lomi poravnanje. Filter je Point za
  pixel-art, Bilinear za art veće rezolucije (inače ružno alias-uje na telefonu).
- **`_headSprite` očišćen na svih 6 tierova** — naslikana tela sadrže glavu, pa bi zaseban Head sloj
  crtao drugo lice preko prvog. `CharacterBuilder` već tretira null HeadSprite.

**Gotchas:**
- `image_size` nano-banana-pro ignoriše; koristi `aspect_ratio` (enum: `auto,21:9,16:9,3:2,4:3,5:4,1:1,4:5,3:4,2:3,9:16`).
  Nevalidna vrednost vrati 422 sa celim enumom — jeftin način da se sazna schema (422 se ne naplaćuje).
- Okruženje nema `jq` ni `python` → JSON za curl se pravi heredoc-om.
- Pri izolovanju sloja **nuliraj i RGB, ne samo alfu** — inače PNG ostane ogroman (583 KB vs 49 KB)
  i svaki preview izgleda kao da je ceo lik preživeo, jer render spljošti alfu.

**Verifikacija:** batchmode 0 `error CS`, `15 sprites ready (0 generated, 15 kept)` (= art preživeo
rebuild), `wired 12/12`, `Scene built and saved`, `SystemsSmokeTest PASS 58/58` (zamena arta nije
napravila regresiju), `tier3_fit.asset _headSprite: {fileID: 0}`.

**Gotchas naučeni ranije (#021):**
- `FindObjectsByType`/`FindAnyObjectByType` **preskaču objekte sa `HideFlags.HideAndDontSave`** →
  rig sa hidden objektima je ostavljao `UpgradeManager._currency` null i svaka kupovina je tiho
  padala (15 lažnih padova u prvom run-u). Test rig NE sme da koristi HideFlags.
- Jedan izuzetak u paketu testova je gutao rezultate svih testova posle njega → svaki scenario ide
  kroz svoj `try/catch` (`Run(name, test)`), sa `EventBus.Clear()` u `finally`.
- Rebuild scene daje ~14k linija diff-a bez semantičke promene (Unity randomizuje lokalne fileID-jeve);
  scenu treba verifikovati markerima iz loga, ne git diff-om.

---

## Faza 3 (kraj) — PixelLab migracija: sav art u pixel artu + animacije

**NALOG #035** — zamena celog arta pixel artom sa PixelLab-a i zatvaranje poslednje stavke Faze 3
(animacije). Radjeno na **trial nalogu od 40 generacija**; potroseno **35**.

Pun izvestaj (cenovnik po alatu, prompt strategija, sve zamke): [`pixellab-migration.md`](pixellab-migration.md).
Sazetak onoga sto je bitno za dalji rad:

**Sta je zamenjeno:** 6 muscle tierova, 6 pozadina lokacija, 18 UI ikonica, 8 kozmetickih slojeva
+ blink, i **12 animacionih klipova** (idle + workout po tieru, 4 frejma svaki = 48 novih frejmova).

**Tri odluke koje su ucinile da 38+ asseta stane u 40 generacija:**

1. **Grid trik za ikonice — 18 ikonica za 5 generacija.** `create_image_pixflux` uredno nacrta
   3x3 mrezu razdvojenih ikonica na transparentnoj pozadini ako se to eksplicitno trazi. Slika se
   lokalno isece po celijama, svaka celija se trimuje na svoj opaque bbox i re-centrira u 64x64
   (relativne velicine se cuvaju — noga ostaje manja od torza umesto da se obe rastegnu na dugme).
   Semantika ume da odluta (trazena „noga", dobijen torzo), pa je jeftinije traziti 9 pojmova
   odjednom i doraditi promasaje sledecim gridom nego crtati 1 po 1.

2. **Kozmetika bez ijedne generacije.** Trik iz #022 („generisi varijantu sa kosom, oduzmi bazu")
   se NE prenosi na PixelLab: na `init_strength` 300 model uopste ne doda kosu (lik ostane celav),
   na 150 precrta celo telo — u oba slucaja razlika je sum, ne sloj. `inpaint_image` bi to resio
   cisto ali kosta **20–40 generacija po pozivu**. Umesto toga: fal.ai slojevi iz #022 su smanjeni
   na 96x144 i propusteni kroz tvrdu alfu (alpha < 110 -> 0) + kvantizaciju boje (korak 28).
   Poklapaju se **po konstrukciji**, jer i pixel tela i ti slojevi poticu iz iste 848x1264
   kompozicije.

3. **Animacija po 1 generaciju.** `animate_image` radi na „loose" PNG-u (ne trazi PixelLab rig,
   za razliku od `animate_character`). `96 x 144 x 4 = 55.296 <= 65.536` -> 1 gen po klipu.
   Ovo je bio ceo razlog zasto je canvas u #034 zakljucan bas na 96x144.

**Registracija se ne prepusta modelu.** Svaki tier je img2img iz **svog** fal.ai originala
smanjenog na 96x144: model prevodi stil, ali pozu/visinu glave/liniju stopala nasledjuje iz init
slike. Isti princip kao #022, drugi alat.

**`init_image_strength` je obrnut** od uobicajenog img2img — veci broj znaci da se ulaz VISE cuva
(150 = pravi edit, 300 = suptilno, 500 = jedva menja). Na 150 je model „naduvao" tier1 i tier2 pa
je progresija misica nestala (tier2 je izgledao krupnije od tier3); presli su na **260**.
Za tier1 (zahtev korisnika: „ultra mrsav i tuzan") ni to nije bilo dovoljno jer izvorna slika nije
dovoljno mrsava — reseno tako sto je init slika prvo **suzena po X na 68%**, pa je model dobio
stvarno mrsavu siluetu da prevede.

**Arhivirano, ne obrisano:** `Art/Character/_originals/fal_ai_848x1264/` — 48 fajlova (stari
`_idle*`/`_work*` frejmovi + napusteni forearm-rig: `_armless`, `_forearm_l/r`, `_noforearm`,
`flex_tier1–6`). Morali su da odu jer se PPU izvodi iz visine teksture: frejm od 1264 px i telo od
144 px zauzimaju **istih 1.5 world jedinica**, pa bi velicina bila tacna ali bi lik pri disanju
treperio izmedju pixel arta i naslikanog arta. `flex_*` i forearm fajlove ionako niko ne ucitava.

**Popravljeno usput — 🔴 ikonice bi bile NEVIDLJIVE.** `.meta` nadzivi PNG koji opisuje. Stare
ikonice (game-icons.net, #030) bile su **512×512 u `spriteMode: Multiple`** sa zapecenim sub-rect-om
`(x:17, y:19, w:478, h:476)`. Kad se preko njih ubaci 64×64 pixel art, Unity i dalje kropuje taj
pravougaonik — koji lezi **potpuno van nove teksture** — pa ne nastane nijedan sprite. Isti kvar koji
je vec jednom uhvacen na telu lika („nevidljiv lik sa ispravnim ozicenjem"), samo u drugom folderu.
Character/ i Backgrounds/ su bili zasticeni (`ConfigureAllInFolder` / `ConfigureImporter` na svakom
run-u), Icons/ nije bio — niko ga nikad nije konfigurisao iz koda.

Dodato: `CoreLoopSceneBootstrap.ConfigureIconImporters()` (Sprite · **Single** · **Point** filter ·
bez mipmap-a · uncompressed · `alphaIsTransparency`), log marker `18 icons ready`.

> **Poziv MORA biti pre `GetOrCreate*` blokova.** Prvi put je stavljen dole, uz `ConfigureUiKit()`,
> i tada se video pravi efekat kvara: `GetOrCreateUpgrade` zici `_icon` kroz `LoadIcon(id)`, sto je
> u tom trenutku vracalo **null**, pa je u `.asset` upisano `_icon: {fileID: 0}` na svih 7 upgrade-a
> i 2 boostera. Uhvaceno u `git diff` pre commit-a; posle premestanja sve reference su
> `{fileID: 21300000}` sa svojim GUID-om. Point filter je jednako bitan: 64×64 art se u UI-ju
> naduvava na ~72–96 px, pa bi bilinear vratio bas onu mutnoc zbog koje se i islo u pixel art.

**Gotchas:**
- `get_image` **ne vraca `.png` URL** — PNG dolazi inline kao base64 `{"type":"image","data":"…"}`,
  a `download:` link je bez ekstenzije. Regex koji trazi `\.png` nikad ne pogodi i posao „istekne"
  iako je odavno gotov. Generacija se ne gubi: job id ostaje validan (`-JobId` pokupi rezultat).
- **PowerShell:** `param([string]$Bg)` i `$bg = [Drawing.Image]::FromFile(...)` su ISTA promenljiva
  (imena su case-insensitive), a tip iz `param` bloka ostaje zakucan — slika se tiho pretvori u
  string `"System.Drawing.Bitmap"`, `$bg.Width` postane `$null`, i `New-Object Drawing.Bitmap 0,0`
  padne sa „Parameter is not valid".
- `create_image_pixflux` prima najvise **400 px po strani**; `animate_image` trazi frejm <= 256x256
  i `w*h*frames <= 524288`.
- Generator ikonica ume da utisne sitan watermark u donji desni ugao slike — udje u bbox celije i
  pomeri centriranje. Ocistiti pre sečenja.

**Verifikacija:** batchmode 0 `error CS`, `15 sprites ready (0 generated, 15 kept)` (= novi art je
prezhiveo rebuild), `6 backgrounds ready`, `_gameConfig wired on 13/13`, `Scene built and saved`,
`SystemsSmokeTest PASS — 221 checks, 0 failures`. T12 (svaki tier ima idle i workout frejmove istog
canvasa i PPU-a kao staticna poza) je bio taj koji je sprecio da se animacije samo obrisu — bez
njega bi lik tiho ostao bez klipova.

---

## Faza 3 (popravke posle prvog playtesta)

**NALOG #036** — playtest je otkrio tri stvari na animaciji; sve tri su imale isti koren:
klipovi vise nisu rucno autorizovani nego generisani, a sistem oko njih je jos uvek pretpostavljao
rucno autorizovane.

**1. „Ghosting" — cross-fade na pixel artu.** `CharacterAnimator` je crtao donji frejm u punoj
neprozirnosti i pretapao sledeci preko njega, kroz drugi `SpriteRenderer` (`BodyBlendRenderer`).
U kodu je stajao komentar da je to bezbedno **samo** dok se susedni frejmovi razlikuju za tanak pojas
obrisa trupa — sto je vazilo za naslikani 848×1264 art. `animate_image` precrta senčenje po celom telu,
pa je pretapanje crtalo **dva tela odjednom**. Pixel art se ne pretapa — snap-uje.
Blend renderer je uklonjen u celosti (i iz `CharacterBuilder`-a).

**2. „Delayed" — dva uzroka.**
- *Klip je bio potkracen.* `animate_image` vraca `frame_count + 1` frejmova (indeks 0 = ulazna slika
  nepromenjena), ali MCP odgovor **inline-uje samo prve 4 slike** bez obzira koliko ih ima; ostatak
  postoji iskljucivo iza `.../images/<job>/download?index=N`. U #035 su snimljene te 4 (indeksi 0–3),
  pa je u igru usao klip `[staticna, KOPIJA staticne, m1, m2]` — duplirani prvi frejm (vidljiv kao
  zastoj na pocetku svakog udisaja) i izgubljen poslednji. Sada se svi frejmovi preuzimaju po indeksu
  (`pxl-frames.ps1`), a idle koristi indekse 1..4.
- *Curl je bio „hold", ne rep.* Stara logika je rampom dizala ruke u zadrzanu pozu za
  `WorkoutRaiseSeconds` i isto toliko ih spustala, pa je lik uvek kaskao za inputom. Sada se workout
  klip **vrti u petlji** (`WorkoutCyclesPerSecond`), pocinje od prvog repa i ocigledno „radi serije".
  Tunable-i `_workoutRaiseSeconds` / `_workoutHoldPulseSpeed` / `_workoutHoldPulseDuty` su zamenjeni
  jednim `_workoutCyclesPerSecond`.

**3. Kosa i brada nisu pratile telo.** Kozmetika su **statični slojevi preko animiranog tela** — to je
radilo dok je art bio rucno autorizovan tako da lobanja ostane pixel-identicna izmedju frejmova.
Generisani klipovi to ne rade: glava poskakuje sa dahom i okrece se ka bučici, pa se kosa odlepi.
Crtanje kose po frejmu nije opcija (sloj se dobija kao diff prema baznoj pozi, a taj trik ne prezivljava
PixelLab — vidi #035).

Resenje: **`Editor/FrameAnchorBaker`** meri, po frejmu, koliko se glava pomerila u odnosu na staticnu
pozu (najvisi neprozirni red + tezistе po X u pojasu ispod njega; uzorkovanje je stegnuto na sredinu
canvasa da bucica podignuta do ramena ne odvuce teziste u stranu). Offset-i se pisu u
`MuscleTierData._idleHeadOffsets` / `_workoutHeadOffsets` u pikselima, a `CharacterAnimator` njima
pomera Hair/Beard/Blink slojeve. Bake se vrti na svakom rebuild-u scene, odmah posle `GetOrCreateTier`.
Izmereno: glava padne do 4 px i pomeri se ~3.8 px u stranu tokom curl-a.

**Novi curl (zahtev korisnika): jedna pa druga ruka, gleda u bucicu koju podize.**
- img2img **ne ume** da doda bucice u ruke (na `init_strength` 200 jedva se naziru, a telo se raspadne).
  Umesto toga: `animate_image` sa akcijom „podize bucicu sa poda", pa se **poslednji frejm te animacije**
  koristi kao baza sa bucicama u obe ruke. Iz te baze ide curl (8 frejmova = 2 gen), pa su bucice
  prisutne u SVAKOM frejmu i petlja nema pop.
- Naizmenicnost se mora traziti **negativno**: opis redosleda nije bio dovoljan (3 od 6 tierova je
  diglo obe ruke odjednom), tek „ONLY ONE arm moves at a time … never both arms at once" je proslo.
- Animator preskace indeks 0 workout klipa — to je staticna poza bez bucica, koja bi jednom po petlji
  ispustila tegove iz ruku.

**Usput:** ikonice povecane (dugmad 0.42/0.56 -> 0.56/0.72 precnika, redovi 96 -> 120 px, gains 72 -> 96)
— na starim frakcijama je pixel-art motiv bio necitljiv na telefonu.

**Novi test:** `T13 head anchors per frame` — broj offset-a mora da odgovara broju frejmova, i workout
track ne sme biti sav nula. Bitno jer `CharacterBuilder` **tiho odustaje** od kompenzacije kad se
brojevi ne slazu (bolje nego pomerati kozmetiku pogresnim brojevima), pa bi ustajao bake prosao bez
ijednog upozorenja — kosa bi se samo opet odlepila. Negativna kontrola: nuliranje
`_workoutHeadOffsets` na tier1 obara tacno tu jednu proveru (`FAIL — 312 checks, 1 failures`).

**Verifikacija:** 0 `error CS` · `15 sprites ready (0 generated, 15 kept)` · `88 png import settings applied` ·
`6 backgrounds` · `18 icons ready` · `frame anchors baked on 6 tier(s)` · `_gameConfig wired on 13/13` ·
`Scene built and saved` · `SystemsSmokeTest PASS — 312 checks, 0 failures`.

**Gotcha:** `animate_image` naplacuje po `ceil(w*h*frames/65536)` — 96×144×8 = 2 generacije, a 4 frejma
1. Ali **frejmovi preko cetvrtog ne stizu inline** — ko ih ne preuzme po indeksu, tiho dobije krnji klip.

**NALOG #037** — drugi playtest: sorc nije pratio telo, UI je i dalje bio Unity-default, x10 nije postojao.

**Sorc siri od lika — jedan sprite, sest sirina kukova.** Kozmetika je do sada bila JEDAN sprite po
komadu. To je prolazilo dok su tela bila fal.ai varijante iste kompozicije; PixelLab je tierovima
promenio siluetu, pa je isti par sorca visio sa strane mrsavog lika i sekao masivnog.

Resenje: **`Editor/ShortsGenerator` krojii sorc iz siluete SAMOG tiera.** Dva merenja to nose:
- *Pojas se ne pogadja frakcijom canvasa nego se nalazi iz gacica na telu.* Izmerena paleta:
  fill `62,68,93`, nijanse `38,41,63` i `26,28,42`, highlight `82,87,109`.
  ⚠️ Prva verzija je uzimala „plavkasto i tamno" i **obukla ceo lik od temena do stopala** — jer je
  najtamnija nijansa gacica ujedno boja obrisa oko celog tela. Popravka: strozi prag (`B−R > 0.09`)
  **plus zahtev da postoji NIZ od bar 5 susednih takvih piksela** — odeca je popunjena povrsina,
  obris od 1 px nije.
- *Kukovi su, po redu, onaj neprozirni niz koji SADRZI centralnu kolonu.* Na visini kukova silueta je
  `ruka | praznina | kukovi | praznina | ruka`, pa bi uzimanje celog raspona reda razvuklo sorc preko
  obe ruke.

Posledica na podatke: `CosmeticData` dobija `_tierSprites` + `SpriteForTier(tier)` (fallback na
zajednicki sprite), `CosmeticEquippedEvent` nosi i `CosmeticData` (ne samo Sprite), a
`CharacterBuilder` pamti sta je obuceno po sloju i **ponovo razresava sloj na promenu tiera** — inace
bi rast tiera ostavio stari kroj na novom telu. Kosa i brada nemaju per-tier varijante: lobanja se
kroz tierove menja zanemarljivo.

**UI: generisan pixel art umesto Unity default-a.** `create_ui_asset` (40 gen) sa `pieces` — panel,
dugme, plocica, tab i traka na jednom 512×512 platnu, uz **telo tier3 kao `style_image_base64`**, pa
je paleta/outline/pixel scale isti kao kod lika. Komadi se seku lokalno (isti postupak kao icon grid).
- Okrugli diskovi su izbaceni: `CircleSprite` sada vraca `plate_pixel` (kvadratna plocica sa toplim
  uglovima). To je bio poslednji Unity-default element u igri koja je inace cela pixel art.
- **9-slice je obavezan**, inace se 2px ram razmaze u gradijent kad se panel od 232×168 razvuce na
  modal od 760×980. Zato `SetPixelUi` upisuje `spriteBorder`, a `CreateImage`/`ApplySlicing`
  automatski prebacuju svaki sprite sa ramom na `Image.Type.Sliced`.
- `PanelColor`/`ButtonColor` su sada beli: povrsine nose boju u artu, pa bi mnozenje tamnim tintom
  spljostilo slate u crno.

**x10 toggle.** `Economy/BuyMultiplier` (staticno stanje + event) — dugme stoji jednom na vrhu modala,
a svaki red se sam preracunava; drzanje multiplikatora po redu bi znacilo sedam kopija istog stanja.
Namerno se NE cuva u save-u: to je pogled na panel, ne progres, i igrac koji se vrati posle nedelju
dana ne treba da zatekne x10 naoruzan nad manjim budzetom.
- `UpgradeManager.GetCost(id, count)` sabira **geometrijski niz**, ne `count × trenutna cena` —
  kvotirati jeftinu cenu pa naplatiti pravu je nacin na koji bulk-buy dugmad lazu igraca.
- `AffordableLevels(id, count)` vraca koliko se stvarno moze kupiti, pa dugme kvotira **ono sto ce
  zaista kupiti** (x7 kad ima za 7 od 10) umesto da stoji sivo bez objasnjenja.

**Novi test T14** — sorc mora imati 6 razlicitih krojeva. Bitno jer `SpriteForTier` namerno pada na
zajednicki sprite kad varijanta fali, pa bi pokvaren generator tiho vratio jedan par na svih sest tela.
Negativna kontrola: uperiti svih 6 referenci u isti sprite → pada tacno „the six cuts are six
different sprites [1 distinct]".

**Verifikacija:** 0 `error CS` · `18 shorts sprites ready` · `18 icons ready` ·
`frame anchors baked on 6 tier(s)` · `wired 13/13` · `Scene built and saved` ·
`SystemsSmokeTest PASS — 356 checks, 0 failures`.

**NALOG #038** — pixel font, tabovi Body/Equipment/Macros, nova ekonomija, rest timer, klikabilne lokacije.

**Pixel font iz sopstvenih glifova.** `PixelFont` (5×7, do sada samo za zapecene placeholder labele)
prosiren sa interpunkcijom i pretvoren u pravi TMP font asset (`Editor/PixelFontAssetGenerator`):
atlas 1×, `GlyphRenderMode.RASTER`, TMP Bitmap shader, Point filter. Bez TTF-a — TTF bi TMP
rasterizovao u SDF i vratio omeksan, sto je bas ono sto se menja.
- `CreateText` **snap-uje velicinu na ceo umnozak visine glifa** (7 px). Na 34pt bi 7px glif padao na
  frakcione ekranske piksele: kolone izlaze sire jedna od druge i tekst “pliva” dok se broj menja.
- Font nema mala slova (5×7 nema mesta za descendere) → `FontStyles.UpperCase` na svemu.
- ⚠️ **`new SerializedObject(font)` snima snapshot SVIH serijalizovanih polja pri kreiranju**, pa je
  `ApplyModifiedProperties` vracao prazne tabele preko glifova upisanih oko njega. Asset se sacuvao sa
  **1 znakom i atlasom 0×0** — sto se renderuje kao potpuno prazan HUD, bez ijednog upozorenja.
  Popravka: `m_AtlasWidth/Height/Padding/RenderMode` se pisu **refleksijom** u istom prolazu, plus
  provera „ucitaj sa diska i uporedi" odmah po snimanju.
- Znaci ▶ i ▲ uklonjeni iz UI stringova: u bitmap fontu glif koji ne postoji ne crta **nista**.

**Tabovi + nova ekonomija.** `UpgradeCategory` (Body / Equipment / Macros) + `UpgradeData._locationId`.
- **Body** — 5 misicnih grupa + 2 pasivna izvora, bez kapa.
- **Equipment** — 4 komada po lokaciji (24 ukupno), skupo i **konacno** (`MaxLevel` 5), jer se
  lokacija zatvara i time sto je sva oprema kupljena; neogranicen komad bi taj uslov ucinio
  nedostiznim. Gear iz lokacije do koje igrac nije stigao se **krije**, ne sivi — lista stvari koje
  ne mozes da kupis nije informacija.
- **Macros** — protein → gains/rep, carbs → **max energija** (novi `StatType.MaxEnergy`, kesira ga
  `EnergySystem` iz `StatsChangedEvent`), fats → pasivni prihod.
- **Otkljucavanje lokacije su sada TRI uslova** umesto jednog kumulativnog zbira: Body target, sva
  oprema te lokacije na maksimumu, i **najnizi** od tri makroa na targetu. Jedan zbir je dozvoljavao
  da se sledeca lokacija probije onim upgrade-om koji je slucajno najjeftiniji — a tabovi postoje
  bas da to sprece. Max energija se namerno NE mnozi lokacijskim/prestige multiplikatorom.

**Rest timer** (`Economy/UpgradeCooldownManager`, `ISaveable`) — na svakih 10 nivoa, trajanje
`base × (2n−1)` = 5 / 15 / 25 min. Gate-uje **samo kupovinu**; tapkanje, pasivni prihod, boosteri i
sve ostalo rade dalje (§10 pravilo 3: reklama nikad ne sme da stoji izmedju igraca i igranja).
Rewarded reklama **skracuje** (`UpgradeCooldownAdCutSeconds`), ne brise — inace prestaje da bude
boost i postaje prekidac koji uklanja mehaniku. Rok se cuva kao **apsolutno UTC vreme**, pa istice i
dok je aplikacija zatvorena; „preostale sekunde" bi force-quit pretvorile u besplatan skip.
Bulk kupovina koja preskoci vise pragova odjednom arm-uje samo najnoviji.

**Locations: redovi su dugmad.** Bili su cist tekst sa `raycastTarget = false` uz jedno MOVE UP
dugme, pa tap na lokaciju nije radio nista — sto se cita kao pokvareno dugme, ne kao „ovo je samo
labela". `LocationManager.TrySelect(index)` vraca na vec posecenu lokaciju (unapred se i dalje ide
samo kroz `TryAdvance` i njegove uslove); red koji se ne moze pritisnuti je ujedno i zatamnjen.

**Testovi:** T15 (font razresava znakove — uhvatio bas gornji „prazan HUD" kvar), T16 (rest timer:
arm-uje se tacno na pragu, blokira kupovinu, zarada radi dalje, svaki sledeci duzi, reklama skracuje
ali ne brise, rok ide u save). T3 prepisan na tri uslova i sada tvrdi da Body **sam** ne otkljucava.
Rig je dobio `UpgradeCooldownManager` — bez njega bi ceo paket kupovao kroz kapiju koja u igri
postoji; `BuyLevels` ga svesno gasi refleksijom (reset-metoda u produkcionoj klasi bi pre ili kasnije
bila pozvana iz igre).

**Verifikacija:** 0 `error CS` · `18 icons ready` · `18 shorts sprites ready` · `pixel font: 58 glyphs` ·
`frame anchors baked on 6 tier(s)` · `_gameConfig wired on 14/14` · `Scene built and saved` ·
`SystemsSmokeTest PASS — 390 checks, 0 failures`.

**NALOG #039** — tri nalaza iz drugog playtesta + UI po referenci (Medieval Idle Prayer).

**1. Sorc je imao jednu duzu nogavicu.** Ispod medjunozja centralna kolona je providna, pa je
`TryHipSpan` padao na „najblizi niz centru" i oblacio **jednu** nogu — druga je ostajala gola.
Popravka: po redu se uzimaju **svi** neprozirni nizovi i crtaju se oni koji se preklapaju sa rasponom
pojasa (jedan iznad medjunozja, dva kad se noge razdvoje). Ruke su iskljucene po konstrukciji, jer
ne dodiruju raspon pojasa.

**2. Bucice su „prolazile iza nogu".** Nisu — u artu su uredno ispred nogu u svim frejmovima
(provereno na celom curl klipu). Pokrivao ih je **sloj sorca**: bucice su naslikane U telo, a sorc
je sloj iznad tela (`sortingOrder` 10), pa je crtao preko njih. Sada `HemLimitFromWorkoutFrames`
meri, po tieru, najvisi tamni ne-kozni piksel **u kolonama u kojima se sorc uopste crta**, i porub
staje iznad njega.
> ⚠️ Prva verzija je merila i ±12 px van kukova; posto bucice vise **van** kukova, svaki tier je
> prijavio sudar i sorc je bio skracen na gacice bez razloga.
>
> **Kompromis koji ostaje:** na ovom artu duzi sorc se stvarno sudara sa bucicom u donjem polozaju,
> pa su sorcevi kratki (poza-gacice). Prava alternativa je izvuci bucice u zaseban sloj iznad sorca —
> to je nov po-frejmu sloj i nije radjeno.

**3. UI po referenci** (korisnik poslao snimke iz Medieval Idle Prayer): ikonice znatno vece
(dugmad 0.56/0.72 -> 0.64/0.88 precnika, redovi 120 -> 148 px, booster 84 -> 104, gains 96 -> 112),
tabovi 216×84 -> 220×100, a **x1/x10 prekidac je premesten u zaglavlje modala** nasuprot „X" —
isto mesto gde referenca drzi svoj MAX, pa se cita kao svojstvo celog panela a ne reda iznad kog stoji.

**Verifikacija:** 0 `error CS` · `18 shorts sprites ready` · `pixel font: 58 glyphs` ·
`_gameConfig wired on 14/14` · `Scene built and saved` · `SystemsSmokeTest PASS — 390 checks, 0 failures`.

**NALOG #040** — bucice u svoj sloj, sorc opet pune duzine, HUD bez natpisa.

**Bucice vise ne prolaze kroz sorc — resen je REDOSLED, ne duzina.** Tegovi su naslikani U telo, a
sorc je sloj iznad tela, pa je odeca crtala preko gvozdja. U #039 je to zaobidjeno skracivanjem sorca
(izgubila se duzina); sada `Editor/HeldItemExtractor` izvlaci tegove iz svakog workout frejma u
zaseban sprite koji `CharacterBuilder` crta na `sortingOrder = Shorts + 5`. Isti pikseli, isto mesto,
jedan sloj vise — tamo gde sorc ne dodiruje gvozdje kopije se poklapaju i nista se ne menja.
Sorc je vracen na punu duzinu (`HemLimitFromWorkoutFrames` obrisan).

Prepoznavanje gvozdja je proslo kroz dve popravke:
1. „tamno u frejmu" je hvatalo i **gacice** → dodato „a nije bilo tamno u staticnoj pozi".
2. To i dalje nije bilo dovoljno: model **precrta pojas za piksel-dva** izmedju frejmova, pa je ta
   ivica izasla kao plocа velicine sorca koja se crta PREKO sorca. Prvo resenje (pravougaonik oko
   gacica) jeste to sredilo, ali je **pojelo bucice** koje u donjem polozaju leze uz kukove.
   Konacno: **maska oblika** — svi tamni pikseli staticne poze, prosireni za 3 px.
   Boja ne pomaze: izmereno na tier5, gacice `38,41,63` naspram gvozdja `46,55,76`.

**HUD bez natpisa.** Ikonica vec kaze koji je booster; ispod nje stoji samo zivo stanje
(`AD 2x`, odbrojavanje), ne i naziv. Isti pristup kao u referenci (Medieval Idle Prayer): natpisi
nestaju, brojevi ostaju.

**Ostaje otvoreno:** na tierovima 2/3/5 curl podize **samo jednu** ruku — druga samo drzi teg.
Isti promasaj kao ranije na 1/4/6, samo u drugom smeru; regenerise se sa eksplicitnim „u prvoj
polovini desna, u drugoj LEVA".

**Verifikacija:** 0 `error CS` · `48 held-item sprites ready` · `18 shorts sprites ready` ·
`wired 14/14` · `Scene built and saved` · `SystemsSmokeTest PASS — 390 checks, 0 failures`.

**NALOG #041** — naizmenicni curl kroz ogledalo + red izvrsavanja generatora.

**Model ne ume da naizmenicno dize ruke.** Generisani klip uvek podize SAMO jednu ruku; druga drzi
teg ceo ciklus (provereno na svih 6 tierova). Dva re-roll-a sa sve eksplicitnijim promptom su bila
**gora**: jedan je vratio „obe ruke odjednom", drugi je prestao da dize bilo sta. Oba vracena iz git-a.

Resenje bez ijedne generacije: **`Editor/CurlMirrorGenerator` preslikava autorskih 8 frejmova po X**
i pise ih kao frejmove 9–16. Lik je celav, front-view i skoro simetrican, pa je ogledalo doslovno rep
druge ruke — ukljucujuci i okret glave, koji se preslika da prati bucicu koja je sada gore. Klip od
16 frejmova je pravi naizmenicni ciklus.

**Red izvrsavanja generatora — tih kvar koji je ovo otkrilo.** `PlaceholderArtGenerator.Generate()`
(koji kroz `ConfigureAllInFolder` podesava importere za CEO folder) radi na pocetku bootstrap-a, a
mirror/held/shorts pisu nove PNG-ove POSLE njega. Ti fajlovi su zato ostajali na Unity default-u
(PPU 100, auto-cropped `Multiple` rect), pa je `AssignFrames` vezivao **pokvarene pod-sprite-ove**:
`34x129` umesto `96x144`. Ranije se nije primetilo samo zato sto su sorc fajlovi vec postojali od
proslog build-a. Popravka: svi art generatori su pomereni **pre** kreiranja tierova, a odmah za njima
ide drugi prolaz `PlaceholderArtGenerator.ConfigureAll()` (novi javni ulaz).
> Test T12 je ovo uhvatio odmah — 96 padova sa tacnim dimenzijama u poruci.

**Verifikacija:** 0 `error CS` · `48 mirrored curl frames` · `96 held-item sprites` ·
`18 shorts sprites` · `250 png import settings re-applied` · `wired 14/14` · `Scene built and saved` ·
`SystemsSmokeTest PASS — 534 checks, 0 failures`.
