using System;
using System.Data;
using Npgsql;
using System.Configuration;

namespace DBCleanup
{
    class Program
    {
        static void Main()
        {
            string connStr = "Host=localhost;Port=5432;Database=aocr_db;Username=postgres;Password=admin"; 
            // Try to read from web.config if possible, but hardcoding local defaults for now just in case.
            // Let's actually connect and delete orphaned records.

            try {
                using (var conn = new NpgsqlConnection(connStr)) {
                    conn.Open();
                    
                    using (var cmd = new NpgsqlCommand("DELETE FROM aocr_tb_factura_pago WHERE orden_id NOT IN (SELECT id FROM aocr_or_orden);", conn)) {
                        int facturas = cmd.ExecuteNonQuery();
                        Console.WriteLine("Deleted " + facturas + " orphaned records from aocr_tb_factura_pago");
                    }
                    
                    using (var cmd = new NpgsqlCommand("DELETE FROM aocr_tbpago WHERE codigo_solicitud NOT IN (SELECT id FROM aocr_or_orden);", conn)) {
                        int pagos = cmd.ExecuteNonQuery();
                        Console.WriteLine("Deleted " + pagos + " orphaned records from aocr_tbpago");
                    }
                    
                    Console.WriteLine("SUCCESS");
                }
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
