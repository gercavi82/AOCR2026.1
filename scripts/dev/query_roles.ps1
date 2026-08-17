$binDir = "c:\proyectos\AOCR\CapaPresentacion\bin"
Get-ChildItem -Path $binDir -Filter "*.dll" | ForEach-Object {
    try {
        [Reflection.Assembly]::LoadFrom($_.FullName) | Out-Null
    } catch {}
}
$cs = 'Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Pooling=true;'
$conn = New-Object Npgsql.NpgsqlConnection($cs)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = 'SELECT codigorol, descripcion, activo FROM rol ORDER BY codigorol;'
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Output ("{0} | {1} | {2}" -f $reader["codigorol"], $reader["descripcion"], $reader["activo"])
}
$conn.Close()
