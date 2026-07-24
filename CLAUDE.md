# Idle GymBro — Development Guide (CLAUDE.md)

> Ovaj fajl je glavni kontekst i vodič za razvoj igre **Idle GymBro**.
> Claude Code ga automatski učitava. Pročitaj ga PRE bilo kakvog rada na projektu.
> Kada nešto uradiš, **ažuriraj [Trenutni status](#17-trenutni-status)** (kompaktno)
> i dopiši detalje/gotchas u [`docs/dev-log.md`](docs/dev-log.md).

---

## 1. Koncept

Idle/incremental mobilna igra u stilu *Masters of Madness*, *Medieval Idle Prayer* itd.
Igrač je **gymbro** koji trenira, skuplja mišiće i napreduje kroz lokacije.

- Držiš ekran → troši **energiju**, generiše **Gains** (glavna valuta).
- Trošiš Gains na upgrade-ove → veći prihod → napreduješ.
- Karakter je potpuno **customizabilan** (kosa, brada, majica, šorc, patike, tetovaže, dodaci).
- **Mišići vidno rastu** kroz progresiju — glavni vizuelni feedback igre.
- Počinje kod kuće bez opreme → garaža → teretana → ... → Mr. Olympia.
- Idle žanr: zarađuje i **offline** i preko **pasivnog prihoda**.

**Identitet igre:** gym kultura + meme humor ("natty or not", "leg day", "do you even lift").
Jak, deljiv identitet = besplatni viralni marketing.

---

## 2. Zaključane odluke (ne menjati bez dogovora)

| Tema | Odluka |
|---|---|
| Engine | **Unity 6 (LTS)**, 2D URP |
| Jezik | **C#** |
| Art | **Pixel art, front-view** (standard: [`docs/art-brief.md`](docs/art-brief.md) — canvas 128×192, PPU 128, pivot bottom-center). Izvor: Aseprite/umetnik/AI → **sve mora biti swappable** kroz imenovane slotove |
| Platforma | **Android prvo** (min API 24, IL2CPP, ARM64) |
| Data | **ScriptableObjects** (data-driven, balans bez koda) |
| Monetizacija | **Soft "Medieval Idle Prayer" model** — duga progresija, opt-in reklame, soft wall, zarada nenametljiva (vidi [sekciju 10](#10-monetizacija)) |

---

## 3. Tech stack (paketi)

| Sloj | Alat |
|---|---|
| Engine | Unity 6 LTS, 2D URP |
| Input | Unity Input System |
| Tekst | TextMeshPro |
| Animacije / juice | Animator + **DOTween** |
| Reklame | **Unity LevelPlay** (mediation) + AdMob/Unity Ads |
| IAP | **Unity IAP** |
| Save | **JSON (Newtonsoft)**, lokalni fajl, **enkriptovan** (AES-CBC — implementirano) |
| Analytics | GameAnalytics ili Unity Analytics (besplatno) |

**Instalirano (core):** URP, 2D feature, Input System, TMP, Newtonsoft. **Dodaje se u svojoj fazi:** DOTween (Faza 4), LevelPlay + Unity IAP (Faza 5), Analytics (Faza 8).
> Napomena (Unity 6): TextMeshPro dolazi **unutar `com.unity.ugui`** — nema zasebnog TMP paketa; Essentials resursi su commit-ovani u repo.

---

## 4. Arhitektura projekta

```
Assets/
  _Game/
    Scripts/
      Core/          GameManager, TickSystem, SaveSystem, EventBus
      Economy/       CurrencyManager, UpgradeManager, formule
      Gameplay/      EnergySystem, TapController, ExerciseController
      Character/     CharacterBuilder, CustomizationManager, MuscleTiers
      Progression/   LocationManager, PrestigeManager
      Meta/          DailyRewards, Achievements, Quests
      Monetization/  AdManager, IAPManager
      UI/            screen kontroleri (MVC/MVVM)
      Data/          ScriptableObject definicije (klase)
    Data/            .asset fajlovi (vežbe, upgrade, lokacije, cosmetics)
    Art/  Audio/  Prefabs/  Scenes/
```

### Ključni principi (OBAVEZNO poštovati)

1. **Data-driven** — sve brojke (cene, gains, growth rate, energija) žive u ScriptableObject-ima, NIKAD hardkodovane u logici. Balansira se kao dizajner, bez rebuild-a.
2. **Art odvojen od logike** — sprite-ovi se referenciraju kroz ScriptableObject slotove. Zamena arta = zamena `.png` fajla u slotu, bez diranja koda.
3. **EventBus (observer pattern)** — sistemi emituju evente (`GainsChangedEvent`, `UpgradePurchasedEvent`...), UI sluša. UI ne poziva logiku direktno.
4. **TickSystem** — centralni tick (npr. svakih 100ms) ažurira energiju/regen, pasivni prihod, boostere.
5. **Scope discipline** — sve van trenutne faze roadmapa je POST-MVP. Ne dodavati feature-e unapred.
6. **Scena se GENERIŠE, ne edituje ručno** — `SampleScene` gradi `Editor/CoreLoopSceneBootstrap` (menu **IdleGymBro → Build Core Loop Scene**, ili headless `-executeMethod`). Tool je idempotentan (briše i ponovo gradi `CoreLoop` root) → **svaka ručna izmena scene se GUBI na sledeći rebuild**. Nova UI/sistem komponenta u sceni = izmena bootstrap tool-a, pa rebuild.

### Verifikacioni protokol (svaki nalog pre commit-a)

- **Compile:** Unity batchmode (`-batchmode -quit -nographics -logFile`) — log bez `error CS`.
- **Scena:** rebuild kroz bootstrap; self-check u logu mora reći `_gameConfig wired on N/N`.
- **Smoke testovi:** headless `-executeMethod` (npr. `SaveSystemSmokeTest.RunSaveRoundTrip`).
- Editor i batchmode **ne mogu istovremeno** (project lock). Prvi batchmode posle novih skripti ume samo da kompajlira — ponovi run.
- Pun spisak naučenih gotcha-a: [`docs/dev-log.md`](docs/dev-log.md#poznati-gotchas).

---

## 5. Core mehanike + formule

### Energija & Gains (aktivni trening)
- Držiš ekran → svaki "rep tick" troši `energyPerRep`, dodaje `gainsPerRep`.
- Energija prazna → karakter staje (umoran), regen kreće kad pustiš.
- Formula:
  ```
  gainsPerRep = baseGain × Σ(bonusi delova tela) × globalMultiplier × boosterMultiplier
  ```
- Upgrade-abilno: `maxEnergy`, `energyRegenRate`, `energyPerRep`, `gainsPerRep`.

### Pasivni prihod (čini igru IDLE, ne clicker)
- `gainsPerSecond` iz pasivnih izvora (trening partner, članarina...). Kuca kroz TickSystem i kad ne tapkaš.

### Offline zarada
```
timeAway     = now − lastSaveTime
offlineGains = min(timeAway, offlineCap) × gainsPerSecond × offlineEfficiency
```
- Claim popup: "Dok si spavao, tvoj gymbro je trenirao... +X Gains" + dugme **"Udvostruči (reklama)"**.
- `offlineCap` (npr. 2h → upgrade do 12h) = razlog za povratak + monetizacija.

---

## 6. Ekonomija i balans

### Skaliranje cena (eksponencijalno)
```
cost(level) = baseCost × growthRate ^ level
```
- `growthRate` ≈ **1.07–1.15**. Predlog: niži na startu (~1.07) za brz rani hook, viši u kasnim upgrade-ovima/fazama (~1.15+) za namerni **soft wall**. Fino tunirati kroz analitiku.

### Pravila balansa (u duhu soft-wall modela — vidi [sekciju 10](#10-monetizacija))
- **Kriva troškova: blaga na startu, strmija u kasnim fazama** → rani igrač brzo napreduje (hook), kasni oseća da mora da bude strpljiv ili da uzme boost.
- Rast prihoda prati cene ali **malo zaostaje** → "sledeći upgrade je taman predaleko" osećaj drži igrača.
- **Soft wall dizajniran namerno:** posle određene tačke F2P tempo (bez reklama, bez plaćanja) se **vidno usporava, ali kriva nikad ne postaje beskonačna** — sve je dostižno strpljenjem.
- **Duga progresija je cilj:** prosečan igrač NE sme da završi za par dana. Max level / endgame se dostiže nedeljama/mesecima (+ slojevi prestige-a).
- **NE pogađati brojke** — staviti u ScriptableObject, pa tunirati kroz analitiku (gde igrači zapnu = zid pretežak/prelak).

### Prestige ("Nova sezona / New Bulk")
```
respect          = k × sqrt(totalGainsEarned)
globalMultiplier = 1 + respect × factor
```
- Reset progresa za trajni bonus. Otključava se tek posle prvog celog run-a.

---

## 7. Karakter & art sistem (najkompleksniji deo)

### Muscle tiers
5–6 nivoa naduvanosti baznog tela: mršav → slim-fit → fit → jacked → mass monster → **enhanced (Gear)**.
Kako Gains rastu, telo prelazi u sledeći tier. **Najzadovoljavajući vizuelni feedback.**

### Modularni layered sprite (redosled dubine)
```
[pozadina] < telo(tier) < donji veš/šorc < patike < majica < ruke < glava(tier) < brada < kosa < dodaci
```
Kustomizacija = zamena sprite-a u sloju.

### Pravila za AI art (i buduću zamenu kvalitetnijim)
> **Puni standard: [`docs/art-brief.md`](docs/art-brief.md)** — stil (pixel art, front-view), canvas 128×192, PPU 128, pivot bottom-center, slojevi, imenovanje, folderi, Unity import, animacije. Odluka o stilu: **pixel art, front-view** (po referenci koju je korisnik dao).
- **Fiksne anchor tačke i rezolucija** (rame, kuk, glava, canvas veličina) — dogovoreni standard. Svaki art (AI ili budući) poštuje iste anchore → sve se poravna bez prepravki.
- **Style guide od prve**: rezolucija, **front-view** ugao kamere, proporcije, paleta. Isti brief za AI i budućeg umetnika.
- **Imenovani slotovi**: `body_tier3`, `hair_01`, `beard_02`... → zamena arta = zamena fajla u slotu, ne refaktor.
- **MVP**: 1 muscle tier + par slojeva. Sistem prvo, art posle. NE generisati gomilu arta pre nego sistem proradi s placeholderima.

### Animacije
- MVP: frame-by-frame sprite animacija po vežbi (sklek, čučanj); kustom slojevi prate iste keyframe-ove.
- Napredno (post-MVP): skeletal/bone (Unity 2D Animation).

---

## 8. Kustomizacija
Kategorije: kosa, brada, ton kože, majica, šorc, patike, tetovaže, dodaci (slušalice, kaiš, rukavice, lanci).
Svaki item = ScriptableObject: `id`, `sloj`, `sprite`, `cena`, `nacinOtkljucavanja` (progresija / Gains / IAP / reklama).

---

## 9. Progresija — lokacije

| # | Lokacija | Vežbe | Otključava |
|---|---|---|---|
| 1 | Kuća (bodyweight) | sklek, čučanj, zgib, trbušnjak | start |
| 2 | Garaža/dvorište | bučice, klupa, vratilo | Gains prag |
| 3 | Basic teretana | šipke, mašine | Gains prag |
| 4 | Hardcore gym | mrtvo dizanje, teški vučci | Gains prag |
| 5 | Plaža/takmičenje | poziranje, bodybuilding show | Gains prag |
| 6 | Mr. Olympia (endgame) | — | prestige-gated |

Svaka: nova pozadina, nove vežbe (veći base gains), novi upgrade-ovi, nova kozmetika.
Prelazak = milestone (mesto za interstitial reklamu).

---

## 10. Monetizacija

### ⭐ Filozofija — "Medieval Idle Prayer" model (NAJVAŽNIJE PRAVILO IGRE)

Vodeći princip cele igre. Svaka odluka o balansu, reklamama i IAP-u MORA ga poštovati.

1. **Duga progresija** — max level / endgame se NE dostiže lako. Tunira se da kompletiranje traje nedeljama/mesecima za prosečnog igrača (kasne faze = strmija kriva + slojevi prestige-a).
2. **Soft wall, NIKAD hard wall** — u jednom trenutku F2P napredak (bez reklama i bez plaćanja) postaje **vidno usporen ali nikad zaustavljen**. Troškovi skaliraju brže od prihoda, ali svaki sadržaj je dostižan besplatno — samo sporije. Ništa se ne zaključava iza plaćanja.
3. **Reklame nikad nametnute** — sve je opt-in rewarded ("boost" koji igrač bira) ili utkano u fantaziju (Gear = steroidi). Interstitial minimalno, nikad ne blokira igranje, nema "plati da nastaviš".
4. **Ne sme da se VIDI da je zarada prioritet** — boosteri se prezentuju kao gameplay feature (pre-workout, protein), NE kao "BUY NOW" banneri. Bez dark patterna, bez lažne hitnje, bez pop-up spama. Igra deluje kao opuštajući idle, ne kao kockarnica.
5. **Ali se ipak zarađuje** — usporenje stvara **prirodnu, nenametnutu želju** da igrač pogleda rewarded reklamu ili uzme Gear. Konverzija dolazi iz vrednosti i strpljenja, ne iz prisile. Suptilno, ali profitabilno.

> **Test svake monetizacione odluke:** "Da li bi ovo smetalo igraču koji nikad ne plaća?" Ako da → preblizu je agresivnom modelu, ublaži.

### Rewarded (dobrovoljno, igrač bira)
- **Pre-workout** — +energija, −trošenje, x2 gains na X min
- **Protein šejk** — instant paket gainsa
- **"Spot me bro"** — x2 offline zarada
- Udvostruči nagradu (daily, achievement)

### Interstitial (umereno!)
Na prelasku lokacije, posle N prestige-a. Previše = ubija retenciju.

### Gear (steroidi) = premium
- Skida SVE reklame
- Trajni x2+ na gains
- Ekskluzivna kozmetika: vene, "enhanced" muscle tier, glow
- **Model: jednokratni "Remove ads + Gear" paket** (npr. 4.99$) — najbolja konverzija + najpoštenije.

### Kozmetički IAP
Ekskluzivni outfiti/tetovaže — čisto vizuelno, **ne pay-to-win**.

**Etika:** free igrač MORA moći da napreduje. Reklame ubrzavaju, Gear uklanja frikciju. Pošteno = bolji reviewi = retencija.

---

## 11. UI ekrani (MVP)
> **Ciljni raspored glavnog ekrana: [`docs/ui-layout.md`](docs/ui-layout.md)** (po uzoru na Medieval Idle Prayer; svako dugme se dodaje tek u svojoj fazi, na unapred rezervisanom mestu).
- Main game (karakter + energy bar + gains counter + tap zona + booster dugmad)
- Upgrade panel (delovi tela, pasivni prihod) — *implementiran kao modal*
- Wardrobe / kustomizacija
- Offline claim popup
- Shop (Gear, kozmetika)
- Prestige ekran *(post-MVP)*
- Daily / Quests *(post-MVP)*

### UI standard (zaključano kroz Fazu 2)
- **Dizajn prostor: portrait 1080×1920**; `CanvasScaler` = ScaleWithScreenSize + **`ScreenMatchMode.Expand`** (dizajn uvek staje na ekran — i na telefonu i u landscape Game view; modali ne mogu da prelete ekran).
- **`EventSystem` + `InputSystemUIInputModule` (AssignDefaultActions) je OBAVEZAN** — bez njega nijedno UI dugme ne prima klik. Bootstrap ga pravi.
- **Modali kroz `UI/ModalToggle`** — open dugme + „X" + klik na dimmer zatvara; panel počinje skriven; dimmer je raycast target (blokira igru ispod).
- **Tap nad UI-jem ne trenira** — `TapController` preskače input kad je `EventSystem.IsPointerOverGameObject()`.

---

## 12. Meta sistemi (retencija) — post-MVP
- **Daily streak** — nagrada za uzastopne dane (gym doslednost tema)
- **Achievements** — "10.000 sklekova", "Prvi 1kg gainsa"
- **Daily quests** — "Uradi 500 čučnjeva", "Kupi 3 upgrade-a"
- **Sezonski eventi** — Summer Shred, Bulking Season

---

## 13. Ideje za kasnije (POST-MVP, ne dirati sad)
Form/combo ritam mehanika · Flex/Photo mode za deljenje · Rival/leaderboard · Pets/companions · Injuries (overtraining rizik).

---

## 14. Roadmap po fazama

| Faza | Trajanje | Cilj |
|---|---|---|
| **0. Setup** | 2-3 dana | Unity projekat, folderi, paketi, git |
| **1. Core loop** | 1 ned | Tap→energija→gains, TickSystem, save/load. Placeholder kocka umesto lika |
| **2. Ekonomija** | 1 ned | Upgrade sistem (delovi tela), pasivni prihod, offline zarada |
| **3. Karakter** | 1-2 ned | Modularni sprite, 1 muscle tier, osnovna kustomizacija, 1 animacija |
| **4. MVP polish** | 1 ned | UI, 1 booster, juice (DOTween), zvuk → **igriv MVP** |
| **5. Monetizacija** | 1 ned | LevelPlay reklame + Unity IAP + Gear |
| **6. Progresija** | 1-2 ned | Lokacija 2-3, prestige, više muscle tierova |
| **7. Meta** | 1 ned | Daily, achievements, quests |
| **8. Soft launch** | — | Android internal test, analitika, balans tuning |

**MVP = faze 0–4 (~4-5 nedelja) do igrivog na telefonu.**

---

## 15. Rizici / podsetnici
- **Art je usko grlo** — sistem slojeva s placeholderima prvo, art posle.
- **Balans se ne pogađa** — ScriptableObject + analitika + iteracija.
- **Scope creep** — sve iz sekcije 12/13 je post-MVP.
- **Save security** — enkriptuj save (bitno kad ima IAP).
- **Testiraj na pravom telefonu rano** — tap feel i performanse su drugačiji nego u editoru.

---

## 16. Konvencije koda
- C# naming: `PascalCase` za klase/metode/properties, `camelCase` za lokalne/parametre, `_camelCase` za privatna polja. Inspector reference: `[SerializeField] private`. 4-space indent, Allman zagrade.
- Jedan ScriptableObject tip po sistemu (npr. `ExerciseData`, `UpgradeData`, `LocationData`, `CosmeticData`).
- Logika ne referencira art direktno — uvek preko data slota.
- Manageri komuniciraju kroz EventBus, ne međusobnim direktnim pozivima gde god je moguće.
- Komentari samo gde objašnjavaju "zašto"/constraint, ne "šta".

### Ustaljeni obrasci (primenjuju se u svakom novom sistemu)
- **Eventi:** `public readonly struct XyzEvent : IGameEvent` sa ctor-om; žive uz svog primarnog publisher-a ili u `*Events.cs`.
- **Pretplate:** `Subscribe` u `OnEnable`, simetričan `Unsubscribe` u `OnDisable` — bez izuzetka (leak-ovi).
- **Null-config guard:** sistem sa `_gameConfig` loguje grešku JEDNOM (`_missingConfigLogged`) i gasi svoju funkciju, nikad ne baca.
- **Inicijalni event u `Start()`** (ne u `OnEnable`) — da su svi subscriberi spremni. Redosled izvršavanja: `GameManager` je `-1000` (EventBus.Clear pre pretplata), `SaveSystem` je `+1000` (restore posle default state-a).
- **Efektivne stat vrednosti:** sistemi keširaju vrednost iz `StatsChangedEvent` (default = config base pre prvog eventa); `UpgradeManager` je jedini koji agregira.
- **Valuta:** `double` za Gains (idle brojevi rastu); trošenje isključivo kroz `CurrencyManager.TrySpend`.

---

## 17. Trenutni status

> Detaljna istorija svakog naloga (šta, kako verifikovano, gotchas): [`docs/dev-log.md`](docs/dev-log.md).

**Faza 0 (Setup) — GOTOVA · Faza 1 (Core loop) — GOTOVA · Faza 2 (Ekonomija) — funkcionalna · Faza 3 (Karakter) — u toku**

- [x] #001 Projekat + paketi + git + core backbone (`EventBus`, `TickSystem`, `GameConfig`, `GameManager`); Android SDK/NDK kroz `scripts/setup-dev-env.ps1`
- [x] #002 Core loop: hold → `TapEvent` → energija → `RepPerformedEvent` → Gains (event-driven)
- [x] #003 HUD (gains/energy/passive rate) + `NumberFormatter` + `PlaceholderCharacter`
- [x] #004 Scena iz koda: `Editor/CoreLoopSceneBootstrap` + TMP Essentials + `GameConfig.asset`
- [x] #005 `SaveSystem` (AES + Newtonsoft, autosave/pause/quit) + `ISaveable` + smoke test
- [x] #006 Pasivni prihod + offline zarada (§5 formula) + `OfflineClaimPopup`
- [x] #007 Upgrade sistem (`UpgradeData` SO, `StatsChangedEvent` agregacija, `TrySpend`) + 3 placeholder upgrade-a
- [x] Fix: `EventSystem` (klikabilna dugmad) + upgrade **modal** (`ModalToggle`) + tap-over-UI guard + `CanvasScaler.Expand`
- [x] #008 Karakter sistem (Faza 3): `CharacterBuilder` (layered SpriteRenderer stack, world-space), muscle tiers po **`TotalEarned`** (lifetime — kupovina ne smanjuje mišiće), `MuscleTierData`/`CosmeticData` SO, `PlaceholderArtGenerator` (10 PNG placeholder-a po art-brief specifikaciji)
- [ ] Faza 3 nastavak: animacije (idle + rep po tieru), wardrobe/kustomizacija UI, pravi pixel art (čeka assete — [`docs/asset-checklist.md`](docs/asset-checklist.md))
- [ ] Balans tuning (krive cena/prihoda kroz playtest — §6/§10)
- [ ] Više stat tipova (maxEnergy/regen), još upgrade-ova; prestige je post-MVP

> **Sledeći korak (za razmatranje):** Faza 3 nastavak (animacioni sistem + wardrobe) · ILI Faza 4 polish (HUD raspored po [`docs/ui-layout.md`](docs/ui-layout.md), DOTween juice, zvuk) · ILI balans tuning.
> Setup na novom PC-u: `scripts/setup-dev-env.ps1` (vidi `SETUP.md`).

### Radni model (arhitekta + pod-agenti)
Arhitekta (Opus/Fable) piše „Nalog za Pod-Agenta" → jeftiniji model (Sonnet/Haiku) piše kod → arhitekta radi pregled (konvencije §16, leak-ovi, data-driven, event ordering) → **verifikacioni protokol iz §4** → commit + push (samo arhitekta). Pod-agent ne commit-uje.

> **Pun protokol orkestracije** (role, pravila, sub-agent profili, format naloga): [`docs/agent-workflow.md`](docs/agent-workflow.md).

*(Claude: posle svakog koraka ažuriraj checklistu gore + dopiši detalje u `docs/dev-log.md`.)*
