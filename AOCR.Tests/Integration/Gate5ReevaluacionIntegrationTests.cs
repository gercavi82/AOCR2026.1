using System;
using System.Configuration;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class Gate5ReevaluacionIntegrationTests
    {
        private static string Cs => Environment.GetEnvironmentVariable("AOCR_INTEGRATION_CONNECTION")
            ?? ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        [TestMethod, TestCategory("Integration")]
        public void Gate5_EsquemaReal_ConservaAntecedentesYCiclos()
        {
            using (var cn = Open())
            {
                Assert.AreEqual(4L, Scalar(cn, @"SELECT COUNT(*) FROM information_schema.columns
WHERE table_name='aocr_tbinforme_inspeccion' AND column_name IN
('codigo_informe_anterior','codigo_no_conformidad_origen','ciclo_evaluacion','es_reevaluacion');"));
                Assert.AreEqual(4L, Scalar(cn, @"SELECT COUNT(*) FROM information_schema.columns
WHERE table_name='aocr_tblv_operacional_eae' AND column_name IN
('codigo_lista_anterior','codigo_no_conformidad_origen','ciclo_evaluacion','es_reevaluacion');"));
                Assert.AreEqual(4L, Scalar(cn, @"SELECT COUNT(*) FROM pg_constraint WHERE conname IN
('fk_informe_ciclo_anterior','fk_informe_ciclo_nc','fk_lv_ciclo_anterior','fk_lv_ciclo_nc');"));
            }
        }

        [TestMethod, TestCategory("Integration")]
        public void Gate5_EntradaInvalida_NoCreaInformeNiLv()
        {
            long informesAntes, listasAntes;
            using (var cn = Open())
            {
                informesAntes = Scalar(cn, "SELECT COUNT(*) FROM public.aocr_tbinforme_inspeccion;");
                listasAntes = Scalar(cn, "SELECT COUNT(*) FROM public.aocr_tblv_operacional_eae;");
            }
            Assert.ThrowsException<InvalidOperationException>(() =>
                new ReevaluacionInspeccionService().Preparar(int.MaxValue, int.MaxValue, int.MaxValue, true));
            using (var cn = Open())
            {
                Assert.AreEqual(informesAntes, Scalar(cn, "SELECT COUNT(*) FROM public.aocr_tbinforme_inspeccion;"));
                Assert.AreEqual(listasAntes, Scalar(cn, "SELECT COUNT(*) FROM public.aocr_tblv_operacional_eae;"));
            }
        }

        private static NpgsqlConnection Open() { var cn = new NpgsqlConnection(Cs); cn.Open(); return cn; }
        private static long Scalar(NpgsqlConnection cn, string sql) { using (var cmd = new NpgsqlCommand(sql, cn)) return Convert.ToInt64(cmd.ExecuteScalar()); }
    }
}
