using System;
using System.Configuration;
using CapaDatos.DAOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class Gate1NoConformidadRelacionesIntegrationTests
    {
        private static string ConnectionString
        {
            get
            {
                var value = Environment.GetEnvironmentVariable("AOCR_INTEGRATION_CONNECTION");
                if (!string.IsNullOrWhiteSpace(value)) return value;
                var item = ConfigurationManager.ConnectionStrings["AOCRConnection"];
                return item != null ? item.ConnectionString : null;
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Gate1_EsquemaReal_TieneRelacionesRestriccionesEIndices()
        {
            using (var cn = Abrir())
            {
                Assert.AreEqual(12L, ScalarLong(cn, @"SELECT COUNT(*) FROM information_schema.columns
WHERE table_schema='public' AND table_name='aocr_tbnoconformidad'
AND column_name IN ('codigo_nc_raiz','codigo_solicitud_origen','codigo_inspeccion_origen','codigo_informe_origen','codigo_solicitud_nueva','codigo_inspeccion_nueva','codigo_informe_cierre','ciclo_evaluacion','fecha_cierre','usuario_cierre','observacion_cierre','correlation_id');"));
                Assert.IsTrue(ScalarLong(cn, "SELECT COUNT(*) FROM pg_constraint WHERE conname IN ('ck_aocr_nc_tipo_ruta','ck_aocr_nc_version_positiva','fk_aocr_nc_raiz','fk_aocr_nc_solicitud_nueva','fk_aocr_nc_inspeccion_nueva','fk_aocr_nc_informe_cierre');") >= 6);
                Assert.IsTrue(ScalarLong(cn, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname='public' AND indexname IN ('ux_aocr_nc_raiz_version','ux_aocr_nc_solicitud_activa_por_raiz','ux_aocr_nc_solicitud_nueva','ux_aocr_nc_correlation');") >= 4);
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Gate1_VinculoNuevaEvaluacion_EsTransaccionalEIdempotente()
        {
            using (var cn = Abrir())
            using (var tx = cn.BeginTransaction())
            {
                try
                {
                    int ncId;
                    int solicitudId;
                    int? inspeccionId;
                    using (var cmd = new NpgsqlCommand(@"SELECT nc.codigo_no_conformidad,s.codigo_solicitud,i.codigo_inspeccion
FROM public.aocr_tbnoconformidad nc
JOIN public.aocr_tbsolicitud s ON TRUE
LEFT JOIN public.aocr_tbinspeccion i ON i.codigo_solicitud=s.codigo_solicitud
WHERE UPPER(nc.tipo_ruta)='CON_INSPECCION'
ORDER BY nc.codigo_no_conformidad LIMIT 1 FOR UPDATE OF nc;", cn, tx))
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) Assert.Inconclusive("No existe una NC CON_INSPECCION para validar el DAO real.");
                        ncId = rd.GetInt32(0);
                        solicitudId = rd.GetInt32(1);
                        inspeccionId = rd.IsDBNull(2) ? (int?)null : rd.GetInt32(2);
                    }

                    var correlation = "GATE1-" + Guid.NewGuid().ToString("N");
                    var dao = new NoConformidadDAO();
                    Assert.IsTrue(dao.VincularNuevaEvaluacion(ncId, solicitudId, inspeccionId, correlation, tx));
                    Assert.IsTrue(dao.VincularNuevaEvaluacion(ncId, solicitudId, inspeccionId, correlation, tx));

                    using (var verify = new NpgsqlCommand("SELECT codigo_solicitud_nueva,correlation_id FROM public.aocr_tbnoconformidad WHERE codigo_no_conformidad=@id;", cn, tx))
                    {
                        verify.Parameters.AddWithValue("@id", ncId);
                        using (var rd = verify.ExecuteReader())
                        {
                            Assert.IsTrue(rd.Read());
                            Assert.AreEqual(solicitudId, rd.GetInt32(0));
                            Assert.AreEqual(correlation, rd.GetString(1));
                        }
                    }
                }
                finally { tx.Rollback(); }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Gate1_RestriccionTipoRuta_RechazaValorInvalidoSinPersistir()
        {
            using (var cn = Abrir())
            using (var tx = cn.BeginTransaction())
            {
                try
                {
                    using (var cmd = new NpgsqlCommand("UPDATE public.aocr_tbnoconformidad SET tipo_ruta='RUTA_INVALIDA' WHERE codigo_no_conformidad=(SELECT MIN(codigo_no_conformidad) FROM public.aocr_tbnoconformidad);", cn, tx))
                    {
                        try { cmd.ExecuteNonQuery(); Assert.Fail("La restriccion debio rechazar la ruta invalida."); }
                        catch (PostgresException ex) { Assert.AreEqual("23514", ex.SqlState); }
                    }
                }
                finally { tx.Rollback(); }
            }
        }

        private static NpgsqlConnection Abrir()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString)) Assert.Inconclusive("AOCRConnection no configurada.");
            var cn = new NpgsqlConnection(ConnectionString);
            cn.Open();
            return cn;
        }

        private static long ScalarLong(NpgsqlConnection cn, string sql)
        {
            using (var cmd = new NpgsqlCommand(sql, cn)) return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }
}
