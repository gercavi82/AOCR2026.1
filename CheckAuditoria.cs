using System;
using Npgsql;

class Program
{
    static void Main()
    {
        string cs = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;";
        
        try
        {
            using (var conn = new NpgsqlConnection(cs))
            {
                conn.Open();
                Console.WriteLine("Columnas actuales de aocr_tbauditoria:\n");
                
                using (var cmd = new NpgsqlCommand(
                    "SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = 'aocr_tbauditoria' ORDER BY ordinal_position",
                    conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine($"{reader["column_name"],25} | {reader["data_type"],20} | NULL={reader["is_nullable"]}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}
