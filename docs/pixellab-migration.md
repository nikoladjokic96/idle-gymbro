# PixelLab migracija — izveštaj

> **Status: ZAVRŠENA na trial nalogu (NALOG #035).** Sav art koji igra učitava je sada pixel art.
> Odluka korisnika (2026-08-12): *„sve zameni novim generisanim assetima i stilom sa pixellaba…
> takođe koristi pixellab i za generisanje ikonica za UI"*, pa *„potroši trial verziju, generiši
> osnovno šta možeš"*.
>
> Čitaj zajedno sa `CLAUDE.md` §2/§17 i [`art-brief.md`](art-brief.md).

---

## 1. Šta je zamenjeno (i po kojoj ceni)

Trial nalog ima **40 generacija ukupno**. Prvobitna procena je bila da najjeftiniji pun replace
traži 38 gen *bez ijednog re-roll-a i bez animacija* — dakle neizvodljivo. Ispalo je izvodljivo,
zato što dve stavke uopšte nisu koštale ono što je cenovnik sugerisao:

| Grupa | Komada | Naivna cena | **Stvarno** | Kako |
|---|---|---|---|---|
| Muscle tierovi | 6 | 6 | **9** | `create_image_pixflux` img2img + 3 re-roll-a (tier1 ×2, tier2) |
| Pozadine lokacija | 6 | 6 | **6** | `create_image_pixflux`, 216×384 |
| UI ikonice | 18 | 18 | **5** | **grid trik** — 9 ikonica u jednoj slici, pa lokalno sečenje |
| Kozmetika (3 kose, 2 brade, 3 šorca) | 8 | 8 | **0** | smanjeni fal.ai slojevi iz #022 (vidi §3) |
| *— propali pokušaji kozmetike* | — | — | *2* | *img2img nije hteo da doda kosu (§3)* |
| Animacije (idle + workout × 6 tierova) | 12 klipova / 48 frejmova | ~24+ | **12** | `animate_image`, 4 frejma po 1 gen |
| **Ukupno** | | **38+** | **35 / 40** | ostalo **5** rezerve |

> Brojka je očitana sa `get_balance` posle svega: `generations_used: 35, generations_remaining: 5`.

---

## 2. Tri nalaza koja su odlučila pipeline

### (a) `create_character` je neupotrebljiv — canvas mu je kvadratni
`size` je jedan integer (16–256), a stvarni canvas je još ~40% veći „to make room for animations".
Kvadrat ne može da nosi portret lika, a 8 pravaca nam ne treba (igra je front-view).
**Zamena:** `create_image_pixflux` (slobodan 96×144) → `animate_image` (radi na „loose" PNG-u,
ne traži PixelLab rig, za razliku od `animate_character`/`animate_object`).

### (b) Registracija se NE prepušta modelu
Svaki tier je `img2img` iz **svog** fal.ai originala (#022) smanjenog na 96×144.
Model prevodi stil u pixel art, ali pozu, visinu glave i liniju stopala nasleđuje iz init slike →
**svih 6 tierova se poklapa po konstrukciji**, isto kao što je #022 radio kroz edite anchor-a.

> `init_image_strength` je **obrnut** od uobičajenog img2img: veći broj = **više se čuva** ulaz.
> 150 = pravi edit, 300 = suptilno, 500 = jedva menja.
> Na 150 model je „naduvao" tier1/tier2 pa je progresija mišića nestala → oni idu na **260**.
> Za tier1 („ultra mršav i tužan", zahtev korisnika) ni to nije bilo dovoljno, jer izvorna slika
> nije dovoljno mršava: rešeno tako što je **init slika prvo suzena po X na 68%**
> (`png-squash.ps1`), pa je model dobio stvarno mršavu siluetu da prevede.

### (c) Grid trik — N ikonica za 1 generaciju
`create_image_pixflux` bez problema nacrta **3×3 mrežu razdvojenih ikonica na transparentnoj
pozadini** ako se to eksplicitno traži. Slika se onda lokalno iseče po ćelijama, svaka ćelija se
trimuje na svoj opaque bounding box i re-centrira u 64×64 (relativne veličine se čuvaju, pa noga
ostaje manja od torza umesto da se obe rastegnu na dugme).

**Cena: 18 ikonica za 5 generacija.** Semantika ume da odluta (traženo „noga", dobijen torzo), pa
je jeftinije tražiti 9 pojmova odjednom i doraditi promašaje sledećim gridom nego crtati 1 po 1.

---

## 3. Kozmetika — jedini deo koji PixelLab NIJE nacrtao

Trik iz #022 („generiši varijantu sa kosom, oduzmi bazu, dobiješ sloj") **ne prenosi se na PixelLab**:

- `init_strength` 300 → model uopšte ne doda kosu (lik ostane ćelav), a telo suptilno precrta.
- `init_strength` 150 → doda malo ili ništa, ali precrta **celo** telo → razlika je šum, ne sloj.
- `inpaint_image` bi to rešio čisto (maskira se samo teme, ostatak je zamrznut), ali košta
  **20–40 generacija po pozivu** — pola trijala za jednu frizuru.

**Rešenje bez ijedne generacije:** fal.ai kozmetički slojevi iz #022 su već izvučeni iz *iste*
848×1264 kompozicije iz koje su nastala i nova pixel tela. Smanjeni na 96×144 poklapaju se
**po konstrukciji** — kosa sleće na teme, brada na vilicu, šorc na kukove, na svakom tieru.
Da prestanu da izgledaju kao glatke slike na pixel telu, propušteni su kroz **tvrdu alfu**
(alpha < 110 → 0; meki halo je glavni znak da sprite nije pixel art) i **kvantizaciju boje**
(korak 28 po kanalu). Isto je urađeno sa `blink_01.png`.

---

## 4. Kadar pozadina (216×384)

Pozadina je 15 world jedinica visoka, kamera je ortho size 5 → **na 9:16 telefonu vidi se samo
centralnih 144×256 px**. Lik stoji centriran, stopala na **redu ~253 od 384** (66% visine).
Zato svaki prompt traži „pod u donjoj trećini, prazna sredina, bez ljudi".

`PlaceholderBackgroundGenerator` **izvodi PPU** (`height / 15`) umesto hardkodovanih 128 — bez toga
bi se pozadina od 216×384 renderovala na 1/5 veličine (popravljeno u #034).

---

## 5. Šta je arhivirano

`Assets/_Game/Art/Character/_originals/fal_ai_848x1264/` — 48 fajlova:
stari animacioni frejmovi (`_idle1/2`, `_work1/2/3` × 6 tierova) i napušteni forearm-rig delovi
(`_armless`, `_forearm_l/r`, `_noforearm`, `flex_tier1–6`).

Zašto su morali da odu: PPU se izvodi iz visine teksture, pa bi frejm od 1264 px i telo od 144 px
zauzimali **istih 1.5 world jedinica** — veličina bi bila tačna, ali bi lik pri disanju treperio
između pixel arta i naslikanog arta. Forearm/flex fajlove ionako niko ne učitava
(`CoreLoopSceneBootstrap` referencira samo `_idle*`, `_work*` i `blink_01`).

---

## 6. Alat (u scratchpad-u, nije u repou)

MCP server `pixellab` je registrovan u `~/.claude.json` pod projektom (local scope, **token nije u
repou**). Native `mcp__pixellab__*` alati se nisu učitali ni posle restarta sesije, pa ceo posao ide
kroz direktan JSON-RPC preko HTTP-a:

| Skript | Uloga |
|---|---|
| `pxl-list.ps1` | `tools/list`, `-Name X` ispisuje pun JSON schema alata |
| `pxl-gen.ps1` | create → poll → snimi PNG; **svaki sirov odgovor se čuva na disk** da plaćena generacija ne propadne ako parsiranje pukne |
| `mk-args.ps1` | sastavlja `create_image_pixflux` argumente (inline-uje base64 iz fajla) |
| `png-resize.ps1` · `png-squash.ps1` · `png-zoom.ps1` · `png-sheet.ps1` | skaliranje, sužavanje siluete, nearest-neighbour pregled, kontakt-traka |
| `slice-grid.ps1` | seče icon-sheet grid na ćelije, trimuje i re-centrira svaku |
| `pixelize.ps1` | tvrda alfa + kvantizacija boje |
| `compose.ps1` | prikazuje pozadinu tačno onako kako je Unity kadrira, sa likom na mestu |

> **Zamka koja je pojela jednu generaciju:** rezultat `get_image` **ne** vraća `.png` URL — nosi PNG
> inline kao base64 `{"type":"image","data":"…"}`, a `download:` link je bez ekstenzije.
> Regex koji traži `\.png` nikad ne pogodi i posao „istekne" iako je odavno gotov.
> Generacija nije izgubljena: job id ostaje validan, pa se rezultat pokupi sa `-JobId`.
>
> **Zamka u PowerShell-u:** `param([string]$Bg)` i `$bg = [Drawing.Image]::FromFile(...)` su **ista
> promenljiva** (imena su case-insensitive) — tip iz `param` bloka ostaje zakucan, pa se slika tiho
> pretvori u string `"System.Drawing.Bitmap"`, a `$bg.Width` postane `$null`.

---

## 7. Otvoreno

1. **Zvuk** — SFX su i dalje generisani placeholderi ([`asset-catalog.md`](asset-catalog.md) §4).
2. **UI paneli i font** — ostaju generisani oblici iz `UiShapeGenerator`. `create_ui_asset`
   (20–40 gen/panel) i `create_font` (25 gen) se ne isplate, a 9-slice panel sa zapečenim
   pikselima ionako izgleda loše rastegnut.
3. **Lice na tier1** — telo je tačno (koščato, uska ramena, upali grudni koš), ali je na ~12 px
   izraz više „gaunt" nego jasno tužan. Traži ciljan re-roll ako smeta.
4. **Kozmetika nije animirana** — statični slojevi preko animiranog tela; na suptilnom disanju se
   ne primećuje.
