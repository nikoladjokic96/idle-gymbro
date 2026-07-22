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
