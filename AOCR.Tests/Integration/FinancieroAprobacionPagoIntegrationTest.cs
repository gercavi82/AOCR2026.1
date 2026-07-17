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
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var cmdO = new NpgsqlCommand("INSERT INTO aocr_or_orden (id, numero_orden, estado, codigo_usuario) OVERRIDING SYSTEM VALUE SELECT 125, 'ORD-125', 'PROCESADA', (SELECT MIN(idusuario) FROM usuario) WHERE NOT EXISTS (SELECT 1 FROM aocr_or_orden WHERE id = 125); UPDATE aocr_or_orden SET estado = 'PROCESADA', codigo_usuario = (SELECT MIN(idusuario) FROM usuario) WHERE id = 125;", cn)) { cmdO.ExecuteNonQuery(); }
                using (var cmdP = new NpgsqlCommand("INSERT INTO aocr_tbpago (codigo_pago, estado) SELECT 1, 'PENDIENTE' WHERE NOT EXISTS (SELECT 1 FROM aocr_tbpago WHERE codigo_pago = 1);", cn)) { cmdP.ExecuteNonQuery(); }
                using (var cmd = new NpgsqlCommand("INSERT INTO aocr_tb_factura_pago (orden_id, file_path, numero_factura, fecha_emision, subtotal, iva, total, file_name, file_size, content_type, creado_por, creado_en) SELECT 125, 'C:\\proyectos\\AOCR\\AOCR.Tests\\bin\\Debug\\dummy_comprobante.pdf', '001', CURRENT_DATE, 100.0, 15.0, 115.0, 'dummy.pdf', 100, 'application/pdf', 'system', CURRENT_TIMESTAMP WHERE NOT EXISTS (SELECT 1 FROM aocr_tb_factura_pago WHERE orden_id = 125);", cn))
                {
                    cmd.ExecuteNonQuery();
                }
            }

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
