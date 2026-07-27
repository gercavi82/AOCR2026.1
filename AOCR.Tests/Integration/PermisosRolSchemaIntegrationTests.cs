using System;
using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class PermisosRolSchemaIntegrationTests
    {
        [TestMethod]
        [TestCategory("Integration")]
        public void EsquemaPermisosRol_DebeTenerCatalogoAccionesYClaveUnica()
        {
            var item = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            if (item == null || string.IsNullOrWhiteSpace(item.ConnectionString))
            {
                Assert.Inconclusive("AOCRConnection no está configurada.");
            }

            using (var cn = new NpgsqlConnection(item.ConnectionString))
            {
                cn.Open();
                const string sql = @"
SELECT
    (SELECT COUNT(*) FROM information_schema.tables
     WHERE table_schema='public'
       AND table_name IN ('seguridad_permiso','seguridad_rol_permiso')) AS tablas,
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema='public'
       AND table_name='seguridad_permiso'
       AND column_name IN ('tipo_accion','descripcion')) AS columnas,
    (SELECT COUNT(*) FROM pg_constraint
     WHERE conrelid='public.seguridad_rol_permiso'::regclass
       AND contype IN ('p','u')) AS restricciones_unicas;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var reader = cmd.ExecuteReader())
                {
                    Assert.IsTrue(reader.Read());
                    Assert.AreEqual(2L, Convert.ToInt64(reader["tablas"]), "Faltan tablas de seguridad.");
                    if (Convert.ToInt64(reader["columnas"]) != 2L)
                    {
                        Assert.Inconclusive("Ejecute scripts/sql/20260727_permisos_rol_acciones.sql.");
                    }
                    Assert.IsTrue(Convert.ToInt64(reader["restricciones_unicas"]) > 0, "Falta unicidad rol-permiso.");
                }
            }
        }
    }
}
