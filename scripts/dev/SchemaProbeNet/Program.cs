using Npgsql;

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Uso: SchemaProbeNet <connectionString>");
    Environment.ExitCode = 2;
    return;
}

var connectionString = args[0];
var sqlFilePath = args.Length > 1 ? args[1] : null;
var rollbackAfterValidation = args.Any(a => string.Equals(a, "--rollback", StringComparison.OrdinalIgnoreCase));
var tables = new[]
{
    "aocr_tbaeronave_solicitud",
    "aocr_tbdocumento",
    "aocr_tbhistorialestado",
    "aocr_tbhistorial_estado",
    "aocr_tbinspeccion",
    "aocr_tbsolicitud"
};

try
{
    await using var cn = new NpgsqlConnection(connectionString);
    await cn.OpenAsync();

    if (!string.IsNullOrWhiteSpace(sqlFilePath))
    {
        await ExecuteSqlFileAsync(cn, sqlFilePath, rollbackAfterValidation);
        return;
    }

    Console.WriteLine("=== CONTEXTO ===");
    await using (var cmdCtx = new NpgsqlCommand("SHOW search_path;", cn))
    {
        Console.WriteLine("search_path: " + (await cmdCtx.ExecuteScalarAsync() ?? "<null>"));
    }

    Console.WriteLine();
    Console.WriteLine("=== TABLAS RELACIONADAS (historial/documento/aeronave/inspeccion/solicitud) ===");
    await using (var cmdTables = new NpgsqlCommand(@"
SELECT n.nspname, c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND (
        c.relname ILIKE '%historial%'
        OR c.relname ILIKE '%documento%'
        OR c.relname ILIKE '%aeronave%'
        OR c.relname ILIKE '%inspeccion%'
        OR c.relname ILIKE '%solicitud%'
      )
ORDER BY n.nspname, c.relname;", cn))
    await using (var rdT = await cmdTables.ExecuteReaderAsync())
    {
        while (await rdT.ReadAsync())
        {
            Console.WriteLine("TABLE|" + rdT["nspname"] + "|" + rdT["relname"]);
        }
    }

    foreach (var table in tables)
    {
        await PrintTableAsync(cn, table);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine("ERROR: " + ex.GetType().Name + " - " + ex.Message);
    Environment.ExitCode = 1;
}

static async Task ExecuteSqlFileAsync(NpgsqlConnection cn, string sqlFilePath, bool rollbackAfterValidation)
{
    if (!File.Exists(sqlFilePath))
    {
        throw new FileNotFoundException("No existe el archivo SQL.", sqlFilePath);
    }

    var sql = await File.ReadAllTextAsync(sqlFilePath);
    if (rollbackAfterValidation)
    {
        sql = System.Text.RegularExpressions.Regex.Replace(sql, @"(?im)^\s*(BEGIN|COMMIT);\s*$", string.Empty);
        await using var tx = await cn.BeginTransactionAsync();
        await using var validationCommand = new NpgsqlCommand(sql, cn, tx);
        await validationCommand.ExecuteNonQueryAsync();
        await tx.RollbackAsync();
        Console.WriteLine("VALIDACION_DDL_OK_ROLLBACK");
        return;
    }
    await using var cmd = new NpgsqlCommand(sql, cn);
    await using var reader = await cmd.ExecuteReaderAsync();

    var resultSet = 1;
    do
    {
        Console.WriteLine();
        Console.WriteLine("=== RESULTSET " + resultSet + " ===");

        var colCount = reader.FieldCount;
        if (colCount <= 0)
        {
            Console.WriteLine("(sin columnas)");
        }
        else
        {
            var headers = new string[colCount];
            for (var i = 0; i < colCount; i++)
            {
                headers[i] = reader.GetName(i);
            }

            Console.WriteLine(string.Join(" | ", headers));

            var rows = 0;
            while (await reader.ReadAsync())
            {
                var values = new string[colCount];
                for (var i = 0; i < colCount; i++)
                {
                    values[i] = reader.IsDBNull(i) ? "<null>" : Convert.ToString(reader.GetValue(i));
                }

                if (rows < 40)
                {
                    Console.WriteLine(string.Join(" | ", values));
                }

                rows++;
            }

            Console.WriteLine("(filas totales: " + rows + ")");
        }

        resultSet++;
    }
    while (await reader.NextResultAsync());
}

static async Task PrintTableAsync(NpgsqlConnection cn, string table)
{
    Console.WriteLine();
    Console.WriteLine("=== TABLE: " + table + " ===");

    await using (var cmdReg = new NpgsqlCommand("SELECT to_regclass(@tbl)::text;", cn))
    {
        cmdReg.Parameters.AddWithValue("@tbl", $"public.{table}");
        Console.WriteLine("to_regclass: " + (await cmdReg.ExecuteScalarAsync() ?? "<null>"));
    }

    await using (var cmdCols = new NpgsqlCommand(@"
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = @table
ORDER BY ordinal_position;", cn))
    {
        cmdCols.Parameters.AddWithValue("@table", table);

        await using var rd = await cmdCols.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var name = rd["column_name"] == DBNull.Value ? null : rd["column_name"].ToString();
            var type = rd["data_type"] == DBNull.Value ? null : rd["data_type"].ToString();
            var nullable = rd["is_nullable"] == DBNull.Value ? null : rd["is_nullable"].ToString();
            var def = rd["column_default"] == DBNull.Value ? "<null>" : rd["column_default"].ToString();
            Console.WriteLine("COLUMN|" + name + "|" + type + "|" + nullable + "|" + def);
        }
    }

    await using (var cmdC = new NpgsqlCommand(@"
SELECT c.conname, c.contype, pg_get_constraintdef(c.oid)
FROM pg_constraint c
JOIN pg_class t ON t.oid = c.conrelid
JOIN pg_namespace n ON n.oid = t.relnamespace
WHERE n.nspname = 'public' AND t.relname = @table
ORDER BY c.conname;", cn))
    {
        cmdC.Parameters.AddWithValue("@table", table);
        await using var rd = await cmdC.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            Console.WriteLine("CONSTRAINT|" + rd["conname"] + "|" + rd["contype"] + "|" + rd["pg_get_constraintdef"]);
        }
    }
}
