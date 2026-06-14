# Smoke test HTTP post go-live (T+15m / T+30m)
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,
    [switch]$IncludeAuthPages
)

$ErrorActionPreference = "Continue"
$BaseUrl = $BaseUrl.TrimEnd("/")

$anonymousChecks = @(
    @{ Path = "/Health/Live"; Expect = 200; Name = "Health Live" },
    @{ Path = "/Health/Ready"; Expect = 200; Name = "Health Ready" },
    @{ Path = "/Health/Details"; Expect = 200; Name = "Health Details" },
    @{ Path = "/Account/Login"; Expect = 200; Name = "Login page" }
)

$authChecks = @(
    @{ Path = "/Tecnico"; Expect = 200; Name = "Tecnico index" },
    @{ Path = "/RevisionDocumental"; Expect = 200; Name = "RevisionDocumental index" },
    @{ Path = "/SolicitudAOCR"; Expect = 200; Name = "SolicitudAOCR index" },
    @{ Path = "/OrdenRecaudacion"; Expect = 200; Name = "OrdenRecaudacion index" }
)

Write-Host "=== AOCR smoke-test $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl"
Write-Host ""

$all = $anonymousChecks
if ($IncludeAuthPages) { $all += $authChecks }

$ok = 0; $fail = 0
foreach ($c in $all) {
    $url = $BaseUrl + $c.Path
    try {
        $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 20 -MaximumRedirection 5
        $pass = ($r.StatusCode -eq $c.Expect) -or ($r.StatusCode -eq 302 -and $c.Path -ne "/Health/Live")
        if ($pass) {
            Write-Host "[OK]   $($c.Name) — HTTP $($r.StatusCode)" -ForegroundColor Green
            $ok++
        } else {
            Write-Host "[FAIL] $($c.Name) — HTTP $($r.StatusCode) (esperado $($c.Expect))" -ForegroundColor Red
            $fail++
        }
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        if ($code -eq 401 -or $code -eq 403) {
            Write-Host "[WARN] $($c.Name) — HTTP $code (requiere sesión; validar manual con rol)" -ForegroundColor Yellow
            $ok++
        } else {
            Write-Host "[FAIL] $($c.Name) — HTTP $code $($_.Exception.Message)" -ForegroundColor Red
            $fail++
        }
    }
}

Write-Host ""
Write-Host "Resultado: OK=$ok FAIL=$fail" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
if ($fail -gt 0) { exit 1 }
