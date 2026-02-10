# Test IBM DB2 iSeries Connection from PowerShell
# Requires IBM.Data.DB2.iSeries.dll (NuGet) and .NET Framework

Add-Type -Path "C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\packages\IBM.Data.DB2.iSeries.7.1.0\lib\net40\IBM.Data.DB2.iSeries.dll"

$connectionString = "DataSource=172.20.16.14;UserID=DGAC;Password=DGAC2024;DefaultCollection=DGACSYS;"

try {
    $conn = New-Object IBM.Data.DB2.iSeries.iDB2Connection($connectionString)
    $conn.Open()
    Write-Host "✅ Conexión exitosa a AS/400 (DB2 iSeries)" -ForegroundColor Green
    $query = "SELECT CIACOD, CIANOM FROM CIAARC WHERE CIAEST = 'AC' FETCH FIRST 5 ROWS ONLY"
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $query
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "Empresa: $($reader[0]) - $($reader[1])"
    }
    $reader.Close()
    $conn.Close()
} catch {
    Write-Host "❌ Error de conexión: $_" -ForegroundColor Red
}
