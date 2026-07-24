# Idle GymBro — HUD Layout Blueprint

> Ciljni raspored glavnog ekrana, po uzoru na *Medieval Idle Prayer* (referentni screenshot
> od korisnika, 2026-07-23). Ovo je **blueprint**: svako dugme se implementira tek u svojoj
> fazi (§14 scope discipline), ali mesto mu je unapred rezervisano ovde — da UI ne bismo
> preslagivali svaki put.

## Raspored (portrait 1080×1920)

```
┌─────────────────────────────────────────────┐
│ [Story    ]      4.46B  |  157K/s   [Settings]│  ← gains + passive rate (postoji)
│ [progress%]   (energy bar ispod)    [        ]│
│                                              │
│ [Boost:   ]                         [Buff 1] │  ← aktivni buffovi (ikonica+tajmer)
│ [2x passive]                        [Buff 2] │
│ [Boost:   ]                                  │
│ [2x tap   ]        KARAKTER        [UPGRADES]│  ← otvara upgrade modal (postoji)
│ [Offer/   ]      (centar scene,    [Shop    ]│  ← permanent boosts (IAP/Gear)
│ [discount ]       muscle tiers)              │
│                                              │
│                                     [Periodic]│  ← claim na ~15 min: % currency
│ [Quests/  ] [Timed ]                [claim  ] │
│ [Achievmnt] [event ]                          │
└─────────────────────────────────────────────┘
```

## Elementi → faza implementacije

| Element | Pozicija | Šta radi | Faza |
|---|---|---|---|
| **Story progress** | gore levo | Na dugmetu: % progresa trenutne lokacije (0–100% „kućni trening"). Progres = udeo kupljenih upgrade-ova lokacije. Na 100% → otključava sledeću lokaciju (street workout...). Klik → modal sa listom levela/lokacija (§9). | **6 (Progresija)** |
| **Boost: 2x passive** | levo, ispod story | Rewarded reklama → 2x pasivni prihod na X min (§10 opt-in) | **5 (Monetizacija)** |
| **Boost: 2x tap** | levo, ispod | Rewarded reklama → 2x tap prihod na X min | **5 (Monetizacija)** |
| **Offer/discount** | levo, ispod | Povremena ponuda: popust na resurse/boostere | **5+ (post-MVP)** |
| **Settings** | gore desno | Audio on/off, kredit, itd. | **4 (Polish)** |
| **Aktivni buffovi** | desno, ispod settings | Ikonice trenutno aktivnih boostera sa tajmerom | **4/5 (uz boostere)** |
| **Quests/Achievements** | dole levo | „Držao si tap ukupno 60s" → claim % valute. Badge kad ima za claim. | **7 (Meta)** |
| **Timed event** | dole, pored quests | Vremenski event / takmičenje sa tajmerom | **post-MVP (§12)** |
| **UPGRADES** | desno, sredina | Otvara upgrade modal | ✅ postoji (trenutno dole centar; seli se na desnu ivicu u Fazi 4 UI pass-u) |
| **Shop (permanent boosts)** | desno, ispod UPGRADES | Trajni boosti / Gear paket (IAP) | **5 (Monetizacija)** |
| **Periodic claim** | dole desno | Na svakih ~15 min spremna nagrada: % trenutnog prihoda; klik = claim | **7 (Meta/retencija)** |
| **Gains + rate** | gore centar | Ukupno + „X/s" | ✅ postoji |
| **Energy bar** | gore centar, ispod gains | | ✅ postoji |
| **Karakter** | centar | Layered sprite, muscle tiers | ✅ Faza 3 (u toku) |

## Principi
- **Ivice ekrana = dugmad, centar = karakter/scena.** Tap zona za trening je sve što nije UI (tap-over-UI guard već postoji).
- Leva ivica = „daj mi nešto" (progres, boosti, ponude). Desna ivica = sistemsko (settings, stanje buffova). Dno = meta petlje (quests, event) + glavni CTA (upgrades).
- Sva nova dugmad idu kroz bootstrap tool (scena se generiše!) i poštuju §11 UI standard (Expand, ModalToggle za modale, EventSystem).
- Monetizacione elemente (boosti/offer) prezentovati kao gameplay (pre-workout, protein...) — §10 pravilo 4.
