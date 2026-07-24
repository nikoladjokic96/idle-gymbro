# Idle GymBro — Asset Catalog (koji fajl je šta i gde ide)

> **Ovo je „mapa" svih placeholder asseta.** Svaki fajl u tabelama ispod je generisan
> placeholder sa **baked-in labelom** (roze tekst na slici piše šta je) — zameniš ga svojim
> pravim artom **1:1** (isti fajl, isto ime, ista veličina) i sve legne bez diranja koda.
>
> Tehnički standard (pivot, PPU, kako se crta): [`art-brief.md`](art-brief.md). Kratka lista
> prioriteta šta prvo crtati: [`asset-checklist.md`](asset-checklist.md).
>
> Placeholderi se generišu iz koda (menu **IdleGymBro → Generate Placeholder Character Art** /
> **Generate Placeholder Backgrounds** / **Generate Placeholder SFX**, ili automatski pri
> rebuild-u scene). Ako obrišeš placeholder i staviš svoj PNG istog imena — ostaje tvoj.

---

## 1. GLAVNI KARAKTER (sprite slojevi)

Folder: `Assets/_Game/Art/Character/Placeholders/` · sve **128×192 px**, pivot **bottom-center**, PPU 128.
Lik se u igri sastavlja slaganjem ovih slojeva (world-space, u centru ekrana). Zameni siluete pravim pixel artom.

| Fajl | Baked labela | Šta je | Gde se vidi / uloga |
|---|---|---|---|
| `body_tier1.png` | BODY1 | Telo — **Skinny** (start) | Glavni lik; vidi se od 0 ukupne zarade |
| `body_tier2.png` | BODY2 | Telo — **Slim Fit** | Na ~1.000 ukupne zarade (TIER UP) |
| `body_tier3.png` | BODY3 | Telo — **Fit** | Na ~25.000 |
| `body_tier4.png` | BODY4 | Telo — **Jacked** | Na ~500.000 |
| `body_tier5.png` | BODY5 | Telo — **Mass Monster** | Na ~10M |
| `body_tier6.png` | BODY6 | Telo — **Enhanced** (Gear) | Na ~500M (endgame look, vene/glow) |
| `head_01.png` | HEAD | Glava (deljena za sve tierove) | Sloj iznad tela |
| `hair_01.png` | HAIR | Kosa | Sloj iznad glave |
| `beard_01.png` | BEARD | Brada | Sloj iznad glave |
| `shorts_01.png` | SHORTS | Šorc | Sloj iznad tela |

> Sva tela moraju imati **istu visinu i stopala na istoj liniji** — silueta se samo širi kroz tierove.
> Tier 3 je „referentni" (kao slika koju si poslao); 1–2 mršaviji, 4–6 masivniji.

---

## 2. POZADINE (jedna po lokaciji)

Folder: `Assets/_Game/Art/Backgrounds/Placeholders/` · sve **1080×1920 px** (portret, ceo ekran), pivot center, PPU 128.
Prikazuju se iza lika; menjaju se automatski kad pređeš lokaciju (story progres). Zameni pravim scenama.

| Fajl | Baked labela | Lokacija (u igri) | Ambijent za crtanje |
|---|---|---|---|
| `bg_home.png` | HOME | **Home Workout** (start) | Soba/dnevni boravak, bez opreme |
| `bg_street.png` | STREET | **Street Workout** | Ulica/park, šipke za zgibove |
| `bg_basic_gym.png` | GYM | **Basic Gym** | Obična teretana, mašine |
| `bg_hardcore_gym.png` | HARDCORE | **Hardcore Gym** | Mračna „powerlifting" sala |
| `bg_beach.png` | BEACH | **Venice Beach** | Plaža, outdoor gym |
| `bg_olympia.png` | OLYMPIA | **Mr. Olympia** | Bina/takmičenje, reflektori |

> Neka gornje 2/3 budu tamnije/neutralnije da HUD tekst gore ostane čitljiv (donja trećina = „pod").

---

## 3. ZVUK (placeholder SFX)

Folder: `Assets/_Game/Audio/Placeholders/` · WAV, mono. Zameni pravim zvukovima istog imena.

| Fajl | Kada svira | Predlog |
|---|---|---|
| `tap.wav` | svaki rep (tap/hold) | kratak „grunt"/udarac; čuće se često — nenametljivo |
| `buy.wav` | kupovina upgrade-a | zadovoljavajući „ka-ching"/clank tegova |
| `tier_up.wav` | mišići pređu tier | trijumfalni stinger |
| `booster.wav` | aktiviran booster | „whoosh"/energija |

---

## 4. Šta NE treba da crtaš (za sada)

Ovi elementi koriste **tekst**, ne sliku — nema art posla dok ne odlučimo drugačije:
- **Upgrade dugmad** (Chest/Arms/Back/Abs/Legs, Training Partner, Gym Membership) — tekstualne kartice u scroll listi.
- **Booster dugmad** (Pre-Workout, Protein Shake) — tekst + stanje.
- **Lokacije lista, Settings, Story %** — tekst.
- **Ikonice** za upgrade/booster/dugmad — opcione, POST-MVP; ako ih budeš hteo, dodajemo icon slotove tada.

---

## 5. Kako da zameniš (workflow)

1. Nacrtaj u Aseprite-u na tačnoj veličini (128×192 karakter, 1080×1920 pozadina), poštuj pivot iz [`art-brief.md`](art-brief.md).
2. Sačuvaj PNG **preko** placeholder fajla istim imenom (npr. `body_tier3.png`), ili mi pošalji pa ubacim.
3. U Unity-ju se sam reimportuje. Ako si menjao ime/dodao novi asset — javi da preožičim slot.
4. **Ne pokreći „Generate Placeholder..." posle toga** — pregazilo bi tvoj art placeholder-om (generatori su samo za početni set).

> **Minimalni set da igra prestane da liči na placeholder:** `body_tier3` + `head_01` + `hair_01` +
> `beard_01` + `shorts_01` + `bg_home` (6 fajlova). Ostalo po prioritetu iz [`asset-checklist.md`](asset-checklist.md).
