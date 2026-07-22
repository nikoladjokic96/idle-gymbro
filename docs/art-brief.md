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

| Parametar | Vrednost |
|---|---|
| **Canvas po frejmu** | **128 × 192 px** (portret), isto za SVE slojeve i frejmove |
| **Registracija** | lik centriran po X; **stopala na fiksnoj baznoj liniji** (npr. y=8 od dna) |
| **Pivot** | **Bottom-Center** — isti za svaki sloj → slojevi se slažu pixel-perfect |
| **Pixels Per Unit (PPU)** | **128** (isto za sve sprite-ove) |
| **Skala** | crtaj u 1× (128×192); Unity skalira gore Point filterom |

### Unity import (za svaki sprite/sheet)
- **Filter Mode: Point (no filter)**
- **Compression: None**
- **Generate Mip Maps: OFF**
- **Mesh Type: Full Rect**
- **Pixels Per Unit: 128**
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

## 7. Animacije (§7)

- **Frame-by-frame** sprite sheet, horizontalna traka; svaka ćelija = **128×192** (isti canvas), konzistentan broj frejmova.
- Import: Sprite Mode = **Multiple**, sliceuj po ćeliji (128×192).
- **MVP (Faza 3):**
  - `idle` — disanje, **2–4 frejma**, loop.
  - **1 vežba** (npr. bicep curl ili sklek) — **4–6 frejmova**.
- Kustom slojevi (kosa, brada…) prate iste keyframe-ove. Napredno (skeletal/bone) je post-MVP.

## 8. Kako da napraviš (tooling)

- **Preporuka: [Aseprite](https://www.aseprite.org/)** — standard za pixel art (slojevi, animacija, paleta, export sheet-ova). Alternativa: Piskel (besplatan, browser), Krita, Photoshop.
- Zaključaj **jednu paletu** (izvuci iz reference ili definiši ~24 boje) i koristi je svuda.
- Radi svaki sloj na istom 128×192 canvasu, stopala na baznoj liniji.
- Export: svaki sloj/frejm kao PNG (ili sheet) u odgovarajući folder iznad.
- **Opcije za izvor arta:** (a) ti crtaš u Aseprite-u; (b) pixel-art umetnik dobije ovaj brief; (c) AI-generisanje — moguće za koncept, ali čist modularni pixel art sa fiksnom registracijom je AI-ju težak; realnije je Aseprite.

## 9. Šta ja (Claude) gradim uz ovo

Kad slotovi/PNG-ovi postoje, ja pišem runtime sistem (Faza 3, §7):
- `CharacterBuilder` / layer kompozitor (slaže slojeve po depth order-u),
- `MuscleTiers` swap (telo prelazi tier kad Gains pređu prag),
- `CosmeticData` ScriptableObject-i (`id`, sloj, sprite, cena, način otključavanja),
- animacioni state (idle/vežba po lokaciji).
Art se kači u imenovane slotove — **zamena arta = zamena PNG-a, bez diranja koda** (§4 pravilo 2).

## 10. Za potvrdu (pre Faze 3)

- [ ] View = **front** (potvrđeno referencom)?
- [ ] Canvas **128×192** i PPU **128** ok, ili želiš krupnije (npr. 160×224)?
- [ ] Ko crta: ti (Aseprite) / umetnik / AI?
- [ ] Prvi MVP asset set: **tier 4 telo + kosa + brada + šorc + gloves + 1 idle + 1 vežba** — dovoljno da sistem proradi vizuelno.
