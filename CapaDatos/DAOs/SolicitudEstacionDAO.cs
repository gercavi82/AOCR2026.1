using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// AC-02: DAO para persistencia y consulta de estaciones con fechas de inspección independientes.
    /// Soporta transaccionalidad atómica, sincronización diferencial, auditoría y compatibilidad histórica.
    /// </summary>
    public class SolicitudEstacionDAO
    {
        private static readonly Lazy<string> _connectionString = new Lazy<string>(ResolveConnectionString);

        private static string GetConnectionString()
        {
            return _connectionString.Value;
        }

        private static string ResolveConnectionString()
        {
            var envConnection = Environment.GetEnvironmentVariable("AOCR_CONNSTR_AOCRCONNECTION");
            if (!string.IsNullOrWhiteSpace(envConnection))
            {
                return envConnection;
            }

            var connSetting = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            if (connSetting != null && !string.IsNullOrWhiteSpace(connSetting.ConnectionString))
            {
                return connSetting.ConnectionString;
            }

            return ConexionDAO.CadenaConexion;
        }

        /// <summary>
        /// Lista las estaciones activas asociadas a una solicitud AOCR.
        /// Si no existen registros en la tabla aditiva, aplica fallback de compatibilidad histórica.
        /// </summary>
        public List<SolicitudEstacionInspeccion> ListarPorSolicitud(int solicitudId)
        {
            if (solicitudId <= 0) return new List<SolicitudEstacionInspeccion>();

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();
                if (!ExisteTabla(conn, "aocr_tbsolicitud_estacion"))
                {
                    return new List<SolicitudEstacionInspeccion>();
                }

                const string sql = @"
                    SELECT 
                        id AS Id,
                        solicitud_id AS SolicitudId,
                        estacion_codigo AS EstacionCodigo,
                        estacion_nombre AS EstacionNombre,
                        fecha_inicio AS FechaInicio,
                        fecha_fin AS FechaFin,
                        inspector_id AS InspectorId,
                        inspector_nombre AS InspectorNombre,
                        inspeccion_id AS InspeccionId,
                        estado AS Estado,
                        version AS Version,
                        activo AS Activo,
                        observacion AS Observacion,
                        creado_en AS CreadoEn,
                        creado_por AS CreadoPor,
                        actualizado_en AS ActualizadoEn,
                        actualizado_por AS ActualizadoPor
                    FROM public.aocr_tbsolicitud_estacion
                    WHERE solicitud_id = @solicitudId AND activo = TRUE
                    ORDER BY fecha_inicio ASC, id ASC;";

                return conn.Query<SolicitudEstacionInspeccion>(sql, new { solicitudId }).ToList();
            }
        }

        /// <summary>
        /// Guarda las estaciones de forma transaccional sincronizando los registros:
        /// - Inserta estaciones nuevas.
        /// - Actualiza estaciones modificadas (incrementando versión).
        /// - Inactiva lógicamente las estaciones retiradas.
        /// - Registra auditoría formal.
        /// </summary>
        public bool GuardarEstaciones(int solicitudId, IEnumerable<SolicitudEstacionInspeccion> estaciones, int? usuarioId)
        {
            if (solicitudId <= 0) return false;

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        var resultado = GuardarEstacionesTransaccional(solicitudId, estaciones, usuarioId, conn, tx);
                        if (resultado)
                        {
                            tx.Commit();
                        }
                        else
                        {
                            tx.Rollback();
                        }
                        return resultado;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Guarda las estaciones dentro de una transacción existente (ej. FormularioCompleto).
        /// </summary>
        public bool GuardarEstacionesTransaccional(
            int solicitudId,
            IEnumerable<SolicitudEstacionInspeccion> estaciones,
            int? usuarioId,
            IDbConnection conn,
            IDbTransaction tx)
        {
            if (solicitudId <= 0 || conn == null) return false;

            if (!ExisteTabla(conn, tx, "aocr_tbsolicitud_estacion"))
            {
                return false;
            }

            var listaEntrante = (estaciones ?? Enumerable.Empty<SolicitudEstacionInspeccion>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.EstacionCodigo))
                .ToList();

            // 1. Obtener existentes en BD
            const string sqlExistentes = @"
                SELECT id, UPPER(TRIM(estacion_codigo)) AS Codigo, version, activo
                FROM public.aocr_tbsolicitud_estacion
                WHERE solicitud_id = @solicitudId;";

            var existentes = conn.Query(sqlExistentes, new { solicitudId }, tx).ToList();
            var codigosEntrantes = new HashSet<string>(
                listaEntrante.Select(e => e.EstacionCodigo.Trim().ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase);

            // 2. Inactivar las estaciones retiradas
            foreach (var ex in existentes)
            {
                string cod = ex.codigo;
                bool activo = ex.activo;
                int id = ex.id;

                if (activo && !codigosEntrantes.Contains(cod))
                {
                    conn.Execute(@"
                        UPDATE public.aocr_tbsolicitud_estacion
                        SET activo = FALSE,
                            actualizado_en = NOW(),
                            actualizado_por = @usuarioId
                        WHERE id = @id;", new { id, usuarioId }, tx);
                }
            }

            // 3. Procesar entrantes (Insert / Update)
            foreach (var est in listaEntrante)
            {
                string codigoNorm = est.EstacionCodigo.Trim().ToUpperInvariant();
                string nombreNorm = string.IsNullOrWhiteSpace(est.EstacionNombre)
                    ? codigoNorm
                    : est.EstacionNombre.Trim();

                DateTime fInicio = est.FechaInicio != default(DateTime) ? est.FechaInicio.Date : DateTime.Today;
                DateTime fFin = est.FechaFin != default(DateTime) ? est.FechaFin.Date : fInicio;

                if (fFin < fInicio)
                {
                    fFin = fInicio;
                }

                var existente = existentes.FirstOrDefault(e => string.Equals((string)e.codigo, codigoNorm, StringComparison.OrdinalIgnoreCase));

                if (existente != null)
                {
                    int idExistente = existente.id;
                    int verActual = (int)existente.version;

                    conn.Execute(@"
                        UPDATE public.aocr_tbsolicitud_estacion
                        SET estacion_nombre = @nombreNorm,
                            fecha_inicio = @fInicio,
                            fecha_fin = @fFin,
                            inspector_id = @inspectorId,
                            inspector_nombre = @inspectorNombre,
                            estado = COALESCE(NULLIF(@estado, ''), estado),
                            observacion = @observacion,
                            version = @nuevaVersion,
                            activo = TRUE,
                            actualizado_en = NOW(),
                            actualizado_por = @usuarioId
                        WHERE id = @idExistente;",
                        new
                        {
                            idExistente,
                            nombreNorm,
                            fInicio,
                            fFin,
                            inspectorId = est.InspectorId,
                            inspectorNombre = est.InspectorNombre,
                            estado = est.Estado,
                            observacion = est.Observacion,
                            nuevaVersion = verActual + 1,
                            usuarioId
                        }, tx);
                }
                else
                {
                    conn.Execute(@"
                        INSERT INTO public.aocr_tbsolicitud_estacion (
                            solicitud_id,
                            estacion_codigo,
                            estacion_nombre,
                            fecha_inicio,
                            fecha_fin,
                            inspector_id,
                            inspector_nombre,
                            inspeccion_id,
                            estado,
                            version,
                            activo,
                            observacion,
                            creado_en,
                            creado_por
                        ) VALUES (
                            @solicitudId,
                            @codigoNorm,
                            @nombreNorm,
                            @fInicio,
                            @fFin,
                            @inspectorId,
                            @inspectorNombre,
                            @inspeccionId,
                            COALESCE(NULLIF(@estado, ''), 'SOLICITADA'),
                            1,
                            TRUE,
                            @observacion,
                            NOW(),
                            @usuarioId
                        );",
                        new
                        {
                            solicitudId,
                            codigoNorm,
                            nombreNorm,
                            fInicio,
                            fFin,
                            inspectorId = est.InspectorId,
                            inspectorNombre = est.InspectorNombre,
                            inspeccionId = est.InspeccionId,
                            estado = est.Estado,
                            observacion = est.Observacion,
                            usuarioId
                        }, tx);
                }
            }

            // 4. Auditoría en la transacción
            if (ExisteTabla(conn, tx, "aocr_tbauditoria"))
            {
                string detalle = string.Format(
                    "Actualización de estaciones de inspección para SolicitudId={0}. Estaciones: [{1}]",
                    solicitudId,
                    string.Join(", ", listaEntrante.Select(e => string.Format("{0} ({1:yyyy-MM-dd} al {2:yyyy-MM-dd})",
                        e.EstacionCodigo, e.FechaInicio, e.FechaFin))));

                conn.Execute(@"
                    INSERT INTO public.aocr_tbauditoria (modulo, accion, usuario_id, detalle, fecha)
                    VALUES ('SOLICITUD_AOCR', 'ACTUALIZAR_ESTACIONES_INSPECCION', @usuarioId, @detalle, NOW());",
                    new { usuarioId, detalle }, tx);
            }

            return true;
        }

        /// <summary>
        /// Reconstruye estaciones a partir de solicitudes históricas cuando no hay registros en la tabla aditiva.
        /// </summary>
        public static List<SolicitudEstacionInspeccion> ObtenerCompatibilidadHistorica(
            SolicitudAOCR solicitud,
            IEnumerable<Inspeccion> inspecciones)
        {
            var resultado = new List<SolicitudEstacionInspeccion>();
            if (solicitud == null) return resultado;

            DateTime fechaBase = DateTime.Today;
            var inspeccionPrincipal = (inspecciones ?? Enumerable.Empty<Inspeccion>())
                .OrderByDescending(i => i.CodigoInspeccion)
                .FirstOrDefault();

            if (inspeccionPrincipal != null && inspeccionPrincipal.FechaProgramada.HasValue)
            {
                fechaBase = inspeccionPrincipal.FechaProgramada.Value.Date;
            }
            else if (solicitud.FechaInicioOperacion.HasValue)
            {
                fechaBase = solicitud.FechaInicioOperacion.Value.Date;
            }
            else if (solicitud.FechaSolicitud.HasValue)
            {
                fechaBase = solicitud.FechaSolicitud.Value.Date;
            }

            DateTime fechaFin = solicitud.FechaFinOperacion.HasValue && solicitud.FechaFinOperacion.Value >= fechaBase
                ? solicitud.FechaFinOperacion.Value.Date
                : fechaBase;

            var aeropuertosTexto = solicitud.AeropuertosEcuador ?? string.Empty;
            var aeropuertosOtros = solicitud.AeropuertosEcuadorOtros ?? string.Empty;

            var tokens = aeropuertosTexto
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (!string.IsNullOrWhiteSpace(aeropuertosOtros))
            {
                tokens.Add(aeropuertosOtros.Trim());
            }

            if (!tokens.Any())
            {
                if (!string.IsNullOrWhiteSpace(solicitud.Ciudad))
                {
                    tokens.Add(solicitud.Ciudad.Trim());
                }
                else
                {
                    tokens.Add("UIO");
                }
            }

            int idVirtual = 1;
            foreach (var token in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string codigo = NormalizarCodigoEstacion(token);
                string nombre = NormalizarNombreEstacion(token, codigo);

                resultado.Add(new SolicitudEstacionInspeccion
                {
                    Id = -(idVirtual++), // IDs virtuales negativos para compatibilidad histórica
                    SolicitudId = solicitud.CodigoSolicitud,
                    EstacionCodigo = codigo,
                    EstacionNombre = nombre,
                    FechaInicio = fechaBase,
                    FechaFin = fechaFin,
                    InspectorId = inspeccionPrincipal != null ? inspeccionPrincipal.CodigoInspector : null,
                    InspectorNombre = inspeccionPrincipal != null ? inspeccionPrincipal.InspectorPrincipalNombre : null,
                    InspeccionId = inspeccionPrincipal != null ? (int?)inspeccionPrincipal.CodigoInspeccion : null,
                    Estado = "PROGRAMADA_HISTORICA",
                    Version = 1,
                    Activo = true
                });
            }

            return resultado;
        }

        public static string NormalizarCodigoEstacion(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return "EST";
            var limpio = token.Trim().ToUpperInvariant();

            if (limpio.Contains("QUITO") || limpio == "UIO") return "UIO";
            if (limpio.Contains("GUAYAQUIL") || limpio == "GYE") return "GYE";
            if (limpio.Contains("MANTA") || limpio == "MEC") return "MEC";
            if (limpio.Contains("LATACUNGA") || limpio == "LTX") return "LTX";
            if (limpio.Contains("CUENCA") || limpio == "CUE") return "CUE";
            if (limpio.Contains("BALTRA") || limpio == "GPS") return "GPS";
            if (limpio.Contains("SAN CRISTOBAL") || limpio == "SCY") return "SCY";

            // Tomar hasta 4 letras o alfanumérico
            var match = Regex.Match(limpio, @"[A-Z]{3,4}");
            if (match.Success) return match.Value;

            return limpio.Length <= 6 ? limpio : limpio.Substring(0, 6);
        }

        public static string NormalizarNombreEstacion(string token, string codigo)
        {
            switch (codigo)
            {
                case "UIO": return "Quito (UIO) - Aeropuerto Internacional Mariscal Sucre";
                case "GYE": return "Guayaquil (GYE) - Aeropuerto Internacional José Joaquín de Olmedo";
                case "MEC": return "Manta (MEC) - Aeropuerto Eloy Alfaro";
                case "LTX": return "Latacunga (LTX) - Aeropuerto Cotopaxi";
                case "CUE": return "Cuenca (CUE) - Aeropuerto Mariscal La Mar";
                case "GPS": return "Baltra (GPS) - Aeropuerto Seymour";
                case "SCY": return "San Cristóbal (SCY) - Aeropuerto de San Cristóbal";
                default:
                    return string.IsNullOrWhiteSpace(token) ? codigo : token.Trim();
            }
        }

        private static bool ExisteTabla(IDbConnection conn, string tableName)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = @tableName;";

            return conn.ExecuteScalar<int>(sql, new { tableName }) > 0;
        }

        private static bool ExisteTabla(IDbConnection conn, IDbTransaction tx, string tableName)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = @tableName;";

            return conn.ExecuteScalar<int>(sql, new { tableName }, tx) > 0;
        }
    }
}
