using System;
using System.Configuration;
using CapaNegocio.Services;
using CapaModelo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class Gate4NuevaSolicitudPorNcIntegrationTests
    {
        private static string Cs => Environment.GetEnvironmentVariable("AOCR_INTEGRATION_CONNECTION") ?? ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        [TestMethod, TestCategory("Integration")]
        public void Gate4_EsquemaReal_TieneRelacionesOrigenEIdempotencia()
        {
            using(var cn=Open())
            {
                Assert.AreEqual(5L,Scalar(cn,@"SELECT COUNT(*) FROM information_schema.columns WHERE table_name='aocr_tbsolicitud'
AND column_name IN ('codigo_solicitud_origen','codigo_inspeccion_origen','codigo_informe_origen','codigo_nc_origen','modulo_origen');"));
                Assert.AreEqual(4L,Scalar(cn,"SELECT COUNT(*) FROM pg_constraint WHERE conname IN ('fk_solicitud_nc_origen_gate4','fk_solicitud_solicitud_origen_gate4','fk_solicitud_inspeccion_origen_gate4','fk_solicitud_informe_origen_gate4');"));
                Assert.AreEqual(2L,Scalar(cn,"SELECT COUNT(*) FROM pg_indexes WHERE indexname IN ('ux_solicitud_activa_nc_gate4','ix_solicitud_origen_gate4');"));
                Assert.AreEqual(2L,Scalar(cn,"SELECT COUNT(*) FROM information_schema.columns WHERE table_name='aocr_tbsolicitud' AND column_name IN ('modulo_destino','tipo_tramite_origen');"));
                Assert.AreEqual(1L,Scalar(cn,"SELECT COUNT(*) FROM pg_constraint WHERE conname='chk_solicitud_modulo_destino_gate4';"));
            }
        }

        [TestMethod, TestCategory("Integration")]
        public void Gate4_Servicio_EntradaSinNcNoCreaSolicitudParalela()
        {
            long antes; using(var cn=Open()) antes=Scalar(cn,"SELECT COUNT(*) FROM aocr_tbsolicitud;");
            var resultado=new NuevaInspeccionPorNcService().Crear(int.MaxValue, int.MaxValue, "gate4-test");
            Assert.IsFalse(resultado.Ok);
            long despues; using(var cn=Open()) despues=Scalar(cn,"SELECT COUNT(*) FROM aocr_tbsolicitud;");
            Assert.AreEqual(antes,despues);
        }

        [TestMethod, TestCategory("Integration")]
        public void Gate4_IndiceUnico_SeAplicaSoloASolicitudesActivas()
        {
            using(var cn=Open()) using(var cmd=new NpgsqlCommand("SELECT indexdef FROM pg_indexes WHERE indexname='ux_solicitud_activa_nc_gate4';",cn))
            {
                var def=Convert.ToString(cmd.ExecuteScalar());
                StringAssert.Contains(def,"UNIQUE"); StringAssert.Contains(def,"codigo_nc_origen"); StringAssert.Contains(def,"deleted_at IS NULL");
            }
        }
        [TestMethod]
        public void Gate4_Destino_DiferenciaEmisionRenovacionYModificacion()
        {
            string modulo, tramite;
            Assert.IsTrue(NuevaInspeccionPorNcService.TryResolverDestino(new SolicitudAOCR{TipoSolicitud=1},out modulo,out tramite));
            Assert.AreEqual("M5_SOLICITUD_INSPECCION_EMISION_RENOVACION",modulo);Assert.AreEqual("EMISION",tramite);
            Assert.IsTrue(NuevaInspeccionPorNcService.TryResolverDestino(new SolicitudAOCR{TipoSolicitud=2},out modulo,out tramite));
            Assert.AreEqual("M5_SOLICITUD_INSPECCION_EMISION_RENOVACION",modulo);Assert.AreEqual("RENOVACION",tramite);
            Assert.IsTrue(NuevaInspeccionPorNcService.TryResolverDestino(new SolicitudAOCR{TipoSolicitud=3,AeropuertosEcuador="SEQM"},out modulo,out tramite));
            Assert.AreEqual("M6_SOLICITUD_INSPECCION_MODIFICACION",modulo);Assert.AreEqual("MODIFICACION_CON_NUEVO_AEROPUERTO",tramite);
        }
        private static NpgsqlConnection Open(){var c=new NpgsqlConnection(Cs);c.Open();return c;}
        private static long Scalar(NpgsqlConnection c,string s){using(var x=new NpgsqlCommand(s,c))return Convert.ToInt64(x.ExecuteScalar());}
    }
}
