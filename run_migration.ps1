Add-Type -Path 'C:\proyectos\AOCR\packages\Npgsql.4.1.13\lib\net461\Npgsql.dll'
$connString = 'Host=172.20.16.55;Port=5432;Database=dgac_des;Username=postgres;Password=postgres'
$conn = New-Object Npgsql.NpgsqlConnection($connString)
$conn.Open()
$sql = Get-Content -Path 'c:\proyectos\AOCR\scripts\20260719_aocr_fr3_outbox.sql' -Raw
$cmd = New-Object Npgsql.NpgsqlCommand($sql, $conn)
$cmd.ExecuteNonQuery()
$conn.Close()
Write-Host 'Migracion ejecutada con exito'
