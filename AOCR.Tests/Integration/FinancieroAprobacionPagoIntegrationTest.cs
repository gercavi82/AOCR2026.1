using CapaDatos.Constants;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class FinancieroAprobacionPagoIntegrationTest
    {
        private const string ConnectionString =
            "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;Command Timeout=120;";

        [TestMethod]
        [TestCategory("Integration")]
        public void AprobarPagoCompleto_Orden125_PersisteEstadoAprobadoSinViolacionConstraint()
        {
            var orchestrator = new FinancieroAprobacionPagoOrchestrator(ConnectionString);
            var resultado = orchestrator.AprobarPagoCompleto(125, 1, "VALIDACION_AGENTE", 1);

            Assert.IsTrue(resultado.Exito, resultado.Error ?? "Aprobación falló sin mensaje.");
            Assert.AreEqual(125, resultado.OrdenId);

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                using (var cmdPago = new NpgsqlCommand(
                    "SELECT UPPER(COALESCE(estado, '')) FROM aocr_tbpago WHERE codigo_pago = 1", cn))
                {
                    var estadoPago = cmdPago.ExecuteScalar()?.ToString();
                    Assert.AreEqual(EstadoPago.Aprobado, estadoPago);
                }

                using (var cmdOrden = new NpgsqlCommand(
                    "SELECT UPPER(COALESCE(estado, '')) FROM aocr_or_orden WHERE id = 125", cn))
                {
                    var estadoOrden = cmdOrden.ExecuteScalar()?.ToString();
                    Assert.AreEqual(EstadoOrden.Completada, estadoOrden);
                }
            }
        }
    }
}
