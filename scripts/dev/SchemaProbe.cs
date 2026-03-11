using System;
using System.Collections.Generic;
using Npgsql;

internal static class SchemaProbe
{
    private static void Main(string[] args)
    {
        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Uso: SchemaProbe <connectionString>");
            Environment.ExitCode = 2;
            return;
        }

        var connectionString = args[0];
        var tables = new[]
        {
            "aocr_tbaeronave_solicitud",
            "aocr_tbdocumento",
            "aocr_tbhistorialestado",
            "aocr_tbhistorial_estado"
        };

        try
        {
            using (var cn = new NpgsqlConnection(connectionString))
            {
                cn.Open();

                Console.WriteLine("=== CONTEXTO ===");
                using (var cmdCtx = new NpgsqlCommand("SHOW search_path;", cn))
                {
                    Console.WriteLine("search_path: " + (cmdCtx.ExecuteScalar() ?? "<null>"));
                }

                Console.WriteLine();
                Console.WriteLine("=== TABLAS RELACIONADAS (historial/documento/aeronave) ===");
                using (var cmdTables = new NpgsqlCommand(@"
SELECT n.nspname, c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND (
        c.relname ILIKE '%historial%'
        OR c.relname ILIKE '%documento%'
        OR c.relname ILIKE '%aeronave%'
      )
ORDER BY n.nspname, c.relname;", cn))
                using (var rdT = cmdTables.ExecuteReader())
                {
                    while (rdT.Read())
                    {
                        Console.WriteLine("TABLE|" + rdT["nspname"] + "|" + rdT["relname"]);
                    }
                }

                foreach (var table in tables)
                {
                    PrintTable(cn, table);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.GetType().Name + " - " + ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void PrintTable(NpgsqlConnection cn, string table)
    {
        Console.WriteLine();
        Console.WriteLine("=== TABLE: " + table + " ===");

        using (var cmdReg = new NpgsqlCommand(
            "SELECT to_regclass(@tbl)::text;",
            cn))
        {
            cmdReg.Parameters.AddWithValue("@tbl", table);
            Console.WriteLine("to_regclass: " + (cmdReg.ExecuteScalar() ?? "<null>"));
        }

        using (var cmdCols = new NpgsqlCommand(@"
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = @table
ORDER BY ordinal_position;", cn))
        {
            cmdCols.Parameters.AddWithValue("@table", table);

            using (var rd = cmdCols.ExecuteReader())
            {
                while (rd.Read())
                {
                    var name = rd["column_name"] == DBNull.Value ? null : rd["column_name"].ToString();
                    var type = rd["data_type"] == DBNull.Value ? null : rd["data_type"].ToString();
                    var nullable = rd["is_nullable"] == DBNull.Value ? null : rd["is_nullable"].ToString();
                    var def = rd["column_default"] == DBNull.Value ? "<null>" : rd["column_default"].ToString();
                    Console.WriteLine("COLUMN|" + name + "|" + type + "|" + nullable + "|" + def);
                }
            }
        }

        using (var cmdC = new NpgsqlCommand(@"
SELECT c.conname, c.contype, pg_get_constraintdef(c.oid)
FROM pg_constraint c
JOIN pg_class t ON t.oid = c.conrelid
JOIN pg_namespace n ON n.oid = t.relnamespace
WHERE n.nspname = 'public' AND t.relname = @table
ORDER BY c.conname;", cn))
        {
            cmdC.Parameters.AddWithValue("@table", table);
            using (var rd = cmdC.ExecuteReader())
            {
                while (rd.Read())
                {
                    Console.WriteLine("CONSTRAINT|" + rd["conname"] + "|" + rd["contype"] + "|" + rd["pg_get_constraintdef"]);
                }
            }
        }
    }
}
