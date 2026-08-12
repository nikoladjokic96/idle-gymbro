# Idle GymBro — Asset Checklist

> ✅ **ZATVORENO (NALOG #035).** Ova lista je postojala dok se čekalo da neko nacrta art.
> Više se ne čeka: sav art koji igra učitava je generisan i instaliran —
> **6 muscle tierova · 12 animacionih klipova · 8 kozmetika + blink · 6 pozadina · 18 UI ikonica**.
>
> - Šta je koji fajl i gde stoji → [`asset-catalog.md`](asset-catalog.md)
> - Canvas, pivot, PPU, Unity import → [`art-brief.md`](art-brief.md)
> - Kako je nastao i šta je koštao → [`pixellab-migration.md`](pixellab-migration.md)

---

## Jedino što još NIJE pravi asset

| Šta | Gde | Napomena |
|---|---|---|
| **SFX** (4 zvuka) | `Assets/_Game/Audio/Placeholders/` | `tap.wav`, `buy.wav`, `tier_up.wav`, `booster.wav` — deterministički generisani tonovi. Zamena je 1:1 po imenu, bez koda. |

---

## Ako budeš crtao ručno (npr. u Aseprite-u)

Zameni PNG **istim imenom i istom veličinom**; Unity sam reimportuje, kod se ne dira.

| Tip | Canvas | Pivot | Registracija |
|---|---|---|---|
| Lik i svi kozmetički slojevi | **96 × 144** | bottom-center | centriran po X, stopala na istoj baznoj liniji |
| Animacioni frejm | **96 × 144** | isto kao statična poza | `body_tier<N>_idle1..4.png` / `_work1..4.png` |
| UI ikonica | **64 × 64** | — | transparentna pozadina, motiv centriran |
| Pozadina lokacije | **216 × 384** | center | pod u donjoj trećini; **vidi se samo centralnih 144×256** |

**Dva pravila koja se ne smeju prekršiti:**

1. **Ne pokreći `IdleGymBro → DANGER — Regenerate Placeholder Character Art`** — to je jedini put
   koji briše pravi art. Obični `Generate…` preskače postojeće fajlove
   (log: `15 sprites ready (0 generated, 15 kept)`).
2. **Animacioni frejm mora deliti canvas i PPU sa statičnom pozom** — `SystemsSmokeTest` T12 to
   proverava, a u igri bi lik inače poskakivao između frejmova i kozmetika bi se odlepila.
