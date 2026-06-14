# Validación de infraestructura preproducción/producción AOCR
# Ejecutar en el servidor IIS o estación con acceso a BD/SMTP/IIS.
param(
    [string]$PublishPath = "C:\AOCR\publicacion1",
    [string]$SiteName = "",
    [string]$BaseUrl = "",
    [string]$DbHost = "172.20.16.55",
    [int]$DbPort = 5432,
    [string]$DbName = "dgac_des",
    [string]$DbUser = "root",
    [string]$PgPassword = $env:PGPASSWORD
)

$ErrorActionPreference = "Continue"
$results = @()

function Add-Result {
    param([string]$Id, [string]$Item, [bool]$Ok, [string]$Detail)
    $script:results += [pscustomobject]@{
        Id     = $Id
        Item   = $Item
        Estado = if ($Ok) { "OK" } else { "FALLO" }
        Detalle = $Detail
    }
}

Write-Host "=== AOCR validate-infra $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" -ForegroundColor Cyan

# 1. Carpeta publicación
$pubOk = Test-Path $PublishPath
Add-Result "INF-01" "Carpeta publicación ($PublishPath)" $pubOk $(if ($pubOk) { "Existe" } else { "No encontrada" })

if ($pubOk) {
    $bin = Join-Path $PublishPath "bin\CapaPresentacion.dll"
    $web = Join-Path $PublishPath "Web.config"
    Add-Result "INF-02" "CapaPresentacion.dll publicado" (Test-Path $bin) $(if (Test-Path $bin) { (Get-Item $bin).LastWriteTime.ToString() } else { "Ausente" })
    Add-Result "INF-03" "Web.config publicado" (Test-Path $web) $(if (Test-Path $web) { "OK" } else { "Ausente" })
}

# 2. App_Data permisos escritura
$appData = Join-Path $PublishPath "App_Data"
if (Test-Path $appData) {
    $testFile = Join-Path $appData ("write_test_{0}.tmp" -f [guid]::NewGuid().ToString("N").Substring(0,8))
    try {
        "test" | Out-File -FilePath $testFile -Encoding utf8
        Remove-Item $testFile -Force
        Add-Result "INF-04" "Permisos escritura App_Data" $true "Escritura OK"
    } catch {
        Add-Result "INF-04" "Permisos escritura App_Data" $false $_.Exception.Message
    }
} else {
    Add-Result "INF-04" "Permisos escritura App_Data" $false "App_Data no existe"
}

# 3. .NET Framework 4.8
try {
    $net48 = Get-ChildItem "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\" -ErrorAction Stop |
        Get-ItemProperty -Name Release -ErrorAction Stop
    $okNet = $net48.Release -ge 528040
    Add-Result "INF-05" ".NET Framework 4.8+" $okNet ("Release=" + $net48.Release)
} catch {
    Add-Result "INF-05" ".NET Framework 4.8+" $false "No se pudo leer registro"
}

# 4. IIS App Pool
$appcmd = Join-Path $env:windir "system32\inetsrv\appcmd.exe"
if (Test-Path $appcmd) {
    if (-not [string]::IsNullOrWhiteSpace($SiteName)) {
        $pool = (& $appcmd list site /site.name:$SiteName /text:applicationPool 2>$null).Trim()
        if ($pool) {
            $runtime = (& $appcmd list apppool /name:$pool /text:managedRuntimeVersion 2>$null).Trim()
            $okPool = $runtime -match "v4"
            Add-Result "INF-06" "IIS App Pool $pool" $okPool "Runtime=$runtime"
        } else {
            Add-Result "INF-06" "IIS App Pool" $false "Sitio no encontrado: $SiteName"
        }
    } else {
        Add-Result "INF-06" "IIS App Pool" $true "Omitido (pase -SiteName)"
    }
} else {
    Add-Result "INF-06" "IIS App Pool" $false "appcmd no disponible (no es servidor IIS)"
}

# 5. Variable entorno AOCR_CONNSTR_POSTGRESQL
$envConn = $env:AOCR_CONNSTR_POSTGRESQL
Add-Result "INF-07" "AOCR_CONNSTR_POSTGRESQL" (-not [string]::IsNullOrWhiteSpace($envConn)) $(if ($envConn) { "Configurada (longitud $($envConn.Length))" } else { "No definida; usa Web.config fallback" })

# 6. PostgreSQL
if (-not $PgPassword) {
    Add-Result "INF-08" "PostgreSQL conectividad" $false "PGPASSWORD no definido"
} else {
    $pgOk = $false
    $pgDetail = ""
    try {
        $env:PGPASSWORD = $PgPassword
        $psql = Get-Command psql -ErrorAction SilentlyContinue
        if ($psql) {
            $out = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -c "SELECT 1" 2>&1
            $pgOk = ($LASTEXITCODE -eq 0)
            $pgDetail = if ($pgOk) { "SELECT 1 OK" } else { ($out -join " ") }
        } else {
            $pgDetail = "psql no en PATH"
        }
    } catch {
        $pgDetail = $_.Exception.Message
    } finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
    $pgLabel = "PostgreSQL ($DbHost`:$DbPort/$DbName)"
    Add-Result "INF-08" $pgLabel $pgOk $pgDetail
}

# 7. email_queue
if ($PgPassword -and (Get-Command psql -ErrorAction SilentlyContinue)) {
    try {
        $env:PGPASSWORD = $PgPassword
        $q = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -c "SELECT COUNT(*) FROM email_queue WHERE COALESCE(status,estado,'') IN ('PENDIENTE','PENDING')" 2>&1
        $qOk = ($LASTEXITCODE -eq 0)
        Add-Result "INF-09" "Cola email_queue" $qOk $(if ($qOk) { "Pendientes: $($q.Trim())" } else { $q -join " " })
    } catch {
        Add-Result "INF-09" "Cola email_queue" $false $_.Exception.Message
    } finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

# 8. Certificados .p12
$certPaths = @(
    (Join-Path $PublishPath "App_Data\firma"),
    (Join-Path $PublishPath "App_Data\Certificados")
)
$foundCerts = @()
foreach ($dir in $certPaths) {
    if (Test-Path $dir) {
        $foundCerts += Get-ChildItem $dir -Filter "*.p12" -ErrorAction SilentlyContinue
        $foundCerts += Get-ChildItem $dir -Filter "*.pfx" -ErrorAction SilentlyContinue
    }
}
Add-Result "INF-10" "Certificados .p12/.pfx en App_Data" ($foundCerts.Count -gt 0) ("Encontrados: " + $foundCerts.Count)

# 9. Health endpoints (si BaseUrl)
if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
    $urls = @(
        "$BaseUrl/Health/Live",
        "$BaseUrl/Health/Ready",
        "$BaseUrl/Health/Details"
    )
    foreach ($u in $urls) {
        try {
            $r = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 15
            Add-Result "INF-11" $u ($r.StatusCode -lt 400) ("HTTP $($r.StatusCode)")
        } catch {
            $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
            Add-Result "INF-11" $u $false ("HTTP $code - $($_.Exception.Message)")
        }
    }
}

# Resumen
Write-Host ""
$results | Format-Table -AutoSize
$fail = ($results | Where-Object { $_.Estado -eq "FALLO" }).Count
Write-Host "Total: $($results.Count) | OK: $($results.Count - $fail) | FALLO: $fail" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
if ($fail -gt 0) { exit 1 }
exit 0
