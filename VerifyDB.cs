using System;
using Npgsql;

class CheckDB
{
    static void Main()
    {
        string cs = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;";
        
        try
        {
            using (var conn = new NpgsqlConnection(cs))
            {
                conn.Open();
                Console.WriteLine("✓ Conexión a PostgreSQL exitosa\n");
                
                // Verificar columnas
                Console.WriteLine("=== COLUMNAS DE aocr_tbauditoria ===\n");
                using (var cmd = new NpgsqlCommand(
                    @"SELECT column_name, data_type, is_nullable 
                      FROM information_schema.columns 
                      WHERE table_name = 'aocr_tbauditoria' 
                      ORDER BY ordinal_position",
                    conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        bool found_usuario_id = false;
                        bool found_fecha = false;
                        bool found_entidad = false;
                        
                        while (reader.Read())
                        {
                            string col = reader["column_name"].ToString();
                            string type = reader["data_type"].ToString();
                            string nullable = reader["is_nullable"].ToString();
                            
                            Console.WriteLine($"{col,25} | {type,30} | NULL={nullable}");
                            
                            if (col == "usuario_id") found_usuario_id = true;
                            if (col == "fecha") found_fecha = true;
                            if (col == "entidad") found_entidad = true;
                        }
                        
                        Console.WriteLine("\n=== VERIFICACIÓN DE COLUMNAS ===\n");
                        Console.WriteLine($"usuario_id EXISTS: {(found_usuario_id ? "✅ SÍ" : "❌ NO")}");
                        Console.WriteLine($"fecha EXISTS:      {(found_fecha ? "✅ SÍ" : "❌ NO")}");
                        Console.WriteLine($"entidad EXISTS:    {(found_entidad ? "✅ SÍ" : "❌ NO")}");
                    }
                }
                
                // Verificar índices
                Console.WriteLine("\n=== ÍNDICES EN aocr_tbauditoria ===\n");
                using (var cmd = new NpgsqlCommand(
                    @"SELECT indexname FROM pg_indexes 
                      WHERE tablename = 'aocr_tbauditoria' 
                      AND indexname LIKE 'idx_aocr%'
                      ORDER BY indexname",
                    conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.HasRows)
                        {
                            Console.WriteLine("❌ NO HAY ÍNDICES CREADOS");
                        }
                        while (reader.Read())
                        {
                            Console.WriteLine($"✅ {reader["indexname"]}");
                        }
                    }
                }
                
                // Verificar trigger
                Console.WriteLine("\n=== TRIGGERS EN aocr_tbauditoria ===\n");
                using (var cmd = new NpgsqlCommand(
                    @"SELECT trigger_name FROM information_schema.triggers 
                      WHERE event_object_table = 'aocr_tbauditoria'",
                    conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.HasRows)
                        {
                            Console.WriteLine("❌ NO HAY TRIGGERS CREADOS");
                        }
                        while (reader.Read())
                        {
                            Console.WriteLine($"✅ {reader["trigger_name"]}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
