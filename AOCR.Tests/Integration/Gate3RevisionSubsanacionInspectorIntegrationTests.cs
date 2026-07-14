using System;
using System.Configuration;
using CapaDatos.DAOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class Gate3RevisionSubsanacionInspectorIntegrationTests
    {
        private static string Cs => Environment.GetEnvironmentVariable("AOCR_INTEGRATION_CONNECTION") ??
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        [TestMethod, TestCategory("Integration")]
        public void Gate3_EsquemaReal_TieneDecisionAuditoriaYRestricciones()
        {
            using (var cn = Open())
            {
                Assert.AreEqual(4L, Scalar(cn, @"SELECT COUNT(*) FROM information_schema.columns WHERE table_name='aocr_tbdocumento_subsanacion'
AND column_name IN ('decision_inspector','comentario_inspector','codigo_usuario_revision','fecha_revision');"));
                Assert.AreEqual(3L, Scalar(cn, "SELECT COUNT(*) FROM pg_constraint WHERE conname IN ('chk_docsub_decision_gate3','chk_docsub_rechazo_comentario_gate3','fk_docsub_revisor_gate3');"));
            }
        }

        [TestMethod, TestCategory("Integration")]
        public void Gate3_Dao_RechazoSinComentarioEsBloqueadoAntesDePersistir()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                new NoConformidadDAO().RegistrarDecisionDocumentoSubsanado(1, 1, false, "  ", 1));
        }

        [TestMethod, TestCategory("Integration")]
        public void Gate3_CheckReal_ExigeComentarioParaRechazo()
        {
            using (var cn = Open())
            {
                using (var cmd = new NpgsqlCommand("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='chk_docsub_rechazo_comentario_gate3';", cn))
                {
                    var definition = Convert.ToString(cmd.ExecuteScalar());
                    StringAssert.Contains(definition, "RECHAZADO_SUBSANACION");
                    StringAssert.Contains(definition, "comentario_inspector");
                }
            }
        }

        private static NpgsqlConnection Open() { var cn=new NpgsqlConnection(Cs);cn.Open();return cn; }
        private static long Scalar(NpgsqlConnection cn,string sql){using(var cmd=new NpgsqlCommand(sql,cn))return Convert.ToInt64(cmd.ExecuteScalar());}
    }
}
