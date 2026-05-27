using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using CapaModelo;
using CapaDatos.Services;
using Dapper;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class UsuarioCompaniaRTDAO
    {
        private NpgsqlConnection CrearConexion()
        {
            return ConexionDAO.CrearConexion();
        }

        public List<UsuarioCompaniaRT> ObtenerCompaniasAsignadas(int usuarioId, bool soloActivas = true)
        {
            if (usuarioId <= 0)
            {
                return new List<UsuarioCompaniaRT>();
            }

            using (var cn = CrearConexion())
            {
                try
                {
                    if (!TablaUsuarioCompaniaDisponible(cn))
                    {
                        return new List<UsuarioCompaniaRT>();
                    }

                    var hasId = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "id");
                    var hasNombre = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "compania_nombre");
                    var hasUsuoid = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "usuoid");
                    var hasActivo = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "activo");
                    var hasCreatedAt = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "created_at");
                    var hasCreatedBy = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "created_by");
                    var hasUpdatedAt = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "updated_at");
                    var hasUpdatedBy = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "updated_by");

                    var sql = new StringBuilder();
                    sql.AppendLine("SELECT");
                    sql.AppendLine("    " + (hasId ? "id" : "0") + " AS Id,");
                    sql.AppendLine("    usuario_id AS UsuarioId,");
                    sql.AppendLine("    compania_codigo AS CompaniaCodigo,");
                    sql.AppendLine("    " + (hasNombre ? "COALESCE(compania_nombre, '')" : "''") + " AS CompaniaNombre,");
                    sql.AppendLine("    " + (hasUsuoid ? "COALESCE(usuoid, '')" : "''") + " AS Usuoid,");
                    sql.AppendLine("    " + (hasActivo ? "COALESCE(activo, TRUE)" : "TRUE") + " AS Activo,");
                    sql.AppendLine("    " + (hasCreatedAt ? "created_at" : "NOW()") + " AS CreatedAt,");
                    sql.AppendLine("    " + (hasCreatedBy ? "COALESCE(created_by, '')" : "''") + " AS CreatedBy,");
                    sql.AppendLine("    " + (hasUpdatedAt ? "updated_at" : "NULL") + " AS UpdatedAt,");
                    sql.AppendLine("    " + (hasUpdatedBy ? "updated_by" : "NULL") + " AS UpdatedBy");
                    sql.AppendLine("FROM aocr_usuario_compania_rt");
                    sql.AppendLine("WHERE usuario_id = @usuarioId");
                    if (hasActivo)
                    {
                        sql.AppendLine("  AND (@soloActivas = FALSE OR activo = TRUE)");
                    }
                    sql.AppendLine("ORDER BY " + (hasNombre ? "compania_nombre NULLS LAST, " : string.Empty) + "compania_codigo;");

                    return cn.Query<UsuarioCompaniaRT>(sql.ToString(), new { usuarioId, soloActivas }).ToList();
                }
                catch (PostgresException ex) when (EsErrorInfraestructura(ex))
                {
                    return new List<UsuarioCompaniaRT>();
                }
            }
        }

        public bool UsuarioTieneCompaniaAsignada(int usuarioId, string companiaCodigo)
        {
            if (usuarioId <= 0 || string.IsNullOrWhiteSpace(companiaCodigo))
            {
                return false;
            }

            using (var cn = CrearConexion())
            {
                try
                {
                    if (!TablaUsuarioCompaniaDisponible(cn))
                    {
                        return false;
                    }

                    var hasActivo = ExisteColumna(cn, null, "aocr_usuario_compania_rt", "activo");
                    var sql = @"
SELECT COUNT(1)
FROM aocr_usuario_compania_rt
WHERE usuario_id = @usuarioId
  AND UPPER(TRIM(compania_codigo)) = UPPER(TRIM(@companiaCodigo))" +
                        (hasActivo ? "\n  AND activo = TRUE;" : ";");

                    var total = cn.ExecuteScalar<int>(sql, new { usuarioId, companiaCodigo });
                    return total > 0;
                }
                catch (PostgresException ex) when (EsErrorInfraestructura(ex))
                {
                    return false;
                }
            }
        }

        public bool GuardarAsignaciones(
            int usuarioId,
            IEnumerable<UsuarioCompaniaRT> companias,
            string usuarioRegistro,
            bool reemplazar = true)
        {
            if (usuarioId <= 0)
            {
                return false;
            }

            var actor = string.IsNullOrWhiteSpace(usuarioRegistro) ? "sistema" : usuarioRegistro.Trim();
            var listaNormalizada = (companias ?? Enumerable.Empty<UsuarioCompaniaRT>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CompaniaCodigo))
                .Select(c => new UsuarioCompaniaRT
                {
                    UsuarioId = usuarioId,
                    CompaniaCodigo = (c.CompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant(),
                    CompaniaNombre = (c.CompaniaNombre ?? string.Empty).Trim(),
                    Usuoid = NormalizarUsuoid(c.Usuoid),
                    Activo = true
                })
                .GroupBy(c => c.CompaniaCodigo, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        CompletarMetadataCompanias(listaNormalizada);

                        if (!TablaUsuarioCompaniaDisponible(cn, tx))
                        {
                            tx.Rollback();
                            return false;
                        }

                        var hasNombre = ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "compania_nombre");
                        var hasUsuoid = ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "usuoid");
                        var hasActivo = ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "activo");
                        var hasCreatedAt = ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "created_at");
                        var hasCreatedBy = ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "created_by");
                        var hasUpdatedAt = ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "updated_at");
                        var hasUpdatedBy = ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "updated_by");

                        if (reemplazar)
                        {
                            if (hasActivo)
                            {
                                var setDesactivar = new List<string> { "activo = FALSE" };
                                if (hasUpdatedAt)
                                {
                                    setDesactivar.Add("updated_at = NOW()");
                                }
                                if (hasUpdatedBy)
                                {
                                    setDesactivar.Add("updated_by = @actor");
                                }

                                var sqlDesactivar = @"
UPDATE aocr_usuario_compania_rt
SET " + string.Join(", ", setDesactivar) + @"
WHERE usuario_id = @usuarioId;";

                                cn.Execute(sqlDesactivar, new { usuarioId, actor }, tx);
                            }
                            else
                            {
                                cn.Execute("DELETE FROM aocr_usuario_compania_rt WHERE usuario_id = @usuarioId;", new { usuarioId }, tx);
                            }

                            if (listaNormalizada.Count == 0)
                            {
                                tx.Commit();
                                return true;
                            }
                        }
                        else if (listaNormalizada.Count == 0)
                        {
                            tx.Rollback();
                            return false;
                        }

                        var setActualizar = new List<string>();
                        if (hasNombre)
                        {
                            setActualizar.Add("compania_nombre = @CompaniaNombre");
                        }
                        if (hasUsuoid)
                        {
                            setActualizar.Add("usuoid = @Usuoid");
                        }
                        if (hasActivo)
                        {
                            setActualizar.Add("activo = TRUE");
                        }
                        if (hasUpdatedAt)
                        {
                            setActualizar.Add("updated_at = NOW()");
                        }
                        if (hasUpdatedBy)
                        {
                            setActualizar.Add("updated_by = @Actor");
                        }
                        if (setActualizar.Count == 0)
                        {
                            setActualizar.Add("compania_codigo = compania_codigo");
                        }

                        var sqlActualizarExistente = @"
UPDATE aocr_usuario_compania_rt
SET " + string.Join(", ", setActualizar) + @"
WHERE usuario_id = @UsuarioId
  AND UPPER(TRIM(compania_codigo)) = UPPER(TRIM(@CompaniaCodigo));";

                        var columnasInsert = new List<string> { "usuario_id", "compania_codigo" };
                        var valoresInsert = new List<string> { "@UsuarioId", "@CompaniaCodigo" };
                        if (hasNombre)
                        {
                            columnasInsert.Add("compania_nombre");
                            valoresInsert.Add("@CompaniaNombre");
                        }
                        if (hasUsuoid)
                        {
                            columnasInsert.Add("usuoid");
                            valoresInsert.Add("@Usuoid");
                        }
                        if (hasActivo)
                        {
                            columnasInsert.Add("activo");
                            valoresInsert.Add("TRUE");
                        }
                        if (hasCreatedAt)
                        {
                            columnasInsert.Add("created_at");
                            valoresInsert.Add("NOW()");
                        }
                        if (hasCreatedBy)
                        {
                            columnasInsert.Add("created_by");
                            valoresInsert.Add("@Actor");
                        }
                        if (hasUpdatedAt)
                        {
                            columnasInsert.Add("updated_at");
                            valoresInsert.Add("NOW()");
                        }
                        if (hasUpdatedBy)
                        {
                            columnasInsert.Add("updated_by");
                            valoresInsert.Add("@Actor");
                        }

                        var sqlInsertarSiNoExiste = @"
INSERT INTO aocr_usuario_compania_rt
    (" + string.Join(", ", columnasInsert) + @")
SELECT
    " + string.Join(", ", valoresInsert) + @"
WHERE NOT EXISTS
(
    SELECT 1
    FROM aocr_usuario_compania_rt
    WHERE usuario_id = @UsuarioId
      AND UPPER(TRIM(compania_codigo)) = UPPER(TRIM(@CompaniaCodigo))
);";

                        foreach (var compania in listaNormalizada)
                        {
                            var parametros = new
                            {
                                compania.UsuarioId,
                                compania.CompaniaCodigo,
                                compania.CompaniaNombre,
                                compania.Usuoid,
                                Actor = actor
                            };

                            var rows = cn.Execute(sqlActualizarExistente, parametros, tx);
                            if (rows <= 0)
                            {
                                cn.Execute(sqlInsertarSiNoExiste, parametros, tx);
                            }
                        }

                        tx.Commit();
                        return true;
                    }
                    catch (PostgresException ex) when (EsErrorInfraestructura(ex))
                    {
                        tx.Rollback();
                        return false;
                    }
                    catch
                    {
                        tx.Rollback();
                        return false;
                    }
                }
            }
        }

        public bool AgregarCompania(int usuarioId, string companiaCodigo, string companiaNombre, string usuarioRegistro)
        {
            if (usuarioId <= 0 || string.IsNullOrWhiteSpace(companiaCodigo))
            {
                return false;
            }

            var compania = new UsuarioCompaniaRT
            {
                UsuarioId = usuarioId,
                CompaniaCodigo = companiaCodigo,
                CompaniaNombre = companiaNombre
            };

            return GuardarAsignaciones(usuarioId, new[] { compania }, usuarioRegistro, false);
        }

        private static bool EsErrorInfraestructura(PostgresException ex)
        {
            if (ex == null)
            {
                return false;
            }

            // 42P01: undefined_table, 42703: undefined_column, 42P10: invalid_column_reference.
            // 42501: insufficient_privilege, 23505: unique_violation.
            return ex.SqlState == "42P01"
                || ex.SqlState == "42703"
                || ex.SqlState == "42P10"
                || ex.SqlState == "42501"
                || ex.SqlState == "23505";
        }

        private bool TablaUsuarioCompaniaDisponible(NpgsqlConnection cn, IDbTransaction tx = null)
        {
            AsegurarEstructuraBasica(cn, tx);

            if (!ExisteTabla(cn, tx, "aocr_usuario_compania_rt"))
            {
                return false;
            }

            return ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "usuario_id")
                && ExisteColumna(cn, tx, "aocr_usuario_compania_rt", "compania_codigo");
        }

        private static bool ExisteTabla(NpgsqlConnection cn, IDbTransaction tx, string nombreTabla)
        {
            const string sql = @"
SELECT COUNT(1)
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = @nombreTabla;";

            return cn.ExecuteScalar<int>(sql, new { nombreTabla }, tx) > 0;
        }

        private static bool ExisteColumna(NpgsqlConnection cn, IDbTransaction tx, string nombreTabla, string nombreColumna)
        {
            const string sql = @"
SELECT COUNT(1)
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = @nombreTabla
  AND column_name = @nombreColumna;";

            return cn.ExecuteScalar<int>(sql, new { nombreTabla, nombreColumna }, tx) > 0;
        }

        private static void AsegurarEstructuraBasica(NpgsqlConnection cn, IDbTransaction tx)
        {
            try
            {
                const string ddl = @"
CREATE TABLE IF NOT EXISTS aocr_usuario_compania_rt
(
    id              SERIAL PRIMARY KEY,
    usuario_id      INT NOT NULL,
    compania_codigo VARCHAR(20) NOT NULL
);

ALTER TABLE aocr_usuario_compania_rt ADD COLUMN IF NOT EXISTS compania_nombre VARCHAR(250);
ALTER TABLE aocr_usuario_compania_rt ADD COLUMN IF NOT EXISTS usuoid VARCHAR(30);
ALTER TABLE aocr_usuario_compania_rt ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE aocr_usuario_compania_rt ADD COLUMN IF NOT EXISTS created_at TIMESTAMP NOT NULL DEFAULT NOW();
ALTER TABLE aocr_usuario_compania_rt ADD COLUMN IF NOT EXISTS created_by VARCHAR(120) NOT NULL DEFAULT 'sistema';
ALTER TABLE aocr_usuario_compania_rt ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP NULL;
ALTER TABLE aocr_usuario_compania_rt ADD COLUMN IF NOT EXISTS updated_by VARCHAR(120) NULL;

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_compania_rt_usuario_id ON aocr_usuario_compania_rt (usuario_id);
CREATE INDEX IF NOT EXISTS idx_aocr_usuario_compania_rt_usuario_codigo ON aocr_usuario_compania_rt (usuario_id, compania_codigo);
CREATE INDEX IF NOT EXISTS idx_aocr_usuario_compania_rt_usuoid ON aocr_usuario_compania_rt (usuoid);";

                cn.Execute(ddl, transaction: tx);
            }
            catch (PostgresException ex) when (EsErrorInfraestructura(ex))
            {
                // Best effort. If DDL is not permitted, main operations continue with fallbacks.
            }
        }

        private static string NormalizarUsuoid(string usuoid)
        {
            if (string.IsNullOrWhiteSpace(usuoid))
            {
                return string.Empty;
            }

            var valor = usuoid.Trim().ToUpperInvariant();
            if (valor.Length > 30)
            {
                valor = valor.Substring(0, 30);
            }

            return valor;
        }

        private static void CompletarMetadataCompanias(IList<UsuarioCompaniaRT> companias)
        {
            if (companias == null || companias.Count == 0)
            {
                return;
            }

            EmpresaAS400DAO empresaDao = null;
            var cache = new Dictionary<string, Empresa>(StringComparer.OrdinalIgnoreCase);

            foreach (var compania in companias)
            {
                if (compania == null || string.IsNullOrWhiteSpace(compania.CompaniaCodigo))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(compania.CompaniaNombre) && !string.IsNullOrWhiteSpace(compania.Usuoid))
                {
                    continue;
                }

                var codigo = (compania.CompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant();
                Empresa empresa;
                if (!cache.TryGetValue(codigo, out empresa))
                {
                    try
                    {
                        if (empresaDao == null)
                        {
                            empresaDao = new EmpresaAS400DAO(new SecureConfigurationService());
                        }

                        empresa = empresaDao.ObtenerEmpresaPorCodigo(codigo);
                    }
                    catch
                    {
                        empresa = null;
                    }

                    cache[codigo] = empresa;
                }

                if (empresa == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(compania.CompaniaNombre) && !string.IsNullOrWhiteSpace(empresa.Nombre))
                {
                    compania.CompaniaNombre = empresa.Nombre.Trim();
                }

                if (string.IsNullOrWhiteSpace(compania.Usuoid) && !string.IsNullOrWhiteSpace(empresa.CodigoNumeroCia))
                {
                    compania.Usuoid = NormalizarUsuoid(empresa.CodigoNumeroCia);
                }
            }
        }
    }
}
