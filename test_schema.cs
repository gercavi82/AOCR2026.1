using System;
using System.Data;
using Npgsql;
using System.Configuration;

class Program
{
    static void Main()
    {
        string connStr = "Host=localhost;Database=aocr_db;Username=postgres;Password=postgres";
        using (var conn = new NpgsqlConnection(connStr))
        {
            conn.Open();
            using (var cmd = new NpgsqlCommand("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'aocr_or_orden';", conn))
            using (var reader = cmd.ExecuteReader())
            {
                Console.WriteLine("Columns in aocr_or_orden:");
                while (reader.Read())
                {
                    Console.WriteLine($"{reader.GetString(0)} - {reader.GetString(1)}");
                }
            }
        }
    }
}
