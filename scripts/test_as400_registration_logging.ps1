param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\CapaDatos\bin\Release\CapaDatos.dll')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Web
[void][Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath))
$staticPrivate = [Reflection.BindingFlags]'Static,NonPublic'
$instancePrivate = [Reflection.BindingFlags]'Instance,NonPublic'
$logDirectory = Join-Path ([IO.Path]::GetTempPath()) ('aocr-as400-log-test-' + [Guid]::NewGuid().ToString('N'))
$logger = New-Object CapaDatos.Services.LoggingService($logDirectory)
$loggerField = [CapaDatos.Services.LoggingServiceFactory].GetField('_instance', $staticPrivate)
$previousLogger = $loggerField.GetValue($null)

try {
    $loggerField.SetValue($null, $logger)
    $record = New-Object CapaDatos.Models.UsuarioAs400Record
    $record.ClaveHash = 'HASH_PRIVADO_PRUEBA'
    $record.Correo = 'persona-prueba@example.invalid'
    $record.Identificacion = '1234567890'
    $errorConstructor = [System.Data.Odbc.OdbcError].GetConstructors($instancePrivate)[0]
    $errors = [Activator]::CreateInstance([System.Data.Odbc.OdbcErrorCollection], $true)
    $addError = $errors.GetType().GetMethod('Add', $instancePrivate)
    foreach ($nativeCode in @(-302, -420)) {
        $message = "Fallo simulado $($record.ClaveHash) $($record.Correo) $($record.Identificacion);PWD={secreto};UID=cuenta;"
        $odbcError = $errorConstructor.Invoke(@('driver de prueba', $message, '22001', $nativeCode))
        [void]$addError.Invoke($errors, @($odbcError))
    }
    $createException = [System.Data.Odbc.OdbcException].GetMethod('CreateException', $staticPrivate)
    $returnCode = [Enum]::ToObject($createException.GetParameters()[1].ParameterType, -1)
    $odbcException = $createException.Invoke($null, @($errors, $returnCode))
    $wrapped = New-Object System.Exception('Mensaje publico generico', $odbcException)
    $method = [CapaDatos.DAOs.UsuarioAS400DAO].GetMethod('RegistrarErrorAs400', $staticPrivate)
    [void]$method.Invoke($null, @($wrapped.PSObject.BaseObject, 'INSERTAR', 'TESTLIB', 'USUARC', $record.PSObject.BaseObject))
    [void]$method.Invoke($null, @($odbcException.PSObject.BaseObject, 'CONSULTAR_COLUMNAS', 'TESTLIB', 'USUAR1', $record.PSObject.BaseObject))
    $log = Get-Content -LiteralPath (Join-Path $logDirectory ('AOCR_' + (Get-Date -Format yyyyMMdd) + '.log')) -Raw
    foreach ($expected in @('REGISTRO_ERROR', 'Etapa=INSERTAR', 'TESTLIB.USUARC', 'TESTLIB.USUAR1', 'CONSULTAR_COLUMNAS', 'SQLState=22001', 'NativeError=-302', 'NativeError=-420', 'Mensaje publico generico')) {
        if (-not $log.Contains($expected)) { throw "Falta evidencia en log: $expected" }
    }
    foreach ($secret in @($record.ClaveHash, $record.Correo, $record.Identificacion, 'secreto', 'cuenta')) {
        if ($log.Contains($secret)) { throw 'El log contiene un valor privado de prueba.' }
    }
    Write-Output 'PASS: archivo persistido, excepcion interna, todos los errores ODBC, etapas y datos privados ocultos. Sin conexion a AS400.'
    Write-Output "Evidencia: $logDirectory"
}
finally {
    $loggerField.SetValue($null, $previousLogger)
}
