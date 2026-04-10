using System.Globalization;
using Npgsql;

var connectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;";
var userId = args.Length > 0 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId)
    ? parsedId
    : (int?)null;

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();

Console.WriteLine("=== Ultimos usuarios (muestra) ===");
await using (var listCmd = new NpgsqlCommand(@"
SELECT idusuario, codigousuario, nombreusuario, estadoactividad
FROM public.usuario
ORDER BY idusuario DESC
LIMIT 20;", conn))
await using (var reader = await listCmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
    {
        Console.WriteLine(
            $"id={reader.GetInt32(0)}, codigo={reader.GetString(1)}, nombre={reader.GetString(2)}, activo={(reader.GetString(3) == "1")}");
    }
}

if (userId is null)
{
    Console.WriteLine("\nPasa el id del usuario como argumento para revisar bloqueos de borrado.");
    return;
}

Console.WriteLine($"\n=== Diagnostico de borrado para id={userId} ===");

await using var userCmd = new NpgsqlCommand(@"
SELECT idusuario, codigousuario
FROM public.usuario
WHERE idusuario = @id;", conn);
userCmd.Parameters.AddWithValue("id", userId.Value);

int currentId;
string currentCodigo;
await using (var reader = await userCmd.ExecuteReaderAsync())
{
    if (!await reader.ReadAsync())
    {
        Console.WriteLine("Usuario no existe.");
        return;
    }

    currentId = reader.GetInt32(0);
    currentCodigo = reader.GetString(1);
}

Console.WriteLine($"Usuario encontrado: id={currentId}, codigo={currentCodigo}");

var fkRows = new List<(string Constraint, string ChildTable, string ChildColumn, string ParentColumn)>();

await using (var fkCmd = new NpgsqlCommand(@"
SELECT
    con.conname AS constraint_name,
    con.conrelid::regclass::text AS child_table,
    att2.attname AS child_column,
    att1.attname AS parent_column
FROM pg_constraint con
JOIN unnest(con.confkey) WITH ORDINALITY p(attnum, ordinality) ON TRUE
JOIN unnest(con.conkey) WITH ORDINALITY c(attnum, ordinality) ON c.ordinality = p.ordinality
JOIN pg_attribute att1 ON att1.attrelid = con.confrelid AND att1.attnum = p.attnum
JOIN pg_attribute att2 ON att2.attrelid = con.conrelid AND att2.attnum = c.attnum
WHERE con.contype = 'f'
  AND con.confrelid = 'public.usuario'::regclass
ORDER BY con.conname, c.ordinality;", conn))
await using (var reader = await fkCmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
    {
        fkRows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
    }
}

foreach (var fk in fkRows)
{
    object value;
    if (fk.ParentColumn.Equals("idusuario", StringComparison.OrdinalIgnoreCase))
    {
        value = currentId;
    }
    else if (fk.ParentColumn.Equals("codigousuario", StringComparison.OrdinalIgnoreCase))
    {
        value = currentCodigo;
    }
    else
    {
        Console.WriteLine($"[FK {fk.Constraint}] {fk.ChildTable}.{fk.ChildColumn} -> usuario.{fk.ParentColumn} (no analizado)");
        continue;
    }

    var countSql = $"SELECT COUNT(*) FROM {fk.ChildTable} WHERE {fk.ChildColumn} = @v;";
    await using var countCmd = new NpgsqlCommand(countSql, conn);
    countCmd.Parameters.AddWithValue("v", value);
    var count = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);
    if (count > 0)
    {
        Console.WriteLine($"[FK {fk.Constraint}] bloquea: {fk.ChildTable}.{fk.ChildColumn} -> {count} fila(s)");
    }
}

await using var tx = await conn.BeginTransactionAsync();
try
{
    await using var delCmd = new NpgsqlCommand("DELETE FROM public.usuario WHERE idusuario = @id;", conn, tx);
    delCmd.Parameters.AddWithValue("id", currentId);
    var rows = await delCmd.ExecuteNonQueryAsync();
    Console.WriteLine($"\nDELETE simulado OK. Filas afectadas: {rows}");
    await tx.RollbackAsync();
}
catch (PostgresException ex)
{
    Console.WriteLine("\nDELETE simulado fallo:");
    Console.WriteLine($"SqlState: {ex.SqlState}");
    Console.WriteLine($"Table: {ex.TableName}");
    Console.WriteLine($"Constraint: {ex.ConstraintName}");
    Console.WriteLine($"Message: {ex.MessageText}");
    Console.WriteLine($"Detail: {ex.Detail}");
    await tx.RollbackAsync();
}
