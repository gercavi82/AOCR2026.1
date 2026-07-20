using System;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using CapaDatos.Entidades;
using CapaDatos.DAOs;
using System.IO;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class Fr3OutboxIntegrationTests
    {
        private string _connString;
        private Fr3OutboxDAO _dao;

        [TestInitialize]
        public void Setup()
        {
            _connString = System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString 
                ?? "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=postgres;Password=postgres";
            _dao = new Fr3OutboxDAO(_connString);

            // Crear la tabla si no existe (Migracion)
            string sqlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\scripts\20260719_aocr_fr3_outbox.sql");
            if (File.Exists(sqlPath))
            {
                using (var conn = new NpgsqlConnection(_connString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(File.ReadAllText(sqlPath), conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        [TestMethod]
        public void EncolarEvento_Should_Insert_And_Ignore_Duplicate_EventKey()
        {
            // Arrange
            string eventKey = "FR3_TEST_" + Guid.NewGuid();
            var evento = new Fr3OutboxEvent
            {
                EventKey = eventKey,
                OrdenId = 99999,
                Estado = "PENDIENTE",
                Intentos = 0,
                Payload = "{\"test\": true}"
            };

            // Act 1: Insertar primera vez
            bool result1 = _dao.EncolarEvento(evento);

            // Act 2: Intentar insertar de nuevo con el mismo eventKey
            bool result2 = _dao.EncolarEvento(evento);

            // Assert
            Assert.IsTrue(result1, "El primer insert debe retornar true.");
            Assert.IsFalse(result2, "El segundo insert debe retornar false por conflicto (ON CONFLICT DO NOTHING).");

            // Cleanup
            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("DELETE FROM aocr_fr3_outbox WHERE event_key = @key", conn))
                {
                    cmd.Parameters.AddWithValue("@key", eventKey);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
