param(
    [int]$DaysBack = 1,
    [string]$LogsPath = "CapaPresentacion/App_Data/Logs",
    [switch]$IncludeUnhandled = $true,
    [switch]$ExportCsv = $true,
    [switch]$UseFixedOutputNames = $false,
    [switch]$IncludeEmail = $true
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$resolvedLogsPath = $LogsPath
if (-not [System.IO.Path]::IsPathRooted($LogsPath)) {
    $resolvedLogsPath = Join-Path $projectRoot $LogsPath
}

if (-not (Test-Path $resolvedLogsPath)) {
    throw "No existe la carpeta de logs: $resolvedLogsPath"
}

$since = (Get-Date).AddDays(-1 * [math]::Abs($DaysBack))
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if ($UseFixedOutputNames) {
    $reportPath = Join-Path $resolvedLogsPath "REPORTE_ERRORES_ULTIMO.log"
    $csvPath = Join-Path $resolvedLogsPath "REPORTE_ERRORES_ULTIMO.csv"
} else {
    $reportPath = Join-Path $resolvedLogsPath ("REPORTE_ERRORES_{0}.log" -f $timestamp)
    $csvPath = Join-Path $resolvedLogsPath ("REPORTE_ERRORES_{0}.csv" -f $timestamp)
}

$aocrFiles = Get-ChildItem -Path $resolvedLogsPath -Filter "AOCR_*.log" -File |
    Where-Object { $_.LastWriteTime -ge $since } |
    Sort-Object LastWriteTime

$errorEntries = New-Object System.Collections.Generic.List[object]
$emailEntries = New-Object System.Collections.Generic.List[object]
$levelCount = @{}
$errCodeCount = @{}
$emailCount = @{}

$emailPatterns = @(
    "Correo enviado exitosamente",
    "Enviando correo",
    "correo con adjunto enviado",
    "EnviarRecuperar",
    "EMAIL_FACTURA",
    "Factura aprobada",
    "Fallo envio de factura",
    "No hay destinatarios validos",
    "SMTP no configurado"
)

foreach ($file in $aocrFiles) {
    $lines = Get-Content -Path $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $lineTimestamp = $null
        if ($line.Length -ge 23 -and $line -match "^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}") {
            try {
                $lineTimestamp = [datetime]::ParseExact($line.Substring(0, 23), "yyyy-MM-dd HH:mm:ss.fff", $null)
            }
            catch { }
        }

        if ($line -match "^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \| (?<level>[A-Z]+)\s+\| (?<rest>.*)$") {
            if ($lineTimestamp -ne $null -and $lineTimestamp -lt $since) { continue }
            $level = $matches["level"].Trim()
            if (-not $levelCount.ContainsKey($level)) { $levelCount[$level] = 0 }
            $levelCount[$level]++
        }

        if ($line -match "\| ERROR \|") {
            if ($lineTimestamp -ne $null -and $lineTimestamp -lt $since) { continue }
            $exception = ""
            if ($i + 1 -lt $lines.Count -and $lines[$i + 1] -match "^\s+Exception:") {
                $exception = $lines[$i + 1].Trim()
            }

            $errCode = ""
            if ($line -match "ERR:(?<err>[A-Z_]+)") {
                $errCode = $matches["err"]
                if (-not $errCodeCount.ContainsKey($errCode)) { $errCodeCount[$errCode] = 0 }
                $errCodeCount[$errCode]++
            }

            $errorEntries.Add([pscustomobject]@{
                    File      = $file.Name
                    Line      = $line
                    Exception = $exception
                    ErrCode   = $errCode
                })
        }

        if ($IncludeEmail) {
            foreach ($pat in $emailPatterns) {
                if ($line -match [regex]::Escape($pat)) {
                    if ($lineTimestamp -ne $null -and $lineTimestamp -lt $since) { break }
                    if (-not $emailCount.ContainsKey($pat)) { $emailCount[$pat] = 0 }
                    $emailCount[$pat]++
                    $emailEntries.Add([pscustomobject]@{
                            File = $file.Name
                            Line = $line
                            Tag  = $pat
                        })
                    break
                }
            }
        }
    }
}

$unhandledPath = Join-Path $resolvedLogsPath "UnhandledExceptions.log"
$unhandledItems = New-Object System.Collections.Generic.List[string]
$unhandledTypeCount = @{}

if ($IncludeUnhandled -and (Test-Path $unhandledPath)) {
    $uLines = Get-Content -Path $unhandledPath
    for ($j = 0; $j -lt $uLines.Count; $j++) {
        $l = $uLines[$j]
        if ($l -match "^\d{4}-\d{2}-\d{2} ") {
            $entryDate = [datetime]::ParseExact($l.Substring(0, 23), "yyyy-MM-dd HH:mm:ss.fff", $null)
            if ($entryDate -ge $since) {
                $head = $l
                $next = if ($j + 1 -lt $uLines.Count) { $uLines[$j + 1] } else { "" }
                $summary = ($head + " | " + $next).Trim()
                $unhandledItems.Add($summary)

                if ($next -match "^(?<etype>[A-Za-z0-9\._]+Exception)") {
                    $etype = $matches["etype"]
                    if (-not $unhandledTypeCount.ContainsKey($etype)) { $unhandledTypeCount[$etype] = 0 }
                    $unhandledTypeCount[$etype]++
                }
            }
        }
    }
}

$out = New-Object System.Collections.Generic.List[string]
$out.Add("REPORTE DE ERRORES AOCR")
$out.Add(("Generado: {0}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss")))
$out.Add(("Ventana analizada: desde {0}" -f $since.ToString("yyyy-MM-dd HH:mm:ss")))
$out.Add(("Carpeta logs: {0}" -f (Resolve-Path $resolvedLogsPath)))
$out.Add("")
$out.Add("ARCHIVOS ANALIZADOS")
if ($aocrFiles.Count -eq 0) {
    $out.Add("- No se encontraron archivos AOCR_*.log en la ventana indicada.")
} else {
    foreach ($f in $aocrFiles) {
        $out.Add(("- {0} | {1} bytes | modificado {2}" -f $f.Name, $f.Length, $f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")))
    }
}

$out.Add("")
$out.Add("RESUMEN DE NIVELES")
if ($levelCount.Keys.Count -eq 0) {
    $out.Add("- Sin eventos estructurados en el periodo.")
} else {
    foreach ($k in ($levelCount.Keys | Sort-Object)) {
        $out.Add(("- {0}: {1}" -f $k, $levelCount[$k]))
    }
}

$out.Add("")
$out.Add(("ERRORES DETECTADOS (AOCR_*.log): {0}" -f $errorEntries.Count))
if ($errorEntries.Count -eq 0) {
    $out.Add("- No se detectaron líneas con nivel ERROR.")
} else {
    $idx = 1
    foreach ($e in $errorEntries) {
        $out.Add(("{0}. [{1}] {2}" -f $idx, $e.File, $e.Line))
        if (-not [string]::IsNullOrWhiteSpace($e.Exception)) {
            $out.Add(("   {0}" -f $e.Exception))
        }
        $idx++
    }
}

$out.Add("")
$out.Add("TOP CODIGOS ERR")
if ($errCodeCount.Keys.Count -eq 0) {
    $out.Add("- Sin códigos ERR:* en los errores del periodo.")
} else {
    foreach ($k in ($errCodeCount.Keys | Sort-Object)) {
        $out.Add(("- {0}: {1}" -f $k, $errCodeCount[$k]))
    }
}

$out.Add("")
$out.Add(("EVENTOS EMAIL (AOCR_*.log): {0}" -f $emailEntries.Count))
if (-not $IncludeEmail) {
    $out.Add("- Análisis de correo deshabilitado.")
} elseif ($emailEntries.Count -eq 0) {
    $out.Add("- No se detectaron eventos de correo en la ventana.")
} else {
    $idx = 1
    foreach ($e in $emailEntries) {
        $out.Add(("{0}. [{1}] {2}" -f $idx, $e.File, $e.Line))
        $idx++
    }
}

$out.Add("")
$out.Add("TOP PATRONES EMAIL")
if (-not $IncludeEmail) {
    $out.Add("- Análisis de correo deshabilitado.")
} elseif ($emailCount.Keys.Count -eq 0) {
    $out.Add("- Sin patrones de correo detectados.")
} else {
    foreach ($k in ($emailCount.Keys | Sort-Object)) {
        $out.Add(("- {0}: {1}" -f $k, $emailCount[$k]))
    }
}

$out.Add("")
$out.Add(("UNHANDLED EXCEPTIONS (últimos {0} días): {1}" -f [math]::Abs($DaysBack), $unhandledItems.Count))
if ($IncludeUnhandled -and (Test-Path $unhandledPath)) {
    if ($unhandledItems.Count -eq 0) {
        $out.Add("- Sin nuevas excepciones no controladas en la ventana.")
    } else {
        $preview = [Math]::Min(20, $unhandledItems.Count)
        for ($p = 0; $p -lt $preview; $p++) {
            $out.Add(("{0}. {1}" -f ($p + 1), $unhandledItems[$p]))
        }
        if ($unhandledItems.Count -gt $preview) {
            $out.Add(("- ... {0} entradas adicionales omitidas" -f ($unhandledItems.Count - $preview)))
        }
    }

    $out.Add("")
    $out.Add("TOP TIPOS DE EXCEPCION (Unhandled)")
    if ($unhandledTypeCount.Keys.Count -eq 0) {
        $out.Add("- No se pudo clasificar tipos de excepción.")
    } else {
        foreach ($k in ($unhandledTypeCount.Keys | Sort-Object)) {
            $out.Add(("- {0}: {1}" -f $k, $unhandledTypeCount[$k]))
        }
    }
} else {
    $out.Add("- Archivo UnhandledExceptions.log no encontrado o análisis deshabilitado.")
}

$out | Set-Content -Path $reportPath -Encoding UTF8

Write-Host ("Reporte generado: {0}" -f $reportPath)

if ($ExportCsv) {
    $rows = New-Object System.Collections.Generic.List[object]

    foreach ($e in $errorEntries) {
        $rows.Add([pscustomobject]@{
                Tipo      = "ERROR"
                Archivo    = $e.File
                Linea      = $e.Line
                Exception  = $e.Exception
                CodigoErr  = $e.ErrCode
            })
    }

    if ($IncludeEmail) {
        foreach ($m in $emailEntries) {
            $rows.Add([pscustomobject]@{
                    Tipo      = "EMAIL"
                    Archivo    = $m.File
                    Linea      = $m.Line
                    Exception  = ""
                    CodigoErr  = $m.Tag
                })
        }
    }

    if ($IncludeUnhandled -and (Test-Path $unhandledPath)) {
        foreach ($u in $unhandledItems) {
            $rows.Add([pscustomobject]@{
                    Tipo      = "UNHANDLED"
                    Archivo    = "UnhandledExceptions.log"
                    Linea      = $u
                    Exception  = ""
                    CodigoErr  = ""
                })
        }
    }

    $rows | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
    Write-Host ("CSV generado: {0}" -f $csvPath)
}
