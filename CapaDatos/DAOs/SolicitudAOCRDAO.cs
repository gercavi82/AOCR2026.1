using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaDatos.Constants;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class SolicitudAOCRDAO
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        // ============================
        // LISTADOS
        // ============================
        public List<SolicitudAOCR> ListarActivas() => ObtenerPorFiltro("deleted_at IS NULL");
        public List<SolicitudAOCR> ObtenerTodos() => ObtenerPorFiltro("1=1");

        public List<SolicitudAOCR> ObtenerPorUsuario(int codigoUsuario)
        {
            return ObtenerPorFiltro(
                "codigo_usuario = @u AND deleted_at IS NULL",
                cmd => cmd.Parameters.AddWithValue("@u", codigoUsuario)
            );
        }

        public List<SolicitudAOCR> ObtenerPorEstado(string estado)
        {
            return ObtenerPorFiltro(
                "estado = @e AND deleted_at IS NULL",
                cmd => cmd.Parameters.AddWithValue("@e", estado ?? "")
            );
        }

        // Múltiples estados a la vez
        public List<SolicitudAOCR> ObtenerPorEstados(params string[] estados)
        {
            if (estados == null || estados.Length == 0)
                return ObtenerTodos();

            var placeholders = new List<string>();
            for (int i = 0; i < estados.Length; i++)
                placeholders.Add($"@e{i}");

            string where = $"estado = ANY (ARRAY[{string.Join(",", placeholders)}]) AND deleted_at IS NULL";

            return ObtenerPorFiltro(where, cmd =>
            {
                for (int i = 0; i < estados.Length; i++)
                    cmd.Parameters.AddWithValue($"@e{i}", estados[i] ?? string.Empty);
            });
        }

        public List<SolicitudAOCR> ObtenerPendientesRevision() => ObtenerPorEstado("ENVIADO_A_INSPECTOR");

        public List<SolicitudAOCR> ObtenerParaValidacionJefatura()
        {
            return ObtenerPorFiltro(
                "estado = @e AND deleted_at IS NULL",
                cmd => cmd.Parameters.AddWithValue("@e", "ENVIADO_A_JEFATURA")
            );
        }

        public List<SolicitudAOCR> ObtenerPendientesAsignacion()
        {
            // Obtener solicitudes aprobadas o en estado de inspección que aún no tienen inspector asignado
            string sql = @"
                SELECT s.* 
                FROM aocr_tbsolicitud s
                LEFT JOIN aocr_tbinspeccion i ON s.codigo_solicitud = i.codigo_solicitud
                WHERE s.estado IN ('APROBADA', 'INSPECCION_SOLICITADA') 
                  AND s.deleted_at IS NULL
                  AND i.codigo_inspeccion IS NULL
                ORDER BY s.fecha_solicitud DESC";

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var rd = cmd.ExecuteReader())
                {
                    var lista = new List<SolicitudAOCR>();
                    while (rd.Read()) lista.Add(Mapear(rd));
                    return lista;
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
                        return rd.Read() ? Mapear(rd) : null;
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
                var columnas = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                var columnaCodCiudad = ResolverColumnaCodigoCiudad(columnas);

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

                var sql = $@"
        INSERT INTO aocr_tbsolicitud (
            {string.Join("," + Environment.NewLine + "            ", columnasInsert)}
        ) VALUES (
            {string.Join("," + Environment.NewLine + "            ", valoresInsert)}
        ) RETURNING codigo_solicitud";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@NumeroSolicitud", (object)(solicitud.NumeroSolicitud ?? ""));
                    cmd.Parameters.AddWithValue("@FechaSolicitud", (object)solicitud.FechaSolicitud ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TipoSolicitud", (object)(solicitud.TipoSolicitud ?? 1));
                    cmd.Parameters.AddWithValue("@NombreOperador", (object)(solicitud.NombreOperador ?? ""));
                    cmd.Parameters.AddWithValue("@Ruc", (object)(solicitud.Ruc ?? ""));
                    cmd.Parameters.AddWithValue("@RazonSocial", (object)(solicitud.RazonSocial ?? ""));

                    cmd.Parameters.AddWithValue("@Email", (object)(solicitud.Email ?? ""));
                    cmd.Parameters.AddWithValue("@Telefono", (object)(solicitud.Telefono ?? ""));
                    cmd.Parameters.AddWithValue("@Direccion", (object)(solicitud.Direccion ?? ""));
                    cmd.Parameters.AddWithValue("@Ciudad", (object)(solicitud.Ciudad ?? ""));
                    cmd.Parameters.AddWithValue("@Provincia", (object)(solicitud.Provincia ?? ""));
                    cmd.Parameters.AddWithValue("@Pais", (object)(solicitud.Pais ?? ""));
                    cmd.Parameters.AddWithValue("@CodCiudad", (object)(solicitud.CodCiudad ?? ""));
                    cmd.Parameters.AddWithValue("@RepresentanteLegal", (object)(solicitud.RepresentanteLegal ?? ""));
                    cmd.Parameters.AddWithValue("@CedulaRepresentante", (object)(solicitud.CedulaRepresentante ?? ""));
                    cmd.Parameters.AddWithValue("@CorreoRepresentanteTecnico", (object)(solicitud.CorreoRepresentanteTecnico ?? ""));
                    cmd.Parameters.AddWithValue("@NombreComercial", (object)(solicitud.NombreComercial ?? ""));

                    cmd.Parameters.AddWithValue("@TipoOperacion", (object)(solicitud.TipoOperacion ?? ""));
                    cmd.Parameters.AddWithValue("@DescripcionOperacion", (object)(solicitud.DescripcionOperacion ?? ""));
                    cmd.Parameters.AddWithValue("@Observaciones", (object)(solicitud.Observaciones ?? ""));
                    cmd.Parameters.AddWithValue("@ResumenOperacionesEae", (object)(solicitud.ResumenOperacionesEae ?? ""));
                    cmd.Parameters.AddWithValue("@AprobacionesEspeciales", (object)(solicitud.AprobacionesEspeciales ?? ""));
                    cmd.Parameters.AddWithValue("@AprobacionesEspecialesOtros", (object)(solicitud.AprobacionesEspecialesOtros ?? ""));
                    cmd.Parameters.AddWithValue("@AeropuertosEcuador", (object)(solicitud.AeropuertosEcuador ?? ""));
                    cmd.Parameters.AddWithValue("@AeropuertosEcuadorOtros", (object)(solicitud.AeropuertosEcuadorOtros ?? ""));
                    cmd.Parameters.AddWithValue("@CompaniasSeleccionadas", (object)(solicitud.CompaniasSeleccionadas ?? ""));

                    var estadoNormalizado = EstadoSolicitud.Normalizar(solicitud.Estado);
                    cmd.Parameters.AddWithValue("@Estado", estadoNormalizado);

                    cmd.Parameters.AddWithValue("@CodigoUsuario", solicitud.CodigoUsuario);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ============================
        // ACTUALIZAR (COMPLETO)
        // ============================
        public bool ActualizarGeneral(SolicitudAOCR s)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var columnas = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                var columnaCodCiudad = ResolverColumnaCodigoCiudad(columnas);
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

                    cmd.Parameters.AddWithValue("@email", (object)(s.Email ?? ""));
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
                        cmd.Parameters.AddWithValue("@correo_representante_tecnico", (object)(s.CorreoRepresentanteTecnico ?? ""));
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
                    cmd.Parameters.AddWithValue("@observaciones", (object)(s.Observaciones ?? ""));

                    cmd.Parameters.AddWithValue("@codigo_tecnico", (object)s.CodigoTecnico ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated_by", (object)(s.UpdatedBy ?? "sistema"));

                    return cmd.ExecuteNonQuery() > 0;
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
        // ASIGNAR INSPECTORES (CORREGIDO A ESQUEMA REAL)
        // ✅ aocr_tbinspeccion tiene codigo_inspector, fecha_programada, hora_programada, comentarios/observaciones
        // ============================
        public bool AsignarInspectores(int codigoSolicitud, int inspectorPrincipal, int? inspectorApoyo,
            DateTime fecha, string obs, out string mensaje)
        {
            try
            {
                using (var cn = new NpgsqlConnection(ConnectionString))
                {
                    cn.Open();

                    // 1) Crear/actualizar una inspección "programada" para la solicitud
                    // (si tú ya manejas varias inspecciones, aquí se puede ajustar)
                    const string sql = @"
INSERT INTO aocr_tbinspeccion (codigo_solicitud, tipo, fecha_programada, codigo_inspector, comentarios, estado, created_at, created_by)
VALUES (@sol, 1, @fecha, @insp, @obs, 'PROGRAMADA', NOW(), @usr)
RETURNING codigo_inspeccion;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@sol", codigoSolicitud);
                        cmd.Parameters.AddWithValue("@fecha", fecha.Date);
                        cmd.Parameters.AddWithValue("@insp", inspectorPrincipal);
                        cmd.Parameters.AddWithValue("@obs", obs ?? "");
                        cmd.Parameters.AddWithValue("@usr", "sistema");

                        var idInspeccion = Convert.ToInt32(cmd.ExecuteScalar());

                        // 2) Cambiar estado de la solicitud
                        CambiarEstado(codigoSolicitud, "INSPECCION_ASIGNADA", inspectorPrincipal, "Inspección programada. Inspección #" + idInspeccion);

                        mensaje = "Asignación realizada con éxito. Inspección creada: " + idInspeccion;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
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

                NombreOperador = GetString(rd, "nombre_operador"),
                Ruc = GetString(rd, "ruc"),
                RazonSocial = GetString(rd, "razon_social"),

                Email = GetString(rd, "email"),
                Telefono = GetString(rd, "telefono"),
                Direccion = GetString(rd, "direccion"),
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

                RepresentanteLegal = GetString(rd, "representante_legal"),
                CedulaRepresentante = GetString(rd, "cedula_representante"),
                CorreoRepresentanteTecnico = FirstNonEmpty(
                    GetString(rd, "correo_representante_tecnico"),
                    GetString(rd, "email_representante_tecnico")),
                NombreComercial = FirstNonEmpty(
                    GetString(rd, "nombre_comercial"),
                    GetString(rd, "nombre_comercial_compania")),

                TipoOperacion = GetString(rd, "tipo_operacion"),
                DescripcionOperacion = GetString(rd, "descripcion_operacion"),
                ResumenOperacionesEae = FirstNonEmpty(
                    GetString(rd, "resumen_operaciones_eae"),
                    GetString(rd, "resumen_operaciones")),
                Observaciones = GetString(rd, "observaciones"),
                AprobacionesEspeciales = GetString(rd, "aprobaciones_especiales"),
                AprobacionesEspecialesOtros = GetString(rd, "aprobaciones_especiales_otros"),
                AeropuertosEcuador = GetString(rd, "aeropuertos_ecuador"),
                AeropuertosEcuadorOtros = GetString(rd, "aeropuertos_ecuador_otros"),
                CompaniasSeleccionadas = FirstNonEmpty(
                    GetString(rd, "companias_seleccionadas"),
                    GetString(rd, "companias_relacionadas")),

                CodigoUsuario = GetInt(rd, "codigo_usuario"),
                CodigoTecnico = GetNullableInt(rd, "codigo_tecnico"),

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
    }
}
