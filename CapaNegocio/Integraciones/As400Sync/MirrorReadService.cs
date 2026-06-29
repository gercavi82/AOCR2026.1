using System;
using System.Collections.Generic;
using Npgsql;
using CapaNegocio.Services;

using System.Linq;

namespace CapaNegocio.Integraciones.As400Sync
{
    public class MirrorUsuarioDto
    {
        public string CodigoUsuario { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Correo { get; set; }
        public string EstadoActividad { get; set; }
        public string CodigoRol { get; set; }
        public string CodigoCiudad { get; set; }
        public string Cargo { get; set; }
        public string NombreCorto { get; set; }
        public DateTime? SourceUpdatedAt { get; set; }
        public DateTime MirrorSyncedAt { get; set; }
    }

    public class MirrorCompaniaDto
    {
        public string CodigoOaci { get; set; }
        public string CodigoIata { get; set; }
        public string CodigoNumeroCia { get; set; }
        public string NombreCompania { get; set; }
    }

    public class MirrorIdentificacionDto
    {
        public string CodigoUsuario { get; set; }
        public string Ruc { get; set; }
        public string Cedula { get; set; }
        public DateTime? SourceUpdatedAt { get; set; }
        public DateTime MirrorSyncedAt { get; set; }
    }

    public class MirrorFr3CabeceraDto
    {
        public decimal Secuencial { get; set; }
        public string Aeropuerto { get; set; }
        public string Anio { get; set; }
        public string FechaControlVuelo { get; set; }
        public string TipoOperacion { get; set; }
        public string RutaPlanVuelo { get; set; }
        public int NumAterrizaPais { get; set; }
        public decimal Total { get; set; }
        public decimal GranTotal { get; set; }
        public string Autorizacion { get; set; }
        public string Observacion { get; set; }
        public string Ruc { get; set; }
        public string NombreCliente { get; set; }
        public string Estado { get; set; }
        public string NacInter { get; set; }
        public string NombreCia { get; set; }
        public string Matricula { get; set; }
        public decimal ValorCharter { get; set; }
        public string FormaPago { get; set; }
        public string CodigoBanco { get; set; }
        public string Deposito { get; set; }
        public string NumeroFactura { get; set; }
        public string FechaCreacion { get; set; }
        public string HoraCreacionRaw { get; set; }
        public string HoraCreacion { get; set; }
        public string Procesado { get; set; }
        public DateTime MirrorSyncedAt { get; set; }
    }

    public class MirrorSyncStatusDto
    {
        public string Tabla { get; set; }
        public string Estado { get; set; }
        public DateTime? UltimaSync { get; set; }
        public string UltimaClaveSync { get; set; }
        public string UltimoError { get; set; }
        public DateTime ActualizadoEn { get; set; }
    }

    public class MirrorReadService
    {
        private readonly string _connectionString;
        private static readonly object MissingMirrorObjectsLock = new object();
        private static readonly Dictionary<string, DateTime> MissingMirrorObjectsUntilUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan MissingMirrorObjectCooldown = TimeSpan.FromMinutes(10);

        public MirrorReadService()
        {
            var env = As400MirrorSyncOptionsFactory.Create();
            _connectionString = env.PostgresMirrorConnectionString;
        }

        public MirrorUsuarioDto ObtenerUsuarioPorCodigo(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return null;
            }

            if (ShouldSkipMirrorObject("mirror_raw.usuarc") || ShouldSkipMirrorObject("mirror_raw.usuar1"))
            {
                return null;
            }

            const string sql = @"
                  SELECT u.usucod, u.usunom, u.usuape, u.usucor, u.usuest, u.usuco4, u.usuco5,
                      a.usucar, a.usuno1, NULL::timestamp AS source_updated_at, u._mirror_synced_at
                  FROM mirror_raw.usuarc u
             LEFT JOIN mirror_raw.usuar1 a ON a.usuco8 = u.usucod
                 WHERE u.usucod = @codigo
                   AND COALESCE(u._is_deleted, false) = false
                 LIMIT 1";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("codigo", codigoUsuario.Trim());
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return null;

                        return new MirrorUsuarioDto
                        {
                            CodigoUsuario = rd.IsDBNull(0) ? null : rd.GetString(0),
                            Nombres = rd.IsDBNull(1) ? null : rd.GetString(1),
                            Apellidos = rd.IsDBNull(2) ? null : rd.GetString(2),
                            Correo = rd.IsDBNull(3) ? null : rd.GetString(3),
                            EstadoActividad = rd.IsDBNull(4) ? null : rd.GetString(4),
                            CodigoRol = rd.IsDBNull(5) ? null : rd.GetString(5),
                            CodigoCiudad = rd.IsDBNull(6) ? null : rd.GetString(6),
                            Cargo = rd.IsDBNull(7) ? null : rd.GetString(7),
                            NombreCorto = rd.IsDBNull(8) ? null : rd.GetString(8),
                            SourceUpdatedAt = rd.IsDBNull(9) ? (DateTime?)null : rd.GetDateTime(9),
                            MirrorSyncedAt = rd.IsDBNull(10) ? DateTime.MinValue : rd.GetDateTime(10)
                        };
                    }
                }
            }
            catch (PostgresException ex)
            {
                RegisterMissingMirrorObjectIfApplicable("mirror_raw.usuarc", ex);
                RegisterMissingMirrorObjectIfApplicable("mirror_raw.usuar1", ex);
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerUsuarioPorCodigo no disponible: " + ex.MessageText, "MirrorReadService");
                return null;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error consultando usuario en espejo", ex.ToString(), "MirrorReadService");
                return null;
            }
        }

        public IList<MirrorCompaniaDto> ListarCompaniasActivas(int take)
        {
            var list = new List<MirrorCompaniaDto>();
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return list;
            }

            if (ShouldSkipMirrorObject("mirror_raw.ciaarc"))
            {
                return list;
            }

            if (take <= 0) take = 100;

            const string sql = @"
                SELECT ciacod, ciaco2, ciaco3, cianom
                  FROM mirror_raw.ciaarc
                 WHERE COALESCE(_is_deleted, false) = false
                   AND TRIM(COALESCE(ciaest, '')) = 'AC'
              ORDER BY cianom
                 LIMIT @take";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("take", take);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new MirrorCompaniaDto
                            {
                                CodigoOaci = rd.IsDBNull(0) ? null : rd.GetString(0),
                                CodigoIata = rd.IsDBNull(1) ? null : rd.GetString(1),
                                CodigoNumeroCia = rd.IsDBNull(2) ? null : rd.GetString(2),
                                NombreCompania = rd.IsDBNull(3) ? null : rd.GetString(3)
                            });
                        }
                    }
                }
            }
            catch (PostgresException ex)
            {
                RegisterMissingMirrorObjectIfApplicable("mirror_raw.ciaarc", ex);
                LogBL.RegistrarAdvertencia("MirrorReadService.ListarCompaniasActivas no disponible: " + ex.MessageText, "MirrorReadService");
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ListarCompaniasActivas no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }

        public MirrorCompaniaDto ObtenerCompaniaPorCodigo(string codigoOaci)
        {
            if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(codigoOaci))
            {
                return null;
            }

            if (ShouldSkipMirrorObject("mirror_raw.ciaarc"))
            {
                return null;
            }

            const string sql = @"
                SELECT ciacod, ciaco2, ciaco3, cianom
                  FROM mirror_raw.ciaarc
                 WHERE COALESCE(_is_deleted, false) = false
                   AND UPPER(TRIM(COALESCE(ciacod, ''))) = @codigo
                 LIMIT 1";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("codigo", codigoOaci.Trim().ToUpperInvariant());
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            return null;
                        }

                        return new MirrorCompaniaDto
                        {
                            CodigoOaci = rd.IsDBNull(0) ? null : rd.GetString(0),
                            CodigoIata = rd.IsDBNull(1) ? null : rd.GetString(1),
                            CodigoNumeroCia = rd.IsDBNull(2) ? null : rd.GetString(2),
                            NombreCompania = rd.IsDBNull(3) ? null : rd.GetString(3)
                        };
                    }
                }
            }
            catch (PostgresException ex)
            {
                RegisterMissingMirrorObjectIfApplicable("mirror_raw.ciaarc", ex);
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerCompaniaPorCodigo no disponible: " + ex.MessageText, "MirrorReadService");
                return null;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerCompaniaPorCodigo no disponible: " + ex.Message, "MirrorReadService");
                return null;
            }
        }

        public string ObtenerCodigoCiudadPorClavesUsuario(IEnumerable<string> clavesUsuario)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return null;
            }

            if (ShouldSkipMirrorObject("mirror_raw.usuarc") || ShouldSkipMirrorObject("mirror_raw.usuar1"))
            {
                return null;
            }

            var claves = NormalizarClaves(clavesUsuario);
            if (claves.Count == 0)
            {
                return null;
            }

            const string sql = @"
                SELECT
                    COALESCE(NULLIF(BTRIM(a.usuco9), ''), NULLIF(BTRIM(u.usuco5), '')) AS codigo_ciudad
                FROM mirror_raw.usuarc u
                LEFT JOIN mirror_raw.usuar1 a
                  ON UPPER(BTRIM(COALESCE(a.usuco8, ''))) = UPPER(BTRIM(COALESCE(u.usucod, '')))
                 AND COALESCE(a._is_deleted, false) = false
               WHERE COALESCE(u._is_deleted, false) = false
                 AND (
                        UPPER(BTRIM(COALESCE(u.usucod, ''))) = ANY(@claves)
                     OR UPPER(BTRIM(COALESCE(a.usuco8, ''))) = ANY(@claves)
                 )
                 AND COALESCE(NULLIF(BTRIM(a.usuco9), ''), NULLIF(BTRIM(u.usuco5), '')) IS NOT NULL
               ORDER BY
                    CASE WHEN NULLIF(BTRIM(a.usuco9), '') IS NOT NULL THEN 0 ELSE 1 END,
                    u._mirror_synced_at DESC
               LIMIT 1";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("claves", claves.ToArray());
                    var value = cmd.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                    {
                        return null;
                    }

                    var ciudad = Convert.ToString(value);
                    return string.IsNullOrWhiteSpace(ciudad) ? null : ciudad.Trim().ToUpperInvariant();
                }
            }
            catch (PostgresException ex)
            {
                RegisterMissingMirrorObjectIfApplicable("mirror_raw.usuarc", ex);
                RegisterMissingMirrorObjectIfApplicable("mirror_raw.usuar1", ex);
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerCodigoCiudadPorClavesUsuario no disponible: " + ex.MessageText, "MirrorReadService");
                return null;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerCodigoCiudadPorClavesUsuario no disponible: " + ex.Message, "MirrorReadService");
                return null;
            }
        }

        public MirrorIdentificacionDto ObtenerIdentificacionPorClavesUsuario(IEnumerable<string> clavesUsuario)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return null;
            }

            if (ShouldSkipMirrorObject("mirror_raw.usuarc"))
            {
                return null;
            }

            var claves = NormalizarClaves(clavesUsuario);
            if (claves.Count == 0)
            {
                return null;
            }

            const string sql = @"
                SELECT
                    NULLIF(BTRIM(u.usucod), '') AS codigo_usuario,
                    NULLIF(BTRIM(u.usunum), '') AS ruc,
                    NULLIF(BTRIM(u.usuced), '') AS cedula,
                    NULL::timestamp AS source_updated_at,
                    u._mirror_synced_at
                FROM mirror_raw.usuarc u
               WHERE COALESCE(u._is_deleted, false) = false
                 AND UPPER(BTRIM(COALESCE(u.usucod, ''))) = ANY(@claves)
                 AND (
                        NULLIF(BTRIM(u.usunum), '') IS NOT NULL
                     OR NULLIF(BTRIM(u.usuced), '') IS NOT NULL
                 )
               ORDER BY u._mirror_synced_at DESC
               LIMIT 1";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("claves", claves.ToArray());
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            return null;
                        }

                        return new MirrorIdentificacionDto
                        {
                            CodigoUsuario = rd.IsDBNull(0) ? null : rd.GetString(0),
                            Ruc = rd.IsDBNull(1) ? null : rd.GetString(1),
                            Cedula = rd.IsDBNull(2) ? null : rd.GetString(2),
                            SourceUpdatedAt = rd.IsDBNull(3) ? (DateTime?)null : rd.GetDateTime(3),
                            MirrorSyncedAt = rd.IsDBNull(4) ? DateTime.MinValue : rd.GetDateTime(4)
                        };
                    }
                }
            }
            catch (PostgresException ex)
            {
                RegisterMissingMirrorObjectIfApplicable("mirror_raw.usuarc", ex);
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerIdentificacionPorClavesUsuario no disponible: " + ex.MessageText, "MirrorReadService");
                return null;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerIdentificacionPorClavesUsuario no disponible: " + ex.Message, "MirrorReadService");
                return null;
            }
        }

        public string ObtenerEstacionPorCodigoCiudad(string codigoCiudad)
        {
            if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(codigoCiudad))
            {
                return null;
            }

            var codigoNormalizado = codigoCiudad.Trim().ToUpperInvariant();
            var estacion = ObtenerEstacionDesdeTabla("mirror_raw.opuarc01", "opucod", "opuest", codigoNormalizado);
            if (!string.IsNullOrWhiteSpace(estacion))
            {
                return estacion;
            }

            return ObtenerEstacionDesdeTabla("mirror_raw.oidar2", "oidco3", "oidno2", codigoNormalizado);
        }

        private string ObtenerEstacionDesdeTabla(string tabla, string columnaCodigo, string columnaEstacion, string codigoCiudad)
        {
            if (string.IsNullOrWhiteSpace(tabla) || string.IsNullOrWhiteSpace(columnaCodigo) || string.IsNullOrWhiteSpace(columnaEstacion))
            {
                return null;
            }

            if (ShouldSkipMirrorObject(tabla))
            {
                return null;
            }

            var sql = string.Format(@"
                SELECT NULLIF(BTRIM({0}), '')
                  FROM {1}
                 WHERE COALESCE(_is_deleted, false) = false
                   AND UPPER(BTRIM(COALESCE({2}, ''))) = @codigo
                 LIMIT 1", columnaEstacion, tabla, columnaCodigo);

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("codigo", codigoCiudad);
                    var value = cmd.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                    {
                        return null;
                    }

                    var estacion = Convert.ToString(value);
                    return string.IsNullOrWhiteSpace(estacion) ? null : estacion.Trim();
                }
            }
            catch (PostgresException ex)
            {
                RegisterMissingMirrorObjectIfApplicable(tabla, ex);
                LogBL.RegistrarAdvertencia(
                    string.Format(
                        "MirrorReadService.ObtenerEstacionDesdeTabla no disponible: tabla={0}, codCiudad={1}, error={2}",
                        tabla,
                        codigoCiudad ?? "(null)",
                        ex.MessageText),
                    "MirrorReadService");
                return null;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia(
                    string.Format(
                        "MirrorReadService.ObtenerEstacionDesdeTabla no disponible: tabla={0}, codCiudad={1}, error={2}",
                        tabla,
                        codigoCiudad ?? "(null)",
                        ex.Message),
                    "MirrorReadService");
                return null;
            }
        }

        public string ObtenerRucCompaniaPorCodigo(string codigoOaci, string nombreCompania = null)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return string.Empty;
            }

            // 1) Buscar en mirror_raw.ciaarc (CIARUC): catálogo de compañías AS400 con RUC directo.
            //    Prioridad: código OACI exacto (CIACOD), luego nombre (CIANOM o CIANO1).
            if (!ShouldSkipMirrorObject("mirror_raw.ciaarc"))
            {
                const string sqlCiaarc = @"
                    SELECT NULLIF(TRIM(COALESCE(ciaruc, '')), '') AS ruc
                      FROM mirror_raw.ciaarc
                     WHERE COALESCE(_is_deleted, false) = false
                       AND NULLIF(TRIM(COALESCE(ciaruc, '')), '') IS NOT NULL
                       AND (
                            (@codigo <> '' AND UPPER(TRIM(COALESCE(ciacod, ''))) = @codigo)
                            OR
                            (@nombre <> '' AND (
                                UPPER(TRIM(COALESCE(cianom, ''))) = @nombre
                                OR UPPER(TRIM(COALESCE(ciano1, ''))) = @nombre
                            ))
                       )
                  ORDER BY _mirror_synced_at DESC
                     LIMIT 1";

                try
                {
                    using (var conn = new NpgsqlConnection(_connectionString))
                    using (var cmd = new NpgsqlCommand(sqlCiaarc, conn))
                    {
                        conn.Open();
                        cmd.Parameters.AddWithValue("codigo", (codigoOaci ?? string.Empty).Trim().ToUpperInvariant());
                        cmd.Parameters.AddWithValue("nombre", (nombreCompania ?? string.Empty).Trim().ToUpperInvariant());
                        var rucCiaarc = (cmd.ExecuteScalar() as string ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(rucCiaarc))
                        {
                            return rucCiaarc;
                        }
                    }
                }
                catch (PostgresException ex)
                {
                    RegisterMissingMirrorObjectIfApplicable("mirror_raw.ciaarc", ex);
                    LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerRucCompaniaPorCodigo (ciaarc): " + ex.MessageText, "MirrorReadService");
                }
                catch (Exception ex)
                {
                    LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerRucCompaniaPorCodigo (ciaarc): " + ex.Message, "MirrorReadService");
                }
            }

            // 2) Buscar en mirror_raw.opcarc (OPCRUC) — catálogo de operadores AS400, 5000+ entradas.
            //    Clave: opccod (código OACI) o opcnom (nombre).
            if (!ShouldSkipMirrorObject("mirror_raw.opcarc"))
            {
                const string sqlOpcarc = @"
                    SELECT NULLIF(TRIM(COALESCE(opcruc, '')), '') AS ruc
                      FROM mirror_raw.opcarc
                     WHERE NULLIF(TRIM(COALESCE(opcruc, '')), '') IS NOT NULL
                       AND (
                            (@codigo <> '' AND UPPER(TRIM(COALESCE(opccod, ''))) = @codigo)
                            OR
                            (@codigo <> '' AND UPPER(TRIM(COALESCE(opcco1, ''))) = @codigo)
                            OR
                            (@nombre <> '' AND UPPER(TRIM(COALESCE(opcnom, ''))) = @nombre)
                       )
                  ORDER BY opccod
                     LIMIT 1";

                try
                {
                    using (var conn = new NpgsqlConnection(_connectionString))
                    using (var cmd = new NpgsqlCommand(sqlOpcarc, conn))
                    {
                        conn.Open();
                        cmd.Parameters.AddWithValue("codigo", (codigoOaci ?? string.Empty).Trim().ToUpperInvariant());
                        cmd.Parameters.AddWithValue("nombre", (nombreCompania ?? string.Empty).Trim().ToUpperInvariant());
                        var rucOpcarc = (cmd.ExecuteScalar() as string ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(rucOpcarc))
                        {
                            return rucOpcarc;
                        }
                    }
                }
                catch (PostgresException ex)
                {
                    RegisterMissingMirrorObjectIfApplicable("mirror_raw.opcarc", ex);
                    LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerRucCompaniaPorCodigo (opcarc): " + ex.MessageText, "MirrorReadService");
                }
                catch (Exception ex)
                {
                    LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerRucCompaniaPorCodigo (opcarc): " + ex.Message, "MirrorReadService");
                }
            }

            // 3) Fallback: buscar en mirror_raw.opcar5 (OPCRU1) — FR3 del cliente.
            if (ShouldSkipMirrorObject("mirror_raw.opcar5"))
            {
                return string.Empty;
            }

            const string sql = @"
                SELECT NULLIF(TRIM(COALESCE(opcru1, '')), '') AS ruc
                  FROM mirror_raw.opcar5
                 WHERE COALESCE(_is_deleted, false) = false
                   AND NULLIF(TRIM(COALESCE(opcru1, '')), '') IS NOT NULL
                   AND (
                        (@codigo <> '' AND UPPER(TRIM(COALESCE(opcc08, ''))) = @codigo)
                        OR
                        (@nombre <> '' AND UPPER(TRIM(COALESCE(opcno5, ''))) = @nombre)
                   )
              ORDER BY _mirror_synced_at DESC
                 LIMIT 1";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("codigo", (codigoOaci ?? string.Empty).Trim().ToUpperInvariant());
                    cmd.Parameters.AddWithValue("nombre", (nombreCompania ?? string.Empty).Trim().ToUpperInvariant());
                    return (cmd.ExecuteScalar() as string ?? string.Empty).Trim();
                }
            }
            catch (PostgresException ex)
            {
                RegisterMissingMirrorObjectIfApplicable("mirror_raw.opcar5", ex);
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerRucCompaniaPorCodigo no disponible: " + ex.MessageText, "MirrorReadService");
                return string.Empty;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerRucCompaniaPorCodigo no disponible: " + ex.Message, "MirrorReadService");
                return string.Empty;
            }
        }

        private static bool ShouldSkipMirrorObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            lock (MissingMirrorObjectsLock)
            {
                DateTime untilUtc;
                if (!MissingMirrorObjectsUntilUtc.TryGetValue(objectName, out untilUtc))
                {
                    return false;
                }

                if (DateTime.UtcNow <= untilUtc)
                {
                    return true;
                }

                MissingMirrorObjectsUntilUtc.Remove(objectName);
                return false;
            }
        }

        private static void RegisterMissingMirrorObjectIfApplicable(string objectName, PostgresException ex)
        {
            if (string.IsNullOrWhiteSpace(objectName) || ex == null)
            {
                return;
            }

            var sqlState = ex.SqlState ?? string.Empty;
            var isMissingObject =
                string.Equals(sqlState, "42P01", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sqlState, "42703", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sqlState, "3F000", StringComparison.OrdinalIgnoreCase);

            if (!isMissingObject)
            {
                return;
            }

            lock (MissingMirrorObjectsLock)
            {
                MissingMirrorObjectsUntilUtc[objectName] = DateTime.UtcNow.Add(MissingMirrorObjectCooldown);
            }
        }

        private static List<string> NormalizarClaves(IEnumerable<string> clavesUsuario)
        {
            if (clavesUsuario == null)
            {
                return new List<string>();
            }

            return clavesUsuario
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToUpperInvariant())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Lista registros FR3 (cabecera) desde el espejo, ordenados por fecha de creacion descendente.
        /// Devuelve lista vacía si el mirror no está disponible (fallback seguro).
        /// </summary>
        public IList<MirrorFr3CabeceraDto> ListarFr3Recientes(int take = 100, string aeropuerto = null, string anio = null)
        {
            var list = new List<MirrorFr3CabeceraDto>();
            if (string.IsNullOrWhiteSpace(_connectionString)) return list;
            if (take <= 0) take = 100;

            var whereParts = new List<string> { "COALESCE(is_deleted, false) = false" };
            if (!string.IsNullOrWhiteSpace(aeropuerto))
                whereParts.Add("UPPER(TRIM(aeropuerto_codigo)) = UPPER(@aer)");
            if (!string.IsNullOrWhiteSpace(anio))
                whereParts.Add("TRIM(anio) = @anio");

            var sql = @"
                SELECT secuencial_fr3, aeropuerto_codigo, anio, fecha_control_vuelo_raw, tipo_operacion_codigo, ruta_plan_vuelo, numero_aterrizajes_pais,
                       total, gran_total, autorizacion, observacion, ruc_cedula, contribuyente_nombre, estado_raw,
                       nacional_internacional, compania_nombre, matricula, valor_charter, forma_pago_codigo, banco_codigo, deposito,
                       numero_documento, fecha_registro_raw, hora_creacion_raw, hora_creacion, procesado, mirror_synced_at
                  FROM mirror_clean.v_fr3_cabecera
                 WHERE " + string.Join(" AND ", whereParts) + @"
              ORDER BY fecha_registro_raw DESC, hora_creacion_raw DESC, secuencial_fr3 DESC
                 LIMIT @take";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("take", take);
                    if (!string.IsNullOrWhiteSpace(aeropuerto))
                        cmd.Parameters.AddWithValue("aer", aeropuerto.Trim().ToUpper());
                    if (!string.IsNullOrWhiteSpace(anio))
                        cmd.Parameters.AddWithValue("anio", anio.Trim());

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new MirrorFr3CabeceraDto
                            {
                                Secuencial        = rd.IsDBNull(0) ? 0 : rd.GetDecimal(0),
                                Aeropuerto        = rd.IsDBNull(1) ? null : rd.GetString(1),
                                Anio              = rd.IsDBNull(2) ? null : rd.GetString(2),
                                FechaControlVuelo = rd.IsDBNull(3) ? null : rd.GetString(3),
                                TipoOperacion     = rd.IsDBNull(4) ? null : rd.GetString(4),
                                RutaPlanVuelo     = rd.IsDBNull(5) ? null : rd.GetString(5),
                                NumAterrizaPais   = rd.IsDBNull(6) ? 0 : rd.GetInt32(6),
                                Total             = rd.IsDBNull(7) ? 0m : rd.GetDecimal(7),
                                GranTotal         = rd.IsDBNull(8) ? 0m : rd.GetDecimal(8),
                                Autorizacion      = rd.IsDBNull(9) ? null : rd.GetString(9),
                                Observacion       = rd.IsDBNull(10) ? null : rd.GetString(10),
                                Ruc               = rd.IsDBNull(11) ? null : rd.GetString(11),
                                NombreCliente     = rd.IsDBNull(12) ? null : rd.GetString(12),
                                Estado            = rd.IsDBNull(13) ? null : rd.GetString(13),
                                NacInter          = rd.IsDBNull(14) ? null : rd.GetString(14),
                                NombreCia         = rd.IsDBNull(15) ? null : rd.GetString(15),
                                Matricula         = rd.IsDBNull(16) ? null : rd.GetString(16),
                                ValorCharter      = rd.IsDBNull(17) ? 0m : rd.GetDecimal(17),
                                FormaPago         = rd.IsDBNull(18) ? null : rd.GetString(18),
                                CodigoBanco       = rd.IsDBNull(19) ? null : rd.GetString(19),
                                Deposito          = rd.IsDBNull(20) ? null : rd.GetString(20),
                                NumeroFactura     = rd.IsDBNull(21) ? null : rd.GetString(21),
                                FechaCreacion     = rd.IsDBNull(22) ? null : rd.GetString(22),
                                HoraCreacionRaw   = rd.IsDBNull(23) ? null : rd.GetString(23),
                                HoraCreacion      = rd.IsDBNull(24) ? null : rd.GetString(24),
                                Procesado         = rd.IsDBNull(25) ? null : rd.GetString(25),
                                MirrorSyncedAt    = rd.IsDBNull(26) ? DateTime.MinValue : rd.GetDateTime(26)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ListarFr3Recientes no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }

        /// <summary>
        /// Obtiene estado actual de watermarks para monitoreo del sync.
        /// Devuelve lista vacía si las tablas sync no existen todavía.
        /// </summary>
        public IList<MirrorSyncStatusDto> ObtenerEstadoSync()
        {
            var list = new List<MirrorSyncStatusDto>();
            if (string.IsNullOrWhiteSpace(_connectionString)) return list;

            const string sql = @"
                SELECT table_name, status, last_success_ts, last_success_key, last_error, updated_at
                  FROM sync.watermark
              ORDER BY table_name";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new MirrorSyncStatusDto
                            {
                                Tabla           = rd.IsDBNull(0) ? null : rd.GetString(0),
                                Estado          = rd.IsDBNull(1) ? null : rd.GetString(1),
                                UltimaSync      = rd.IsDBNull(2) ? (DateTime?)null : rd.GetDateTime(2),
                                UltimaClaveSync = rd.IsDBNull(3) ? null : rd.GetString(3),
                                UltimoError     = rd.IsDBNull(4) ? null : rd.GetString(4),
                                ActualizadoEn   = rd.IsDBNull(5) ? DateTime.MinValue : rd.GetDateTime(5)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerEstadoSync no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }

        /// <summary>
        /// Últimos lotes registrados en sync.batch_log (para dashboard de admin).
        /// </summary>
        public IList<SyncBatchResult> ObtenerUltimosLotes(int take = 30)
        {
            var list = new List<SyncBatchResult>();
            if (string.IsNullOrWhiteSpace(_connectionString)) return list;
            if (take <= 0) take = 30;

            const string sql = @"
                SELECT batch_id, table_name, status, rows_read, rows_applied, rows_rejected, rows_deleted,
                       latency_ms, error, started_at, ended_at
                  FROM sync.batch_log
              ORDER BY started_at DESC
                 LIMIT @take";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("take", take);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var started  = rd.IsDBNull(9) ? DateTime.MinValue : rd.GetDateTime(9);
                            var ended    = rd.IsDBNull(10)? (DateTime?)null : rd.GetDateTime(10);
                            var latencyMs = rd.IsDBNull(7) ? 0 : Convert.ToInt64(rd.GetValue(7));
                            list.Add(new SyncBatchResult
                            {
                                BatchId      = rd.IsDBNull(0) ? Guid.Empty : rd.GetGuid(0),
                                TableName    = rd.IsDBNull(1) ? null : rd.GetString(1),
                                Status       = rd.IsDBNull(2) ? null : rd.GetString(2),
                                RowsRead     = rd.IsDBNull(3) ? 0 : rd.GetInt32(3),
                                RowsApplied  = rd.IsDBNull(4) ? 0 : rd.GetInt32(4),
                                RowsRejected = rd.IsDBNull(5) ? 0 : rd.GetInt32(5),
                                RowsDeleted  = rd.IsDBNull(6) ? 0 : rd.GetInt32(6),
                                Error        = rd.IsDBNull(8) ? null : rd.GetString(8),
                                Duration     = ended.HasValue ? ended.Value - started : TimeSpan.Zero
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerUltimosLotes no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }

        public void SincronizarFr3DesdeEspejo()
        {
            LogBL.RegistrarInfo("[FR3_SYNC][START] Iniciando sincronización de FR3 desde el espejo hacia la base local.", "FR3_SYNC");
            
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // Asegurar que las columnas existen en la base de datos
                    using (var cmdAlter = new NpgsqlCommand(@"
                        ALTER TABLE public.aocr_or_orden ADD COLUMN IF NOT EXISTS numero_fr3 VARCHAR(50);
                        ALTER TABLE public.aocr_or_orden ADD COLUMN IF NOT EXISTS fecha_fr3 TIMESTAMP;
                        ALTER TABLE public.aocr_or_orden ADD COLUMN IF NOT EXISTS fecha_actualizacion TIMESTAMP;
                    ", conn))
                    {
                        cmdAlter.ExecuteNonQuery();
                    }

                    // Log de filas leídas en el espejo
                    int filasEspejo = 0;
                    using (var cmdCount = new NpgsqlCommand("SELECT COUNT(*) FROM mirror_raw.opcar5 WHERE COALESCE(_is_deleted, false) = false", conn))
                    {
                        filasEspejo = Convert.ToInt32(cmdCount.ExecuteScalar());
                    }
                    LogBL.RegistrarInfo(string.Format("[FR3_SYNC][AS400_ROWS] Filas en espejo AS400 (PostgreSQL): {0}", filasEspejo), "FR3_SYNC");

                    // 1. Actualizar aocr_tb_factura_pago con los datos del espejo
                    const string sqlUpdateFacturas = @"
                        UPDATE aocr_tb_factura_pago fp
                        SET
                            fr3_estado = 'FR3_GENERADO',
                            fr3_numero = TRIM(f.opcsec::text || '-' || f.opcaer || '-' || f.opcano),
                            fr3_secuencial = f.opcsec,
                            fr3_aeropuerto = f.opcaer,
                            fr3_anio = f.opcano,
                            fr3_generado_en = CASE
                                WHEN f.opcda4 IS NOT NULL AND f.opcda4 > 0 
                                THEN TO_TIMESTAMP(f.opcda4::text || ' ' || LPAD(COALESCE(f.opch01, 0)::text, 6, '0'), 'YYYYMMDD HH24MISS')
                                ELSE NOW()
                            END,
                            updated_at = NOW()
                        FROM mirror_raw.opcar5 f
                        JOIN aocr_or_orden o ON (
                            (f.opcobs LIKE '%' || o.numero_orden || '%')
                            OR (f.opcobs LIKE '%ORD:' || o.id::text || '%')
                            OR (
                                TRIM(fp.numero_factura) = TRIM(f.opcnum::text)
                                AND TRIM(fp.numero_factura) <> '' 
                                AND TRIM(fp.numero_factura) <> '0'
                                AND TRIM(f.opcnum::text) <> ''
                                AND TRIM(f.opcnum::text) <> '0'
                            )
                            OR (
                                TRIM(fp.numero_factura) = TRIM(f.opcche)
                                AND TRIM(fp.numero_factura) <> '' 
                                AND TRIM(fp.numero_factura) <> '0'
                                AND TRIM(f.opcche) <> ''
                                AND TRIM(f.opcche) <> '0'
                            )
                        )
                        WHERE fp.orden_id = o.id
                          AND f.opcsec IS NOT NULL
                          AND f.opcsec > 0
                          AND (fp.fr3_numero IS NULL OR fp.fr3_numero <> TRIM(f.opcsec::text || '-' || f.opcaer || '-' || f.opcano) OR fp.fr3_estado <> 'FR3_GENERADO')";

                    int facturasActualizadas = 0;
                    using (var cmdFacturas = new NpgsqlCommand(sqlUpdateFacturas, conn))
                    {
                        facturasActualizadas = cmdFacturas.ExecuteNonQuery();
                    }
                    if (facturasActualizadas > 0)
                    {
                        LogBL.RegistrarInfo(string.Format("[FR3_SYNC][UPSERT_OK] Filas insertadas/actualizadas en factura_pago: {0}", facturasActualizadas), "FR3_SYNC");
                    }

                    // 2. Actualizar aocr_or_orden con los datos del espejo
                    const string sqlUpdateOrdenes = @"
                        UPDATE aocr_or_orden o
                        SET
                            numero_fr3 = TRIM(f.opcsec::text || '-' || f.opcaer || '-' || f.opcano),
                            fecha_fr3 = CASE
                                WHEN f.opcda4 IS NOT NULL AND f.opcda4 > 0 
                                THEN TO_TIMESTAMP(f.opcda4::text || ' ' || LPAD(COALESCE(f.opch01, 0)::text, 6, '0'), 'YYYYMMDD HH24MISS')
                                ELSE NOW()
                            END,
                            estado = CASE
                                WHEN UPPER(COALESCE(o.estado, '')) = 'FACTURADA' THEN 'COMPLETADA'
                                ELSE o.estado
                            END,
                            fecha_actualizacion = NOW()
                        FROM mirror_raw.opcar5 f
                        LEFT JOIN aocr_tb_factura_pago fp ON (
                            (TRIM(fp.numero_factura) = TRIM(f.opcnum::text)
                             AND TRIM(fp.numero_factura) <> ''
                             AND TRIM(fp.numero_factura) <> '0'
                             AND TRIM(f.opcnum::text) <> ''
                             AND TRIM(f.opcnum::text) <> '0')
                            OR
                            (TRIM(fp.numero_factura) = TRIM(f.opcche)
                             AND TRIM(fp.numero_factura) <> ''
                             AND TRIM(fp.numero_factura) <> '0'
                             AND TRIM(f.opcche) <> ''
                             AND TRIM(f.opcche) <> '0')
                        )
                        WHERE (
                            (f.opcobs LIKE '%' || o.numero_orden || '%')
                            OR (f.opcobs LIKE '%ORD:' || o.id::text || '%')
                            OR (fp.orden_id = o.id)
                        )
                        AND f.opcsec IS NOT NULL
                        AND f.opcsec > 0
                        AND (o.numero_fr3 IS NULL OR o.numero_fr3 <> TRIM(f.opcsec::text || '-' || f.opcaer || '-' || f.opcano))";

                    int ordenesActualizadas = 0;
                    using (var cmdOrdenes = new NpgsqlCommand(sqlUpdateOrdenes, conn))
                    {
                        ordenesActualizadas = cmdOrdenes.ExecuteNonQuery();
                    }

                    LogBL.RegistrarInfo(string.Format("[FR3_SYNC][ORDEN_UPDATE_OK] Filas actualizadas en orden_recaudacion: {0}", ordenesActualizadas), "FR3_SYNC");

                    // 3. Consultar y registrar logs detallados de las órdenes asociadas
                    const string sqlDetalleCambios = @"
                        SELECT o.numero_orden, o.numero_fr3, o.ruc_cedula, o.total
                        FROM aocr_or_orden o
                        WHERE o.fecha_actualizacion >= NOW() - INTERVAL '5 seconds'
                          AND o.numero_fr3 IS NOT NULL 
                          AND TRIM(o.numero_fr3) <> ''";

                    using (var cmdDetalle = new NpgsqlCommand(sqlDetalleCambios, conn))
                    using (var reader = cmdDetalle.ExecuteReader())
                    {
                        bool matchesFound = false;
                        while (reader.Read())
                        {
                            matchesFound = true;
                            var numOrden = reader.GetString(0);
                            var numFr3 = reader.GetString(1);
                            var ruc = reader.IsDBNull(2) ? "N/D" : reader.GetString(2);
                            var total = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
                            
                            LogBL.RegistrarInfo(string.Format(
                                "[FR3_SYNC][UPSERT_OK] Asociada Orden: {0} con FR3: {1}. RUC: {2}, Monto: {3:C}", 
                                numOrden, numFr3, ruc, total), "FR3_SYNC");
                        }

                        if (!matchesFound && facturasActualizadas == 0 && ordenesActualizadas == 0)
                        {
                            LogBL.RegistrarInfo("[FR3_SYNC][NO_MATCH] No se encontraron nuevas asociaciones entre órdenes locales y el espejo.", "FR3_SYNC");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("[FR3_SYNC][ERROR] Error completo durante la sincronización: " + ex.ToString(), "FR3_SYNC");
            }
        }
    } // class MirrorReadService
} // namespace
