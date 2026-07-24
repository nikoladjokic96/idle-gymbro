# Idle GymBro — Asset Checklist (šta treba nacrtati)

> Radna lista za pixel art. Tehnički standard (canvas, PPU, pivot, folderi, Unity import):
> [`art-brief.md`](art-brief.md) — **pročitaj njega prvo**. Ovde je samo ŠTA treba i kojim redom.
>
> Svaki fajl ovde 1:1 zamenjuje generisani placeholder — ubaciš PNG preko postojećeg
> fajla u `Assets/_Game/Art/Character/Placeholders/` (ili novi u svoj folder + javi da preožičim slot).

**Format za sve:** PNG, providna pozadina, **128×192 px po frejmu**, lik centriran po X,
**stopala na y=8 od dna**, bez anti-aliasa. Paleta ~24–32 boje, ista svuda.

---

## A. SLIKE (statični sprite-ovi, jedan PNG po fajlu)

### Prioritet 1 — prvi playable art pass (zamenjuje placeholder siluete)
| # | Fajl | Šta je | Napomena |
|---|---|---|---|
| 1 | `body_tier3.png` | telo „Fit" | prvo telo koje igrač duže gleda — počni od njega |
| 2 | `head_01.png` | glava (bez kose/brade) | zaseban sloj od tela |
| 3 | `hair_01.png` | kosa | crna, kao na referenci |
| 4 | `beard_01.png` | brada | puna brada, kao na referenci |
| 5 | `shorts_01.png` | šorc | crni |
| 6 | `background_home.png` | pozadina lokacije 1 (soba/kuća) | **1080×1920** (ceo ekran), mračnija da HUD bude čitljiv |

### Prioritet 2 — muscle tiers (glavni vizuelni feedback igre)
| # | Fajl | Šta je |
|---|---|---|
| 7 | `body_tier1.png` | mršav (start) |
| 8 | `body_tier2.png` | slim-fit |
| 9 | `body_tier4.png` | jacked |
| 10 | `body_tier5.png` | mass monster |
| 11 | `body_tier6.png` | enhanced/Gear (vene, glow — premium fantazija) |

> Ista visina/pivot za sve tierove — silueta se širi, stopala ostaju na istoj liniji.
> Tier 3 ti je referenca; 1–2 su mršaviji, 4–6 masivniji.

### Prioritet 3 — prva kustomizacija (po 2–3 varijante)
| # | Fajlovi | Šta je |
|---|---|---|
| 12 | `hair_02.png`, `hair_03.png` | još frizura (plava, ćelav+kapa...) |
| 13 | `beard_02.png` | brkovi ili kozja bradica |
| 14 | `shorts_02.png`, `shorts_03.png` | varijante (crveni, camo...) |
| 15 | `shirt_01.png` | majica/tank top (sloj preko torza; „bez majice" = prazan slot) |
| 16 | `shoes_01.png` | patike |
| 17 | `accessory_headphones_01.png`, `accessory_chain_01.png` | dodaci |

---

## B. ANIMACIJE (sprite sheet — horizontalna traka, svaka ćelija 128×192)

> Sheet = svi frejmovi jedan do drugog u JEDNOM PNG-u (npr. 4 frejma → 512×192).
> Konstantan broj frejmova po animaciji; lik na istom mestu u svakoj ćeliji.

### Prioritet 1 — MVP
| # | Fajl | Animacija | Frejmovi | Napomena |
|---|---|---|---|---|
| A1 | `idle_tier3_sheet.png` | disanje/stajanje | **2–4** | loop; suptilno (ramena gore-dole 1–2 px) |
| A2 | `curl_tier3_sheet.png` | bicep curl sa bučicom | **4–6** | „rep" animacija — pušta se na svaki rep; poslednji frejm = vrh pokreta |

### Prioritet 2 — po jedan sheet za ostale tierove koje nacrtaš
| # | Fajl | Napomena |
|---|---|---|
| A3 | `idle_tier1_sheet.png`, `curl_tier1_sheet.png` | isti keyframe-ovi kao tier3, mršavija silueta |
| A4 | ... isto za tier 2/4/5/6 kad stignu tela | kustom slojevi (kosa/šorc) prate iste keyframe-ove |

### Post-MVP (NE crtati sad)
- Nove vežbe po lokaciji: sklek, čučanj, bench, deadlift... (svaka = novi sheet po tieru)
- Flex/pobednička poza, umoran (energija = 0)

---

## Kako da isporučiš
1. Crtaj u Aseprite-u (ili Piskel/Krita) na 128×192 canvasu, stopala na y=8.
2. Export: **File → Export Sprite Sheet** (horizontal strip) za animacije; običan PNG za statične.
3. Fajlove mi pošalji ili ubaci u `Assets/_Game/Art/Character/` po folderima iz art-brief-a §6 — ja ih uvezujem u slotove, podešavam Unity import i ožičavam.

**Minimalni skup da igra prestane da liči na placeholder:** stavke **1–6 + A1 + A2** (8 fajlova).
