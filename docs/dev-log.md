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
