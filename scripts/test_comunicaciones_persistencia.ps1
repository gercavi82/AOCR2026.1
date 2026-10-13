param(
    [string]$PostgresBin = 'C:/Program Files/PostgreSQL/18/bin',
    [string]$Compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
)
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskDirectory = Join-Path $taskRoot ('.tmp/comunicaciones-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskDirectory | Out-Null
$databaseDirectory = Join-Path $taskDirectory 'pgdata'
$started = $false
Push-Location $taskRoot
try {
    # El harness rechaza cualquier otro host, puerto o nombre de base.
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 55439)
    $listener.Start()
    $listener.Stop()
    & (Join-Path $PostgresBin 'initdb.exe') -D $databaseDirectory -U aocr_review -A trust --encoding=UTF8 --no-locale | Out-File (Join-Path $taskDirectory 'initdb.log')
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo inicializar la base temporal.' }
    & (Join-Path $PostgresBin 'pg_ctl.exe') -D $databaseDirectory -l (Join-Path $taskDirectory 'postgres.log') -o '-p 55439 -h 127.0.0.1' -w start
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar PostgreSQL temporal.' }
    $started = $true
    & (Join-Path $PostgresBin 'createdb.exe') -h 127.0.0.1 -p 55439 -U aocr_review comunicaciones_test
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la base de pruebas.' }
    foreach ($sqlFile in @('scripts/test_comunicaciones_fixture.sql', 'scripts/db/20261012_fechas_inspector.sql')) {
        & (Join-Path $PostgresBin 'psql.exe') -h 127.0.0.1 -p 55439 -U aocr_review -d comunicaciones_test -v ON_ERROR_STOP=1 -f $sqlFile
        if ($LASTEXITCODE -ne 0) { throw ('Falló SQL de prueba: ' + $sqlFile) }
    }
    Copy-Item -Path 'CapaPresentacion/bin/*.dll' -Destination $taskDirectory
    $testExe = Join-Path $taskDirectory 'ComunicacionesPersistenciaTest.exe'
    & $Compiler /nologo (('/out:' + $testExe)) /r:CapaDatos/bin/Debug/CapaDatos.dll /r:CapaNegocio/bin/Debug/CapaNegocio.dll /r:CapaModelo/bin/Debug/CapaModelo.dll /r:CapaPresentacion/bin/Npgsql.dll /r:netstandard.dll /r:System.Data.dll /r:System.Transactions.dll scripts/test_comunicaciones_persistencia.cs
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el harness; compile primero AOCR.sln en Debug.' }
    $testConfiguration = New-Object System.Xml.XmlDocument
    $testConfiguration.LoadXml('<configuration><connectionStrings><add name="AOCRConnection" connectionString="Host=127.0.0.1;Port=55439;Database=comunicaciones_test;Username=aocr_review;Pooling=true" providerName="Npgsql"/><add name="DefaultConnection" connectionString="Host=127.0.0.1;Port=55439;Database=comunicaciones_test;Username=aocr_review;Pooling=true" providerName="Npgsql"/></connectionStrings></configuration>')
    $referenceConfiguration = New-Object System.Xml.XmlDocument
    $referenceConfiguration.Load((Join-Path $taskRoot 'AOCR.Tests/bin/Debug/AOCR.Tests.dll.config'))
    if ($referenceConfiguration.configuration.runtime) {
        [void]$testConfiguration.DocumentElement.AppendChild($testConfiguration.ImportNode($referenceConfiguration.configuration.runtime, $true))
    }
    $testConfiguration.Save(($testExe + '.config'))
    & $testExe 'Host=127.0.0.1;Port=55439;Database=comunicaciones_test;Username=aocr_review;Pooling=true'
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las verificaciones de persistencia.' }
    & (Join-Path $PostgresBin 'psql.exe') -h 127.0.0.1 -p 55439 -U aocr_review -d comunicaciones_test -v ON_ERROR_STOP=1 -f scripts/db/20261012_fechas_inspector.sql
    if ($LASTEXITCODE -ne 0) { throw 'La migración no es idempotente.' }
}
finally {
    if ($started) { & (Join-Path $PostgresBin 'pg_ctl.exe') -D $databaseDirectory -m fast -w stop }
    Pop-Location
}
