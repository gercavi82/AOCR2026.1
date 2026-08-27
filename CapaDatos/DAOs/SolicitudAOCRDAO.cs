using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Npgsql;
using Dapper;
using CapaDatos.Constants;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class SolicitudAOCRDAO
    {
        private readonly ILoggingService _logger = LoggingServiceFactory.Create();

        private sealed class InspeccionExistenteInfo
        {
            public int CodigoInspeccion { get; set; }
            public string NumeroInspeccion { get; set; }
            public string Estado { get; set; }
        }

        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        private static List<string> ObtenerEstadosAsignacionInicialSql()
        {
            return EstadoSolicitudSql.EstadosCoordinacionPendienteAsignacionSql.ToList();
        }

        private static bool EstadoPermiteAsignacionInicial(string estado)
        {
            return EstadoSolicitudSql.EstadoPermiteAsignacionInicial(estado);
        }

        // ============================
        // LISTADOS
        // ============================
        public List<SolicitudAOCR> ListarActivas() => ObtenerPorFiltro("deleted_at IS NULL");
        public List<SolicitudAOCR> ObtenerTodos() => ObtenerPorFiltro("1=1");

        // ============================
        // BÚSQUEDA GLOBAL (incluye finalizadas y no finalizadas)
        // ============================
        public List<SolicitudAOCR> BuscarGlobal(string termino, int limite = 50)
        {
            if (string.IsNullOrWhiteSpace(termino))
                return new List<SolicitudAOCR>();

            var t = termino.Trim();
            if (limite <= 0 || limite > 500) limite = 50;

            var lista = new List<SolicitudAOCR>();
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var columnas = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                var filtrosBusqueda = new List<string>
                {
                    "CAST(codigo_solicitud AS TEXT) ILIKE @q"
                };

                void AgregarFiltroTexto(string nombreColumna)
                {
                    if (columnas.Contains(nombreColumna))
                    {
                        filtrosBusqueda.Add("COALESCE(" + nombreColumna + ", '') ILIKE @q");
                    }
                }

                AgregarFiltroTexto("numero_solicitud");
                AgregarFiltroTexto("nombre_operador");
                AgregarFiltroTexto("nombre_explotador");
                AgregarFiltroTexto("operador");
                AgregarFiltroTexto("nombre_compania");
                AgregarFiltroTexto("compania_nombre");
                AgregarFiltroTexto("razon_social");
                AgregarFiltroTexto("razon_social_operador");
                AgregarFiltroTexto("nombre_comercial");
                AgregarFiltroTexto("ruc");
                AgregarFiltroTexto("ruc_operador");
                AgregarFiltroTexto("ruc_explotador");
                AgregarFiltroTexto("identificacion_ruc");
                AgregarFiltroTexto("numero_ruc");
                AgregarFiltroTexto("numero_aoc");
                AgregarFiltroTexto("estado");

                var where = @"
                deleted_at IS NULL AND (
                       " + string.Join("\n                    OR ", filtrosBusqueda) + @"
                )";

                string sql = $@"SELECT * FROM aocr_tbsolicitud WHERE {where} ORDER BY fecha_solicitud DESC NULLS LAST, codigo_solicitud DESC LIMIT @lim";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@q", "%" + t + "%");
                    cmd.Parameters.AddWithValue("@lim", limite);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read()) lista.Add(Mapear(rd));
                    }
                }
            }

            _logger.LogInfo("[BuscarGlobal] termino='" + t + "' resultados=" + lista.Count);
            return lista;
        }

        public List<SolicitudAOCR> ObtenerPorUsuario(int codigoUsuario)
        {
            return ObtenerPorUsuario((int?)codigoUsuario);
        }

        public List<SolicitudAOCR> ObtenerPorUsuario(int? codigoUsuario)
        {
            if (!codigoUsuario.HasValue || codigoUsuario.Value <= 0)
            {
                return ObtenerTodos();
            }

            return ObtenerPorFiltro(
                "codigo_usuario = @u AND deleted_at IS NULL",
                cmd => cmd.Parameters.AddWithValue("@u", codigoUsuario.Value)
            );
        }

        public string ObtenerIdentificacionRecientePorCompania(string codigoCompania, string nombreCompania, int? codigoUsuarioPreferido = null)
        {
            var codigo = (codigoCompania ?? string.Empty).Trim().ToUpperInvariant();
            var nombre = (nombreCompania ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(nombre))
            {
                return string.Empty;
            }

            const string sql = @"
                SELECT
                    COALESCE(NULLIF(TRIM(ruc), ''), NULLIF(TRIM(cedula_representante), ''), '') AS identificacion
                FROM aocr_tbsolicitud
                WHERE deleted_at IS NULL
                  AND @nombre <> ''
                  AND (
                        UPPER(TRIM(COALESCE(nombre_operador, ''))) = @nombre
                        OR UPPER(TRIM(COALESCE(razon_social, ''))) = @nombre
                  )
                  AND (
                        NULLIF(TRIM(COALESCE(ruc, '')), '') IS NOT NULL
                        OR NULLIF(TRIM(COALESCE(cedula_representante, '')), '') IS NOT NULL
                  )
                ORDER BY
                    CASE
                        WHEN @codigoUsuarioPreferido IS NOT NULL AND codigo_usuario = @codigoUsuarioPreferido THEN 0
                        ELSE 1
                    END,
                    fecha_solicitud DESC NULLS LAST,
                    codigo_solicitud DESC
                LIMIT 1;";

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                return (cn.ExecuteScalar<string>(sql, new
                {
                    nombre,
                    codigoUsuarioPreferido
                }) ?? string.Empty).Trim();
            }
        }

        public List<SolicitudAOCR> ObtenerPorEstado(string estado)
        {
            return ObtenerPorEstados(estado);
        }

        // Múltiples estados a la vez
        public List<SolicitudAOCR> ObtenerPorEstados(params string[] estados)
        {
            if (estados == null || estados.Length == 0)
                return ObtenerTodos();

            var estadosFiltro = ExpandirEstadosEquivalentes(estados);
            if (estadosFiltro.Count == 0)
                return ObtenerTodos();

            const string where = @"
                REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') = ANY (@estados)
                AND deleted_at IS NULL";

            return ObtenerPorFiltro(where, cmd =>
            {
                cmd.Parameters.AddWithValue("@estados", estadosFiltro.ToArray());
            });
        }

        private static List<string> ExpandirEstadosEquivalentes(IEnumerable<string> estados)
        {
            var equivalencias = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    EstadoSolicitud.AOCR_EnRevision,
                    new[]
                    {
                        EstadoSolicitud.AOCR_EnRevision,
                        "AOCR_EN_REVISION",
                        "AOCR EN REVISION",
                        "ENVIADO_A_JEFATURA",
                        "ENVIADO A JEFATURA"
                    }
                },
                {
                    EstadoSolicitud.AOCR_Validado,
                    new[]
                    {
                        EstadoSolicitud.AOCR_Validado,
                        "VALIDADO",
                        "VALIDADO_TECNICAMENTE",
                        "ENVIADO_A_LEGALIZACION",
                        "ENVIADO A LEGALIZACION",
                        "APROBADO_POR_DIRECCION"
                    }
                },
                {
                    EstadoSolicitud.AOCR_Legalizado,
                    new[]
                    {
                        EstadoSolicitud.AOCR_Legalizado,
                        "LEGALIZADO",
                        "CERTIFICADO_LEGALIZADO"
                    }
                },
                {
                    EstadoSolicitud.AOCR_EmitidoRecibido,
                    new[]
                    {
                        EstadoSolicitud.AOCR_EmitidoRecibido,
                        "CERTIFICADO_EMITIDO",
                        "AOCR_EMITIDO",
                        "AOCR_ENTREGADO",
                        "AOCR_EMITIDO_RECIBIDO"
                    }
                }
            };

            var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var estado in estados ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(estado))
                {
                    continue;
                }

                var normalizado = EstadoSolicitud.Normalizar(estado);
                var estadoFiltroOriginal = NormalizarEstadoFiltro(estado);
                var estadoFiltroNormalizado = NormalizarEstadoFiltro(normalizado);

                resultado.Add(estadoFiltroOriginal);
                if (DebeAgregarEstadoNormalizado(estadoFiltroOriginal, estadoFiltroNormalizado, normalizado))
                {
                    resultado.Add(estadoFiltroNormalizado);
                }

                string[] alias;
                if (equivalencias.TryGetValue(normalizado, out alias))
                {
                    foreach (var item in alias)
                    {
                        resultado.Add(NormalizarEstadoFiltro(item));
                    }
                }
            }

            return resultado
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();
        }

        private static bool DebeAgregarEstadoNormalizado(string estadoOriginal, string estadoNormalizado, string normalizadoCanonico)
        {
            if (string.IsNullOrWhiteSpace(estadoNormalizado))
            {
                return false;
            }

            if (!string.Equals(normalizadoCanonico, EstadoSolicitud.Pendiente, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(estadoOriginal, estadoNormalizado, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return estadoOriginal.Contains("PENDIENTE")
                   || estadoOriginal.Contains("BORRADOR")
                   || estadoOriginal.Contains("SOLICITUD CREADA");
        }

        private static string NormalizarEstadoFiltro(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return string.Empty;
            }

            var valor = estado.Trim()
                .ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U")
                .Replace('_', ' ');

            while (valor.Contains("  "))
            {
                valor = valor.Replace("  ", " ");
            }

            return valor;
        }

        public List<SolicitudAOCR> ObtenerPendientesRevision()
        {
            return ObtenerPorEstados(
                "ENVIADO_A_INSPECTOR",
                "EN_REVISION",
                EstadoSolicitud.Pendiente,
                EstadoSolicitud.EnRevision,
                EstadoSolicitud.DocumentacionPendiente,
                EstadoSolicitud.Subsanada
            );
        }

        public List<SolicitudAOCR> ObtenerParaValidacionJefatura()
        {
            return ObtenerPorEstados(
                "ENVIADO_A_JEFATURA",
                EstadoSolicitud.AOCR_EnElaboracion,
                EstadoSolicitud.AOCR_EnRevision
            );
        }



        /// <summary>
        /// Marca la solicitud como pendiente de asignación de inspector por Coordinación
        /// (tras envío documental del RT/Solicitante).
        /// </summary>
        public void MarcarPendienteAsignacionCoordinacion(int codigoSolicitud, string usuario)
        {
            if (codigoSolicitud <= 0)
            {
                return;
            }

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var columnas = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                var sets = new List<string> { "updated_at = NOW()" };
                if (columnas.Contains("pendiente_asignacion_inspector"))
                {
                    sets.Add("pendiente_asignacion_inspector = TRUE");
                }

                if (columnas.Contains("updated_by"))
                {
                    sets.Add("updated_by = @usuario");
                }

                var sql = "UPDATE aocr_tbsolicitud SET " + string.Join(", ", sets) +
                          " WHERE codigo_solicitud = @id AND deleted_at IS NULL";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@usuario", (object)(usuario ?? "sistema") ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<SolicitudAOCR> ObtenerPendientesAsignacion()
        {
            var estadosPendientesAsignacion = ObtenerEstadosAsignacionInicialSql();
            var estadosExcluidos = EstadoSolicitudSql.EstadosExcluidosBandejaCoordinacionSql.ToList();
            var estadoCanonicoSql = EstadoSolicitudSql.CanonicalExpression("s.estado");

            _logger.LogInfo("[InspeccionesController] DAO.ObtenerPendientesAsignacion inicio. EstadosFiltro=" + string.Join(",", estadosPendientesAsignacion));

            var placeholders = new List<string>();
            for (int i = 0; i < estadosPendientesAsignacion.Count; i++)
            {
                placeholders.Add("@e" + i);
            }

            var placeholdersExcluidos = new List<string>();
            for (int i = 0; i < estadosExcluidos.Count; i++)
            {
                placeholdersExcluidos.Add("@x" + i);
            }

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                var columnasSolicitud = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                var columnasInspeccion = ObtenerColumnasTabla(cn, "aocr_tbinspeccion");
                var tieneInspectorSql = EstadoSolicitudSql.ExpresionTieneInspectorEfectivo(
                    "s",
                    columnasSolicitud,
                    columnasInspeccion);

                var sql = @"
                SELECT s.*
                FROM aocr_tbsolicitud s
                WHERE s.deleted_at IS NULL
                  AND (
                      " + estadoCanonicoSql + @" IN (" + string.Join(", ", placeholders) + @")
                      OR COALESCE(s.pendiente_asignacion_inspector, FALSE) = TRUE
                  )
                  AND " + estadoCanonicoSql + @" NOT IN (" + string.Join(", ", placeholdersExcluidos) + @")
                  AND NOT " + tieneInspectorSql + @"
                  AND (
                      COALESCE(s.pendiente_asignacion_inspector, FALSE) = TRUE
                      OR COALESCE(s.solicitud_finalizada_rt, FALSE) = TRUE
                      OR " + estadoCanonicoSql + @" IN (
                          'EN REVISION',
                          'ACEPTACION DOCUMENTAL',
                          'PENDIENTE ASIGNACION RT',
                          'DOCUMENTACION COMPLETA',
                          'REQUIERE INSPECCION'
                      )
                  )
                ORDER BY COALESCE(s.updated_at, s.fecha_solicitud, s.created_at) DESC, s.codigo_solicitud DESC";

                try
                {
                    const string sqlDiagnosticoEstados = @"
                        SELECT UPPER(COALESCE(estado,'')) AS estado, COUNT(*)
                        FROM aocr_tbsolicitud
                        WHERE deleted_at IS NULL
                        GROUP BY UPPER(COALESCE(estado,''))
                        ORDER BY COUNT(*) DESC
                        LIMIT 15;";

                    using (var cmdDiag = new NpgsqlCommand(sqlDiagnosticoEstados, cn))
                    using (var rdDiag = cmdDiag.ExecuteReader())
                    {
                        while (rdDiag.Read())
                        {
                            var estado = rdDiag.IsDBNull(0) ? "" : rdDiag.GetString(0);
                            var total = rdDiag.IsDBNull(1) ? 0 : rdDiag.GetInt64(1);
                            _logger.LogInfo("[InspeccionesController] EstadoDetectado=" + estado + ", Total=" + total);
                        }
                    }

                    var sqlDiagnosticoInspeccion = @"
                        SELECT
                            COUNT(DISTINCT s.codigo_solicitud) AS total_solicitudes,
                            COUNT(DISTINCT CASE WHEN i.codigo_inspeccion IS NOT NULL THEN s.codigo_solicitud END) AS con_inspeccion,
                            COUNT(DISTINCT CASE WHEN i.codigo_inspector IS NOT NULL THEN s.codigo_solicitud END) AS con_inspector_asignado,
                            COUNT(DISTINCT CASE WHEN i.codigo_inspeccion IS NOT NULL AND i.codigo_inspector IS NULL THEN s.codigo_solicitud END) AS con_inspeccion_sin_inspector
                        FROM aocr_tbsolicitud s
                        LEFT JOIN aocr_tbinspeccion i ON i.codigo_solicitud = s.codigo_solicitud
                        WHERE s.deleted_at IS NULL
                          AND " + estadoCanonicoSql + @" = ANY (@estados);";

                    using (var cmdDiagIns = new NpgsqlCommand(sqlDiagnosticoInspeccion, cn))
                    {
                        cmdDiagIns.Parameters.AddWithValue("@estados", estadosPendientesAsignacion.ToArray());
                        using (var rdDiagIns = cmdDiagIns.ExecuteReader())
                        {
                            if (rdDiagIns.Read())
                            {
                                var totalSolicitudes = rdDiagIns.IsDBNull(0) ? 0 : rdDiagIns.GetInt64(0);
                                var conInspeccion = rdDiagIns.IsDBNull(1) ? 0 : rdDiagIns.GetInt64(1);
                                var conInspector = rdDiagIns.IsDBNull(2) ? 0 : rdDiagIns.GetInt64(2);
                                var conInsSinInspector = rdDiagIns.IsDBNull(3) ? 0 : rdDiagIns.GetInt64(3);
                                _logger.LogInfo("[InspeccionesController] DiagnosticoPendientes => totalSolicitudes=" + totalSolicitudes + ", conInspeccion=" + conInspeccion + ", conInspectorAsignado=" + conInspector + ", conInspeccionSinInspector=" + conInsSinInspector);
                            }
                        }
                    }
                }
                catch (Exception exDiag)
                {
                    _logger.LogWarning("[InspeccionesController] No se pudo ejecutar diagnostico de pendientes: " + exDiag.Message);
                }

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    for (int i = 0; i < estadosPendientesAsignacion.Count; i++)
                    {
                        cmd.Parameters.AddWithValue(placeholders[i], estadosPendientesAsignacion[i]);
                    }

                    for (int i = 0; i < estadosExcluidos.Count; i++)
                    {
                        cmd.Parameters.AddWithValue(placeholdersExcluidos[i], estadosExcluidos[i]);
                    }

                    using (var rd = cmd.ExecuteReader())
                    {
                        var lista = new List<SolicitudAOCR>();
                        while (rd.Read()) lista.Add(Mapear(rd));
                        _logger.LogInfo("[InspeccionesController] DAO.ObtenerPendientesAsignacion resultado=" + lista.Count);
                        for (var i = 0; i < lista.Count && i < 5; i++)
                        {
                            var s = lista[i];
                            _logger.LogInfo("[InspeccionesController] PendienteEjemplo[" + i + "] SolicitudId=" + s.CodigoSolicitud + ", Estado=" + (s.Estado ?? "") + ", Numero=" + (s.NumeroSolicitud ?? ""));
                        }
                        return lista;
                    }
                }
            }
        }

        // ============================
        // OBTENER INDIVIDUAL
        // ============================
        public SolicitudAOCR ObtenerPorId(int id)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                const string sql = @"SELECT * FROM aocr_tbsolicitud
                                     WHERE codigo_solicitud = @id AND deleted_at IS NULL
                                     LIMIT 1";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            return Mapear(rd);
                        }
                    }
                }

                const string sqlLegacy = @"SELECT *
                                          FROM aocr_tbsolicitud
                                          WHERE codigo_solicitud = @id
                                            AND UPPER(TRIM(COALESCE(estado, ''))) <> 'ELIMINADO'
                                          LIMIT 1";
                using (var cmdLegacy = new NpgsqlCommand(sqlLegacy, cn))
                {
                    cmdLegacy.Parameters.AddWithValue("@id", id);
                    using (var rdLegacy = cmdLegacy.ExecuteReader())
                    {
                        return rdLegacy.Read() ? Mapear(rdLegacy) : null;
                    }
                }
            }
        }

        // ✅ COMPATIBILIDAD
        public SolicitudAOCR ObtenerPorCodigo(int codigoSolicitud)
        {
            string sql = @"
        SELECT *
        FROM aocr_tbsolicitud 
        WHERE codigo_solicitud = @CodigoSolicitud
        AND estado != 'Eliminado'";

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@CodigoSolicitud", codigoSolicitud);
                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Mapear(rd) : null;
                    }
                }
            }
        }

        // ============================
        // INSERTAR (COMPLETO)
        // ============================
        public int InsertarConReturn(SolicitudAOCR solicitud)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                return InsertarConReturn(cn, null, solicitud);
            }
        }

        public int InsertarConReturn(NpgsqlConnection cn, NpgsqlTransaction tx, SolicitudAOCR solicitud)
        {
            if (tx == null)
            {
                EnsureColumnasFormularioEmision(cn);
            }
            var columnas = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
            var columnaCodCiudad = ResolverColumnaCodigoCiudad(columnas);
            var correoRepresentanteTecnico = ResolverCorreoRepresentanteTecnicoCanonico(solicitud);
            var emailSolicitud = ResolverEmailSolicitudCanonico(solicitud);

            var columnasInsert = new List<string>
            {
                "numero_solicitud",
                "fecha_solicitud",
                "tipo_solicitud",
                "nombre_operador",
                "ruc",
                "razon_social",
                "email",
                "telefono",
                "direccion",
                "representante_legal",
                "cedula_representante",
                "tipo_operacion",
                "descripcion_operacion",
                "observaciones",
                "estado",
                "codigo_usuario"
            };
            var valoresInsert = new List<string>
            {
                "@NumeroSolicitud",
                "@FechaSolicitud",
                "@TipoSolicitud",
                "@NombreOperador",
                "@Ruc",
                "@RazonSocial",
                "@Email",
                "@Telefono",
                "@Direccion",
                "@RepresentanteLegal",
                "@CedulaRepresentante",
                "@TipoOperacion",
                "@DescripcionOperacion",
                "@Observaciones",
                "@Estado",
                "@CodigoUsuario"
            };

            if (columnas.Contains("ciudad"))
            {
                columnasInsert.Add("ciudad");
                valoresInsert.Add("@Ciudad");
            }
            if (columnas.Contains("provincia"))
            {
                columnasInsert.Add("provincia");
                valoresInsert.Add("@Provincia");
            }
            if (columnas.Contains("pais"))
            {
                columnasInsert.Add("pais");
                valoresInsert.Add("@Pais");
            }
            if (!string.IsNullOrWhiteSpace(columnaCodCiudad))
            {
                columnasInsert.Add(columnaCodCiudad);
                valoresInsert.Add("@CodCiudad");
            }
            if (columnas.Contains("correo_representante_tecnico"))
            {
                columnasInsert.Add("correo_representante_tecnico");
                valoresInsert.Add("@CorreoRepresentanteTecnico");
            }
            if (columnas.Contains("nombre_comercial"))
            {
                columnasInsert.Add("nombre_comercial");
                valoresInsert.Add("@NombreComercial");
            }
            if (columnas.Contains("resumen_operaciones_eae"))
            {
                columnasInsert.Add("resumen_operaciones_eae");
                valoresInsert.Add("@ResumenOperacionesEae");
            }
            if (columnas.Contains("numero_aoc"))
            {
                columnasInsert.Add("numero_aoc");
                valoresInsert.Add("@NumeroAOC");
            }
            if (columnas.Contains("aprobaciones_especiales"))
            {
                columnasInsert.Add("aprobaciones_especiales");
                valoresInsert.Add("@AprobacionesEspeciales");
            }
            if (columnas.Contains("aprobaciones_especiales_otros"))
            {
                columnasInsert.Add("aprobaciones_especiales_otros");
                valoresInsert.Add("@AprobacionesEspecialesOtros");
            }
            if (columnas.Contains("aeropuertos_ecuador"))
            {
                columnasInsert.Add("aeropuertos_ecuador");
                valoresInsert.Add("@AeropuertosEcuador");
            }
            if (columnas.Contains("aeropuertos_ecuador_otros"))
            {
                columnasInsert.Add("aeropuertos_ecuador_otros");
                valoresInsert.Add("@AeropuertosEcuadorOtros");
            }
            if (columnas.Contains("companias_seleccionadas"))
            {
                columnasInsert.Add("companias_seleccionadas");
                valoresInsert.Add("@CompaniasSeleccionadas");
            }
            if (columnas.Contains("codigo_oaci"))
            {
                columnasInsert.Add("codigo_oaci");
                valoresInsert.Add("@CodigoOaci");
            }

            var sql = $@"
        INSERT INTO aocr_tbsolicitud (
            {string.Join("," + Environment.NewLine + "            ", columnasInsert)}
        ) VALUES (
            {string.Join("," + Environment.NewLine + "            ", valoresInsert)}
        ) RETURNING codigo_solicitud";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                if (tx != null)
                {
                    cmd.Transaction = tx;
                }

                cmd.Parameters.AddWithValue("@NumeroSolicitud", (object)(solicitud.NumeroSolicitud ?? ""));
                cmd.Parameters.AddWithValue("@FechaSolicitud", (object)solicitud.FechaSolicitud ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TipoSolicitud", (object)(solicitud.TipoSolicitud ?? 1));
                cmd.Parameters.AddWithValue("@NombreOperador", (object)(solicitud.NombreOperador ?? ""));
                cmd.Parameters.AddWithValue("@Ruc", (object)(solicitud.Ruc ?? ""));
                cmd.Parameters.AddWithValue("@RazonSocial", (object)(solicitud.RazonSocial ?? ""));

                cmd.Parameters.AddWithValue("@Email", (object)(emailSolicitud ?? ""));
                cmd.Parameters.AddWithValue("@Telefono", (object)(solicitud.Telefono ?? ""));
                cmd.Parameters.AddWithValue("@Direccion", (object)(solicitud.Direccion ?? ""));
                cmd.Parameters.AddWithValue("@Ciudad", (object)(solicitud.Ciudad ?? ""));
                cmd.Parameters.AddWithValue("@Provincia", (object)(solicitud.Provincia ?? ""));
                cmd.Parameters.AddWithValue("@Pais", (object)(solicitud.Pais ?? ""));
                cmd.Parameters.AddWithValue("@CodCiudad", (object)(solicitud.CodCiudad ?? ""));
                cmd.Parameters.AddWithValue("@RepresentanteLegal", (object)(solicitud.RepresentanteLegal ?? ""));
                cmd.Parameters.AddWithValue("@CedulaRepresentante", (object)(solicitud.CedulaRepresentante ?? ""));
                cmd.Parameters.AddWithValue("@CorreoRepresentanteTecnico", (object)(correoRepresentanteTecnico ?? ""));
                cmd.Parameters.AddWithValue("@NombreComercial", (object)(solicitud.NombreComercial ?? ""));

                cmd.Parameters.AddWithValue("@TipoOperacion", (object)(solicitud.TipoOperacion ?? ""));
                cmd.Parameters.AddWithValue("@DescripcionOperacion", (object)(solicitud.DescripcionOperacion ?? ""));
                cmd.Parameters.AddWithValue("@Observaciones", (object)(solicitud.Observaciones ?? ""));
                cmd.Parameters.AddWithValue("@ResumenOperacionesEae", (object)(solicitud.ResumenOperacionesEae ?? ""));
                cmd.Parameters.AddWithValue("@NumeroAOC", (object)(solicitud.NumeroAOC ?? ""));
                cmd.Parameters.AddWithValue("@AprobacionesEspeciales", (object)(solicitud.AprobacionesEspeciales ?? ""));
                cmd.Parameters.AddWithValue("@AprobacionesEspecialesOtros", (object)(solicitud.AprobacionesEspecialesOtros ?? ""));
                cmd.Parameters.AddWithValue("@AeropuertosEcuador", (object)(solicitud.AeropuertosEcuador ?? ""));
                cmd.Parameters.AddWithValue("@AeropuertosEcuadorOtros", (object)(solicitud.AeropuertosEcuadorOtros ?? ""));
                cmd.Parameters.AddWithValue("@CompaniasSeleccionadas", (object)(solicitud.CompaniasSeleccionadas ?? ""));
                cmd.Parameters.AddWithValue("@CodigoOaci", (object)(solicitud.CodigoOaci ?? ""));

                var estadoNormalizado = EstadoSolicitud.Normalizar(solicitud.Estado);
                cmd.Parameters.AddWithValue("@Estado", estadoNormalizado);

                cmd.Parameters.AddWithValue("@CodigoUsuario", solicitud.CodigoUsuario);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ============================
        // ACTUALIZAR (COMPLETO)
        // ============================
        /// <summary>
        /// Garantiza que existan las columnas que persisten las secciones del
        /// FormularioEmisionAOCR (Operaciones EAE, representante, compañía).
        /// Sin esto, ActualizarGeneral omitía silenciosamente esos campos y el
        /// guardado reportaba éxito sin persistir los datos.
        /// </summary>
        private void EnsureColumnasFormularioEmision(NpgsqlConnection cn)
        {
            const string sql = @"
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS resumen_operaciones_eae TEXT NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS numero_aoc VARCHAR(100) NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS aprobaciones_especiales TEXT NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS aprobaciones_especiales_otros TEXT NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS aeropuertos_ecuador TEXT NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS aeropuertos_ecuador_otros TEXT NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS companias_seleccionadas TEXT NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS codigo_oaci VARCHAR(10) NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS correo_representante_tecnico VARCHAR(200) NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS nombre_comercial VARCHAR(200) NULL;";

            try
            {
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Si el usuario de BD no tiene permisos DDL se mantiene el comportamiento
                // condicional; la verificación post-guardado reportará el campo faltante.
                _logger.LogWarning("[SOLICITUD_AOCR] No se pudieron asegurar columnas del formulario de emisión: " + ex.Message);
            }
        }

        public bool ActualizarGeneral(SolicitudAOCR s)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                EnsureColumnasFormularioEmision(cn);
                var columnas = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                var columnaCodCiudad = ResolverColumnaCodigoCiudad(columnas);
                var correoRepresentanteTecnico = ResolverCorreoRepresentanteTecnicoCanonico(s);
                var emailSolicitud = ResolverEmailSolicitudCanonico(s);
                var setClauses = new List<string>
                {
                    "numero_solicitud=@numero",
                    "fecha_solicitud=@fecha",
                    "tipo_solicitud=@tipo_solicitud",
                    "estado=@estado",
                    "nombre_operador=@nombre_operador",
                    "ruc=@ruc",
                    "razon_social=@razon_social",
                    "email=@email",
                    "telefono=@telefono",
                    "direccion=@direccion"
                };

                if (columnas.Contains("ciudad"))
                {
                    setClauses.Add("ciudad=@ciudad");
                }

                if (columnas.Contains("provincia"))
                {
                    setClauses.Add("provincia=@provincia");
                }

                if (columnas.Contains("pais"))
                {
                    setClauses.Add("pais=@pais");
                }

                if (!string.IsNullOrWhiteSpace(columnaCodCiudad))
                {
                    setClauses.Add(columnaCodCiudad + "=@cod_ciudad");
                }

                setClauses.Add("representante_legal=@representante_legal");
                setClauses.Add("cedula_representante=@cedula_representante");
                if (columnas.Contains("correo_representante_tecnico"))
                {
                    setClauses.Add("correo_representante_tecnico=@correo_representante_tecnico");
                }
                if (columnas.Contains("nombre_comercial"))
                {
                    setClauses.Add("nombre_comercial=@nombre_comercial");
                }
                setClauses.Add("tipo_operacion=@tipo_operacion");
                setClauses.Add("descripcion_operacion=@descripcion_operacion");
                if (columnas.Contains("resumen_operaciones_eae"))
                {
                    setClauses.Add("resumen_operaciones_eae=@resumen_operaciones_eae");
                }
                if (columnas.Contains("numero_aoc"))
                {
                    setClauses.Add("numero_aoc=@numero_aoc");
                }
                if (columnas.Contains("aprobaciones_especiales"))
                {
                    setClauses.Add("aprobaciones_especiales=@aprobaciones_especiales");
                }
                if (columnas.Contains("aprobaciones_especiales_otros"))
                {
                    setClauses.Add("aprobaciones_especiales_otros=@aprobaciones_especiales_otros");
                }
                if (columnas.Contains("aeropuertos_ecuador"))
                {
                    setClauses.Add("aeropuertos_ecuador=@aeropuertos_ecuador");
                }
                if (columnas.Contains("aeropuertos_ecuador_otros"))
                {
                    setClauses.Add("aeropuertos_ecuador_otros=@aeropuertos_ecuador_otros");
                }
                if (columnas.Contains("companias_seleccionadas"))
                {
                    setClauses.Add("companias_seleccionadas=@companias_seleccionadas");
                }
                if (columnas.Contains("codigo_oaci"))
                {
                    setClauses.Add("codigo_oaci=@codigo_oaci");
                }
                setClauses.Add("observaciones=@observaciones");
                setClauses.Add("codigo_tecnico=@codigo_tecnico");
                setClauses.Add("updated_at=NOW()");
                setClauses.Add("updated_by=@updated_by");

                var sql = @"
UPDATE aocr_tbsolicitud
SET
  " + string.Join("," + Environment.NewLine + "  ", setClauses) + @"
WHERE codigo_solicitud=@id AND deleted_at IS NULL;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", s.CodigoSolicitud);

                    cmd.Parameters.AddWithValue("@numero", (object)(s.NumeroSolicitud ?? ""));
                    cmd.Parameters.AddWithValue("@fecha", (object)s.FechaSolicitud ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo_solicitud", (object)s.TipoSolicitud ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)(s.Estado ?? ""));

                    cmd.Parameters.AddWithValue("@nombre_operador", (object)(s.NombreOperador ?? ""));
                    cmd.Parameters.AddWithValue("@ruc", (object)(s.Ruc ?? ""));
                    cmd.Parameters.AddWithValue("@razon_social", (object)(s.RazonSocial ?? ""));

                    cmd.Parameters.AddWithValue("@email", (object)(emailSolicitud ?? ""));
                    cmd.Parameters.AddWithValue("@telefono", (object)(s.Telefono ?? ""));
                    cmd.Parameters.AddWithValue("@direccion", (object)(s.Direccion ?? ""));
                    if (columnas.Contains("ciudad"))
                    {
                        cmd.Parameters.AddWithValue("@ciudad", (object)(s.Ciudad ?? ""));
                    }
                    if (columnas.Contains("provincia"))
                    {
                        cmd.Parameters.AddWithValue("@provincia", (object)(s.Provincia ?? ""));
                    }
                    if (columnas.Contains("pais"))
                    {
                        cmd.Parameters.AddWithValue("@pais", (object)(s.Pais ?? ""));
                    }
                    if (!string.IsNullOrWhiteSpace(columnaCodCiudad))
                    {
                        cmd.Parameters.AddWithValue("@cod_ciudad", (object)(s.CodCiudad ?? ""));
                    }

                    cmd.Parameters.AddWithValue("@representante_legal", (object)(s.RepresentanteLegal ?? ""));
                    cmd.Parameters.AddWithValue("@cedula_representante", (object)(s.CedulaRepresentante ?? ""));
                    if (columnas.Contains("correo_representante_tecnico"))
                    {
                        cmd.Parameters.AddWithValue("@correo_representante_tecnico", (object)(correoRepresentanteTecnico ?? ""));
                    }
                    if (columnas.Contains("nombre_comercial"))
                    {
                        cmd.Parameters.AddWithValue("@nombre_comercial", (object)(s.NombreComercial ?? ""));
                    }

                    cmd.Parameters.AddWithValue("@tipo_operacion", (object)(s.TipoOperacion ?? ""));
                    cmd.Parameters.AddWithValue("@descripcion_operacion", (object)(s.DescripcionOperacion ?? ""));
                    if (columnas.Contains("resumen_operaciones_eae"))
                    {
                        cmd.Parameters.AddWithValue("@resumen_operaciones_eae", (object)(s.ResumenOperacionesEae ?? ""));
                    }
                    if (columnas.Contains("numero_aoc"))
                    {
                        cmd.Parameters.AddWithValue("@numero_aoc", (object)(s.NumeroAOC ?? ""));
                    }
                    if (columnas.Contains("aprobaciones_especiales"))
                    {
                        cmd.Parameters.AddWithValue("@aprobaciones_especiales", (object)(s.AprobacionesEspeciales ?? ""));
                    }
                    if (columnas.Contains("aprobaciones_especiales_otros"))
                    {
                        cmd.Parameters.AddWithValue("@aprobaciones_especiales_otros", (object)(s.AprobacionesEspecialesOtros ?? ""));
                    }
                    if (columnas.Contains("aeropuertos_ecuador"))
                    {
                        cmd.Parameters.AddWithValue("@aeropuertos_ecuador", (object)(s.AeropuertosEcuador ?? ""));
                    }
                    if (columnas.Contains("aeropuertos_ecuador_otros"))
                    {
                        cmd.Parameters.AddWithValue("@aeropuertos_ecuador_otros", (object)(s.AeropuertosEcuadorOtros ?? ""));
                    }
                    if (columnas.Contains("companias_seleccionadas"))
                    {
                        cmd.Parameters.AddWithValue("@companias_seleccionadas", (object)(s.CompaniasSeleccionadas ?? ""));
                    }
                    if (columnas.Contains("codigo_oaci"))
                    {
                        cmd.Parameters.AddWithValue("@codigo_oaci", (object)(s.CodigoOaci ?? ""));
                    }
                    cmd.Parameters.AddWithValue("@observaciones", (object)(s.Observaciones ?? ""));

                    cmd.Parameters.AddWithValue("@codigo_tecnico", (object)s.CodigoTecnico ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated_by", (object)(s.UpdatedBy ?? "sistema"));

                    var filas = cmd.ExecuteNonQuery();

                    var columnasEsperadas = new[]
                    {
                        "resumen_operaciones_eae", "numero_aoc", "aprobaciones_especiales",
                        "aprobaciones_especiales_otros", "aeropuertos_ecuador", "aeropuertos_ecuador_otros",
                        "companias_seleccionadas", "codigo_oaci", "correo_representante_tecnico", "nombre_comercial"
                    };
                    var columnasOmitidas = columnasEsperadas.Where(c => !columnas.Contains(c)).ToList();

                    _logger.LogInfo(
                        "[SOLICITUD_AOCR][ACTUALIZAR_GENERAL] SolicitudId=" + s.CodigoSolicitud +
                        "; FilasAfectadas=" + filas +
                        "; ColumnasOmitidas=" + (columnasOmitidas.Count > 0 ? string.Join(",", columnasOmitidas) : "ninguna") +
                        "; UpdatedBy=" + (s.UpdatedBy ?? "sistema"));

                    return filas > 0;
                }
            }
        }

        // ✅ COMPATIBILIDAD
        public bool Actualizar(SolicitudAOCR s) => ActualizarGeneral(s);

        public bool CambiarEstado(int id, string estado, int usuario, string obs = "")
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                const string sql = @"
UPDATE aocr_tbsolicitud
SET estado=@e,
    observaciones=@o,
    updated_at=NOW(),
    updated_by=@u
WHERE codigo_solicitud=@id AND deleted_at IS NULL;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    var estadoNormalizado = EstadoSolicitud.Normalizar(estado);
                    cmd.Parameters.AddWithValue("@e", estadoNormalizado);
                    cmd.Parameters.AddWithValue("@o", obs ?? "");
                    cmd.Parameters.AddWithValue("@u", usuario.ToString());
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ============================
        // ASIGNAR INSPECTORES (OPINSPECTORES / OPIAR2)
        // ============================
        public bool AsignarInspectores(
            int codigoSolicitud,
            string inspectorPrincipalCedula,
            string inspectorApoyoCedula,
            DateTime fecha,
            string obs,
            string tipoInspector,
            string usuarioAsignador,
            out string mensaje)
        {
            try
            {
                _logger.LogInfo("[GestionInspeccion] Inicio metodo DAO.AsignarInspectores. SolicitudId=" + codigoSolicitud + ", TipoInspector=" + (tipoInspector ?? "") + ", InspectorPrincipal=" + (inspectorPrincipalCedula ?? "") + ", InspectorApoyo=" + (inspectorApoyoCedula ?? ""));

                var tipoInspectorNormalizado = NormalizarTipoInspector(tipoInspector);
                var cedulaPrincipal = (inspectorPrincipalCedula ?? string.Empty).Trim();
                var cedulaApoyo = (inspectorApoyoCedula ?? string.Empty).Trim();
                var actorAsignador = string.IsNullOrWhiteSpace(usuarioAsignador)
                    ? "sistema"
                    : usuarioAsignador.Trim();

                if (string.IsNullOrWhiteSpace(cedulaPrincipal))
                {
                    _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=Inspector principal vacio.");
                    mensaje = "Debe seleccionar un inspector principal activo.";
                    return false;
                }

                var usuarioInternoRtDao = new UsuarioInternoRTDAO();
                var inspectorPrincipal = usuarioInternoRtDao.ObtenerInspectorAsignableActivo(cedulaPrincipal, tipoInspectorNormalizado);
                if (inspectorPrincipal == null)
                {
                    _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=Inspector principal no existe/activo en catalogo. Cedula=" + cedulaPrincipal);
                    mensaje = "El inspector principal seleccionado no existe o no está activo en el catálogo de inspectores.";
                    return false;
                }

                UsuarioInternoRTRegistro inspectorApoyo = null;
                if (!string.IsNullOrWhiteSpace(cedulaApoyo))
                {
                    inspectorApoyo = usuarioInternoRtDao.ObtenerInspectorAsignableActivo(cedulaApoyo, tipoInspectorNormalizado);
                    if (inspectorApoyo == null)
                    {
                        _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=Inspector apoyo no existe/activo en catalogo. Cedula=" + cedulaApoyo);
                        mensaje = "El inspector de apoyo seleccionado no existe o no está activo en el catálogo de inspectores.";
                        return false;
                    }

                    if (string.Equals(
                        (inspectorPrincipal.UsuarioLogin ?? string.Empty).Trim(),
                        (inspectorApoyo.UsuarioLogin ?? string.Empty).Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=Inspector principal y apoyo son iguales.");
                        mensaje = "El inspector principal y el inspector de apoyo no pueden ser el mismo.";
                        return false;
                    }
                }

                using (var cn = new NpgsqlConnection(ConnectionString))
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        string estadoAnterior = null;
                        using (var cmdEstado = new NpgsqlCommand("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=@id FOR UPDATE;", cn, tx))
                        {
                            cmdEstado.Parameters.AddWithValue("@id", codigoSolicitud);
                            var value = cmdEstado.ExecuteScalar();
                            if (value == null || value == DBNull.Value)
                            {
                                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=Solicitud inexistente o bloqueada para asignacion. SolicitudId=" + codigoSolicitud);
                                mensaje = "La solicitud no existe o no está disponible para asignación.";
                                tx.Rollback();
                                return false;
                            }

                            estadoAnterior = value.ToString();
                        }

                        var columnasSolicitud = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                        var columnasInspeccion = ObtenerColumnasTabla(cn, "aocr_tbinspeccion");
                        var estadoActualNormalizado = EstadoSolicitud.Normalizar(estadoAnterior);

                        var inspeccionExistente = ObtenerUltimaInspeccionPorSolicitud(cn, tx, codigoSolicitud);
                        var esReasignacion = inspeccionExistente != null && PermiteReasignacion(inspeccionExistente.Estado);
                        var estadoPermiteAsignacionInicial = EstadoPermiteAsignacionInicial(estadoAnterior);
                        var estadoPermiteReasignacion =
                            string.Equals(estadoActualNormalizado, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase) &&
                            esReasignacion;

                        string estadoRecaudacion;
                        var requiereRecaudacionFinalizada =
                            !estadoPermiteAsignacionInicial &&
                            !estadoPermiteReasignacion;

                        if (requiereRecaudacionFinalizada
                            && !TieneRecaudacionFinalizada(cn, tx, codigoSolicitud, columnasSolicitud, out estadoRecaudacion))
                        {
                            _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=Recaudacion no finalizada. EstadoRecaudacion=" + (estadoRecaudacion ?? "SIN_ORDEN"));
                            mensaje = string.Equals(estadoRecaudacion, "ANULADA", StringComparison.OrdinalIgnoreCase)
                                ? "No se puede asignar inspector: la orden de recaudación vigente está anulada. Verifique el estado de pago con Financiero."
                                : "No se puede asignar inspector hasta que la recaudación esté finalizada.";
                            tx.Rollback();
                            return false;
                        }

                        if (!estadoPermiteAsignacionInicial && !estadoPermiteReasignacion)
                        {
                            if (inspeccionExistente != null && !PermiteReasignacion(inspeccionExistente.Estado))
                            {
                                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=Inspeccion ya iniciada/no editable. EstadoSolicitud=" + (estadoAnterior ?? "") + ", EstadoInspeccion=" + (inspeccionExistente.Estado ?? ""));
                                mensaje = "La solicitud ya tiene una inspección en ejecución y no puede reasignarse desde esta pantalla.";
                            }
                            else
                            {
                                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=Estado no permitido para asignacion. EstadoActual=" + (estadoAnterior ?? ""));
                                mensaje = "La solicitud no se encuentra en un estado válido para asignación técnica.";
                            }

                            tx.Rollback();
                            return false;
                        }

                        var diagnosticoDocumentos = ObtenerDiagnosticoDocumentosAsignacion(cn, tx, codigoSolicitud, estadoAnterior);
                        _logger.LogInfo(
                            "[AOCR][DOC_FLOW_DOCUMENTOS_CHECK] SolicitudId=" + codigoSolicitud +
                            " TotalDocumentos=" + diagnosticoDocumentos.TotalDocumentos +
                            " Pendientes=" + diagnosticoDocumentos.Pendientes +
                            " Observados=" + diagnosticoDocumentos.Observados +
                            " Aceptados=" + diagnosticoDocumentos.Aceptados +
                            " EstadoDocumental=" + (diagnosticoDocumentos.EstadoDocumental ?? string.Empty) +
                            " PuedeEnviarInspector=" + diagnosticoDocumentos.PuedeEnviarInspector +
                            " MotivoBloqueo=" + (diagnosticoDocumentos.MotivoBloqueo ?? string.Empty));

                        if (!diagnosticoDocumentos.PuedeEnviarInspector)
                        {
                            mensaje = diagnosticoDocumentos.MotivoBloqueo;
                            tx.Rollback();
                            return false;
                        }

                        var inspectorPrincipalCodigo = inspectorPrincipal.UsuarioId.HasValue && inspectorPrincipal.UsuarioId.Value > 0
                            ? inspectorPrincipal.UsuarioId
                            : (inspectorPrincipal.TecnicoId.HasValue && inspectorPrincipal.TecnicoId.Value > 0
                                ? inspectorPrincipal.TecnicoId
                                : ParseIntSafe(inspectorPrincipal.UsuarioLogin));
                        var principalCedulaPersist = (inspectorPrincipal.UsuarioLogin ?? string.Empty).Trim();
                        var inspectorPrincipalNombre = (inspectorPrincipal.NombreVisual ?? string.Empty).Trim();
                        var inspectorPrincipalTipo = (inspectorPrincipal.Tipo ?? string.Empty).Trim();
                        var inspectorApoyoCedulaValue = inspectorApoyo == null
                            ? (object)DBNull.Value
                            : (object)(inspectorApoyo.UsuarioLogin ?? string.Empty).Trim();
                        var inspectorApoyoNombreValue = inspectorApoyo == null
                            ? (object)DBNull.Value
                            : (object)(inspectorApoyo.NombreVisual ?? string.Empty).Trim();
                        var inspectorApoyoTipoValue = inspectorApoyo == null
                            ? (object)DBNull.Value
                            : (object)(inspectorApoyo.Tipo ?? string.Empty).Trim();
                        var estadoNuevo = EstadoSolicitud.Normalizar("INSPECCION_ASIGNADA");
                        var estadoInspeccionPersistencia = ResolverEstadoInspeccionPersistencia(cn, EstadosInspeccion.VERIFICACION_SOLICITUD);

                        var setSolicitud = new List<string>();
                        if (columnasSolicitud.Contains("estado")) setSolicitud.Add("estado=@estado");
                        if (columnasSolicitud.Contains("updated_at")) setSolicitud.Add("updated_at=NOW()");
                        if (columnasSolicitud.Contains("updated_by")) setSolicitud.Add("updated_by=@updated_by");
                        if (columnasSolicitud.Contains("codigo_tecnico")) setSolicitud.Add("codigo_tecnico=@codigo_tecnico");
                        if (columnasSolicitud.Contains("tecnico_responsable_cedula")) setSolicitud.Add("tecnico_responsable_cedula=@tecnico_responsable_cedula");
                        if (columnasSolicitud.Contains("tecnico_responsable_nombre")) setSolicitud.Add("tecnico_responsable_nombre=@tecnico_responsable_nombre");
                        if (columnasSolicitud.Contains("tecnico_responsable_tipo")) setSolicitud.Add("tecnico_responsable_tipo=@tecnico_responsable_tipo");
                        if (columnasSolicitud.Contains("inspector_apoyo_cedula")) setSolicitud.Add("inspector_apoyo_cedula=@inspector_apoyo_cedula");
                        if (columnasSolicitud.Contains("inspector_apoyo_nombre")) setSolicitud.Add("inspector_apoyo_nombre=@inspector_apoyo_nombre");
                        if (columnasSolicitud.Contains("inspector_apoyo_tipo")) setSolicitud.Add("inspector_apoyo_tipo=@inspector_apoyo_tipo");

                        if (setSolicitud.Count == 0)
                        {
                            throw new Exception("No existen columnas editables en aocr_tbsolicitud para registrar la asignación.");
                        }

                        var whereSolicitud = "codigo_solicitud=@id";
                        if (columnasSolicitud.Contains("deleted_at"))
                        {
                            whereSolicitud += " AND deleted_at IS NULL";
                        }

                        var sqlSolicitud = "UPDATE aocr_tbsolicitud SET " + string.Join(", ", setSolicitud) + " WHERE " + whereSolicitud + ";";
                        using (var cmdSolicitud = new NpgsqlCommand(sqlSolicitud, cn, tx))
                        {
                            cmdSolicitud.Parameters.AddWithValue("@id", codigoSolicitud);
                            cmdSolicitud.Parameters.AddWithValue("@estado", (object)estadoNuevo ?? DBNull.Value);
                            cmdSolicitud.Parameters.AddWithValue("@updated_by", (object)actorAsignador ?? DBNull.Value);
                            cmdSolicitud.Parameters.AddWithValue("@codigo_tecnico", (object)inspectorPrincipalCodigo ?? DBNull.Value);
                            cmdSolicitud.Parameters.AddWithValue("@tecnico_responsable_cedula", (object)principalCedulaPersist ?? DBNull.Value);
                            cmdSolicitud.Parameters.AddWithValue("@tecnico_responsable_nombre", (object)inspectorPrincipalNombre ?? DBNull.Value);
                            cmdSolicitud.Parameters.AddWithValue("@tecnico_responsable_tipo", (object)inspectorPrincipalTipo ?? DBNull.Value);
                            cmdSolicitud.Parameters.AddWithValue("@inspector_apoyo_cedula", inspectorApoyoCedulaValue);
                            cmdSolicitud.Parameters.AddWithValue("@inspector_apoyo_nombre", inspectorApoyoNombreValue);
                            cmdSolicitud.Parameters.AddWithValue("@inspector_apoyo_tipo", inspectorApoyoTipoValue);

                            var rows = cmdSolicitud.ExecuteNonQuery();
                            if (rows <= 0)
                            {
                                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False. Motivo=No se actualizo solicitud en PG. SolicitudId=" + codigoSolicitud);
                                throw new Exception("No fue posible actualizar la solicitud AOCR para registrar la asignación.");
                            }

                            _logger.LogInfo(
                                "[DOC_FLOW] SolicitudId=" + codigoSolicitud +
                                " EstadoAnterior=" + (estadoAnterior ?? string.Empty) +
                                " EstadoNuevo=" + (estadoNuevo ?? string.Empty) +
                                " ResponsableAnterior=COORDINADOR" +
                                " ResponsableNuevo=INSPECTOR" +
                                " Usuario=" + actorAsignador +
                                " Rol=COORDINADOR" +
                                " Accion=ENVIAR_A_INSPECTOR" +
                                " InspectorAsignado=" + principalCedulaPersist +
                                " TotalDocumentos=" + diagnosticoDocumentos.TotalDocumentos +
                                " Pendientes=" + diagnosticoDocumentos.Pendientes +
                                " Observados=" + diagnosticoDocumentos.Observados +
                                " MotivoBloqueo=" + (diagnosticoDocumentos.MotivoBloqueo ?? string.Empty));
                        }

                        int codigoInspeccion;
                        if (esReasignacion)
                        {
                            var setInspeccion = new List<string>();
                            if (columnasInspeccion.Contains("tipo")) setInspeccion.Add("tipo=@tipo");
                            if (columnasInspeccion.Contains("fecha_programada")) setInspeccion.Add("fecha_programada=@fecha_programada");
                            if (columnasInspeccion.Contains("estado")) setInspeccion.Add("estado=@estado_inspeccion");
                            if (columnasInspeccion.Contains("codigo_inspector")) setInspeccion.Add("codigo_inspector=@codigo_inspector");
                            if (columnasInspeccion.Contains("comentarios")) setInspeccion.Add("comentarios=@comentarios");
                            if (columnasInspeccion.Contains("updated_at")) setInspeccion.Add("updated_at=NOW()");
                            if (columnasInspeccion.Contains("updated_by")) setInspeccion.Add("updated_by=@updated_by_ins");
                            if (columnasInspeccion.Contains("inspector_principal_cedula")) setInspeccion.Add("inspector_principal_cedula=@inspector_principal_cedula");
                            if (columnasInspeccion.Contains("inspector_principal_nombre")) setInspeccion.Add("inspector_principal_nombre=@inspector_principal_nombre");
                            if (columnasInspeccion.Contains("inspector_principal_tipo")) setInspeccion.Add("inspector_principal_tipo=@inspector_principal_tipo");
                            if (columnasInspeccion.Contains("inspector_apoyo_cedula")) setInspeccion.Add("inspector_apoyo_cedula=@inspector_apoyo_cedula_ins");
                            if (columnasInspeccion.Contains("inspector_apoyo_nombre")) setInspeccion.Add("inspector_apoyo_nombre=@inspector_apoyo_nombre_ins");
                            if (columnasInspeccion.Contains("inspector_apoyo_tipo")) setInspeccion.Add("inspector_apoyo_tipo=@inspector_apoyo_tipo_ins");

                            if (setInspeccion.Count == 0)
                            {
                                throw new Exception("No existen columnas editables en aocr_tbinspeccion para registrar la reasignación.");
                            }

                            var sqlUpdateInspeccion = "UPDATE aocr_tbinspeccion SET " + string.Join(", ", setInspeccion) + " WHERE codigo_inspeccion=@codigo_inspeccion;";
                            using (var cmdIns = new NpgsqlCommand(sqlUpdateInspeccion, cn, tx))
                            {
                                cmdIns.Parameters.AddWithValue("@codigo_inspeccion", inspeccionExistente.CodigoInspeccion);
                                cmdIns.Parameters.AddWithValue("@tipo", 1);
                                cmdIns.Parameters.AddWithValue("@fecha_programada", fecha.Date);
                                cmdIns.Parameters.AddWithValue("@estado_inspeccion", (object)estadoInspeccionPersistencia ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@codigo_inspector", (object)inspectorPrincipalCodigo ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@comentarios", (object)ConstruirComentarioAsignacion(obs, inspectorPrincipal, inspectorApoyo) ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@updated_by_ins", (object)actorAsignador ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@inspector_principal_cedula", (object)principalCedulaPersist ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@inspector_principal_nombre", (object)inspectorPrincipalNombre ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@inspector_principal_tipo", (object)inspectorPrincipalTipo ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@inspector_apoyo_cedula_ins", inspectorApoyoCedulaValue);
                                cmdIns.Parameters.AddWithValue("@inspector_apoyo_nombre_ins", inspectorApoyoNombreValue);
                                cmdIns.Parameters.AddWithValue("@inspector_apoyo_tipo_ins", inspectorApoyoTipoValue);

                                if (cmdIns.ExecuteNonQuery() <= 0)
                                {
                                    throw new Exception("No fue posible actualizar la inspección vigente para la reasignación.");
                                }
                            }

                            codigoInspeccion = inspeccionExistente.CodigoInspeccion;
                        }
                        else
                        {
                            var numeroInspeccion = GenerarNumeroInspeccionUnico(cn, tx, codigoSolicitud, columnasInspeccion);

                            var columnasInsert = new List<string>();
                            var valoresInsert = new List<string>();

                            columnasInsert.Add("codigo_solicitud");
                            valoresInsert.Add("@codigo_solicitud");

                            if (columnasInspeccion.Contains("numero_inspeccion"))
                            {
                                columnasInsert.Add("numero_inspeccion");
                                valoresInsert.Add("@numero_inspeccion");
                            }

                            if (columnasInspeccion.Contains("tipo"))
                            {
                                columnasInsert.Add("tipo");
                                valoresInsert.Add("@tipo");
                            }

                            if (columnasInspeccion.Contains("fecha_programada"))
                            {
                                columnasInsert.Add("fecha_programada");
                                valoresInsert.Add("@fecha_programada");
                            }

                            if (columnasInspeccion.Contains("estado"))
                            {
                                columnasInsert.Add("estado");
                                valoresInsert.Add("@estado_inspeccion");
                            }

                            if (columnasInspeccion.Contains("codigo_inspector"))
                            {
                                columnasInsert.Add("codigo_inspector");
                                valoresInsert.Add("@codigo_inspector");
                            }

                            if (columnasInspeccion.Contains("comentarios"))
                            {
                                columnasInsert.Add("comentarios");
                                valoresInsert.Add("@comentarios");
                            }

                            if (columnasInspeccion.Contains("created_at"))
                            {
                                columnasInsert.Add("created_at");
                                valoresInsert.Add("NOW()");
                            }

                            if (columnasInspeccion.Contains("created_by"))
                            {
                                columnasInsert.Add("created_by");
                                valoresInsert.Add("@created_by");
                            }

                            if (columnasInspeccion.Contains("updated_at"))
                            {
                                columnasInsert.Add("updated_at");
                                valoresInsert.Add("NOW()");
                            }

                            if (columnasInspeccion.Contains("updated_by"))
                            {
                                columnasInsert.Add("updated_by");
                                valoresInsert.Add("@updated_by_ins");
                            }

                            if (columnasInspeccion.Contains("inspector_principal_cedula"))
                            {
                                columnasInsert.Add("inspector_principal_cedula");
                                valoresInsert.Add("@inspector_principal_cedula");
                            }

                            if (columnasInspeccion.Contains("inspector_principal_nombre"))
                            {
                                columnasInsert.Add("inspector_principal_nombre");
                                valoresInsert.Add("@inspector_principal_nombre");
                            }

                            if (columnasInspeccion.Contains("inspector_principal_tipo"))
                            {
                                columnasInsert.Add("inspector_principal_tipo");
                                valoresInsert.Add("@inspector_principal_tipo");
                            }

                            if (columnasInspeccion.Contains("inspector_apoyo_cedula"))
                            {
                                columnasInsert.Add("inspector_apoyo_cedula");
                                valoresInsert.Add("@inspector_apoyo_cedula_ins");
                            }

                            if (columnasInspeccion.Contains("inspector_apoyo_nombre"))
                            {
                                columnasInsert.Add("inspector_apoyo_nombre");
                                valoresInsert.Add("@inspector_apoyo_nombre_ins");
                            }

                            if (columnasInspeccion.Contains("inspector_apoyo_tipo"))
                            {
                                columnasInsert.Add("inspector_apoyo_tipo");
                                valoresInsert.Add("@inspector_apoyo_tipo_ins");
                            }

                            var sqlInsertInspeccion = "INSERT INTO aocr_tbinspeccion (" +
                                                      string.Join(", ", columnasInsert) +
                                                      ") VALUES (" +
                                                      string.Join(", ", valoresInsert) +
                                                      ") RETURNING codigo_inspeccion;";

                            using (var cmdIns = new NpgsqlCommand(sqlInsertInspeccion, cn, tx))
                            {
                                cmdIns.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                                cmdIns.Parameters.AddWithValue("@numero_inspeccion", (object)(numeroInspeccion ?? string.Empty));
                                cmdIns.Parameters.AddWithValue("@tipo", 1);
                                cmdIns.Parameters.AddWithValue("@fecha_programada", fecha.Date);
                                cmdIns.Parameters.AddWithValue("@estado_inspeccion", (object)estadoInspeccionPersistencia ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@codigo_inspector", (object)inspectorPrincipalCodigo ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@comentarios", (object)ConstruirComentarioAsignacion(obs, inspectorPrincipal, inspectorApoyo) ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@created_by", (object)actorAsignador ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@updated_by_ins", (object)actorAsignador ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@inspector_principal_cedula", (object)principalCedulaPersist ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@inspector_principal_nombre", (object)inspectorPrincipalNombre ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@inspector_principal_tipo", (object)inspectorPrincipalTipo ?? DBNull.Value);
                                cmdIns.Parameters.AddWithValue("@inspector_apoyo_cedula_ins", inspectorApoyoCedulaValue);
                                cmdIns.Parameters.AddWithValue("@inspector_apoyo_nombre_ins", inspectorApoyoNombreValue);
                                cmdIns.Parameters.AddWithValue("@inspector_apoyo_tipo_ins", inspectorApoyoTipoValue);

                                codigoInspeccion = Convert.ToInt32(cmdIns.ExecuteScalar());
                            }
                        }

                        var daoAsignacionRt = new UsuarioInternoRTDAO();
                        string mensajeAsignacionRt;
                        if (!daoAsignacionRt.RegistrarAsignacion(
                            codigoSolicitud,
                            principalCedulaPersist,
                            inspectorPrincipalNombre,
                            inspectorPrincipalTipo,
                            actorAsignador,
                            obs,
                            out mensajeAsignacionRt))
                        {
                            throw new Exception("No se pudo registrar la trazabilidad de asignación RT: " + mensajeAsignacionRt);
                        }

                        tx.Commit();

                        _logger.LogInfo("[GestionInspeccion] PuedeGestionar=True. Operacion=" + (esReasignacion ? "REASIGNACION" : "ASIGNACION") + ", SolicitudId=" + codigoSolicitud + ", CodigoInspeccion=" + codigoInspeccion + ", EstadoAnterior=" + (estadoAnterior ?? "") + ", EstadoNuevo=" + (estadoNuevo ?? ""));

                        try
                        {
                            new HistorialEstadoDAO().RegistrarCambio(
                                codigoSolicitud,
                                estadoAnterior,
                                estadoNuevo,
                                inspectorPrincipalCodigo ?? 0,
                                (esReasignacion ? "Reasignación" : "Asignación") + " de inspección Nro. " + codigoInspeccion);
                        }
                        catch
                        {
                            // El historial es auxiliar; la transacción principal ya se consolidó.
                        }

                        mensaje = esReasignacion
                            ? "Reasignación realizada con éxito. Inspección actualizada: " + codigoInspeccion
                            : "Asignación realizada con éxito. Inspección creada: " + codigoInspeccion;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en DAO.AsignarInspectores: " + ex);
                mensaje = "Error en base de datos: " + ex.Message;
                return false;
            }
        }

        // ============================
        // ACTUALIZAR TÉCNICO (OK)
        // ============================
        public bool ActualizarTecnico(int solicitudId, int tecnicoId, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                const string sql = @"
UPDATE aocr_tbsolicitud
SET codigo_tecnico=@t,
    updated_at=NOW(),
    updated_by=@u
WHERE codigo_solicitud=@id AND deleted_at IS NULL;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@t", tecnicoId);
                    cmd.Parameters.AddWithValue("@u", usuarioId.ToString());
                    cmd.Parameters.AddWithValue("@id", solicitudId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ============================
        // INTERNOS
        // ============================
        private List<SolicitudAOCR> ObtenerPorFiltro(string where, Action<NpgsqlCommand> parametros = null)
        {
            var lista = new List<SolicitudAOCR>();
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                string sql = $@"SELECT * FROM aocr_tbsolicitud WHERE {where} ORDER BY fecha_solicitud DESC";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    parametros?.Invoke(cmd);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read()) lista.Add(Mapear(rd));
                    }
                }
            }
            return lista;
        }

        private SolicitudAOCR Mapear(IDataRecord rd)
        {
            return new SolicitudAOCR
            {
                CodigoSolicitud = GetInt(rd, "codigo_solicitud"),
                NumeroSolicitud = GetString(rd, "numero_solicitud"),
                FechaSolicitud = GetNullableDateTime(rd, "fecha_solicitud"),
                TipoSolicitud = GetNullableInt(rd, "tipo_solicitud"),
                Estado = GetString(rd, "estado"),

                NombreOperador = FirstNonEmpty(
                    GetString(rd, "nombre_operador"),
                    GetString(rd, "nombre_explotador"),
                    GetString(rd, "operador"),
                    GetString(rd, "nombre_compania"),
                    GetString(rd, "compania_nombre")),
                Ruc = FirstNonEmpty(
                    GetString(rd, "ruc"),
                    GetString(rd, "ruc_operador"),
                    GetString(rd, "ruc_explotador"),
                    GetString(rd, "identificacion_ruc"),
                    GetString(rd, "numero_ruc")),
                RazonSocial = FirstNonEmpty(
                    GetString(rd, "razon_social"),
                    GetString(rd, "razon_social_operador"),
                    GetString(rd, "nombre_comercial"),
                    GetString(rd, "nombre_compania")),

                Email = FirstNonEmpty(
                    GetString(rd, "email"),
                    GetString(rd, "correo"),
                    GetString(rd, "correo_electronico"),
                    GetString(rd, "email_operador"),
                    GetString(rd, "correo_operador")),
                Telefono = FirstNonEmpty(
                    GetString(rd, "telefono"),
                    GetString(rd, "telefono_operador"),
                    GetString(rd, "telefono_contacto"),
                    GetString(rd, "celular"),
                    GetString(rd, "telefono_representante")),
                Direccion = FirstNonEmpty(
                    GetString(rd, "direccion"),
                    GetString(rd, "direccion_operador"),
                    GetString(rd, "direccion_principal"),
                    GetString(rd, "domicilio")),
                Ciudad = GetString(rd, "ciudad"),
                CodCiudad = FirstNonEmpty(
                    GetString(rd, "cod_ciudad"),
                    GetString(rd, "codigo_ciudad"),
                    GetString(rd, "ciudad_codigo"),
                    GetString(rd, "codigociudad"),
                    GetString(rd, "codigo_ciudad_adic"),
                    GetString(rd, "codigo_ciudad_adicional"),
                    GetString(rd, "cod_ciudad_adic"),
                    GetString(rd, "usuco5")),
                Provincia = GetString(rd, "provincia"),
                Pais = GetString(rd, "pais"),

                RepresentanteLegal = FirstNonEmpty(
                    GetString(rd, "representante_legal"),
                    GetString(rd, "nombre_representante_legal"),
                    GetString(rd, "representante")),
                CedulaRepresentante = GetString(rd, "cedula_representante"),
                CorreoRepresentanteTecnico = FirstNonEmpty(
                    GetString(rd, "correo_representante_tecnico"),
                    GetString(rd, "email_representante_tecnico"),
                    GetString(rd, "correo_representante"),
                    GetString(rd, "email_representante"),
                    GetString(rd, "email"),
                    GetString(rd, "correo"),
                    GetString(rd, "correo_electronico")),
                NombreComercial = FirstNonEmpty(
                    GetString(rd, "nombre_comercial"),
                    GetString(rd, "nombre_comercial_compania")),

                TipoOperacion = GetString(rd, "tipo_operacion"),
                DescripcionOperacion = GetString(rd, "descripcion_operacion"),
                ResumenOperacionesEae = FirstNonEmpty(
                    GetString(rd, "resumen_operaciones_eae"),
                    GetString(rd, "resumen_operaciones")),
                NumeroAOC = GetString(rd, "numero_aoc"),
                Observaciones = GetString(rd, "observaciones"),
                AprobacionesEspeciales = GetString(rd, "aprobaciones_especiales"),
                AprobacionesEspecialesOtros = GetString(rd, "aprobaciones_especiales_otros"),
                AeropuertosEcuador = GetString(rd, "aeropuertos_ecuador"),
                AeropuertosEcuadorOtros = GetString(rd, "aeropuertos_ecuador_otros"),
                CompaniasSeleccionadas = FirstNonEmpty(
                    GetString(rd, "companias_seleccionadas"),
                    GetString(rd, "companias_relacionadas")),
                CodigoOaci = FirstNonEmpty(
                    GetString(rd, "codigo_oaci"),
                    GetString(rd, "codigo_oasi"),
                    GetString(rd, "codigo_icao"),
                    GetString(rd, "icao"),
                    GetString(rd, "codigo_oaci_operador"),
                    GetString(rd, "codigo_oasi_operador"),
                    GetString(rd, "codigo_oaci_compania")),

                CodigoUsuario = GetInt(rd, "codigo_usuario"),
                CodigoTecnico = GetNullableInt(rd, "codigo_tecnico"),
                TecnicoResponsableCedula = GetString(rd, "tecnico_responsable_cedula"),
                TecnicoResponsableNombre = GetString(rd, "tecnico_responsable_nombre"),
                TecnicoResponsableTipo = GetString(rd, "tecnico_responsable_tipo"),
                InspectorApoyoCedula = GetString(rd, "inspector_apoyo_cedula"),
                InspectorApoyoNombre = GetString(rd, "inspector_apoyo_nombre"),
                InspectorApoyoTipo = GetString(rd, "inspector_apoyo_tipo"),

                CreatedAt = GetNullableDateTime(rd, "created_at"),
                UpdatedAt = GetNullableDateTime(rd, "updated_at"),
                CreatedBy = GetString(rd, "created_by"),
                UpdatedBy = GetString(rd, "updated_by"),

                DeletedAt = GetNullableDateTime(rd, "deleted_at"),
                DeletedBy = GetString(rd, "deleted_by")
            };
        }

        private static string GetString(IDataRecord rd, string col)
        {
            object value;
            return TryGetValue(rd, col, out value) ? value.ToString() : null;
        }

        private string ObtenerNumeroSolicitudParaNotificacion(NpgsqlConnection cn, int codigoSolicitud)
        {
            if (cn == null || codigoSolicitud <= 0)
            {
                return codigoSolicitud > 0 ? codigoSolicitud.ToString() : string.Empty;
            }

            try
            {
                const string sql = @"
SELECT COALESCE(NULLIF(TRIM(numero_solicitud), ''), codigo_solicitud::text)
FROM aocr_tbsolicitud
WHERE codigo_solicitud = @codigoSolicitud
LIMIT 1;";

                var numero = cn.QueryFirstOrDefault<string>(sql, new { codigoSolicitud = codigoSolicitud });
                return string.IsNullOrWhiteSpace(numero) ? codigoSolicitud.ToString() : numero.Trim();
            }
            catch
            {
                return codigoSolicitud.ToString();
            }
        }

        private static int GetInt(IDataRecord rd, string col)
        {
            object value;
            return TryGetValue(rd, col, out value) ? Convert.ToInt32(value) : 0;
        }

        private static int? GetNullableInt(IDataRecord rd, string col)
        {
            object value;
            return TryGetValue(rd, col, out value) ? (int?)Convert.ToInt32(value) : null;
        }

        private static DateTime? GetNullableDateTime(IDataRecord rd, string col)
        {
            object value;
            return TryGetValue(rd, col, out value) ? (DateTime?)Convert.ToDateTime(value) : null;
        }

        private static bool TryGetValue(IDataRecord rd, string col, out object value)
        {
            value = null;
            if (rd == null || string.IsNullOrWhiteSpace(col))
            {
                return false;
            }

            for (var i = 0; i < rd.FieldCount; i++)
            {
                if (!string.Equals(rd.GetName(i), col, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rd.IsDBNull(i))
                {
                    return false;
                }

                value = rd.GetValue(i);
                return true;
            }

            return false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return null;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static string ResolverEmailSolicitudCanonico(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            return FirstNonEmpty(solicitud.CorreoRepresentanteTecnico, solicitud.Email);
        }

        private static string ResolverCorreoRepresentanteTecnicoCanonico(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            return FirstNonEmpty(solicitud.CorreoRepresentanteTecnico, solicitud.Email);
        }

        private sealed class DocumentoAsignacionDiagnostico
        {
            public int TotalDocumentos { get; set; }
            public int Pendientes { get; set; }
            public int Observados { get; set; }
            public int Aceptados { get; set; }
            public string EstadoDocumental { get; set; }
            public bool PuedeEnviarInspector { get; set; }
            public string MotivoBloqueo { get; set; }
        }

        private static DocumentoAsignacionDiagnostico ObtenerDiagnosticoDocumentosAsignacion(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int codigoSolicitud,
            string estadoDocumental)
        {
            var diagnostico = new DocumentoAsignacionDiagnostico
            {
                EstadoDocumental = estadoDocumental ?? string.Empty,
                PuedeEnviarInspector = true,
                MotivoBloqueo = string.Empty
            };

            var columnas = ObtenerColumnasTabla(cn, "aocr_tbdocumento");
            if (columnas.Count == 0)
            {
                diagnostico.MotivoBloqueo = "Tabla aocr_tbdocumento no disponible para diagnostico documental.";
                return diagnostico;
            }

            if (!columnas.Contains("codigo_solicitud"))
            {
                diagnostico.MotivoBloqueo = "La tabla aocr_tbdocumento no tiene columna codigo_solicitud para vincular documentos.";
                return diagnostico;
            }

            if (!TieneDocumentacionObligatoriaCompleta(cn, tx, codigoSolicitud))
            {
                diagnostico.PuedeEnviarInspector = false;
                diagnostico.MotivoBloqueo = "No se puede asignar inspector porque el RT aun no ha cargado todos los documentos obligatorios de la Solicitud AOCR.";
                return diagnostico;
            }

            var estadoSql = columnas.Contains("estado")
                ? "UPPER(COALESCE(estado, ''))"
                : "''";
            var validadoSql = columnas.Contains("validado")
                ? "COALESCE(validado, FALSE)"
                : "FALSE";
            var where = "codigo_solicitud = @codigo_solicitud";
            if (columnas.Contains("deleted_at"))
            {
                where += " AND deleted_at IS NULL";
            }

            var sql = @"
                SELECT
                    COUNT(*)::int AS total_documentos,
                    COALESCE(SUM(CASE
                        WHEN " + estadoSql + @" LIKE '%PEND%'
                          OR " + estadoSql + @" LIKE '%BORR%'
                          OR " + estadoSql + @" LIKE '%SUBSAN%'
                        THEN 1 ELSE 0 END), 0)::int AS pendientes,
                    COALESCE(SUM(CASE
                        WHEN " + estadoSql + @" LIKE '%OBS%'
                          OR " + estadoSql + @" LIKE '%RECH%'
                          OR " + estadoSql + @" LIKE '%DEVUEL%'
                        THEN 1 ELSE 0 END), 0)::int AS observados,
                    COALESCE(SUM(CASE
                        WHEN " + validadoSql + @" = TRUE
                          OR " + estadoSql + @" LIKE '%VALID%'
                          OR " + estadoSql + @" LIKE '%ACEPT%'
                          OR " + estadoSql + @" LIKE '%APROB%'
                        THEN 1 ELSE 0 END), 0)::int AS aceptados
                FROM aocr_tbdocumento
                WHERE " + where + ";";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        diagnostico.TotalDocumentos = Convert.ToInt32(rd["total_documentos"]);
                        diagnostico.Pendientes = Convert.ToInt32(rd["pendientes"]);
                        diagnostico.Observados = Convert.ToInt32(rd["observados"]);
                        diagnostico.Aceptados = Convert.ToInt32(rd["aceptados"]);
                    }
                }
            }

            if (diagnostico.TotalDocumentos == 0)
            {
                diagnostico.MotivoBloqueo = "Sin documentos vinculados a la solicitud; se mantiene el flujo por estado documental.";
                return diagnostico;
            }

            if (diagnostico.Pendientes > 0 || diagnostico.Observados > 0)
            {
                diagnostico.PuedeEnviarInspector = false;
                diagnostico.MotivoBloqueo = "No se puede enviar a inspeccion porque existen documentos pendientes u observados.";
                return diagnostico;
            }

            return diagnostico;
        }

        private static bool TieneDocumentacionObligatoriaCompleta(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int codigoSolicitud)
        {
            const string sql = @"
                WITH solicitud AS (
                    SELECT COALESCE(tipo_solicitud, 1) AS tipo_solicitud
                    FROM aocr_tbsolicitud
                    WHERE codigo_solicitud = @codigo_solicitud
                      AND deleted_at IS NULL
                ), requeridos(tipo_canonico) AS (
                    VALUES
                        ('FACTURA'), ('AOC'), ('OPSPECS'), ('MANUAL_OPERACIONES'),
                        ('PERMISO_OPERACION'), ('CERTIFICADO_RUIDO'), ('PODER_REPRESENTANTE')
                ), presentes AS (
                    SELECT DISTINCT CASE UPPER(TRIM(COALESCE(d.tipo_documento, '')))
                        WHEN 'COMPROBANTE_PAGO' THEN 'FACTURA'
                        WHEN 'FACTURA' THEN 'FACTURA'
                        WHEN 'FACTURA_PAGO' THEN 'FACTURA'
                        WHEN 'COPIA_AOC_VALIDA' THEN 'AOC'
                        WHEN 'OPSPECS_ESPECIFICACIONES_OPERACIONALES' THEN 'OPSPECS'
                        WHEN 'MANUAL_OPERACIONES' THEN 'MANUAL_OPERACIONES'
                        WHEN 'PERMISO_OPERACION_CNAC' THEN 'PERMISO_OPERACION'
                        WHEN 'CERTIFICADO_RUIDO_AERONAVES_EAE' THEN 'CERTIFICADO_RUIDO'
                        WHEN 'COPIA_CERTIFICADA_PODER_REPRESENTANTE_ECUADOR' THEN 'PODER_REPRESENTANTE'
                        ELSE NULL
                    END AS tipo_canonico
                    FROM aocr_tbdocumento d
                    WHERE d.codigo_solicitud = @codigo_solicitud
                      AND COALESCE(d.tamano_bytes, 0) > 0
                      AND NULLIF(TRIM(COALESCE(d.nombre_archivo, '')), '') IS NOT NULL
                      AND UPPER(TRIM(COALESCE(d.estado, ''))) <> 'ELIMINADO'
                )
                SELECT NOT EXISTS (
                    SELECT 1
                    FROM requeridos r
                    CROSS JOIN solicitud s
                    WHERE NOT (s.tipo_solicitud = 3 AND r.tipo_canonico = 'CERTIFICADO_RUIDO')
                      AND NOT EXISTS (SELECT 1 FROM presentes p WHERE p.tipo_canonico = r.tipo_canonico)
                );";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                var value = cmd.ExecuteScalar();
                return value != null && value != DBNull.Value && Convert.ToBoolean(value);
            }
        }

        private static HashSet<string> ObtenerColumnasTabla(NpgsqlConnection cn, string tabla)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string sql = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = @tabla
                  AND table_schema NOT IN ('pg_catalog', 'information_schema');";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (!rd.IsDBNull(0))
                        {
                            columnas.Add(rd.GetString(0));
                        }
                    }
                }
            }

            return columnas;
        }

        private static string ResolverColumnaCodigoCiudad(HashSet<string> columnas)
        {
            if (columnas == null || columnas.Count == 0)
            {
                return null;
            }

            if (columnas.Contains("cod_ciudad"))
            {
                return "cod_ciudad";
            }

            if (columnas.Contains("codigo_ciudad"))
            {
                return "codigo_ciudad";
            }

            if (columnas.Contains("ciudad_codigo"))
            {
                return "ciudad_codigo";
            }

            if (columnas.Contains("codigociudad"))
            {
                return "codigociudad";
            }

            if (columnas.Contains("codigo_ciudad_adic"))
            {
                return "codigo_ciudad_adic";
            }

            if (columnas.Contains("codigo_ciudad_adicional"))
            {
                return "codigo_ciudad_adicional";
            }

            if (columnas.Contains("cod_ciudad_adic"))
            {
                return "cod_ciudad_adic";
            }

            if (columnas.Contains("usuco5"))
            {
                return "usuco5";
            }

            return null;
        }

        private static bool TieneRecaudacionFinalizada(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int codigoSolicitud,
            IReadOnlyCollection<string> columnasSolicitud,
            out string estadoRecaudacion)
        {
            estadoRecaudacion = null;

            if (SolicitudIndicaRecaudacionHabilitada(cn, tx, codigoSolicitud, columnasSolicitud, out var motivoBypass))
            {
                estadoRecaudacion = motivoBypass;
                return true;
            }

            const string sqlFinalizada = @"
                SELECT COUNT(*)
                FROM aocr_or_orden o
                WHERE COALESCE(o.codigo_solicitud::text, '') = @codigoSolicitud
                  AND UPPER(COALESCE(o.estado, '')) IN ('FACTURADA', 'COMPLETADA', 'PAGADA');";

            using (var cmdFinalizada = new NpgsqlCommand(sqlFinalizada, cn, tx))
            {
                cmdFinalizada.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud.ToString());
                var totalFinalizadas = Convert.ToInt64(cmdFinalizada.ExecuteScalar());
                if (totalFinalizadas > 0)
                {
                    estadoRecaudacion = "PAGADA";
                    return true;
                }
            }

            const string sql = @"
                SELECT UPPER(COALESCE(o.estado, '')) AS estado
                FROM aocr_or_orden o
                WHERE COALESCE(o.codigo_solicitud::text, '') = @codigoSolicitud
                ORDER BY o.id DESC
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud.ToString());
                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    estadoRecaudacion = "SIN_ORDEN";
                    return false;
                }

                estadoRecaudacion = value.ToString();
                return string.Equals(estadoRecaudacion, "FACTURADA", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(estadoRecaudacion, "COMPLETADA", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(estadoRecaudacion, "PAGADA", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool SolicitudIndicaRecaudacionHabilitada(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int codigoSolicitud,
            IReadOnlyCollection<string> columnasSolicitud,
            out string motivo)
        {
            motivo = null;
            var cols = new HashSet<string>(columnasSolicitud ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var selects = new List<string>();

            if (cols.Contains("pago_aprobado"))
            {
                selects.Add("COALESCE(pago_aprobado, FALSE) AS pago_aprobado");
            }

            if (cols.Contains("solicitud_finalizada_rt"))
            {
                selects.Add("COALESCE(solicitud_finalizada_rt, FALSE) AS solicitud_finalizada_rt");
            }

            if (cols.Contains("pendiente_asignacion_inspector"))
            {
                selects.Add("COALESCE(pendiente_asignacion_inspector, FALSE) AS pendiente_asignacion_inspector");
            }

            if (selects.Count == 0)
            {
                return false;
            }

            var sql = "SELECT " + string.Join(", ", selects) + " FROM aocr_tbsolicitud WHERE codigo_solicitud = @id";
            if (cols.Contains("deleted_at"))
            {
                sql += " AND deleted_at IS NULL";
            }

            sql += " LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                    {
                        return false;
                    }

                    if (cols.Contains("pago_aprobado")
                        && rd["pago_aprobado"] != DBNull.Value
                        && Convert.ToBoolean(rd["pago_aprobado"]))
                    {
                        motivo = "PAGO_APROBADO";
                        return true;
                    }

                    if (cols.Contains("solicitud_finalizada_rt")
                        && rd["solicitud_finalizada_rt"] != DBNull.Value
                        && Convert.ToBoolean(rd["solicitud_finalizada_rt"]))
                    {
                        motivo = "SOLICITUD_FINALIZADA_RT";
                        return true;
                    }

                    if (cols.Contains("pendiente_asignacion_inspector")
                        && rd["pendiente_asignacion_inspector"] != DBNull.Value
                        && Convert.ToBoolean(rd["pendiente_asignacion_inspector"]))
                    {
                        motivo = "PENDIENTE_ASIGNACION";
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizarTipoInspector(string tipoInspector)
        {
            if (string.IsNullOrWhiteSpace(tipoInspector))
            {
                return null;
            }

            var value = tipoInspector.Trim().ToUpperInvariant();
            if (value == "OPS" || value == "AIR")
            {
                return value;
            }

            return null;
        }

        private static int? ParseIntSafe(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? (int?)parsed : null;
        }

        private static InspeccionExistenteInfo ObtenerUltimaInspeccionPorSolicitud(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int codigoSolicitud)
        {
            const string sql = @"
                SELECT codigo_inspeccion, numero_inspeccion, estado
                FROM aocr_tbinspeccion
                WHERE codigo_solicitud = @codigoSolicitud
                ORDER BY codigo_inspeccion DESC
                LIMIT 1
                FOR UPDATE;";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                    {
                        return null;
                    }

                    return new InspeccionExistenteInfo
                    {
                        CodigoInspeccion = rd["codigo_inspeccion"] != DBNull.Value ? Convert.ToInt32(rd["codigo_inspeccion"]) : 0,
                        NumeroInspeccion = rd["numero_inspeccion"] != DBNull.Value ? rd["numero_inspeccion"].ToString() : null,
                        Estado = rd["estado"] != DBNull.Value ? rd["estado"].ToString() : null
                    };
                }
            }
        }

        private static bool PermiteReasignacion(string estadoInspeccion)
        {
            var estadoNormalizado = EstadosInspeccion.NormalizarEstado(estadoInspeccion);
            return string.Equals(estadoNormalizado, EstadosInspeccion.SOLICITUD_INSPECCION_CREADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.VERIFICACION_SOLICITUD, StringComparison.OrdinalIgnoreCase);
        }

        private static string ConstruirComentarioAsignacion(string observaciones, UsuarioInternoRTRegistro principal, UsuarioInternoRTRegistro apoyo)
        {
            var comentarios = string.IsNullOrWhiteSpace(observaciones)
                ? string.Empty
                : observaciones.Trim();

            var principalTexto = "Inspector principal: " + (principal != null ? ((principal.NombreVisual ?? principal.UsuarioLogin) ?? string.Empty).Trim() : string.Empty);
            var apoyoTexto = apoyo == null
                ? string.Empty
                : " | Inspector apoyo: " + (((apoyo.NombreVisual ?? apoyo.UsuarioLogin) ?? string.Empty).Trim());

            if (string.IsNullOrWhiteSpace(comentarios))
            {
                return principalTexto + apoyoTexto;
            }

            return comentarios + " | " + principalTexto + apoyoTexto;
        }

        private string GenerarNumeroInspeccionUnico(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int codigoSolicitud,
            HashSet<string> columnasInspeccion)
        {
            if (columnasInspeccion == null || !columnasInspeccion.Contains("numero_inspeccion"))
            {
                return null;
            }

            var referenciaSolicitud = ObtenerReferenciaSolicitudParaInspeccion(cn, codigoSolicitud);
            var baseNumero = ConstruirNumeroInspeccionBase(referenciaSolicitud, DateTime.Now);
            var candidato = baseNumero;

            for (var intento = 0; intento < 20; intento++)
            {
                using (var cmd = new NpgsqlCommand("SELECT 1 FROM aocr_tbinspeccion WHERE numero_inspeccion=@numero LIMIT 1;", cn, tx))
                {
                    cmd.Parameters.AddWithValue("@numero", candidato);
                    var existe = cmd.ExecuteScalar();
                    if (existe == null || existe == DBNull.Value)
                    {
                        return candidato;
                    }
                }

                candidato = baseNumero + "-" + (intento + 2).ToString("D2");
            }

            return baseNumero + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }

        private string ObtenerReferenciaSolicitudParaInspeccion(NpgsqlConnection cn, int codigoSolicitud)
        {
            return CompactarNumeroSolicitudParaInspeccion(
                ObtenerNumeroSolicitudParaNotificacion(cn, codigoSolicitud),
                codigoSolicitud);
        }

        private static string CompactarNumeroSolicitudParaInspeccion(string numeroSolicitud, int codigoSolicitud)
        {
            var normalizado = string.IsNullOrWhiteSpace(numeroSolicitud)
                ? string.Empty
                : Regex.Replace(numeroSolicitud.Trim().ToUpperInvariant(), @"\s+", string.Empty);

            var coincidencia = !string.IsNullOrWhiteSpace(normalizado)
                ? Regex.Match(normalizado, @"AOCR\d+")
                : Match.Empty;

            if (coincidencia.Success)
            {
                return coincidencia.Value;
            }

            return codigoSolicitud > 0 ? "AOCR" + codigoSolicitud.ToString() : "AOCR";
        }

        private static string ConstruirNumeroInspeccionBase(string referenciaSolicitud, DateTime fechaReferencia)
        {
            var referenciaNormalizada = string.IsNullOrWhiteSpace(referenciaSolicitud)
                ? "AOCR"
                : Regex.Replace(referenciaSolicitud.Trim().ToUpperInvariant(), @"[^A-Z0-9]+", string.Empty);

            return "DGAC-INS-" + fechaReferencia.Year + "-" + referenciaNormalizada;
        }

        private static string ResolverEstadoInspeccionPersistencia(NpgsqlConnection cn, string estadoDeseado)
        {
            var estadoCanonico = EstadosInspeccion.NormalizarEstado(estadoDeseado);
            var permitidos = ObtenerEstadosInspeccionPermitidos(cn);

            if (permitidos.Count == 0)
            {
                return estadoCanonico;
            }

            var estadoCanonicoPermitido = permitidos.FirstOrDefault(v =>
                string.Equals(v, estadoCanonico, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(estadoCanonicoPermitido))
            {
                return estadoCanonicoPermitido;
            }

            var estadoCore = EstadosInspeccion.MapearEstadoCoreCompat(estadoCanonico);
            var estadoCorePermitido = permitidos.FirstOrDefault(v =>
                string.Equals(v, estadoCore, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(estadoCorePermitido))
            {
                return estadoCorePermitido;
            }

            var alias = permitidos.FirstOrDefault(v =>
                string.Equals(EstadosInspeccion.NormalizarEstado(v), estadoCanonico, StringComparison.OrdinalIgnoreCase));

            return !string.IsNullOrWhiteSpace(alias) ? alias : permitidos[0];
        }

        private static List<string> ObtenerEstadosInspeccionPermitidos(NpgsqlConnection cn)
        {
            const string sql = @"
                SELECT pg_get_constraintdef(c.oid)
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = 'public'
                  AND t.relname = 'aocr_tbinspeccion'
                  AND c.contype = 'c'
                  AND (
                        c.conname = 'chk_estado_inspeccion'
                        OR c.conname = 'chk_aocr_tbinspeccion_estado'
                        OR pg_get_constraintdef(c.oid) ILIKE '%estado%'
                      )
                ORDER BY CASE
                    WHEN c.conname = 'chk_estado_inspeccion' THEN 0
                    WHEN c.conname = 'chk_aocr_tbinspeccion_estado' THEN 1
                    ELSE 2
                END
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                var def = cmd.ExecuteScalar() as string;
                if (string.IsNullOrWhiteSpace(def))
                {
                    return new List<string>();
                }

                var values = new List<string>();
                foreach (Match match in Regex.Matches(def, "'([^']+)'"))
                {
                    var value = match.Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (values.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    values.Add(value.Trim());
                }

                return values;
            }
        }
    }
}
