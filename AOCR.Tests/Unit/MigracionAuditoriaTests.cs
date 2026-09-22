using System;
using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class MigracionAuditoriaTests
    {
        private string GetConnectionString()
        {
            var config = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            if (config == null)
                throw new InvalidOperationException("No se encontró AOCRConnection en app.config");
            return config.ConnectionString;
        }

        [TestMethod]
        [Description("Migración: Agregar columna usuario_id a aocr_tbauditoria")]
        public void MigracionAddUsuarioIdAuditoria()
        {
            string connString = GetConnectionString();
            Console.WriteLine($"Conectando a: {connString.Replace("Password=control", "Password=***")}");

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                Console.WriteLine("✓ Conexión abierta");

                // Ejecutar migración
                string sql = @"
ALTER TABLE IF EXISTS public.aocr_tbauditoria
ADD COLUMN IF NOT EXISTS usuario_id INTEGER NULL;

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_usuario_id 
ON public.aocr_tbauditoria (usuario_id);

COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción auditada';
";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    var result = cmd.ExecuteNonQuery();
                    Console.WriteLine($"✓ Script ejecutado: {result} filas afectadas");
                }

                // Verificar que la columna existe
                using (var cmd = new NpgsqlCommand(
                    "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'aocr_tbauditoria' AND column_name = 'usuario_id'",
                    conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read(), "La columna usuario_id no existe en aocr_tbauditoria");
                        string colName = reader["column_name"].ToString();
                        string dataType = reader["data_type"].ToString();
                        Console.WriteLine($"✓ Columna verificada: {colName} ({dataType})");
                        Assert.AreEqual("usuario_id", colName);
                        Assert.AreEqual("integer", dataType);
                    }
                }

                conn.Close();
            }

            Console.WriteLine("✓ MIGRACIÓN COMPLETADA EXITOSAMENTE");
        }
    }
}
