# PixelLab migracija — handoff

> **Status: PRIPREMA ZAVRŠENA, GENERISANJE NIJE POČELO.** Nijedna generacija nije potrošena (`generations_used: 0`).
> Ovaj fajl je tačka nastavka. Čitaj ga zajedno sa `CLAUDE.md` §2 i §17.
> Odluka korisnika (2026-08-12): *„sve zameni novim generisanim assetima i stilom sa pixellaba… takođe koristi pixellab i za generisanje ikonica za UI"*.

---

## 1. Šta je urađeno

- **MCP server dodat i povezan:**
  ```
  pixellab · http · https://api.pixellab.ai/mcp · Scope: Local (private to this project)
  ```
  Zapisan u `~/.claude.json` pod projektom `F:\idle-gymbro`.
  **Token NIJE u repou** — nema `.mcp.json`, ništa se ne commit-uje. Ne premeštati u project scope (procurio bi kroz auto-push na `origin/main`).
  Uklanjanje: `claude mcp remove pixellab -s local`.

- **CLI nije na PATH-u.** Bandlovan je uz VSCode ekstenziju:
  ```
  C:\Users\nikol\.vscode\extensions\anthropic.claude-code-2.1.228-win32-x64\resources\native-binary\claude.exe
  ```

- **Tool set izlistan:** 68 alata (`PixelLab MCP Server v0.2.0`).

---

## 2. ⛔ BLOCKER — trial nalog, 40 generacija

```
credits: $0.00
generations_remaining: 40
generations_used: 0
generations_total: 40
subscription: trial
```

**Ovo ne pokriva „sve zameni".** Cene po alatu (iz tool opisa):

| Alat | Cena | Upotrebljivo sad? |
|---|---|---|
| `create_image_pixflux` (freeform slika, img2img) | **1 gen** | ✅ radni konj |
| `create_character` mode=`standard` | **1 gen** (4/8 pravaca) | ✅ |
| `create_character` mode=`v3` | 2–9 gen | ⚠️ ograničeno |
| `animate_character` mode=`v3` | ~1 gen/pravac ≤96px · 2/dir @128px · 4/dir @160px | ⚠️ zavisi od canvasa |
| `create_character` mode=`pro` | 20–40 gen | ❌ |
| `create_character_state` (kozmetika kao varijanta) | **20–40 gen po komadu** | ❌ |
| `create_ui_asset` (panel) | **20–40 gen po panelu** | ❌ jedan panel pojede ceo trial |
| `create_font` | **25 gen** | ❌ |

### Računica za pun replace (najjeftinija varijanta, 1 gen po assetu)

| Grupa | Komada | Gen |
|---|---|---|
| UI ikonice | 18 | 18 |
| Muscle tierovi (tela) | 6 | 6 |
| Kozmetika (3 kose, 2 brade, 3 šorca) | 8 | 8 |
| Pozadine lokacija | 6 | 6 |
| **Zbir bez ijednog re-roll-a i bez animacija** | **38** | **38 / 40** |

Ostaje **2 generacije rezerve**. Pixel-art generisanje realno traži 2–4 pokušaja po assetu dok se stil ne uhvati, plus style anchor unapred. **Pun replace nije izvodljiv na trialu** — ni blizu. Animacije (30 postojećih frame-ova) i UI paneli/font uopšte ne staju.

### Preporuka

1. **Kupiti PixelLab pretplatu** pre punog replace-a. Realan budžet za ovaj obim: **150–250 generacija** (38 assetа × ~3 pokušaja + style anchor + animacije).
2. **Ili** potrošiti trial na *style anchor + vertikalni presek* (vidi §5) — dokaz da stil valja, pa tek onda kupovina.

---

## 3. Inventar — šta se menja (98 PNG ukupno)

| Folder | Kom. | Sadržaj | Sudbina |
|---|---|---|---|
| `Art/Character/Placeholders/` | 64 | `body_tier1–6` + `_idle1/2`, `_work1/2/3`, `_armless`, `_forearm_l/r`, `_noforearm`, `flex_tier1–6`, `hair_01–03`, `beard_01–02`, `shorts_01–03`, `head_01`, `blink_01` | replace |
| `Art/Character/_originals/` | 6 | fal.ai originali tierova (backup) | arhivirati, ne brisati |
| `Art/Backgrounds/Placeholders/` | 6 | `bg_home`, `bg_street`, `bg_basic_gym`, `bg_hardcore_gym`, `bg_beach`, `bg_olympia` | replace (i dalje placeholderi) |
| `Art/UI/Icons/` | 18 | `icon_abs/arms/back/chest/legs`, `icon_achievements/daily/gains/locations/prestige/reward/settings/upgrades/wardrobe`, `icon_gym_membership/preworkout/protein_shake/training_partner` | replace (sad game-icons.net, CC BY 3.0) |
| `Art/UI/Kit/`, `Art/UI/Shapes/` | 4 | `icon_cross`, `circle`, `panel`, `panel_soft` | kandidati za `create_ui_asset` (skupo) |

Mapa svakog asseta: [`asset-catalog.md`](asset-catalog.md).

---

## 4. Arhitektonske posledice (VAŽNO — ovo nije samo zamena fajlova)

1. **§2 „Zaključane odluke" se menja.** Trenutno: *hand-painted cartoon, 848×1264, fal.ai `nano-banana-pro`* (odluka #022). Novo: *pixel art, PixelLab*. Ovo poništava #022 i vraća nas na pixel art iz originalnog [`art-brief.md`](art-brief.md).

2. **Canvas/PPU se menja.** 848×1264 nema smisla za pixel art. PPU se trenutno izvodi kao `height / 1.5` (`CharacterBuilder`) — to i dalje radi za bilo koju rezoluciju, ali brojke u [`art-brief.md`](art-brief.md) i [`asset-checklist.md`](asset-checklist.md) treba uskladiti.
   > **Cena zavisi od canvasa:** `animate_character` v3 je ~1 gen/pravac na ≤96px, 2/dir na 128px, 4/dir na 160px. **Manji canvas = jeftinije animacije.** Preporuka: **96×144** (najjeftinije) ili **128×192** (stari brief, dvostruko skuplje animacije).

3. **Kozmetika kao slojevi — PixelLab nema direktan ekvivalent.** `create_character_state` daje varijantu lika (isti identitet, druga odeća) ali košta 20–40 gen po komadu. Jeftina alternativa: generisati varijantu preko `create_image_pixflux` img2img (1 gen) pa izolovati sloj postojećim `Editor/CosmeticLayerExtractor`-om (razlika „varijanta − baza"). **Taj tool već postoji i radi** — pisan je za fal.ai, ali je logika generička (razlika u zadatom pojasu).

4. **Front-view only.** Igra je front-view (§2), a PixelLab pravi 4/8 pravaca. Koristimo samo `south`. Ne plaćamo više zbog toga (standard mode = 1 gen bez obzira na broj pravaca), ali `create_image_pixflux` je direktniji za sve što nije lik.

5. **Generatori placeholder-a imaju `File.Exists` guard** (od #022) — neće pregaziti novi art. Ako se u logu pojavi `15 generated` umesto `(0 generated, 15 kept)`, znači da je pravi art nestao s diska.

---

## 5. Predlog plana (kad se odblokira budžet)

**Faza A — style anchor (~4–6 gen).** Jedan `create_character` (standard, humanoid, gymbro tier 3) kao referentni stil. Iz njega izvlačimo paletu/outline/shading za sve ostalo. `create_image_pixflux` prima `init_image_url`, a `create_ui_asset` prima `style_image_base64` — **anchor se prosleđuje svemu ostalom da stil bude jedinstven.**

**Faza B — 6 muscle tierova (~6–18 gen).** img2img iz anchor-a, `init_image_strength` ~150, opis menja samo masu mišića. Ista poza/kadar → registracija tačna po konstrukciji (isti trik kao #022).

**Faza C — 18 UI ikonica (~18–36 gen).** `create_image_pixflux`, 64×64, `no_background=true`, uz anchor kao `init_image_url` za stilsku doslednost.

**Faza D — 6 pozadina (~6–18 gen).** `create_image_pixflux`, `no_background=false`, širok canvas (do 400×400 po osi).

**Faza E — kozmetika (~8–24 gen).** img2img varijante + `CosmeticLayerExtractor`.

**Faza F — animacije (~12–48 gen).** `animate_character` v3, `action_description` = „push-up"/„squat". Ovo bi konačno zatvorilo poslednju stavku Faze 3 iz roadmapa.

---

## 6. Otvorene odluke za korisnika

1. **Budžet** — kupuje se pretplata, ili trošimo trial na Fazu A+delić B kao dokaz stila?
2. **Canvas** — 96×144 (jeftine animacije) ili 128×192 (stari `art-brief.md` standard)?
3. **UI paneli i font** — `create_ui_asset` (20–40 gen/panel) i `create_font` (25 gen) su skupi. Ostaju postojeći `UiShapeGenerator` oblici, ili ulaze u budžet?

---

## 7. Pomoćni alat

Direktan HTTP poziv PixelLab MCP-a bez restarta sesije (JSON-RPC), za slučaj da native alati nisu učitani:

```
scratchpad/pxl.ps1 -Tool <ime_alata> -ArgsJson '<json>'
```

Endpoint je stateless (ne vraća `Mcp-Session-Id`), pa je svaki poziv samostalan: `initialize` → `tools/call`.
Posle restarta sesije native `mcp__pixellab__*` alati su dostupni i ovaj skript više nije potreban.
