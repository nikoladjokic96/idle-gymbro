<#
.SYNOPSIS
  Podiže dev okruženje za Idle GymBro na čistom Windows PC-u.

.DESCRIPTION
  Instalira (preko winget-a):
    - Unity Hub
    - GitHub CLI
  pa kroz Unity Hub CLI instalira fiksiranu verziju Unity editora
  zajedno sa Android Build Support modulima (SDK, NDK, OpenJDK, CMake...).

  Editor se instalira u user-space putanju ($EditorInstallPath) da NE bi
  tražio administratorske (UAC) dozvole. winget koraci mogu tražiti UAC.

  Ponovni pokreti su bezbedni: već instalirane komponente winget/Hub preskaču.

  Posle skripte (ručno, jednom):
    1. Otvori Unity Hub -> Sign in -> Settings > Licenses > Add >
       Get a free personal license   (Unity Personal je besplatan)
    2. gh auth login                  (ako koristiš GitHub CLI)
    3. Unity Hub -> Add -> izaberi kloniran folder projekta i otvori ga.
       Unity sam restaurira pakete iz Packages/manifest.json pri prvom otvaranju.

.NOTES
  $UnityVersion MORA da se poklapa sa ProjectSettings/ProjectVersion.txt.
#>

param(
    [string]$UnityVersion = "6000.0.79f1",
    [string]$EditorInstallPath = "$env:USERPROFILE\Unity\Hub\Editor"
)

$ErrorActionPreference = "Stop"

# Neki terminali (npr. oni koje pokreće Electron aplikacija) postave ovu
# promenljivu, zbog čega se "Unity Hub.exe" ponaša kao čist Node.js umesto
# kao Hub. Sklanjamo je da CLI radi ispravno.
Remove-Item Env:\ELECTRON_RUN_AS_NODE -ErrorAction SilentlyContinue

function Test-Cmd($name) {
    return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

Write-Host "=== Idle GymBro — setup dev okruženja ===" -ForegroundColor Cyan

if (-not (Test-Cmd winget)) {
    throw "winget nije nađen. Instaliraj 'App Installer' iz Microsoft Store-a pa ponovi."
}

Write-Host "`n[1/4] Unity Hub..." -ForegroundColor Yellow
winget install --id Unity.UnityHub --accept-source-agreements --accept-package-agreements --silent

Write-Host "`n[2/4] GitHub CLI..." -ForegroundColor Yellow
winget install --id GitHub.cli --accept-source-agreements --accept-package-agreements --silent

$hub = "C:\Program Files\Unity Hub\Unity Hub.exe"
if (-not (Test-Path $hub)) {
    throw "Unity Hub nije nađen na '$hub'. Otvori NOVI terminal (da se osveži PATH) i ponovi."
}

# Putanja mora da postoji pre nego što je Hub prihvati.
Write-Host "`n[3/4] Putanja za editor: $EditorInstallPath" -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $EditorInstallPath | Out-Null
# '--' se prosleđuje kao element niza da bi prošao verbatim kroz sve verzije
# PowerShell-a (Windows PowerShell 5.1 ume da "pojede" goli -- token).
& $hub @("--", "--headless", "ip", "--set", $EditorInstallPath)

Write-Host "`n[4/4] Instaliram Unity $UnityVersion + Android module (veliki download)..." -ForegroundColor Yellow
# --childModules povlači i OpenJDK/NDK/SDK/CMake kao zavisnosti Android podrške.
& $hub @("--", "--headless", "install", "--version", $UnityVersion,
         "-m", "android", "android-sdk-ndk-tools", "--childModules")

Write-Host "`n=== Automatizovani koraci gotovi ===" -ForegroundColor Green
Write-Host @"
Sledeće (ručno, jednom):
  1. Unity Hub -> Sign in -> Settings > Licenses > Add > Get a free personal license.
  2. gh auth login          (autentikacija GitHub CLI, ako ga koristiš)
  3. Unity Hub -> Add -> izaberi kloniran folder projekta i otvori ga sa $UnityVersion.
     Paketi se restauriraju automatski iz Packages/manifest.json.
"@ -ForegroundColor Cyan
