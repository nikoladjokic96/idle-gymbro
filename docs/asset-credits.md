# Idle GymBro — Asset Credits & Licenses

> Sve što nije napravljeno u ovom projektu mora biti navedeno ovde, sa licencom.
> **Ovo nije formalnost:** CC BY licenca *zahteva* pomen autora, i ako igra ide na
> Play Store bez toga, kršiš licencu. Držati ažurno pri svakom dodavanju asseta.

---

## UI ikonice — game-icons.net

**Licenca:** [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) — dozvoljena komercijalna
upotreba i izmene, **uz obavezan pomen autora**.

Ikonice su preuzete kao bele na providnoj pozadini (512×512 PNG) i boje se u engine-u
(`Image.color`), pa jedan fajl pokriva sve varijante boje.

| Fajl | Original | Autor |
|---|---|---|
| `icon_chest.png` | muscular-torso | Delapouite |
| `icon_arms.png` | biceps | Delapouite |
| `icon_back.png` | muscle-up | Lorc |
| `icon_abs.png` | abdominal-armor | Delapouite |
| `icon_legs.png` | leg | Delapouite |
| `icon_training_partner.png` | person | Delapouite |
| `icon_gym_membership.png` | gym-bag | Delapouite |
| `icon_preworkout.png` | heavy-lightning | Lorc |
| `icon_protein.png` | soda-bottle | Caro Asercion |
| `icon_settings.png` | cog | Lorc |
| `icon_achievements.png` | laurels-trophy | Delapouite |
| `icon_reward.png` | chest | Delapouite |
| `icon_wardrobe.png` | polo-shirt | Delapouite |
| `icon_prestige.png` | strong-man | Delapouite |
| `icon_locations.png` | treasure-map | Lorc |
| `icon_daily.png` | calendar | Delapouite |
| `icon_gains.png` | weight-lifting-up | Delapouite |

**Tekst za in-game credits ekran (obavezan):**

> Icons by Delapouite, Lorc and Caro Asercion — game-icons.net (CC BY 3.0)

---

## UI kit — Kenney

**Licenca:** CC0 (javno dobro) — bez ikakvih obaveza, ni pomen autora nije nužan.

| Fajl | Original | Izvor |
|---|---|---|
| `Art/UI/Kit/icon_cross.png` | icon_cross | [Kenney UI Pack v2](https://kenney.nl/assets/ui-pack) |

> Od Kenney paketa je zadržan **samo cross glif**. Paneli i dugmad iz paketa su imali
> **zapečenu senku/gradijent u samim pikselima**, što se ne može ukloniti bojenjem — a
> dogovoreni stil je ravan, bez lažnog 3D-a. Zamenjeni su generisanim oblicima (ispod).

## UI oblici — generisano

`Art/UI/Shapes/` — `Editor/UiShapeGenerator` pravi bele, antialiasovane oblike
(zaobljeni pravougaonici + krug) sa 9-slice ivicom koja prati radijus. Boja dolazi
isključivo iz palete u `CoreLoopSceneBootstrap` (`Image.color`), pa se izgled menja
na jednom mestu. Bez licence, bez zavisnosti.

## Karakter i pozadine — generisano

`Assets/_Game/Art/Character/` i `Assets/_Game/Art/Backgrounds/` — generisano kroz
`fal-ai/nano-banana-pro` (Gemini image), pa dorađeno alatima u `Scripts/Editor/`
(`CosmeticLayerExtractor`, `ForearmExtractor`). Nema obaveze pomena, ali proveri
uslove korišćenja provajdera pre komercijalnog izdanja.

## Zvuk — generisano

`Assets/_Game/Audio/Placeholders/` — deterministički generisani WAV-ovi
(`Editor/PlaceholderSfxGenerator`). Bez spoljnih izvora.
