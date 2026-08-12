# Idle GymBro — Asset Catalog (koji fajl je šta i gde ide)

> **Mapa svih art/audio asseta u igri.** Za svaki fajl piše šta je, koje je veličine i gde se vidi.
> Zameniš ga svojim fajlom **1:1** (isto ime, ista veličina) i sve legne bez diranja koda.
>
> Tehnički standard (canvas, pivot, PPU, import): [`art-brief.md`](art-brief.md).
> Kako je art nastao i koliko je koštao: [`pixellab-migration.md`](pixellab-migration.md).
>
> **Stanje (NALOG #035):** karakter, pozadine i UI ikonice su **generisani pixel art (PixelLab)**.
> Placeholder generatori (`IdleGymBro → Generate Placeholder …`) i dalje postoje, ali imaju
> `File.Exists` guard — **ne prepisuju pravi art**. Samo `DANGER — Regenerate …` forsira prepis.

---

## 1. GLAVNI KARAKTER (sprite slojevi)

Folder: `Assets/_Game/Art/Character/Placeholders/` · sve **96 × 144 px**, pivot **bottom-center**,
PPU **izveden** (`height / 1.5` → 96). Lik se sastavlja slaganjem slojeva (world-space, centar ekrana).

| Fajl | Šta je | Gde se vidi / uloga |
|---|---|---|
| `body_tier1.png` | Telo — **Skinny** (start) | Ultra mršav, tužan lik; od 0 ukupne zarade |
| `body_tier2.png` | Telo — **Slim Fit** | Na ~1.000 ukupne zarade (TIER UP) |
| `body_tier3.png` | Telo — **Fit** | Na ~25.000 · **style anchor** celog arta |
| `body_tier4.png` | Telo — **Jacked** | Na ~500.000 |
| `body_tier5.png` | Telo — **Mass Monster** | Na ~10M |
| `body_tier6.png` | Telo — **Enhanced** (Gear) | Na ~500M (endgame, suv i venozan) |
| `body_tierN_idle1..4.png` | Idle klip (disanje), 4 frejma | `MuscleTierData._idleFrames`, kad se ne trenira |
| `body_tierN_work1..4.png` | Workout klip (biceps curl), 4 frejma | `MuscleTierData._workoutFrames`, dok se drži ekran |
| `hair_01..03.png` | Kosa (3 varijante) | Sloj iznad glave · wardrobe |
| `beard_01..02.png` | Brada (2 varijante) | Sloj iznad glave · wardrobe |
| `shorts_01..03.png` | Šorc (3 varijante) | Sloj iznad tela · wardrobe |
| `blink_01.png` | Zatvoreni kapci | Kratko se pali preko lica (`CharacterAnimator`) |
| `head_01.png` | *(nekorišćen)* | Tela sadrže glavu → `_headSprite` je null na svim tierovima |

> **Tela sadrže glavu.** Ne dodavati zaseban Head sloj — crtao bi drugo lice preko prvog.
> Sva tela imaju **istu visinu i stopala na istoj liniji**; kroz tierove se menja samo silueta.
> Zato se kosa/brada/šorc poklapaju na svakom tieru bez ijedne ručne korekcije.

**Arhiva:** `Assets/_Game/Art/Character/_originals/` — fal.ai originali (848×1264) iz #022,
plus `fal_ai_848x1264/` sa starim animacionim frejmovima i napuštenim forearm-rig delovima.
Ne briše se, ne koristi se.

---

## 2. POZADINE (jedna po lokaciji)

Folder: `Assets/_Game/Art/Backgrounds/Placeholders/` · sve **216 × 384 px** (9:16), pivot center,
PPU **izveden** (`height / 15` → 25.6). Menjaju se automatski na prelasku lokacije.

| Fajl | Lokacija (u igri) | Scena |
|---|---|---|
| `bg_home.png` | **Home Workout** (start) | Skroman dnevni boravak, sofa, TV, jogа prostirka |
| `bg_street.png` | **Street Workout** | Kalistenika park, šipke, grafiti, panorama grada |
| `bg_basic_gym.png` | **Basic Gym** | Obična teretana, trake za trčanje, mašine |
| `bg_hardcore_gym.png` | **Hardcore Gym** | Mračan podrum, cigla, šipke, reflektor |
| `bg_beach.png` | **Venice Beach** | Palme, okean, outdoor gym, pesak |
| `bg_olympia.png` | **Mr. Olympia** | Bina, crvena zavesa, reflektori, pehari |

> ⚠️ **Kadar:** na 9:16 telefonu vidi se samo **centralnih 144 × 256 px** — ostatak se seče
> (kamera je ortho size 5, pozadina je 15 world jedinica visoka). Lik stoji centriran, stopala su
> na **redu ~253 od 384** (66% visine). Zato: pod u donjoj trećini, sredina prazna, važne stvari
> unutar centralne trećine po X.

---

## 3. UI IKONICE

Folder: `Assets/_Game/Art/UI/Icons/` · sve **64 × 64 px**, transparentna pozadina.
Crtaju se sa skoro belim tintom (`IconTint` 0.98/0.99/1.0), pa boja iz PNG-a prolazi neizmenjena.

| Fajl | Motiv | Gde |
|---|---|---|
| `icon_gains.png` | zlatna bučica | Top bar, brojač valute |
| `icon_upgrades.png` | zelena strelica gore | Dugme UPGRADES (desno) |
| `icon_wardrobe.png` | plava majica | Dugme WARDROBE (dole centar) |
| `icon_settings.png` | zupčanik | Dugme SETTINGS (gore desno) |
| `icon_locations.png` | mapa sa rutom | Story dugme (gore levo) |
| `icon_prestige.png` | zlatna kruna | Dugme NEW BULK |
| `icon_achievements.png` | pehar | Dugme GOALS (dole levo) |
| `icon_daily.png` | kalendar | Daily reward popup |
| `icon_reward.png` | sanduk sa zlatom | Periodic reward (dole desno) |
| `icon_chest.png` · `icon_arms.png` · `icon_back.png` · `icon_abs.png` · `icon_legs.png` | mišićne grupe | Kartice u Upgrades listi |
| `icon_preworkout.png` | energetska limenka | Booster Pre-Workout |
| `icon_protein_shake.png` | šejker | Booster Protein Shake |
| `icon_training_partner.png` | dva lika | Pasivni prihod Training Partner |
| `icon_gym_membership.png` | članska kartica | Pasivni prihod Gym Membership |

**Ostali UI oblici:** `Art/UI/Shapes/` (`panel`, `panel_soft`, `circle`) i `Art/UI/Kit/icon_cross.png`
generiše `Editor/UiShapeGenerator` — ravni beli oblici koje kod tinta. **Nisu** pixel art i ne treba
da budu (9-slice paneli sa zapečenim pikselima izgledaju loše rastegnuti).

---

## 4. ZVUK (placeholder SFX)

Folder: `Assets/_Game/Audio/Placeholders/` · WAV, mono. **Još uvek placeholderi** — zameni istim imenom.

| Fajl | Kada svira | Predlog |
|---|---|---|
| `tap.wav` | svaki rep (tap/hold) | kratak „grunt"/udarac; čuje se često — nenametljivo |
| `buy.wav` | kupovina upgrade-a | zadovoljavajući „ka-ching"/clank tegova |
| `tier_up.wav` | mišići pređu tier | trijumfalni stinger |
| `booster.wav` | aktiviran booster | „whoosh"/energija |

---

## 5. Kako da zameniš (workflow)

1. Nacrtaj na **tačnoj veličini** (96×144 karakter/kozmetika, 216×384 pozadina, 64×64 ikonica),
   poštuj pivot i import pravila iz [`art-brief.md`](art-brief.md).
2. Sačuvaj PNG **preko** postojećeg fajla istim imenom. Unity sam reimportuje.
3. Ako menjaš ime ili dodaješ novi asset — javi, treba prežičiti slot u `CoreLoopSceneBootstrap`.
4. **Ne pokreći `DANGER — Regenerate …`** — to je jedini put koji briše pravi art.

> Ako u logu rebuild-a vidiš `15 generated` umesto `(0 generated, 15 kept)`, pravi art je nestao
> s diska i generator ga je zamenio placeholderima. Vrati iz git-a.
