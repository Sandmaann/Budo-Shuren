# ===============================
# Deployment Script mit appsettings-Merge (robustere Version, JSON-Sanitizer)
# Zusatz:
#  - Prüft, ob die Haupt-DLL in Verwendung ist (Abbruch, falls ja)
#  - Löscht vorhandene Haupt-DLL zuerst und kopiert sie als letzten Schritt
#  - "Touch" der web.config um IIS-Recycle auszulösen
#  - Wartungsmodus per maintenance.flag (wird von MaintenanceMiddleware.cs geprüft)
# ===============================

$SourcePath = "\\<DEIN-BUILD-SERVER>\Deploy\BudoShurenWebsite"
$TargetPath = "\\<DEIN-WEB-SERVER>\BudoShurenWebsite"
$MainDllName = "BudoShurenWebsite.dll"                     # WICHTIG: an dein Projekt anpassen!
$MaintenanceFlag = Join-Path $TargetPath "maintenance.flag"  # MUSS mit ContentRootPath in Program.cs übereinstimmen
$LogFile = Join-Path $TargetPath "deploy-log.txt"

# --- Logging-Funktion ---
function Write-Log {
    param (
        [string]$Message,
        [string]$Level = "INFO"
    )

    $logDir = Split-Path -Path $LogFile -Parent
    if (-not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }

    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $line = "[$timestamp] [$Level] $Message"
    Write-Host $line
    Add-Content -Path $LogFile -Value $line
}

# --- Prüfe ob Datei durch einen anderen Prozess gesperrt ist ---
function Test-FileInUse {
    param([string]$filePath)

    if (-not (Test-Path $filePath)) {
        return $false
    }

    try {
        $stream = [System.IO.File]::Open($filePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $stream.Close()
        return $false
    }
    catch {
        return $true
    }
}

# --- JSON-Sanitizer: entfernt //-Kommentare, /* ... */ und einfache trailing-commas ---
function Remove-JsonComments {
    param ([string]$text)

    if ($null -eq $text) { return $text }

    $text = [regex]::Replace($text, '/\*.*?\*/', '', 'Singleline')

    $pattern = '(?m)(?:"[^"\\]*(?:\\.[^"\\]*)*"|''[^''\\]*(?:\\.[^''\\]*)*''|//.*$)'
    $text = [regex]::Replace($text, $pattern, {
        param($m)
        if ($m.Value.StartsWith('//')) { return '' } else { return $m.Value }
    })

    return $text
}

function Remove-TrailingCommas {
    param ([string]$text)

    if ($null -eq $text) { return $text }

    $text = [regex]::Replace($text, ',\s*(?=[}\]])', '')

    return $text
}

# --- Merge-Funktion für JSON ---
function Merge-Json {
    param (
        [Parameter(Mandatory = $true)] [psobject] $source,
        [Parameter(Mandatory = $true)] [psobject] $target
    )

    foreach ($prop in $source.PSObject.Properties) {
        $key = $prop.Name
        $srcVal = $prop.Value

        if (-not ($target.PSObject.Properties.Name -contains $key)) {
            $target | Add-Member -NotePropertyName $key -NotePropertyValue $srcVal -Force
            Write-Log "Neuer Key hinzugefügt: $key" "DEBUG"
            continue
        }

        $tgtVal = $target.PSObject.Properties[$key].Value

        if ($srcVal -is [System.Management.Automation.PSCustomObject] -and
            $tgtVal -is [System.Management.Automation.PSCustomObject]) {
            Merge-Json -source $srcVal -target $tgtVal
            continue
        }

        if ($srcVal -is [System.Array]) {
            $target.PSObject.Properties[$key].Value = $srcVal
            Write-Log "Array-Wert für Key '$key' ersetzt." "DEBUG"
            continue
        }

        if ($null -eq $tgtVal -or ($tgtVal -is [string] -and [string]::IsNullOrWhiteSpace($tgtVal))) {
            $target.PSObject.Properties[$key].Value = $srcVal
            Write-Log "Key '$key' gesetzt." "DEBUG"
        }
        else {
            Write-Log "Key '$key' im Ziel bereits vorhanden, skip." "DEBUG"
        }
    }
}

# --- Start ---
if (-not (Test-Path $TargetPath)) {
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
}

Write-Log "=== Deployment gestartet ==="

try {
    # Vorab: Prüfen ob Haupt-DLL existiert und in Benutzung ist
    $targetMainDll = Join-Path $TargetPath $MainDllName
    $sourceMainDll = Join-Path $SourcePath $MainDllName

    if (Test-Path $targetMainDll) {
        Write-Log "Check, ob die Haupt-DLL '$MainDllName' in Verwendung ist..."
        if (Test-FileInUse -filePath $targetMainDll) {
            Write-Log "Abbruch: Die Haupt-DLL '$MainDllName' ist aktuell in Verwendung. Deployment wird nicht durchgeführt." "ERROR"
            Read-Host -Prompt "Drücke Enter zum Beenden"
            return
        }
    }
    else {
        Write-Log "Hinweis: Haupt-DLL '$MainDllName' nicht im Zielverzeichnis gefunden; Fortfahren." "DEBUG"
    }

    # 1. Wartungsmodus aktivieren
    # Diese Datei wird von BudoShurenWebsite/Middleware/MaintenanceMiddleware.cs geprüft
    # (Pfad: <ContentRootPath>\maintenance.flag == $TargetPath\maintenance.flag)
    Write-Log "Aktiviere Wartungsmodus..."
    New-Item -Path $MaintenanceFlag -ItemType File -Force | Out-Null
    Start-Sleep -Seconds 2

    # 2. Backup anlegen (mit einfacher Fortschrittsanzeige)
    $BackupRoot = Join-Path $TargetPath "Backup"
    $BackupPath = Join-Path $BackupRoot ("_backup_{0}" -f (Get-Date -Format 'yyyyMMdd_HHmm'))
    Write-Log "Erstelle Backup unter: $BackupPath"
    New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null

    $normalizedTarget = $TargetPath.TrimEnd('\','/')
    $normalizedBackupRoot = $BackupRoot.TrimEnd('\','/')

    $files = Get-ChildItem -Path $TargetPath -Recurse -File -Force -ErrorAction SilentlyContinue |
             Where-Object { -not $_.FullName.StartsWith($normalizedBackupRoot, [System.StringComparison]::OrdinalIgnoreCase) }

    $total = $files.Count
    $copied = 0

    if ($total -eq 0) {
        Write-Log "Keine Dateien zum Backup gefunden." "WARN"
    }
    else {
        Write-Log "Starte Backup von $total Dateien..."
        foreach ($f in $files) {
            $rel = $f.FullName.Substring($normalizedTarget.Length)
            if ($rel.StartsWith('\') -or $rel.StartsWith('/')) {
                $rel = $rel.Substring(1)
            }

            $destFile = Join-Path $BackupPath $rel
            $destDir = Split-Path -Path $destFile -Parent

            if (-not (Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }

            try {
                Copy-Item -Path $f.FullName -Destination $destFile -Force -ErrorAction Stop
                $copied++
            }
            catch {
                Write-Log "Fehler beim Kopieren von '$($f.FullName)': $($_.Exception.Message)" "WARN"
            }

            if (($copied % 10) -eq 0 -or $copied -eq $total) {
                $progressMsg = "Backup: $copied von $total Dateien kopiert."
                Write-Host $progressMsg
                Write-Log $progressMsg "DEBUG"
            }
        }

        Write-Log "Backup abgeschlossen: $copied von $total Dateien erfolgreich kopiert."
    }

    # Entferne vorhandene Haupt-DLL (wichtig: als erstes gelöscht)
    if (Test-Path $targetMainDll) {
        Write-Log "Lösche vorhandene Haupt-DLL: $targetMainDll"
        try {
            Remove-Item -Path $targetMainDll -Force -ErrorAction Stop
            Write-Log "Haupt-DLL gelöscht."
        }
        catch {
            Write-Log "Konnte Haupt-DLL nicht löschen: $($_.Exception.Message)" "ERROR"
            throw
        }
    }

    # 3. appsettings.json mergen
    $NewSettingsPath = Join-Path $SourcePath "appsettings.json"
    $ExistingSettingsPath = Join-Path $TargetPath "appsettings.json"

    if ((Test-Path $NewSettingsPath) -and (Test-Path $ExistingSettingsPath)) {
        Write-Log "Vergleiche und merge appsettings.json..."

        $rawNew = Get-Content $NewSettingsPath -Raw
        $sanitized = Remove-JsonComments -text $rawNew
        $sanitized = Remove-TrailingCommas -text $sanitized

        try {
            $newJson = $sanitized | ConvertFrom-Json -ErrorAction Stop
        }
        catch {
            Write-Log "Fehler beim Parsen der neuen appsettings.json: $($_.Exception.Message)" "ERROR"
            $debugPath = Join-Path $TargetPath ("appsettings.json.parseerror.{0}.txt" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
            Write-Log "Schreibe fehlerhafte Rohdatei nach: $debugPath" "WARN"
            $rawNew | Out-File -FilePath $debugPath -Encoding UTF8
            throw
        }

        $rawExisting = Get-Content $ExistingSettingsPath -Raw
        $sanExisting = Remove-JsonComments -text $rawExisting
        $sanExisting = Remove-TrailingCommas -text $sanExisting

        try {
            $existingJson = $sanExisting | ConvertFrom-Json -ErrorAction Stop
        }
        catch {
            Write-Log "Fehler beim Parsen der bestehenden appsettings.json: $($_.Exception.Message)" "ERROR"
            $debugPath2 = Join-Path $TargetPath ("appsettings.json.existing.parseerror.{0}.txt" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
            Write-Log "Schreibe fehlerhafte bestehende Datei nach: $debugPath2" "WARN"
            $rawExisting | Out-File -FilePath $debugPath2 -Encoding UTF8
            throw
        }

        Merge-Json -source $newJson -target $existingJson

        $existingJson | ConvertTo-Json -Depth 100 | Set-Content -Path $ExistingSettingsPath -Encoding UTF8
        Write-Log "appsettings.json wurde erfolgreich aktualisiert."
    }
    elseif (Test-Path $NewSettingsPath) {
        Copy-Item -Path $NewSettingsPath -Destination $ExistingSettingsPath -Force
        Write-Log "appsettings.json war nicht vorhanden — neue Datei kopiert."
    }
    else {
        Write-Log "appsettings.json wurde im Quellpfad nicht gefunden – skip Merge." "WARN"
    }

    # 4. Dateien kopieren (appsettings.json und Haupt-DLL ausgenommen)
    # WICHTIG: maintenance.flag selbst NICHT überschreiben — liegt außerhalb $SourcePath, daher kein Risiko
    Write-Log "Kopiere neue Dateien (ohne appsettings.json und $MainDllName)..."
    Copy-Item -Path (Join-Path $SourcePath '*') -Destination $TargetPath -Recurse -Force -Exclude "appsettings.json",$MainDllName
    Write-Log "Dateien erfolgreich kopiert."

    # 5. Haupt-DLL als letzten Schritt hinzufügen (falls vorhanden)
    if (Test-Path $sourceMainDll) {
        Write-Log "Kopiere Haupt-DLL zuletzt: $sourceMainDll -> $targetMainDll"
        try {
            Copy-Item -Path $sourceMainDll -Destination $targetMainDll -Force -ErrorAction Stop
            Write-Log "Haupt-DLL erfolgreich kopiert."
        }
        catch {
            Write-Log "Fehler beim Kopieren der Haupt-DLL: $($_.Exception.Message)" "ERROR"
            throw
        }
    }
    else {
        Write-Log "Quell-Haupt-DLL wurde nicht gefunden: $sourceMainDll" "WARN"
    }

    # 6. web.config "touch" um IIS-Recycle auszulösen (Timestamp aktualisieren)
    $webConfig = Join-Path $TargetPath "web.config"
    if (Test-Path $webConfig) {
        try {
            Write-Log "Aktualisiere Timestamp von web.config, um IIS-Recycle auszulösen..."
            (Get-Item $webConfig).LastWriteTime = Get-Date
            Write-Log "web.config getoucht."
        }
        catch {
            Write-Log "Konnte web.config Timestamp nicht setzen: $($_.Exception.Message)" "WARN"
        }
    }
    else {
        Write-Log "web.config nicht gefunden, kein IIS-Recycle nötig." "DEBUG"
    }

    # Kurze Pause, damit IIS den Recycle wirklich abgeschlossen hat, bevor der Wartungsmodus deaktiviert wird
    Write-Log "Warte 5 Sekunden auf IIS-Recycle..."
    Start-Sleep -Seconds 5

    # 7. Wartungsmodus deaktivieren
    Write-Log "Deaktiviere Wartungsmodus..."
    if (Test-Path $MaintenanceFlag) {
        Remove-Item -Path $MaintenanceFlag -Force -ErrorAction SilentlyContinue
    }

    Write-Log "=== Deployment erfolgreich abgeschlossen ==="
}
catch {
    $err = $_.Exception
    Write-Log -Message ("FEHLER: {0}`n{1}" -f $err.Message, $err.StackTrace) -Level "ERROR"
    Write-Log "Deployment abgebrochen." "ERROR"

    if (Test-Path $MaintenanceFlag) {
        Write-Log "Wartungsmodus bleibt aktiv (zur Sicherheit)." "WARN"
    }
    throw
}