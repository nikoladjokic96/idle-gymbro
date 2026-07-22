# Idle GymBro — Development Guide (CLAUDE.md)

> Ovaj fajl je glavni kontekst i vodič za razvoj igre **Idle GymBro**.
> Claude Code ga automatski učitava. Pročitaj ga PRE bilo kakvog rada na projektu.
> Kada nešto uradiš, **ažuriraj sekciju [Trenutni status](#17-trenutni-status)**.

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
| Art | **AI-generisan za sada**, kasnije moguća zamena kvalitetnijim → **sve mora biti swappable** |
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
| Save | **JSON (Newtonsoft)**, lokalni fajl, **enkriptovan** |
| Analytics | GameAnalytics ili Unity Analytics (besplatno) |

**Instalirati na startu:** URP, Input System, TextMeshPro, DOTween, Newtonsoft JSON, LevelPlay, Unity IAP.

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
3. **EventBus (observer pattern)** — sistemi emituju evente (`OnGainsChanged`, `OnUpgradePurchased`), UI sluša. UI ne poziva logiku direktno.
4. **TickSystem** — centralni tick (npr. svakih 100ms) ažurira energiju/regen, pasivni prihod, boostere.
5. **Scope discipline** — sve van trenutne faze roadmapa je POST-MVP. Ne dodavati feature-e unapred.

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
- **Fiksne anchor tačke i rezolucija** (rame, kuk, glava, canvas veličina) — dogovoreni standard. Svaki art (AI ili budući) poštuje iste anchore → sve se poravna bez prepravki.
- **Style guide od prve**: rezolucija, side-view ugao kamere, proporcije, paleta. Isti brief za AI i budućeg umetnika.
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
- Main game (karakter + energy bar + gains counter + tap zona + booster dugmad)
- Upgrade panel (delovi tela, pasivni prihod)
- Wardrobe / kustomizacija
- Offline claim popup
- Shop (Gear, kozmetika)
- Prestige ekran *(post-MVP)*
- Daily / Quests *(post-MVP)*

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
- C# naming: `PascalCase` za klase/metode/properties, `camelCase` za lokalne/parametre, `_camelCase` za privatna polja.
- Jedan ScriptableObject tip po sistemu (npr. `ExerciseData`, `UpgradeData`, `LocationData`, `CosmeticData`).
- Logika ne referencira art direktno — uvek preko data slota.
- Manageri komuniciraju kroz EventBus, ne međusobnim direktnim pozivima gde god je moguće.
- Komentari samo gde objašnjavaju "zašto"/constraint, ne "šta".

---

## 17. Trenutni status

**Faza: 0 (Setup) — GOTOVA. Faza 1 (Core loop) — u toku.**

- [x] Dizajn i plan zaključani (ovaj dokument)
- [x] Unity projekat kreiran (2D URP, Unity **6000.0.79f1**, iz template-a u repo root)
- [x] Folder struktura (`_Game/…`) postavljena — NALOG #001
- [x] Paketi instalirani — core: URP, 2D feature, Input System, TMP, Newtonsoft *(DOTween/LevelPlay/IAP se dodaju u svojim fazama)*
- [x] Git inicijalizovan + povezan sa GitHub-om (`origin` → github.com/nikoladjokic96/idle-gymbro, javni repo)
- [~] Android Build Support: editor + build support OK; **SDK/NDK parcijalno** — dovršiti kroz Hub GUI pred prvi APK build

**Faza 1 (Core loop) — u toku:**
- [x] Core backbone: `EventBus`, `TickSystem`, `TickEvent`, `GameConfig`, `GameManager` — NALOG #001 (Sonnet, review-ovan, kompajlira)
- [ ] EnergySystem (energija: trošenje na tap, regen kroz TickSystem)
- [ ] CurrencyManager (Gains valuta) + TapController (tap → gains)
- [ ] SaveSystem (JSON/Newtonsoft, enkriptovan) + offline zarada
- [ ] Placeholder kocka umesto lika

> **Sledeći korak:** NALOG #002 — sledeći sistem koji se kači na backbone (npr. `EnergySystem` + `CurrencyManager` koji slušaju `TickEvent`).
> Setup na drugom PC-u: `scripts/setup-dev-env.ps1` (vidi `SETUP.md`).

### Radni model (arhitekta + pod-agenti)
Opus (arhitekta) piše „Nalog za Pod-Agenta" → jeftiniji model (Sonnet/Haiku) piše kod → arhitekta radi pregled (konvencije, leak-ovi, data-driven, compile) → commit. Pod-agent ne commit-uje.

*(Claude: ažuriraj ovu sekciju posle svakog urađenog koraka.)*
