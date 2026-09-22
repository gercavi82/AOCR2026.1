using System;
using System.Configuration;
using Npgsql;

namespace MigracionDB
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Obtener la cadena de conexión
                string connectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;";
                
                Console.WriteLine("Conectando a PostgreSQL...");
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    Console.WriteLine("✓ Conexión abierta exitosamente");
                    
                    // Script SQL para agregar la columna
                    string sql = @"
ALTER TABLE IF EXISTS public.aocr_tbauditoria
ADD COLUMN IF NOT EXISTS usuario_id INTEGER NULL;

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_usuario_id 
ON public.aocr_tbauditoria (usuario_id);

COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción auditada (referencia a tabla usuario.idusuario)';
";
                    
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("✓ Script SQL ejecutado exitosamente");
                        Console.WriteLine("✓ Columna usuario_id agregada a aocr_tbauditoria");
                        Console.WriteLine("✓ Índice idx_aocr_tbauditoria_usuario_id creado");
                    }
                    
                    // Verificar que la columna existe
                    using (var cmd = new NpgsqlCommand(@"
SELECT column_name, data_type FROM information_schema.columns 
WHERE table_name = 'aocr_tbauditoria' AND column_name = 'usuario_id';", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Console.WriteLine($"✓ Verificación: Columna '{reader["column_name"]}' de tipo '{reader["data_type"]}' existe correctamente");
                            }
                        }
                    }
                    
                    conn.Close();
                }
                
                Console.WriteLine("\n✓ MIGRACIÓN COMPLETADA EXITOSAMENTE");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
