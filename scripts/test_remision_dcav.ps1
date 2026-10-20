$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskDir = Join-Path $taskRoot ('.tmp/remision-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskDir | Out-Null
$pg = 'C:/Program Files/PostgreSQL/18/bin'
$started = $false
try {
    & "$pg/initdb.exe" -D "$taskDir/pgdata" -U aocr_review -A trust --encoding=UTF8 --no-locale | Out-File "$taskDir/init.log"
    if ($LASTEXITCODE -ne 0) { throw 'initdb' }
    & "$pg/pg_ctl.exe" -D "$taskDir/pgdata" -l "$taskDir/pg.log" -o '-p 55440 -h 127.0.0.1' -w start
    if ($LASTEXITCODE -ne 0) { throw 'pg_ctl' }
    $started = $true
    & "$pg/createdb.exe" -h 127.0.0.1 -p 55440 -U aocr_review remision_test
    if ($LASTEXITCODE -ne 0) { throw 'createdb' }
    Copy-Item "$taskRoot/CapaPresentacion/bin/*.dll" $taskDir
    $exe = "$taskDir/RemisionTest.exe"
    & 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /out:$exe /r:"$taskRoot/CapaPresentacion/bin/CapaDatos.dll" /r:"$taskRoot/CapaPresentacion/bin/CapaModelo.dll" /r:"$taskRoot/CapaPresentacion/bin/CapaNegocio.dll" /r:"$taskRoot/CapaPresentacion/bin/Npgsql.dll" /r:netstandard.dll /r:System.Data.dll /r:System.Configuration.dll "$PSScriptRoot/test_remision_dcav.cs"
    if ($LASTEXITCODE -ne 0) { throw 'csc' }
    $cfg = New-Object System.Xml.XmlDocument
    $cfg.LoadXml('<configuration><connectionStrings><add name="AOCRConnection" connectionString="Host=127.0.0.1;Port=55440;Database=remision_test;Username=aocr_review" /></connectionStrings></configuration>')
    $cola = $cfg.CreateElement('add')
    $cola.SetAttribute('name', 'PostgreSQL')
    $cola.SetAttribute('connectionString', 'Host=127.0.0.1;Port=55440;Database=remision_test;Username=aocr_review')
    [void]$cfg.configuration.connectionStrings.AppendChild($cola)
    $ref = New-Object System.Xml.XmlDocument
    $ref.Load("$taskRoot/AOCR.Tests/bin/Debug/AOCR.Tests.dll.config")
    [void]$cfg.DocumentElement.AppendChild($cfg.ImportNode($ref.configuration.runtime,$true))
    $cfg.Save("$exe.config")
    & $exe "$taskRoot/scripts/sql/20260722_revision_documental_coordinador.sql" "$taskRoot/scripts/sql/20261019_estados_revision_dcav.sql" "$taskRoot/scripts/sql/20261020_solicitud_campos_dcav.sql"
    if ($LASTEXITCODE -ne 0) { throw 'Pruebas fallidas' }
} finally {
    if ($started) { & "$pg/pg_ctl.exe" -D "$taskDir/pgdata" -m fast -w stop }
}
