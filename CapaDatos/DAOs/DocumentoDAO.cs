using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using CapaModelo;
using CapaDatos.Constants;

namespace CapaDatos.DAOs
{
    public class DocumentoDAO
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        /// <summary>
        /// Crea una nueva versión documental, conserva la anterior y registra su
        /// relación con la NC SIN_INSPECCION en una sola transacción PostgreSQL.
        /// Requiere la migración 015_gate2_subsanacion_individual_nc.sql.
        /// </summary>
        public int CrearVersionSubsanadaNc(
            Documento nuevaVersion,
            int codigoDocumentoAnterior,
            int codigoNoConformidad,
            int codigoUsuario,
            string observacionOrigen,
            string hashSha256,
            string correlationId)
        {
            if (nuevaVersion == null) throw new ArgumentNullException(nameof(nuevaVersion));
            if (codigoDocumentoAnterior <= 0) throw new ArgumentOutOfRangeException(nameof(codigoDocumentoAnterior));
            if (codigoNoConformidad <= 0) throw new ArgumentOutOfRangeException(nameof(codigoNoConformidad));
            if (codigoUsuario <= 0) throw new ArgumentOutOfRangeException(nameof(codigoUsuario));
            if (string.IsNullOrWhiteSpace(hashSha256) || hashSha256.Length != 64)
                throw new ArgumentException("El hash SHA-256 es obligatorio.", nameof(hashSha256));

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        int versionAnterior;
                        string estadoAnterior;
                        int solicitudAnterior;
                        using (var lockCmd = new NpgsqlCommand(@"
                            SELECT codigo_solicitud, COALESCE(version, 1), COALESCE(estado, '')
                            FROM aocr_tbdocumento
                            WHERE codigo_documento=@documento
                            FOR UPDATE;", cn, tx))
                        {
                            lockCmd.Parameters.AddWithValue("@documento", codigoDocumentoAnterior);
                            using (var rd = lockCmd.ExecuteReader())
                            {
                                if (!rd.Read()) throw new InvalidOperationException("El documento anterior no existe.");
                                solicitudAnterior = rd.GetInt32(0);
                                versionAnterior = rd.GetInt32(1);
                                estadoAnterior = rd.GetString(2);
                            }
                        }

                        if (solicitudAnterior != nuevaVersion.CodigoSolicitud)
                            throw new InvalidOperationException("El documento no pertenece a la solicitud indicada.");
                        if (string.Equals(estadoAnterior, "ACEPTADO", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(estadoAnterior, "APROBADO", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(estadoAnterior, "ACEPTADO_SUBSANACION", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Un documento aceptado no puede ser reemplazado.");

                        using (var ncCmd = new NpgsqlCommand(@"
                            SELECT 1 FROM aocr_tbnoconformidad
                            WHERE codigo_no_conformidad=@nc
                              AND codigo_solicitud=@solicitud
                              AND UPPER(tipo_ruta)='SIN_INSPECCION'
                              AND estado IN ('FIRMADA_COORDINADOR','EN_SUBSANACION','SUBSANACION_DEVUELTA')
                            FOR UPDATE;", cn, tx))
                        {
                            ncCmd.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            ncCmd.Parameters.AddWithValue("@solicitud", nuevaVersion.CodigoSolicitud);
                            if (ncCmd.ExecuteScalar() == null)
                                throw new InvalidOperationException("La NC no habilita subsanación documental individual.");
                        }

                        nuevaVersion.Version = versionAnterior + 1;
                        var estadoNuevo = ResolverEstadoParaInsercion(cn, nuevaVersion.Estado);
                        int codigoNuevo;
                        using (var insert = new NpgsqlCommand(@"
                            INSERT INTO aocr_tbdocumento
                            (codigo_solicitud,tipo_documento,nombre_archivo,ruta_guardada,tipo,extension,tamano_bytes,
                             estado,validado,fecha_carga,observaciones,version,created_at,created_by,
                             nombre_original,nombre_visible,nombre_fisico)
                            VALUES
                            (@solicitud,@tipo_documento,@nombre,@ruta,'ARCHIVO',@extension,@tamano,
                             @estado,FALSE,@fecha,@observaciones,@version,NOW(),@usuario,
                             @nombre_original,@nombre_visible,@nombre_fisico)
                            RETURNING codigo_documento;", cn, tx))
                        {
                            insert.Parameters.AddWithValue("@solicitud", nuevaVersion.CodigoSolicitud);
                            insert.Parameters.AddWithValue("@tipo_documento", (object)nuevaVersion.TipoDocumento ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@nombre", (object)nuevaVersion.NombreArchivo ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@ruta", (object)nuevaVersion.RutaGuardada ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@extension", (object)nuevaVersion.Extension ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@tamano", (object)nuevaVersion.TamanoBytes ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@estado", (object)estadoNuevo ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@fecha", (object)nuevaVersion.FechaCarga ?? DateTime.Now);
                            insert.Parameters.AddWithValue("@observaciones", (object)nuevaVersion.Observaciones ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@version", nuevaVersion.Version.Value);
                            insert.Parameters.AddWithValue("@usuario", (object)nuevaVersion.UsuarioRegistro ?? "sistema");
                            insert.Parameters.AddWithValue("@nombre_original", (object)(nuevaVersion.NombreArchivoOriginal ?? nuevaVersion.NombreArchivo) ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@nombre_visible", (object)(nuevaVersion.NombreArchivoVisible ?? nuevaVersion.NombreArchivo) ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@nombre_fisico", (object)(nuevaVersion.NombreArchivoFisico ?? nuevaVersion.NombreArchivoGuardado) ?? DBNull.Value);
                            codigoNuevo = Convert.ToInt32(insert.ExecuteScalar());
                        }

                        using (var update = new NpgsqlCommand(@"
                            UPDATE aocr_tbdocumento
                            SET estado=@estado, updated_at=NOW(), updated_by=@usuario
                            WHERE codigo_documento=@documento;", cn, tx))
                        {
                            update.Parameters.AddWithValue("@estado", EstadoDocumentoInstitucional.ResolverEstadoVersionAnterior());
                            update.Parameters.AddWithValue("@usuario", (object)nuevaVersion.UsuarioRegistro ?? "sistema");
                            update.Parameters.AddWithValue("@documento", codigoDocumentoAnterior);
                            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("No se pudo conservar la versión anterior.");
                        }

                        using (var trace = new NpgsqlCommand(@"
                            INSERT INTO aocr_tbdocumento_subsanacion
                            (codigo_subsanacion,nombre_archivo,ruta_archivo,tipo_documento,tamanio_bytes,fecha_carga,
                             codigo_usuario_carga,codigo_no_conformidad,codigo_documento_origen,
                             codigo_documento_nueva_version,version_anterior,version_nueva,observacion_origen,
                             hash_sha256,correlation_id)
                            VALUES
                            (NULL,@nombre,@ruta,@tipo,@tamano,NOW(),@usuario,@nc,@anterior,@nuevo,
                             @version_anterior,@version_nueva,@observacion,@hash,@correlation);", cn, tx))
                        {
                            trace.Parameters.AddWithValue("@nombre", nuevaVersion.NombreArchivoOriginal ?? nuevaVersion.NombreArchivo);
                            trace.Parameters.AddWithValue("@ruta", nuevaVersion.RutaGuardada);
                            trace.Parameters.AddWithValue("@tipo", (object)nuevaVersion.TipoDocumento ?? DBNull.Value);
                            trace.Parameters.AddWithValue("@tamano", (object)nuevaVersion.TamanoBytes ?? DBNull.Value);
                            trace.Parameters.AddWithValue("@usuario", codigoUsuario);
                            trace.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            trace.Parameters.AddWithValue("@anterior", codigoDocumentoAnterior);
                            trace.Parameters.AddWithValue("@nuevo", codigoNuevo);
                            trace.Parameters.AddWithValue("@version_anterior", versionAnterior);
                            trace.Parameters.AddWithValue("@version_nueva", nuevaVersion.Version.Value);
                            trace.Parameters.AddWithValue("@observacion", (object)observacionOrigen ?? DBNull.Value);
                            trace.Parameters.AddWithValue("@hash", hashSha256.ToLowerInvariant());
                            trace.Parameters.AddWithValue("@correlation", (object)correlationId ?? DBNull.Value);
                            trace.ExecuteNonQuery();
                        }

                        tx.Commit();
                        nuevaVersion.CodigoDocumento = codigoNuevo;
                        return codigoNuevo;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        // =========================================================
        // INSERTAR DOCUMENTO
        // =========================================================
        public int Crear(Documento doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var estadosIntento = ConstruirEstadosIntento(cn, doc.Estado);
                var tieneCreatedAt = ExisteColumna(cn, "aocr_tbdocumento", "created_at");
                var columnaUsuario = ExisteColumna(cn, "aocr_tbdocumento", "created_by")
                    ? "created_by"
                    : (ExisteColumna(cn, "aocr_tbdocumento", "usuario_registro") ? "usuario_registro" : string.Empty);
                var tieneNombreOriginal = ExisteColumna(cn, "aocr_tbdocumento", "nombre_original");
                var tieneNombreVisible = ExisteColumna(cn, "aocr_tbdocumento", "nombre_visible");
                var tieneNombreFisico = ExisteColumna(cn, "aocr_tbdocumento", "nombre_fisico");

                var columnas = new List<string>
                {
                    "codigo_solicitud",
                    "tipo_documento",
                    "nombre_archivo",
                    "ruta_guardada",
                    "tipo",
                    "extension",
                    "tamano_bytes",
                    "estado",
                    "validado",
                    "fecha_carga",
                    "observaciones",
                    "version"
                };

                var valores = new List<string>
                {
                    "@codigo_solicitud",
                    "@tipo_documento",
                    "@nombre_archivo",
                    "@ruta_guardada",
                    "@tipo",
                    "@extension",
                    "@tamano_bytes",
                    "@estado",
                    "@validado",
                    "@fecha_carga",
                    "@observaciones",
                    "@version"
                };

                if (tieneCreatedAt)
                {
                    columnas.Add("created_at");
                    valores.Add("NOW()");
                }

                if (!string.IsNullOrWhiteSpace(columnaUsuario))
                {
                    columnas.Add(columnaUsuario);
                    valores.Add("@created_by");
                }

                if (tieneNombreOriginal)
                {
                    columnas.Add("nombre_original");
                    valores.Add("@nombre_original");
                }

                if (tieneNombreVisible)
                {
                    columnas.Add("nombre_visible");
                    valores.Add("@nombre_visible");
                }

                if (tieneNombreFisico)
                {
                    columnas.Add("nombre_fisico");
                    valores.Add("@nombre_fisico");
                }

                string sql = $@"
                    INSERT INTO aocr_tbdocumento
                    ({string.Join(", ", columnas)})
                    VALUES
                    ({string.Join(", ", valores)})
                    RETURNING codigo_documento;";

                for (var i = 0; i < estadosIntento.Count; i++)
                {
                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@codigo_solicitud", doc.CodigoSolicitud);

                        cmd.Parameters.AddWithValue("@tipo_documento", (object)doc.TipoDocumento ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@nombre_archivo", (object)doc.NombreArchivo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ruta_guardada", (object)doc.RutaGuardada ?? DBNull.Value);

                        // Columna "tipo" existe en tu tabla; si no la usas, mandamos "ARCHIVO"
                        cmd.Parameters.AddWithValue("@tipo", (object)"ARCHIVO");

                        cmd.Parameters.AddWithValue("@extension", (object)doc.Extension ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@tamano_bytes", (object)doc.TamanoBytes ?? DBNull.Value);

                        var estado = estadosIntento[i];
                        var pEstado = new NpgsqlParameter("@estado", NpgsqlDbType.Varchar);
                        pEstado.Value = (object)estado ?? DBNull.Value;
                        cmd.Parameters.Add(pEstado);
                        cmd.Parameters.AddWithValue("@validado", (object)doc.Validado ?? false);

                        cmd.Parameters.AddWithValue("@fecha_carga", (object)doc.FechaCarga ?? DateTime.Now);
                        cmd.Parameters.AddWithValue("@observaciones", (object)doc.Observaciones ?? DBNull.Value);

                        cmd.Parameters.AddWithValue("@version", (object)doc.Version ?? DBNull.Value);

                        // Tu propiedad UsuarioRegistro se guarda en created_by
                        if (!string.IsNullOrWhiteSpace(columnaUsuario))
                        {
                            cmd.Parameters.AddWithValue("@created_by", (object)doc.UsuarioRegistro ?? "sistema");
                        }

                        if (tieneNombreOriginal)
                        {
                            cmd.Parameters.AddWithValue("@nombre_original", (object)(doc.NombreArchivoOriginal ?? doc.NombreArchivo) ?? DBNull.Value);
                        }

                        if (tieneNombreVisible)
                        {
                            cmd.Parameters.AddWithValue("@nombre_visible", (object)(doc.NombreArchivoVisible ?? doc.NombreArchivoOriginal ?? doc.NombreArchivo) ?? DBNull.Value);
                        }

                        if (tieneNombreFisico)
                        {
                            cmd.Parameters.AddWithValue("@nombre_fisico", (object)(doc.NombreArchivoFisico ?? doc.NombreArchivoGuardado) ?? DBNull.Value);
                        }

                        try
                        {
                            return Convert.ToInt32(cmd.ExecuteScalar());
                        }
                        catch (PostgresException ex) when (ex.SqlState == "23514" &&
                                                           i < estadosIntento.Count - 1)
                        {
                            // Reintentamos con el siguiente estado candidato ante cualquier CHECK de insercion.
                            continue;
                        }
                    }
                }

                throw new InvalidOperationException(
                    "No se pudo insertar documento con ningun estado compatible para chk_estado_documento. " +
                    "Estados intentados: " + string.Join(", ", estadosIntento.Select(x => x ?? "<NULL>")));
            }
        }

        private static List<string> ConstruirEstadosIntento(NpgsqlConnection cn, string estadoSolicitado)
        {
            var candidatos = new List<string>();

            var estadoResuelto = ResolverEstadoParaInsercion(cn, estadoSolicitado);
            if (!string.IsNullOrWhiteSpace(estadoResuelto))
            {
                candidatos.Add(estadoResuelto);
            }

            var permitidos = ObtenerEstadosPermitidos(cn);
            foreach (var estado in permitidos)
            {
                if (!candidatos.Any(x => string.Equals(x, estado, StringComparison.OrdinalIgnoreCase)))
                {
                    candidatos.Add(estado);
                }
            }

            foreach (var fallback in new[] { "Cargado", "CARGADO", "PENDIENTE", "REGISTRADO", "BORRADOR", "ACTIVO", "A" })
            {
                if (permitidos.Count == 0 &&
                    !candidatos.Any(x => string.Equals(x, fallback, StringComparison.OrdinalIgnoreCase)))
                {
                    candidatos.Add(fallback);
                }
            }

            if (ColumnaEstadoPermiteNull(cn))
            {
                candidatos.Add(null);
            }

            return candidatos;
        }

        private static string ResolverEstadoParaInsercion(NpgsqlConnection cn, string estadoSolicitado)
        {
            var estado = (estadoSolicitado ?? string.Empty).Trim();
            if (string.Equals(estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
            {
                estado = "Cargado";
            }
            else if (string.Equals(estado, "ELIMINADO", StringComparison.OrdinalIgnoreCase))
            {
                estado = "Rechazado";
            }

            var permitidos = ObtenerEstadosPermitidos(cn);

            if (permitidos.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(estado) || string.Equals(estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
                {
                    return "Cargado";
                }

                return estado;
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                var exacto = permitidos.FirstOrDefault(x => string.Equals(x, estado, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exacto))
                {
                    return exacto;
                }
            }

            foreach (var preferido in new[] { "Cargado", "En Revisión", "Aprobado", "Rechazado", "Subsanado", "PENDIENTE", "REGISTRADO", "BORRADOR" })
            {
                var encontrado = permitidos.FirstOrDefault(x => string.Equals(x, preferido, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(encontrado))
                {
                    return encontrado;
                }
            }

            return permitidos[0];
        }

        private static List<string> ObtenerEstadosPermitidos(NpgsqlConnection cn)
        {
            const string sql = @"
                SELECT pg_get_constraintdef(c.oid)
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = 'public'
                  AND t.relname = 'aocr_tbdocumento'
                  AND c.contype = 'c'
                  AND (
                        c.conname = 'chk_estado_documento'
                        OR pg_get_constraintdef(c.oid) ILIKE '%estado%'
                      )
                ORDER BY CASE WHEN c.conname = 'chk_estado_documento' THEN 0 ELSE 1 END
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                var def = cmd.ExecuteScalar() as string;
                if (string.IsNullOrWhiteSpace(def))
                {
                    return new List<string>();
                }

                var valores = Regex.Matches(def, "'([^']+)'")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return valores;
            }
        }

        private static bool ColumnaEstadoPermiteNull(NpgsqlConnection cn)
        {
            const string sql = @"
                SELECT is_nullable
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'aocr_tbdocumento'
                  AND column_name = 'estado'
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                var isNullable = cmd.ExecuteScalar() as string;
                return string.Equals(isNullable, "YES", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool ExisteColumna(NpgsqlConnection cn, string tabla, string columna)
        {
            const string sql = @"
                SELECT 1
                FROM pg_attribute a
                WHERE a.attrelid = to_regclass(@tabla)
                  AND a.attname = @columna
                  AND a.attnum > 0
                  AND NOT a.attisdropped
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                cmd.Parameters.AddWithValue("@columna", columna);
                return cmd.ExecuteScalar() != null;
            }
        }

        // =========================================================
        // OBTENER POR SOLICITUD
        // =========================================================
        public List<Documento> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<Documento>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                var selectFechaValidacion = ExisteColumna(cn, "aocr_tbdocumento", "fecha_validacion")
                    ? "fecha_validacion"
                    : "NULL::timestamp AS fecha_validacion";
                var selectValidadoPor = ExisteColumna(cn, "aocr_tbdocumento", "validado_por")
                    ? "validado_por"
                    : "NULL::varchar AS validado_por";
                var selectNombreOriginal = ExisteColumna(cn, "aocr_tbdocumento", "nombre_original")
                    ? "nombre_original"
                    : "NULL::varchar AS nombre_original";
                var selectNombreVisible = ExisteColumna(cn, "aocr_tbdocumento", "nombre_visible")
                    ? "nombre_visible"
                    : "NULL::varchar AS nombre_visible";
                var selectNombreFisico = ExisteColumna(cn, "aocr_tbdocumento", "nombre_fisico")
                    ? "nombre_fisico"
                    : "NULL::varchar AS nombre_fisico";

                string sql = @"
                    SELECT
                        codigo_documento,
                        codigo_solicitud,
                        tipo_documento,
                        nombre_archivo,
                        " + selectNombreOriginal + @",
                        " + selectNombreVisible + @",
                        " + selectNombreFisico + @",
                        ruta_guardada,
                        extension,
                        tamano_bytes,
                        estado,
                        validado,
                        fecha_carga,
                        " + selectFechaValidacion + @",
                        " + selectValidadoPor + @",
                        observaciones,
                        version,
                        created_by
                    FROM aocr_tbdocumento
                    WHERE codigo_solicitud = @id
                    ORDER BY fecha_carga DESC NULLS LAST, codigo_documento DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoSolicitud);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            lista.Add(Mapear(rd));
                    }
                }
            }

            return lista;
        }

        // =========================================================
        // OBTENER POR ID
        // =========================================================
        public Documento ObtenerPorId(int codigoDocumento)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                var selectFechaValidacion = ExisteColumna(cn, "aocr_tbdocumento", "fecha_validacion")
                    ? "fecha_validacion"
                    : "NULL::timestamp AS fecha_validacion";
                var selectValidadoPor = ExisteColumna(cn, "aocr_tbdocumento", "validado_por")
                    ? "validado_por"
                    : "NULL::varchar AS validado_por";
                var selectNombreOriginal = ExisteColumna(cn, "aocr_tbdocumento", "nombre_original")
                    ? "nombre_original"
                    : "NULL::varchar AS nombre_original";
                var selectNombreVisible = ExisteColumna(cn, "aocr_tbdocumento", "nombre_visible")
                    ? "nombre_visible"
                    : "NULL::varchar AS nombre_visible";
                var selectNombreFisico = ExisteColumna(cn, "aocr_tbdocumento", "nombre_fisico")
                    ? "nombre_fisico"
                    : "NULL::varchar AS nombre_fisico";

                string sql = @"
                    SELECT
                        codigo_documento,
                        codigo_solicitud,
                        tipo_documento,
                        nombre_archivo,
                        " + selectNombreOriginal + @",
                        " + selectNombreVisible + @",
                        " + selectNombreFisico + @",
                        ruta_guardada,
                        extension,
                        tamano_bytes,
                        estado,
                        validado,
                        fecha_carga,
                        " + selectFechaValidacion + @",
                        " + selectValidadoPor + @",
                        observaciones,
                        version,
                        created_by
                    FROM aocr_tbdocumento
                    WHERE codigo_documento = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoDocumento);

                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Mapear(rd) : null;
                    }
                }
            }
        }

        // =========================================================
        // OBTENER TODOS
        // =========================================================
        public List<Documento> ObtenerTodos()
        {
            var lista = new List<Documento>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                var selectFechaValidacion = ExisteColumna(cn, "aocr_tbdocumento", "fecha_validacion")
                    ? "fecha_validacion"
                    : "NULL::timestamp AS fecha_validacion";
                var selectValidadoPor = ExisteColumna(cn, "aocr_tbdocumento", "validado_por")
                    ? "validado_por"
                    : "NULL::varchar AS validado_por";
                var selectNombreOriginal = ExisteColumna(cn, "aocr_tbdocumento", "nombre_original")
                    ? "nombre_original"
                    : "NULL::varchar AS nombre_original";
                var selectNombreVisible = ExisteColumna(cn, "aocr_tbdocumento", "nombre_visible")
                    ? "nombre_visible"
                    : "NULL::varchar AS nombre_visible";
                var selectNombreFisico = ExisteColumna(cn, "aocr_tbdocumento", "nombre_fisico")
                    ? "nombre_fisico"
                    : "NULL::varchar AS nombre_fisico";

                string sql = @"
                    SELECT
                        codigo_documento,
                        codigo_solicitud,
                        tipo_documento,
                        nombre_archivo,
                        " + selectNombreOriginal + @",
                        " + selectNombreVisible + @",
                        " + selectNombreFisico + @",
                        ruta_guardada,
                        extension,
                        tamano_bytes,
                        estado,
                        validado,
                        fecha_carga,
                        " + selectFechaValidacion + @",
                        " + selectValidadoPor + @",
                        observaciones,
                        version,
                        created_by
                    FROM aocr_tbdocumento
                    ORDER BY fecha_carga DESC NULLS LAST, codigo_documento DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(Mapear(rd));
                }
            }

            return lista;
        }

        // =========================================================
        // ACTUALIZAR
        // =========================================================
        public bool Actualizar(Documento doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (doc.CodigoDocumento <= 0) throw new Exception("Código de documento inválido.");

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var estadoPersistencia = ResolverEstadoParaInsercion(cn, doc.Estado);
                var tieneUpdatedAt = ExisteColumna(cn, "aocr_tbdocumento", "updated_at");
                var columnaUpdatedBy = ExisteColumna(cn, "aocr_tbdocumento", "updated_by")
                    ? "updated_by"
                    : (ExisteColumna(cn, "aocr_tbdocumento", "usuario_actualizacion") ? "usuario_actualizacion" : string.Empty);
                var tieneFechaValidacion = ExisteColumna(cn, "aocr_tbdocumento", "fecha_validacion");
                var tieneValidadoPor = ExisteColumna(cn, "aocr_tbdocumento", "validado_por");
                var tieneNombreOriginal = ExisteColumna(cn, "aocr_tbdocumento", "nombre_original");
                var tieneNombreVisible = ExisteColumna(cn, "aocr_tbdocumento", "nombre_visible");
                var tieneNombreFisico = ExisteColumna(cn, "aocr_tbdocumento", "nombre_fisico");

                var setParts = new List<string>
                {
                    "tipo_documento = @tipo_documento",
                    "nombre_archivo = @nombre_archivo",
                    "ruta_guardada = @ruta_guardada",
                    "extension = @extension",
                    "tamano_bytes = @tamano_bytes",
                    "estado = @estado",
                    "validado = @validado",
                    "fecha_carga = @fecha_carga",
                    "observaciones = @observaciones",
                    "version = @version"
                };

                if (tieneFechaValidacion)
                {
                    setParts.Add("fecha_validacion = @fecha_validacion");
                }

                if (tieneValidadoPor)
                {
                    setParts.Add("validado_por = @validado_por");
                }

                if (tieneNombreOriginal)
                {
                    setParts.Add("nombre_original = @nombre_original");
                }

                if (tieneNombreVisible)
                {
                    setParts.Add("nombre_visible = @nombre_visible");
                }

                if (tieneNombreFisico)
                {
                    setParts.Add("nombre_fisico = @nombre_fisico");
                }

                if (tieneUpdatedAt)
                {
                    setParts.Add("updated_at = NOW()");
                }

                if (!string.IsNullOrWhiteSpace(columnaUpdatedBy))
                {
                    setParts.Add(columnaUpdatedBy + " = @updated_by");
                }

                string sql = @"
                    UPDATE aocr_tbdocumento
                    SET " + string.Join(", ", setParts) + @"
                    WHERE codigo_documento = @codigo_documento;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@tipo_documento", (object)doc.TipoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)doc.NombreArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_guardada", (object)doc.RutaGuardada ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@extension", (object)doc.Extension ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tamano_bytes", (object)doc.TamanoBytes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)estadoPersistencia ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@validado", (object)doc.Validado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_carga", (object)doc.FechaCarga ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones", (object)doc.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@version", (object)doc.Version ?? DBNull.Value);
                    if (tieneFechaValidacion)
                    {
                        cmd.Parameters.AddWithValue("@fecha_validacion", (object)doc.FechaValidacion ?? DBNull.Value);
                    }
                    if (tieneValidadoPor)
                    {
                        cmd.Parameters.AddWithValue("@validado_por", (object)doc.ValidadoPor ?? DBNull.Value);
                    }
                    if (tieneNombreOriginal)
                    {
                        cmd.Parameters.AddWithValue("@nombre_original", (object)(doc.NombreArchivoOriginal ?? doc.NombreArchivo) ?? DBNull.Value);
                    }
                    if (tieneNombreVisible)
                    {
                        cmd.Parameters.AddWithValue("@nombre_visible", (object)(doc.NombreArchivoVisible ?? doc.NombreArchivoOriginal ?? doc.NombreArchivo) ?? DBNull.Value);
                    }
                    if (tieneNombreFisico)
                    {
                        cmd.Parameters.AddWithValue("@nombre_fisico", (object)(doc.NombreArchivoFisico ?? doc.NombreArchivoGuardado) ?? DBNull.Value);
                    }
                    if (!string.IsNullOrWhiteSpace(columnaUpdatedBy))
                    {
                        cmd.Parameters.AddWithValue("@updated_by", (object)doc.UsuarioRegistro ?? "sistema");
                    }
                    cmd.Parameters.AddWithValue("@codigo_documento", doc.CodigoDocumento);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // =========================================================
        // ELIMINAR (LÓGICO)
        // =========================================================
        public bool MarcarComoEliminado(int codigoDocumento, string usuario)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var estadoPersistencia = ResolverEstadoParaInsercion(cn, "ELIMINADO");
                var tieneUpdatedAt = ExisteColumna(cn, "aocr_tbdocumento", "updated_at");
                var columnaUpdatedBy = ExisteColumna(cn, "aocr_tbdocumento", "updated_by")
                    ? "updated_by"
                    : (ExisteColumna(cn, "aocr_tbdocumento", "usuario_actualizacion") ? "usuario_actualizacion" : string.Empty);

                var setParts = new List<string>
                {
                    "estado = @estado"
                };

                if (tieneUpdatedAt)
                {
                    setParts.Add("updated_at = NOW()");
                }

                if (!string.IsNullOrWhiteSpace(columnaUpdatedBy))
                {
                    setParts.Add(columnaUpdatedBy + " = @u");
                }

                string sql = @"
                    UPDATE aocr_tbdocumento
                    SET " + string.Join(", ", setParts) + @"
                    WHERE codigo_documento = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoDocumento);
                    cmd.Parameters.AddWithValue("@estado", (object)estadoPersistencia ?? DBNull.Value);
                    if (!string.IsNullOrWhiteSpace(columnaUpdatedBy))
                    {
                        cmd.Parameters.AddWithValue("@u", usuario ?? "sistema");
                    }
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // =========================================================
        // MAPEO ÚNICO
        // =========================================================
        private Documento Mapear(IDataRecord rd)
        {
            return new Documento
            {
                CodigoDocumento = rd["codigo_documento"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_documento"]),
                CodigoSolicitud = rd["codigo_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_solicitud"]),

                TipoDocumento = rd["tipo_documento"] == DBNull.Value ? null : rd["tipo_documento"].ToString(),
                NombreArchivo = rd["nombre_archivo"] == DBNull.Value ? null : rd["nombre_archivo"].ToString(),
                NombreArchivoOriginal = rd["nombre_original"] == DBNull.Value ? null : rd["nombre_original"].ToString(),
                NombreArchivoVisible = rd["nombre_visible"] == DBNull.Value ? null : rd["nombre_visible"].ToString(),
                NombreArchivoFisico = rd["nombre_fisico"] == DBNull.Value ? null : rd["nombre_fisico"].ToString(),
                RutaGuardada = rd["ruta_guardada"] == DBNull.Value ? null : rd["ruta_guardada"].ToString(),

                Extension = rd["extension"] == DBNull.Value ? null : rd["extension"].ToString(),
                TamanoBytes = rd["tamano_bytes"] == DBNull.Value ? (long?)null : Convert.ToInt64(rd["tamano_bytes"]),

                Estado = rd["estado"] == DBNull.Value ? null : rd["estado"].ToString(),
                Validado = rd["validado"] == DBNull.Value ? (bool?)null : Convert.ToBoolean(rd["validado"]),

                FechaCarga = rd["fecha_carga"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_carga"]),
                FechaValidacion = rd["fecha_validacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_validacion"]),
                ValidadoPor = rd["validado_por"] == DBNull.Value ? null : rd["validado_por"].ToString(),
                Observaciones = rd["observaciones"] == DBNull.Value ? null : rd["observaciones"].ToString(),

                Version = rd["version"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["version"]),
                UsuarioRegistro = rd["created_by"] == DBNull.Value ? null : rd["created_by"].ToString()
            };
        }
    }
}
