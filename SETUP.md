# Idle GymBro — Setup na novom uređaju

Vodič kako da na **čistom Windows PC-u** (npr. kućni računar) podigneš isto dev okruženje
i nastaviš rad na projektu. Sav "teški" deo radi skripta [`scripts/setup-dev-env.ps1`](scripts/setup-dev-env.ps1).

> Fiksirana Unity verzija: **6000.0.79f1 (Unity 6 LTS)**.
> Mora da se poklapa sa `ProjectSettings/ProjectVersion.txt` u repou.

---

## Preduslovi

- Windows 10/11
- **winget** (App Installer) — dolazi uz Windows 11; na Win10 iz Microsoft Store-a
- **git** ([git-scm.com](https://git-scm.com))

---

## 1. Kloniraj repo

```powershell
git clone https://github.com/nikoladjokic96/idle-gymbro.git
cd idle-gymbro
```

## 2. Pokreni setup skriptu

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-dev-env.ps1
```

Skripta instalira:
- **Unity Hub** (winget)
- **GitHub CLI** (winget)
- **Unity 6000.0.79f1** + Android Build Support (SDK Platforms 34/35/36, NDK r27c, Build Tools, Platform Tools, CMake, OpenJDK)

Editor se instalira u `%USERPROFILE%\Unity\Hub\Editor` (user-space, bez potrebe za admin dozvolama).
Veliki download (~10+ GB) — pusti da završi.

## 3. Unity nalog + licenca *(jednom, ručno)*

Ovo skripta ne može umesto tebe — aktivacija licence traži prijavu:

1. Otvori **Unity Hub**
2. **Sign in** (ili napravi besplatan Unity nalog)
3. **Settings (⚙️) → Licenses → Add → Get a free personal license**

## 4. GitHub autentikacija *(ako koristiš `gh`)*

```powershell
gh auth login
```
→ `GitHub.com` → `HTTPS` → `Yes` → `Login with a web browser`.

## 5. Otvori projekat

1. Unity Hub → **Add** → izaberi kloniran folder (`idle-gymbro`)
2. Otvori ga sa editorom **6000.0.79f1**
3. Unity **automatski restaurira sve pakete** iz `Packages/manifest.json` pri prvom otvaranju
   (Input System, TextMeshPro, DOTween, Newtonsoft, itd.) — ne treba ručno instalirati.

---

## Android build podešavanja *(podsetnik iz [CLAUDE.md](CLAUDE.md) §2)*

| Opcija | Vrednost |
|---|---|
| Scripting backend | **IL2CPP** |
| Target architecture | **ARM64** |
| Min API level | **24** |
| Platforma | Android (prvo) |

---

## Rešavanje problema

- **`winget` nije prepoznat** → instaliraj *App Installer* iz Microsoft Store-a.
- **`gh` / `Unity Hub` nije prepoznat odmah posle instalacije** → otvori **novi** terminal (PATH se osveži).
- **Editor install pukne zbog permisija** → skripta već koristi user-space putanju; ako si menjao `$EditorInstallPath` na `C:\Program Files\...`, pokreni PowerShell kao Administrator.
- **`Unity Hub.exe` se ponaša čudno iz CLI-ja (traži "module")** → očisti env promenljivu: `Remove-Item Env:\ELECTRON_RUN_AS_NODE` (skripta to već radi).
- **`Provided editor version does not match to any known Unity Editor versions`** → sveža LTS zakrpa još nije u Hub-ovoj listi izdanja, pa `install` traži i `--changeset`. Skripta ga prosleđuje (`$UnityChangeset`, default `4e8d7afad3cd`). Ako bumpuješ `$UnityVersion`, uzmi novi changeset iz `ProjectSettings/ProjectVersion.txt` (`m_EditorVersionWithRevision`).
- **Skripta pukne na kraju sa čudnim znakovima umesto ć/š/ž (`SledeÄ‡e...`)** → `.ps1` mora biti sačuvan kao **UTF-8 with BOM**; Windows PowerShell 5.1 bez BOM-a čita fajl kao ANSI i slomi here-string. Fajl u repou već ima BOM — ne snimaj ga kao "UTF-8 without BOM".
- **Setup javi non-zero exit ali log kaže `All Tasks Completed Successfully.`** → Unity Hub CLI ume da vrati non-zero i posle uspešne instalacije. Skripta zato verifikuje `Unity.exe` i `AndroidPlayer` na disku umesto da veruje exit code-u; ako je verifikacija OK, instalacija je prošla.
