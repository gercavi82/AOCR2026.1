using CapaNegocio.Services;
using CapaDatos.DAOs;
using System;
using System.Transactions;
using Npgsql;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class SolicitudRtEnvioCoordinacionTests
    {
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void Historial_ComparteTransaccionSinConexionAdicional(bool confirmar)
        {
            var connectionString = Environment.GetEnvironmentVariable("AOCR_TEST_POSTGRESQL");
            if (string.IsNullOrWhiteSpace(connectionString))
                Assert.Inconclusive("Requiere AOCR_TEST_POSTGRESQL; utiliza solamente una tabla temporal.");

            using (var cn = new NpgsqlConnection(connectionString))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TEMP TABLE aocr_tbhistorialestado (
                        codigohistorial SERIAL PRIMARY KEY, codigosolicitud INTEGER,
                        estadoanterior TEXT, estadonuevo TEXT, codigousuario INTEGER,
                        observaciones TEXT, fechacambio TIMESTAMP);
                    SET search_path TO pg_temp;", cn))
                    cmd.ExecuteNonQuery();

                using (var scope = new TransactionScope())
                {
                    cn.EnlistTransaction(Transaction.Current);
                    var id = new HistorialEstadoDAO().RegistrarCambioYObtenerCodigo(
                        cn, 5, "SOLICITUD_AOCR_HABILITADA", "En Revision", 1, "Prueba RT");
                    Assert.IsTrue(id.HasValue);
                    if (confirmar) scope.Complete();
                }

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM pg_temp.aocr_tbhistorialestado", cn))
                    Assert.AreEqual(confirmar ? 1L : 0L, (long)cmd.ExecuteScalar());
            }
        }

        [DataTestMethod]
        [DataRow("SOLICITUD_AOCR_HABILITADA")]
        [DataRow("PENDIENTE_CARGA_DOCUMENTAL_RT")]
        [DataRow("PENDIENTE_REVISION_DOCUMENTAL")]
        [DataRow("DOCUMENTACION_PENDIENTE")]
        [DataRow(" solicitud_aocr_habilitada ")]
        [DataRow("BORRADOR")]
        [DataRow("Pendiente")]
        [DataRow("SOLICITUD_CREADA")]
        public void FinalizarFormularioInicial_EnviaACoordinacion(string estado)
        {
            Assert.IsTrue(SolicitudAocrService.RequiereEnvioInicialCoordinacion(estado));
        }

        [DataTestMethod]
        [DataRow("En Revision")]
        [DataRow("Observada")]
        [DataRow("Subsanada")]
        [DataRow("EN_INSPECCION")]
        [DataRow("FINALIZADO")]
        [DataRow("ANULADA")]
        public void ExpedienteEnCurso_NoReenviaComoSolicitudInicial(string estado)
        {
            Assert.IsFalse(SolicitudAocrService.RequiereEnvioInicialCoordinacion(estado));
        }
    }
}
