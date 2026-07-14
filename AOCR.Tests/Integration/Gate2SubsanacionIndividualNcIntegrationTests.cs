using System;
using System.Configuration;
using CapaDatos.DAOs;
using CapaModelo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class Gate2SubsanacionIndividualNcIntegrationTests
    {
        private static string ConnectionString
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("AOCR_INTEGRATION_CONNECTION");
                if (!string.IsNullOrWhiteSpace(env)) return env;
                var item = ConfigurationManager.ConnectionStrings["AOCRConnection"];
                return item == null ? null : item.ConnectionString;
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Gate2_EsquemaReal_TieneVinculoVersionadoNc()
        {
            using (var cn = Abrir())
            {
                Assert.AreEqual(8L, Scalar(cn, @"SELECT COUNT(*) FROM information_schema.columns
WHERE table_schema='public' AND table_name='aocr_tbdocumento_subsanacion'
AND column_name IN ('codigo_no_conformidad','codigo_documento_origen','codigo_documento_nueva_version',
'version_anterior','version_nueva','observacion_origen','hash_sha256','correlation_id');"));
                Assert.AreEqual(4L, Scalar(cn, "SELECT COUNT(*) FROM pg_constraint WHERE conname IN ('fk_docsub_nc_gate2','fk_docsub_origen_gate2','fk_docsub_nueva_gate2','chk_docsub_fuente_gate2');"));
                Assert.AreEqual(2L, Scalar(cn, "SELECT COUNT(*) FROM pg_indexes WHERE indexname IN ('ux_docsub_nueva_gate2','ix_docsub_nc_gate2');"));
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Gate2_Dao_NcInvalidaHaceRollbackSinAlterarDocumento()
        {
            int documentoId;
            long cantidadAntes;
            string estadoAntes;
            using (var cn = Abrir())
            {
                using (var cmd = new NpgsqlCommand("SELECT codigo_documento,COALESCE(estado,'') FROM aocr_tbdocumento ORDER BY codigo_documento LIMIT 1;", cn))
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) Assert.Inconclusive("No existen documentos para probar el rollback real.");
                    documentoId = rd.GetInt32(0);
                    estadoAntes = rd.GetString(1);
                }
                cantidadAntes = Scalar(cn, "SELECT COUNT(*) FROM aocr_tbdocumento;");
            }

            var dao = new DocumentoDAO();
            var original = dao.ObtenerPorId(documentoId);
            Assert.IsNotNull(original);
            var nueva = new Documento
            {
                CodigoSolicitud = original.CodigoSolicitud,
                TipoDocumento = original.TipoDocumento,
                NombreArchivo = "gate2-rollback.pdf",
                NombreArchivoOriginal = "gate2-rollback.pdf",
                NombreArchivoFisico = "gate2-rollback.pdf",
                RutaGuardada = "~/App_Data/Uploads/AOCR/gate2-rollback.pdf",
                Extension = ".pdf",
                TamanoBytes = 8,
                Estado = "SUBSANADO_RT",
                FechaCarga = DateTime.Now,
                UsuarioRegistro = "gate2-test"
            };

            Assert.ThrowsException<InvalidOperationException>(() =>
                dao.CrearVersionSubsanadaNc(nueva, documentoId, int.MaxValue, 1, "observacion", new string('a', 64), "gate2-rollback"));

            using (var cn = Abrir())
            {
                Assert.AreEqual(cantidadAntes, Scalar(cn, "SELECT COUNT(*) FROM aocr_tbdocumento;"));
                using (var cmd = new NpgsqlCommand("SELECT COALESCE(estado,'') FROM aocr_tbdocumento WHERE codigo_documento=@id;", cn))
                {
                    cmd.Parameters.AddWithValue("@id", documentoId);
                    Assert.AreEqual(estadoAntes, Convert.ToString(cmd.ExecuteScalar()));
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Gate2_Integridad_RechazaVinculoIndividualIncompleto()
        {
            using (var cn = Abrir())
            using (var tx = cn.BeginTransaction())
            {
                try
                {
                    using (var cmd = new NpgsqlCommand(@"INSERT INTO aocr_tbdocumento_subsanacion
(codigo_subsanacion,nombre_archivo,ruta_archivo,fecha_carga,codigo_usuario_carga)
VALUES(NULL,'invalido.pdf','~/privado/invalido.pdf',NOW(),1);", cn, tx))
                    {
                        try { cmd.ExecuteNonQuery(); Assert.Fail("El CHECK debio rechazar el vinculo incompleto."); }
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

        private static long Scalar(NpgsqlConnection cn, string sql)
        {
            using (var cmd = new NpgsqlCommand(sql, cn)) return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }
}
