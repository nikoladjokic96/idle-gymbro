# Idle GymBro — Art Brief & Style Guide

> Zaključani standard za sav art (§7 „Style guide od prve"). Svaki AI-generisani ili
> ručno crtani asset MORA da poštuje ovaj dokument da bi se slojevi poravnali bez prepravki.
> Produkcija arta je **Faza 3** — do tada radimo sa placeholderima. Ovaj fajl je referenca
> „šta da napravim i gde da stavim".

---

## 1. Stil (po referentnoj slici)

- **Pixel art**, front-view (lik gleda ka igraču).
- Čist **1px outline**, meko cel-shading (2–3 tona po materijalu).
- Ograničena, **deljena paleta** (~24–32 boje) — ista za sve assete.
- Čitljiv na malom (telefon), „chunky" pikseli — **nema anti-aliasa/blur-a**.
- Ton: gym-bro, meme-friendly, malo kariкaturalno ali cool.

> **Napomena:** ovo menja raniju §7 belešku „side-view" → **front-view** (po tvojoj referenci).
> Front-view najbolje pokazuje fizičku promenu (muscle tiers) i kustomizaciju.

## 2. Tehnički standard (OBAVEZNO isto za sve)

> **Zaključano 2026-08-12 (PixelLab migracija).** Brojke ispod su izvedene iz PixelLab limita,
> ne proizvoljne — vidi [`pixellab-migration.md`](pixellab-migration.md) §4 za obrazloženje.

| Parametar | Vrednost |
|---|---|
| **Canvas lika po frejmu** | **96 × 144 px** (portret 2:3), isto za SVE slojeve i frejmove |
| **Canvas UI ikonice** | **64 × 64 px**, transparentna pozadina |
| **Canvas pozadine** | **216 × 384 px** (9:16 — tačno 1/5 dizajn prostora 1080×1920) |
| **Registracija** | lik centriran po X; **stopala na fiksnoj baznoj liniji** (y=6 od dna) |
| **Pivot** | **Bottom-Center** — isti za svaki sloj → slojevi se slažu pixel-perfect |
| **Pixels Per Unit (PPU)** | **IZVODI SE, ne kuca se ručno** — lik `height / 1.5`, pozadina `height / 15` |
| **Skala** | crtaj u 1× (96×144); Unity skalira gore Point filterom |

### Zašto baš 96 × 144

Canvas je biran da **animacija košta 1 generaciju**. PixelLab naplaćuje `animate_image`
po formuli `ceil(w · h · frames / 65536)` po pravcu:

| Canvas | 4 frejma | 8 frejmova |
|---|---|---|
| **96 × 144** | **1 gen** ✅ | 2 gen |
| 128 × 192 (stari brief) | 2 gen | 3 gen |

Uz 6 tierova × 2 animacije (idle + rep), razlika je **12 vs 24+ generacija** — na nalogu
od 40 to je razlika između „animacije staju" i „ne staju".

> **Ne menjaj canvas bez ponovnog računa.** Limiti: `create_image_pixflux` prima 16–400 px
> po strani (ukupna površina do 400×400); `animate_image` traži frejm ≤ 256×256 i
> `w · h · frame_count ≤ 524288`. 96×144 prolazi kroz oba sa rezervom.

### Unity import (za svaki sprite/sheet)
- **Filter Mode: Point (no filter)**
- **Compression: None**
- **Generate Mip Maps: OFF**
- **Mesh Type: Full Rect**
- **Pixels Per Unit: IZVEDEN iz visine teksture** (`PlaceholderArtGenerator` / `PlaceholderBackgroundGenerator` to rade automatski) — nikad fiksna vrednost, inače art druge rezolucije nemo promeni veličinu scene
- Pivot: Custom → Bottom-Center (ili Custom po anchoru dole)

> Ključ modularnosti: **isti canvas + isti pivot** za svaki sloj. Ako svi crtaju lika
> u 128×192 sa stopalima na istoj liniji, hair/brada/šorc/telo se slažu automatski.

## 3. Slojevi (depth order, §7)

Od pozadine ka napred (svaki sloj = zaseban PNG, providan gde nema sadržaja):

```
[pozadina]  <  telo(tier)  <  šorc  <  patike  <  majica  <  ruke  <  glava(tier)  <  brada  <  kosa  <  dodaci
```

- Za shirtless lik (kao referenca): sloj `majica` prazan.
- `ruke` + `gloves`/dumbbell su blizu napred (drže se ispred trupa).
- `glava(tier)` se blago menja s tierom (deblji vrat na višim tierovima).

## 4. Muscle tiers (§7)

Bazno telo u **6 nivoa** naduvanosti (glavni vizuelni feedback):

| Tier | Naziv | Fajl |
|---|---|---|
| 1 | mršav (skinny) | `body_tier1.png` |
| 2 | slim-fit | `body_tier2.png` |
| 3 | fit | `body_tier3.png` |
| 4 | jacked | `body_tier4.png` |
| 5 | mass monster | `body_tier5.png` |
| 6 | enhanced (Gear) | `body_tier6.png` |

Referentna slika ≈ **tier 4–5**. Isti pivot/anchor za sve tierove (silueta raste, stopala ostaju na liniji).

## 5. Imenovanje slotova

`kategorija_naziv[_tier].png`, lowercase, bez razmaka:
- `body_tier3.png`, `head_tier3.png`
- `hair_01.png`, `hair_02.png`
- `beard_01.png`, `beard_02.png`
- `shorts_01.png`, `shoes_01.png`, `gloves_01.png`
- `accessory_chain_01.png`, `accessory_headphones_01.png`

## 6. Folderi (gde fajlovi idu)

```
Assets/_Game/Art/Character/
  Body/        body_tier1..6.png
  Head/        head_tier1..6.png
  Hair/        hair_01.png ...
  Beard/       beard_01.png ...
  Shorts/      shorts_01.png ...
  Shoes/       shoes_01.png ...
  Gloves/      gloves_01.png ...
  Accessories/ accessory_*.png
  Animations/  <exercise>_<tier>_sheet.png (sprite sheets)
```

## 7. Animacije (§7) — ✅ implementirano (NALOG #035)

**NE sprite sheet.** Svaki frejm je **zaseban PNG** od 96×144, jer `CoreLoopSceneBootstrap.AssignFrames`
skenira fajlove po imenu dok ne naiđe na rupu:

```
body_tier<N>_idle1.png … _idle4.png    →  MuscleTierData._idleFrames    (disanje, loop)
body_tier<N>_work1.png … _work4.png    →  MuscleTierData._workoutFrames (bicep curl, dok se drži ekran)
```

Sprite Mode ostaje **Single** za svaki frejm (Multiple pamti isečene pod-rect-ove, pa bi zamena
arta druge veličine kropovala ustajali pravougaonik).

- **Nedostajući frejm se tiho preskače** — tier bez klipa drži svoju statičnu pozu. Ali
  `SystemsSmokeTest` T12 **traži da svaki tier ima bar 1 idle i 1 workout frejm**, pa se klipovi
  ne smeju samo obrisati.
- Frejm mora deliti **isti canvas i isti PPU** kao statična poza (T12 to proverava) — inače lik
  poskoči između frejmova i kozmetički slojevi prestanu da se poklapaju.
- Kozmetika se **ne animira** — hair/beard/shorts su statični slojevi preko animiranog tela.
  Na 96×144 sa suptilnim disanjem to se ne primećuje; za veće amplitude bi trebalo po-frejm slojeve.

**Kako se prave (PixelLab):** `animate_image` prima „loose" PNG (ne traži PixelLab rig).
`96 × 144 × 4 frejma = 55.296 px ≤ 65.536` → **1 generacija po klipu**.

## 7b. Kozmetika (hair / beard / shorts)

Kozmetički slojevi **nisu generisani PixelLab-om.** Pokušaj kroz `create_image_pixflux` img2img je
pao: na `init_strength` 300 model ne doda kosu uopšte (ostane ćelav), a na 150 precrta celo telo —
u oba slučaja „razlika varijante i baze" (trik iz #022) daje šum umesto sloja. `inpaint_image`, koji
bi to rešio čisto, košta **20–40 generacija po pozivu**.

Umesto toga: **fal.ai slojevi iz #022 su smanjeni sa 848×1264 na 96×144** i propušteni kroz tvrdu
alfu + kvantizaciju boje. Poklapaju se **po konstrukciji** — i pixel tela i ti slojevi potiču iz
iste 848×1264 kompozicije, pa kosa sleće na teme a šorc na kukove na svakom tieru.

## 8. Kako da napraviš (tooling)

- **Preporuka: [Aseprite](https://www.aseprite.org/)** — standard za pixel art (slojevi, animacija, paleta, export sheet-ova). Alternativa: Piskel (besplatan, browser), Krita, Photoshop.
- Zaključaj **jednu paletu** (izvuci iz reference ili definiši ~24 boje) i koristi je svuda.
- Radi svaki sloj na istom **96×144** canvasu, stopala na baznoj liniji.
- Export: svaki sloj/frejm kao zaseban PNG u odgovarajući folder iznad.
- **Izvor arta danas: PixelLab** (vidi [`pixellab-migration.md`](pixellab-migration.md)).
  Registracija se ne prepušta modelu — svaki tier je `img2img` iz **svog** fal.ai originala
  smanjenog na 96×144, pa poza, visina glave i linija stopala ostaju identične po konstrukciji.

## 9. Šta ja (Claude) gradim uz ovo

Kad slotovi/PNG-ovi postoje, ja pišem runtime sistem (Faza 3, §7):
- `CharacterBuilder` / layer kompozitor (slaže slojeve po depth order-u),
- `MuscleTiers` swap (telo prelazi tier kad Gains pređu prag),
- `CosmeticData` ScriptableObject-i (`id`, sloj, sprite, cena, način otključavanja),
- animacioni state (idle/vežba po lokaciji).
Art se kači u imenovane slotove — **zamena arta = zamena PNG-a, bez diranja koda** (§4 pravilo 2).

## 10. Odlučeno (bilo „za potvrdu")

- [x] View = **front** (potvrđeno referencom).
- [x] Canvas: **lik 96×144 · ikonica 64×64 · pozadina 216×384**, PPU izveden (§2).
- [x] Ko crta: **PixelLab generisanje** + lokalna obrada (sečenje gridova, tvrda alfa, kvantizacija).
- [x] Asset set: svih 6 tierova + 8 kozmetika + blink + 6 pozadina + 18 ikonica + idle/workout klipovi.

**Preostalo:** SFX su i dalje placeholderi ([`asset-catalog.md`](asset-catalog.md) §4).
