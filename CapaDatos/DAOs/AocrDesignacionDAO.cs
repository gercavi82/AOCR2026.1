using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using CapaDatos.Constants;
using CapaDatos.Services;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class DircavTransaccionResultado
    {
        public bool Exitoso { get; set; }
        public int HttpStatusCode { get; set; } = 200;
        public string Mensaje { get; set; }
        public int DesignacionId { get; set; }
        public int Version { get; set; }
        public string NuevoEstado { get; set; }
        public bool EsIdempotente { get; set; }
        public AocrDesignacionInspector Designacion { get; set; }
    }

    public class DircavAceptarDocumentacionParams
    {
        public int SolicitudId { get; set; }
        public int DircavUsuarioId { get; set; }
        public string DircavUsuarioNombre { get; set; }
        public string Observacion { get; set; }
        public int? VersionEsperada { get; set; }
    }

    public class DircavDesignarInspectorParams
    {
        public int SolicitudId { get; set; }
        public int? EstacionId { get; set; }
        public int InspectorId { get; set; }
        public string InspectorCedula { get; set; }
        public string InspectorNombre { get; set; }
        public string InspectorTipo { get; set; }
        public int? InspectorApoyoId { get; set; }
        public string InspectorApoyoCedula { get; set; }
        public string InspectorApoyoNombre { get; set; }
        public string InspectorApoyoTipo { get; set; }
        public int DircavUsuarioId { get; set; }
        public string DircavUsuarioNombre { get; set; }
        public string Motivo { get; set; }
        public int? VersionEsperada { get; set; }
    }

    public class DircavFirmarDesignacionParams
    {
        public int SolicitudId { get; set; }
        public int? EstacionId { get; set; }
        public int DircavUsuarioId { get; set; }
        public string DircavUsuarioNombre { get; set; }
        public string RutaPdf { get; set; }
        public string RutaDocumentoFirmado { get; set; }
        public string HashDocumento { get; set; }
        public long TamanioBytes { get; set; }
        public string HuellaCertificado { get; set; }
        public string CodigoVerificacion { get; set; }
        public string MimeType { get; set; } = "application/pdf";
    }

    /// <summary>
    /// DAO para la persistencia transaccional y versionada de designaciones de inspectores por DIRCAV (AC-05 y AC-06).
    /// </summary>
    public class AocrDesignacionDAO
    {
        private readonly string _connectionString;
        private static bool _schemaEnsured = false;
        private static readonly object SchemaLock = new object();

        public AocrDesignacionDAO()
        {
            _connectionString = ResolveConnectionString();
        }

        public AocrDesignacionDAO(string connectionString)
        {
            _connectionString = !string.IsNullOrWhiteSpace(connectionString)
                ? connectionString
                : ResolveConnectionString();
        }

        private static string ResolveConnectionString()
        {
            var env = Environment.GetEnvironmentVariable("AOCR_CONNSTR_AOCRCONNECTION");
            if (!string.IsNullOrWhiteSpace(env)) return env;

            var conn = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            if (conn != null && !string.IsNullOrWhiteSpace(conn.ConnectionString))
            {
                return conn.ConnectionString;
            }

            return ConexionDAO.CadenaConexion;
        }

        public NpgsqlConnection CrearConexion()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public void AsegurarEsquema(NpgsqlConnection conn)
        {
            if (_schemaEnsured) return;
            lock (SchemaLock)
            {
                if (_schemaEnsured) return;

                const string ddl = @"
CREATE TABLE IF NOT EXISTS public.aocr_tbdesignacion_inspector (
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL REFERENCES public.aocr_tbsolicitud(codigo_solicitud),
    inspeccion_id INTEGER NULL,
    estacion_id INTEGER NULL REFERENCES public.aocr_tbsolicitud_estacion(id),
    inspector_id INTEGER NOT NULL,
    inspector_cedula VARCHAR(30) NOT NULL,
    inspector_nombre VARCHAR(200) NOT NULL,
    inspector_apoyo_cedula VARCHAR(30) NULL,
    inspector_apoyo_nombre VARCHAR(200) NULL,
    dircav_usuario_id INTEGER NOT NULL,
    dircav_usuario_nombre VARCHAR(200) NULL,
    estado VARCHAR(80) NOT NULL DEFAULT 'DESIGNACION_PENDIENTE_FIRMA_DIRCAV',
    motivo TEXT NULL,
    version INTEGER NOT NULL DEFAULT 1,
    vigente BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_designacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    fecha_firma TIMESTAMP WITHOUT TIME ZONE NULL,
    ruta_pdf VARCHAR(500) NULL,
    ruta_documento_firmado VARCHAR(500) NULL,
    hash_documento VARCHAR(256) NULL,
    firmado BOOLEAN NOT NULL DEFAULT FALSE,
    usuario_firma VARCHAR(200) NULL,
    tamanio_bytes BIGINT NULL,
    mime_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
    creado_en TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    creado_por VARCHAR(100) NULL,
    actualizado_en TIMESTAMP WITHOUT TIME ZONE NULL,
    actualizado_por VARCHAR(100) NULL
);

-- Columnas aditivas para AC-06
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS ruta_pdf VARCHAR(500) NULL;
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS ruta_documento_firmado VARCHAR(500) NULL;
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS hash_documento VARCHAR(256) NULL;
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS firmado BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS usuario_firma VARCHAR(200) NULL;
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS tamanio_bytes BIGINT NULL;
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS mime_type VARCHAR(100) NOT NULL DEFAULT 'application/pdf';
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS huella_certificado VARCHAR(100) NULL;
ALTER TABLE public.aocr_tbdesignacion_inspector ADD COLUMN IF NOT EXISTS codigo_verificacion VARCHAR(100) NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_designacion_vigente 
    ON public.aocr_tbdesignacion_inspector (solicitud_id, COALESCE(estacion_id, 0)) 
    WHERE vigente = TRUE;

CREATE INDEX IF NOT EXISTS idx_aocr_designacion_solicitud 
    ON public.aocr_tbdesignacion_inspector (solicitud_id);

CREATE INDEX IF NOT EXISTS ix_aocr_designacion_firmado
    ON public.aocr_tbdesignacion_inspector (solicitud_id, firmado, vigente);
";
                try
                {
                    using (var cmd = new NpgsqlCommand(ddl, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    _schemaEnsured = true;
                }
                catch
                {
                    // Si ocurre un error de concurrencia o de permisos DDL en ejecución
                }
            }
        }

        /// <summary>
        /// Registra transaccionalmente una designación o reasignación formal.
        /// Si existía una designación vigente para la solicitud y estación, la inactiva con motivo y nueva versión.
        /// </summary>
        public AocrDesignacionInspector RegistrarDesignacion(
            int solicitudId,
            int? inspeccionId,
            int? estacionId,
            int inspectorId,
            string inspectorCedula,
            string inspectorNombre,
            string inspectorApoyoCedula,
            string inspectorApoyoNombre,
            int dircavUsuarioId,
            string dircavUsuarioNombre,
            string motivo,
            string estado = "DESIGNACION_PENDIENTE_FIRMA_DIRCAV",
            NpgsqlTransaction externalTx = null)
        {
            var conExterno = externalTx != null;
            var conn = conExterno ? externalTx.Connection : CrearConexion();

            try
            {
                if (!conExterno && conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }

                AsegurarEsquema(conn);

                var tx = conExterno ? externalTx : conn.BeginTransaction();
                try
                {
                    // 1. Buscar si hay una designación vigente
                    const string sqlBuscarVigente = @"
SELECT id, version, inspector_cedula 
FROM public.aocr_tbdesignacion_inspector
WHERE solicitud_id = @solicitud_id 
  AND COALESCE(estacion_id, 0) = COALESCE(@estacion_id, 0)
  AND vigente = TRUE
FOR UPDATE;";

                    int versionNueva = 1;
                    int? vigenteId = null;

                    using (var cmdBuscar = new NpgsqlCommand(sqlBuscarVigente, conn, tx))
                    {
                        cmdBuscar.Parameters.AddWithValue("@solicitud_id", solicitudId);
                        cmdBuscar.Parameters.AddWithValue("@estacion_id", (object)estacionId ?? DBNull.Value);

                        using (var dr = cmdBuscar.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                vigenteId = dr.GetInt32(0);
                                var versionActual = dr.GetInt32(1);
                                versionNueva = versionActual + 1;
                            }
                        }
                    }

                    // 2. Inactivar la anterior si existe
                    if (vigenteId.HasValue)
                    {
                        const string sqlInactivar = @"
UPDATE public.aocr_tbdesignacion_inspector
SET vigente = FALSE,
    actualizado_en = NOW(),
    actualizado_por = @actualizado_por,
    motivo = CASE WHEN @motivo IS NOT NULL AND @motivo <> '' THEN @motivo ELSE motivo END
WHERE id = @id;";

                        using (var cmdInact = new NpgsqlCommand(sqlInactivar, conn, tx))
                        {
                            cmdInact.Parameters.AddWithValue("@id", vigenteId.Value);
                            cmdInact.Parameters.AddWithValue("@actualizado_por", (object)dircavUsuarioNombre ?? "DIRCAV");
                            cmdInact.Parameters.AddWithValue("@motivo", (object)motivo ?? DBNull.Value);
                            cmdInact.ExecuteNonQuery();
                        }
                    }

                    // 3. Insertar la nueva designación
                    const string sqlInsert = @"
INSERT INTO public.aocr_tbdesignacion_inspector (
    solicitud_id, inspeccion_id, estacion_id,
    inspector_id, inspector_cedula, inspector_nombre,
    inspector_apoyo_cedula, inspector_apoyo_nombre,
    dircav_usuario_id, dircav_usuario_nombre,
    estado, motivo, version, vigente,
    fecha_designacion, creado_en, creado_por
) VALUES (
    @solicitud_id, @inspeccion_id, @estacion_id,
    @inspector_id, @inspector_cedula, @inspector_nombre,
    @inspector_apoyo_cedula, @inspector_apoyo_nombre,
    @dircav_usuario_id, @dircav_usuario_nombre,
    @estado, @motivo, @version, TRUE,
    NOW(), NOW(), @creado_por
) RETURNING id, fecha_designacion;";

                    int nuevoId = 0;
                    DateTime fechaDesig = DateTime.Now;

                    using (var cmdInsert = new NpgsqlCommand(sqlInsert, conn, tx))
                    {
                        cmdInsert.Parameters.AddWithValue("@solicitud_id", solicitudId);
                        cmdInsert.Parameters.AddWithValue("@inspeccion_id", (object)inspeccionId ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@estacion_id", (object)estacionId ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@inspector_id", inspectorId);
                        cmdInsert.Parameters.AddWithValue("@inspector_cedula", (object)inspectorCedula ?? string.Empty);
                        cmdInsert.Parameters.AddWithValue("@inspector_nombre", (object)inspectorNombre ?? string.Empty);
                        cmdInsert.Parameters.AddWithValue("@inspector_apoyo_cedula", (object)inspectorApoyoCedula ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@inspector_apoyo_nombre", (object)inspectorApoyoNombre ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@dircav_usuario_id", dircavUsuarioId);
                        cmdInsert.Parameters.AddWithValue("@dircav_usuario_nombre", (object)dircavUsuarioNombre ?? "DIRCAV");
                        cmdInsert.Parameters.AddWithValue("@estado", estado ?? "DESIGNACION_PENDIENTE_FIRMA_DIRCAV");
                        cmdInsert.Parameters.AddWithValue("@motivo", (object)motivo ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@version", versionNueva);
                        cmdInsert.Parameters.AddWithValue("@creado_por", (object)dircavUsuarioNombre ?? "DIRCAV");

                        using (var dr = cmdInsert.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                nuevoId = dr.GetInt32(0);
                                fechaDesig = dr.GetDateTime(1);
                            }
                        }
                    }

                    if (!conExterno)
                    {
                        tx.Commit();
                    }

                    return new AocrDesignacionInspector
                    {
                        Id = nuevoId,
                        SolicitudId = solicitudId,
                        InspeccionId = inspeccionId,
                        EstacionId = estacionId,
                        InspectorId = inspectorId,
                        InspectorCedula = inspectorCedula,
                        InspectorNombre = inspectorNombre,
                        InspectorApoyoCedula = inspectorApoyoCedula,
                        InspectorApoyoNombre = inspectorApoyoNombre,
                        DircavUsuarioId = dircavUsuarioId,
                        DircavUsuarioNombre = dircavUsuarioNombre,
                        Estado = estado ?? "DESIGNACION_PENDIENTE_FIRMA_DIRCAV",
                        Motivo = motivo,
                        Version = versionNueva,
                        Vigente = true,
                        FechaDesignacion = fechaDesig,
                        CreadoEn = fechaDesig,
                        CreadoPor = dircavUsuarioNombre
                    };
                }
                catch
                {
                    if (!conExterno) tx.Rollback();
                    throw;
                }
            }
            finally
            {
                if (!conExterno && conn != null)
                {
                    conn.Dispose();
                }
            }
        }

        public AocrDesignacionInspector ObtenerPorId(int designacionId)
        {
            using (var conn = CrearConexion())
            {
                conn.Open();
                AsegurarEsquema(conn);

                const string sql = @"
SELECT id, solicitud_id, inspeccion_id, estacion_id,
       inspector_id, inspector_cedula, inspector_nombre,
       inspector_apoyo_cedula, inspector_apoyo_nombre,
       dircav_usuario_id, dircav_usuario_nombre,
       estado, motivo, version, vigente, fecha_designacion, fecha_firma,
       ruta_pdf, ruta_documento_firmado, hash_documento, firmado,
       usuario_firma, tamanio_bytes, mime_type, huella_certificado, codigo_verificacion
FROM public.aocr_tbdesignacion_inspector
WHERE id = @id;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", designacionId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return null;
                        return Mapear(dr);
                    }
                }
            }
        }

        public AocrDesignacionInspector ObtenerDesignacionVigente(int solicitudId, int? estacionId = null)
        {
            using (var conn = CrearConexion())
            {
                conn.Open();
                AsegurarEsquema(conn);

                const string sql = @"
SELECT id, solicitud_id, inspeccion_id, estacion_id,
       inspector_id, inspector_cedula, inspector_nombre,
       inspector_apoyo_cedula, inspector_apoyo_nombre,
       dircav_usuario_id, dircav_usuario_nombre,
       estado, motivo, version, vigente, fecha_designacion, fecha_firma,
       ruta_pdf, ruta_documento_firmado, hash_documento, firmado,
       usuario_firma, tamanio_bytes, mime_type, huella_certificado, codigo_verificacion
FROM public.aocr_tbdesignacion_inspector
WHERE solicitud_id = @solicitud_id
  AND COALESCE(estacion_id, 0) = COALESCE(@estacion_id, 0)
  AND vigente = TRUE
LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                    cmd.Parameters.AddWithValue("@estacion_id", (object)estacionId ?? DBNull.Value);

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return null;
                        return Mapear(dr);
                    }
                }
            }
        }

        public List<AocrDesignacionInspector> ListarHistorial(int solicitudId)
        {
            var resultado = new List<AocrDesignacionInspector>();
            using (var conn = CrearConexion())
            {
                conn.Open();
                AsegurarEsquema(conn);

                const string sql = @"
SELECT id, solicitud_id, inspeccion_id, estacion_id,
       inspector_id, inspector_cedula, inspector_nombre,
       inspector_apoyo_cedula, inspector_apoyo_nombre,
       dircav_usuario_id, dircav_usuario_nombre,
       estado, motivo, version, vigente, fecha_designacion, fecha_firma,
       ruta_pdf, ruta_documento_firmado, hash_documento, firmado,
       usuario_firma, tamanio_bytes, mime_type, huella_certificado, codigo_verificacion
FROM public.aocr_tbdesignacion_inspector
WHERE solicitud_id = @solicitud_id
ORDER BY version DESC, fecha_designacion DESC;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            resultado.Add(Mapear(dr));
                        }
                    }
                }
            }
            return resultado;
        }

        public void ActualizarRutaPdf(int designacionId, string rutaPdf, long tamanioBytes)
        {
            using (var conn = CrearConexion())
            {
                conn.Open();
                AsegurarEsquema(conn);

                const string sql = @"
UPDATE public.aocr_tbdesignacion_inspector
   SET ruta_pdf = @ruta_pdf,
       tamanio_bytes = @tamanio_bytes,
       actualizado_en = NOW()
 WHERE id = @id;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", designacionId);
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)(rutaPdf ?? string.Empty));
                    cmd.Parameters.AddWithValue("@tamanio_bytes", tamanioBytes);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void MarcarFirmada(
            int designacionId,
            string rutaPdfFirmado,
            string hashDocumento,
            string usuarioFirma,
            DateTime fechaFirma,
            long tamanioBytes)
        {
            using (var conn = CrearConexion())
            {
                conn.Open();
                AsegurarEsquema(conn);

                const string sql = @"
UPDATE public.aocr_tbdesignacion_inspector
   SET firmado = TRUE,
       estado = 'DESIGNACION_FIRMADA_DIRCAV',
       ruta_documento_firmado = @ruta_firmado,
       hash_documento = @hash,
       usuario_firma = @usuario_firma,
       fecha_firma = @fecha_firma,
       tamanio_bytes = @tamanio_bytes,
       actualizado_en = NOW()
 WHERE id = @id;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", designacionId);
                    cmd.Parameters.AddWithValue("@ruta_firmado", (object)(rutaPdfFirmado ?? string.Empty));
                    cmd.Parameters.AddWithValue("@hash", (object)(hashDocumento ?? string.Empty));
                    cmd.Parameters.AddWithValue("@usuario_firma", (object)(usuarioFirma ?? "DIRCAV"));
                    cmd.Parameters.AddWithValue("@fecha_firma", fechaFirma);
                    cmd.Parameters.AddWithValue("@tamanio_bytes", tamanioBytes);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public DircavTransaccionResultado EjecutarAceptacionDocumentalTransaccional(DircavAceptarDocumentacionParams p)
        {
            if (p == null || p.SolicitudId <= 0)
            {
                return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };
            }
            if (p.DircavUsuarioId <= 0)
            {
                return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 401, Mensaje = "Sesión no válida o expirada." };
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        // 1. Bloqueo pesimista de solicitud
                        string estadoActual = null;
                        int versionActual = 1;
                        string estadoDocActual = null;

                        using (var cmd = new NpgsqlCommand("SELECT estado, COALESCE(version, 1), estado_documental FROM public.aocr_tbsolicitud WHERE codigo_solicitud=@solicitud_id FOR UPDATE;", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            using (var rd = cmd.ExecuteReader())
                            {
                                if (rd.Read())
                                {
                                    estadoActual = rd.IsDBNull(0) ? string.Empty : rd.GetString(0).Trim();
                                    versionActual = rd.GetInt32(1);
                                    estadoDocActual = rd.IsDBNull(2) ? string.Empty : rd.GetString(2).Trim();
                                }
                                else
                                {
                                    tx.Rollback();
                                    return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 404, Mensaje = "Solicitud no encontrada." };
                                }
                            }
                        }

                        // 2. Control de concurrencia optimista
                        if (p.VersionEsperada.HasValue && p.VersionEsperada.Value > 0 && p.VersionEsperada.Value != versionActual)
                        {
                            tx.Rollback();
                            return new DircavTransaccionResultado
                            {
                                Exitoso = false,
                                HttpStatusCode = 409,
                                Mensaje = string.Format("Conflicto de concurrencia: la versión esperada ({0}) no coincide con la versión actual ({1}).", p.VersionEsperada.Value, versionActual)
                            };
                        }

                        // 3. Validar estado de origen: debe ser PENDIENTE_DIRCAV
                        if (!string.Equals(estadoActual, AocrEstadosProceso.PendienteDircav, StringComparison.OrdinalIgnoreCase))
                        {
                            tx.Rollback();
                            return new DircavTransaccionResultado
                            {
                                Exitoso = false,
                                HttpStatusCode = 409,
                                Mensaje = string.Format("Conflicto: La solicitud se encuentra en estado '{0}' y no puede ser aceptada directamente por DIRCAV.", estadoActual)
                            };
                        }

                        // 4. Validar que no existan observaciones documentales abiertas
                        if (string.Equals(estadoDocActual, "OBSERVADO", StringComparison.OrdinalIgnoreCase))
                        {
                            tx.Rollback();
                            return new DircavTransaccionResultado
                            {
                                Exitoso = false,
                                HttpStatusCode = 400,
                                Mensaje = "No se puede aceptar el expediente: existen documentos observados pendientes de resolución."
                            };
                        }

                        using (var cmdObs = new NpgsqlCommand(@"
                            SELECT COUNT(*) FROM public.aocr_tbdocumentos 
                            WHERE codigo_solicitud=@solicitud_id 
                              AND (UPPER(estado)='OBSERVADO' OR UPPER(estado)='DEVUELTO_INSPECTOR') 
                              AND activo=TRUE;", cn, tx))
                        {
                            cmdObs.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            var countObs = Convert.ToInt32(cmdObs.ExecuteScalar() ?? 0);
                            if (countObs > 0)
                            {
                                tx.Rollback();
                                return new DircavTransaccionResultado
                                {
                                    Exitoso = false,
                                    HttpStatusCode = 400,
                                    Mensaje = "No se puede aceptar el expediente: existen documentos observados pendientes de resolución."
                                };
                            }
                        }

                        // 5. Actualizar solicitud
                        int nuevaVersion = versionActual + 1;
                        using (var cmdUpd = new NpgsqlCommand(@"
                            UPDATE public.aocr_tbsolicitud
                            SET estado=@estado,
                                estado_documental='ACEPTADO_DIRCAV',
                                version=@version,
                                updated_at=NOW(),
                                updated_by=@updated_by
                            WHERE codigo_solicitud=@solicitud_id;", cn, tx))
                        {
                            cmdUpd.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            cmdUpd.Parameters.AddWithValue("@estado", AocrEstadosProceso.DocumentacionAceptadaDircav);
                            cmdUpd.Parameters.AddWithValue("@version", nuevaVersion);
                            cmdUpd.Parameters.AddWithValue("@updated_by", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                            cmdUpd.ExecuteNonQuery();
                        }

                        // 6. Inserción en historial
                        using (var cmdHist = new NpgsqlCommand(@"
                            INSERT INTO public.aocr_tbhistorial_documental
                            (codigo_solicitud, evento, detalle, codigo_usuario, fecha_evento, created_at, created_by)
                            VALUES
                            (@solicitud_id, @evento, @detalle, @usuario_id, NOW(), NOW(), @created_by);", cn, tx))
                        {
                            cmdHist.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            cmdHist.Parameters.AddWithValue("@evento", AocrEstadosProceso.DocumentacionAceptadaDircav);
                            cmdHist.Parameters.AddWithValue("@detalle", (object)(p.Observacion ?? "Documentación técnica aceptada formalmente por DIRCAV."));
                            cmdHist.Parameters.AddWithValue("@usuario_id", p.DircavUsuarioId);
                            cmdHist.Parameters.AddWithValue("@created_by", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                            cmdHist.ExecuteNonQuery();
                        }

                        // 7. Inserción en auditoría atómica
                        using (var cmdAudit = new NpgsqlCommand(@"
                            INSERT INTO public.aocr_tbauditoria
                            (entidad, accion, usuario, fecha, datos_previos, datos_nuevos)
                            VALUES
                            ('DIRCAV', 'ACEPTAR_DOCUMENTACION', @usuario, NOW(), @datos_previos, @datos_nuevos);", cn, tx))
                        {
                            cmdAudit.Parameters.AddWithValue("@usuario", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                            cmdAudit.Parameters.AddWithValue("@datos_previos", "Estado=" + estadoActual + "; Version=" + versionActual);
                            cmdAudit.Parameters.AddWithValue("@datos_nuevos", "Estado=" + AocrEstadosProceso.DocumentacionAceptadaDircav + "; Version=" + nuevaVersion);
                            cmdAudit.ExecuteNonQuery();
                        }

                        // 8. Encolar notificación en email_queue
                        var emailItem = new CapaDatos.Services.EmailQueueItem
                        {
                            Para = "coordinacion@dgac.gob.ec",
                            ParaNombre = "Coordinación AOCR",
                            Asunto = "AOCR - Documentación técnica aceptada por DIRCAV #" + p.SolicitudId,
                            Cuerpo = "La Autoridad DIRCAV ha aceptado formalmente la documentación técnica del expediente #" + p.SolicitudId + ".",
                            SolicitudId = p.SolicitudId,
                            TipoNotificacion = "SOLICITUD_DOCUMENTACION_ACEPTADA_DIRCAV",
                            EventKey = "AOCR_DIRCAV_ACEPTAR_" + p.SolicitudId + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                            Estado = "PENDIENTE"
                        };
                        bool duplicate;
                        new CapaDatos.Services.EmailQueueService().EncolarConAdjuntosEnTransaccion(cn, tx, emailItem, null, out duplicate);

                        tx.Commit();

                        return new DircavTransaccionResultado
                        {
                            Exitoso = true,
                            HttpStatusCode = 200,
                            NuevoEstado = AocrEstadosProceso.DocumentacionAceptadaDircav,
                            Version = nuevaVersion,
                            Mensaje = "Documentación técnica aceptada formalmente por DIRCAV. Se habilita la designación del Inspector."
                        };
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        Trace.TraceError("[DIRCAV][ACEPTAR_ERROR] SolicitudId=" + p.SolicitudId + "; Error=" + ex);
                        return new DircavTransaccionResultado
                        {
                            Exitoso = false,
                            HttpStatusCode = 500,
                            Mensaje = "No se pudo completar la aceptación documental. Ocurrió un error interno en la transacción."
                        };
                    }
                }
            }
        }

        public DircavTransaccionResultado EjecutarDesignacionTransaccional(
            DircavDesignarInspectorParams p,
            SolicitudEstacionDAO estacionDao = null)
        {
            if (p == null || p.SolicitudId <= 0)
            {
                return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };
            }
            if (p.DircavUsuarioId <= 0)
            {
                return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 401, Mensaje = "Sesión no válida o expirada." };
            }
            if (p.InspectorId <= 0 || string.IsNullOrWhiteSpace(p.InspectorCedula))
            {
                return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 400, Mensaje = "Debe seleccionar un inspector principal activo." };
            }
            if (!string.IsNullOrWhiteSpace(p.InspectorApoyoCedula) &&
                string.Equals(p.InspectorCedula.Trim(), p.InspectorApoyoCedula.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return new DircavTransaccionResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "El inspector de apoyo no puede ser la misma persona que el inspector principal."
                };
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        // 1. Bloqueo pesimista de solicitud
                        string estadoActual = null;
                        int versionActual = 1;

                        using (var cmd = new NpgsqlCommand(@"
                            SELECT estado, COALESCE(version, 1)
                            FROM public.aocr_tbsolicitud
                            WHERE codigo_solicitud=@solicitud_id
                            FOR UPDATE;", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            using (var rd = cmd.ExecuteReader())
                            {
                                if (rd.Read())
                                {
                                    estadoActual = rd.IsDBNull(0) ? string.Empty : rd.GetString(0).Trim();
                                    versionActual = rd.GetInt32(1);
                                }
                                else
                                {
                                    tx.Rollback();
                                    return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 404, Mensaje = "Solicitud no encontrada." };
                                }
                            }
                        }

                        // 2. Control de concurrencia optimista
                        if (p.VersionEsperada.HasValue && p.VersionEsperada.Value > 0 && p.VersionEsperada.Value != versionActual)
                        {
                            tx.Rollback();
                            return new DircavTransaccionResultado
                            {
                                Exitoso = false,
                                HttpStatusCode = 409,
                                Mensaje = string.Format("Conflicto de concurrencia: la versión esperada ({0}) no coincide con la versión actual ({1}).", p.VersionEsperada.Value, versionActual)
                            };
                        }

                        // 3. Validar estado de la solicitud
                        var permiteDesignacion = string.Equals(estadoActual, AocrEstadosProceso.DocumentacionAceptadaDircav, StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(estadoActual, AocrEstadosProceso.PendienteDesignacionDircav, StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(estadoActual, AocrEstadosProceso.DesignacionPendienteFirmaDircav, StringComparison.OrdinalIgnoreCase);

                        if (!permiteDesignacion)
                        {
                            tx.Rollback();
                            return new DircavTransaccionResultado
                            {
                                Exitoso = false,
                                HttpStatusCode = 409,
                                Mensaje = string.Format("Conflicto: No se puede designar el inspector en el estado actual '{0}'. Debe estar en Aceptación Documental DIRCAV.", estadoActual)
                            };
                        }

                        // 4. Validar pertenencia de estación (si se envió)
                        if (p.EstacionId.HasValue && p.EstacionId.Value > 0)
                        {
                            using (var cmdEstCheck = new NpgsqlCommand(@"
                                SELECT COUNT(*) FROM public.aocr_tbsolicitud_estacion
                                WHERE id=@estacion_id AND solicitud_id=@solicitud_id AND activo=TRUE;", cn, tx))
                            {
                                cmdEstCheck.Parameters.AddWithValue("@estacion_id", p.EstacionId.Value);
                                cmdEstCheck.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                                var countEst = Convert.ToInt32(cmdEstCheck.ExecuteScalar() ?? 0);
                                if (countEst == 0)
                                {
                                    tx.Rollback();
                                    return new DircavTransaccionResultado
                                    {
                                        Exitoso = false,
                                        HttpStatusCode = 400,
                                        Mensaje = "La estación indicada no pertenece a la solicitud."
                                    };
                                }
                            }
                        }

                        // 5. Bloqueo de designación vigente actual
                        int? vigenteId = null;
                        int versionDesignacionActual = 0;
                        string vigenteCedula = null;
                        string vigenteApoyoCedula = null;

                        using (var cmdVigente = new NpgsqlCommand(@"
                            SELECT id, version, inspector_cedula, inspector_apoyo_cedula
                            FROM public.aocr_tbdesignacion_inspector
                            WHERE solicitud_id=@solicitud_id
                              AND COALESCE(estacion_id, 0)=COALESCE(@estacion_id, 0)
                              AND vigente=TRUE
                            FOR UPDATE;", cn, tx))
                        {
                            cmdVigente.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            cmdVigente.Parameters.AddWithValue("@estacion_id", (object)p.EstacionId ?? DBNull.Value);
                            using (var rd = cmdVigente.ExecuteReader())
                            {
                                if (rd.Read())
                                {
                                    vigenteId = rd.GetInt32(0);
                                    versionDesignacionActual = rd.GetInt32(1);
                                    vigenteCedula = rd.IsDBNull(2) ? string.Empty : rd.GetString(2).Trim();
                                    vigenteApoyoCedula = rd.IsDBNull(3) ? string.Empty : rd.GetString(3).Trim();
                                }
                            }
                        }

                        // 6. Idempotencia: si es idéntica
                        var apoyoCedulaEntrante = (p.InspectorApoyoCedula ?? string.Empty).Trim();
                        if (vigenteId.HasValue &&
                            string.Equals(vigenteCedula, p.InspectorCedula.Trim(), StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(vigenteApoyoCedula, apoyoCedulaEntrante, StringComparison.OrdinalIgnoreCase))
                        {
                            tx.Rollback();
                            return new DircavTransaccionResultado
                            {
                                Exitoso = true,
                                HttpStatusCode = 200,
                                EsIdempotente = true,
                                DesignacionId = vigenteId.Value,
                                Version = versionDesignacionActual,
                                NuevoEstado = AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                                Mensaje = "El inspector ya se encuentra asignado a este expediente. Estado de designación conservado."
                            };
                        }

                        // 7. Reasignación: exige motivo
                        if (vigenteId.HasValue && !string.Equals(vigenteCedula, p.InspectorCedula.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrWhiteSpace(p.Motivo))
                            {
                                tx.Rollback();
                                return new DircavTransaccionResultado
                                {
                                    Exitoso = false,
                                    HttpStatusCode = 400,
                                    Mensaje = "Para reasignar el inspector a una persona diferente debe especificar un motivo institucional."
                                };
                            }

                            // Inactivar la anterior
                            using (var cmdInact = new NpgsqlCommand(@"
                                UPDATE public.aocr_tbdesignacion_inspector
                                SET vigente=FALSE,
                                    actualizado_en=NOW(),
                                    actualizado_por=@actualizado_por,
                                    motivo=CASE WHEN @motivo IS NOT NULL AND @motivo <> '' THEN @motivo ELSE motivo END
                                WHERE id=@id;", cn, tx))
                            {
                                cmdInact.Parameters.AddWithValue("@id", vigenteId.Value);
                                cmdInact.Parameters.AddWithValue("@actualizado_por", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                                cmdInact.Parameters.AddWithValue("@motivo", (object)(p.Motivo ?? string.Empty));
                                cmdInact.ExecuteNonQuery();
                            }
                        }

                        int versionNuevaDesignacion = versionDesignacionActual + 1;

                        // 8. Insertar nueva designación
                        int nuevaDesignacionId = 0;
                        DateTime fechaDesignacion = DateTime.Now;
                        using (var cmdIns = new NpgsqlCommand(@"
                            INSERT INTO public.aocr_tbdesignacion_inspector
                            (
                                solicitud_id, inspeccion_id, estacion_id,
                                inspector_id, inspector_cedula, inspector_nombre,
                                inspector_apoyo_cedula, inspector_apoyo_nombre,
                                dircav_usuario_id, dircav_usuario_nombre,
                                estado, motivo, version, vigente,
                                fecha_designacion, creado_en, creado_por
                            )
                            VALUES
                            (
                                @solicitud_id, NULL, @estacion_id,
                                @inspector_id, @inspector_cedula, @inspector_nombre,
                                @inspector_apoyo_cedula, @inspector_apoyo_nombre,
                                @dircav_usuario_id, @dircav_usuario_nombre,
                                @estado, @motivo, @version, TRUE,
                                NOW(), NOW(), @creado_por
                            ) RETURNING id, fecha_designacion;", cn, tx))
                        {
                            cmdIns.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            cmdIns.Parameters.AddWithValue("@estacion_id", (object)p.EstacionId ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@inspector_id", p.InspectorId);
                            cmdIns.Parameters.AddWithValue("@inspector_cedula", (object)p.InspectorCedula ?? string.Empty);
                            cmdIns.Parameters.AddWithValue("@inspector_nombre", (object)p.InspectorNombre ?? string.Empty);
                            cmdIns.Parameters.AddWithValue("@inspector_apoyo_cedula", (object)p.InspectorApoyoCedula ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@inspector_apoyo_nombre", (object)p.InspectorApoyoNombre ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@dircav_usuario_id", p.DircavUsuarioId);
                            cmdIns.Parameters.AddWithValue("@dircav_usuario_nombre", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                            cmdIns.Parameters.AddWithValue("@estado", AocrEstadosProceso.DesignacionPendienteFirmaDircav);
                            cmdIns.Parameters.AddWithValue("@motivo", (object)(p.Motivo ?? string.Empty));
                            cmdIns.Parameters.AddWithValue("@version", versionNuevaDesignacion);
                            cmdIns.Parameters.AddWithValue("@creado_por", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));

                            using (var rdIns = cmdIns.ExecuteReader())
                            {
                                if (rdIns.Read())
                                {
                                    nuevaDesignacionId = rdIns.GetInt32(0);
                                    fechaDesignacion = rdIns.GetDateTime(1);
                                }
                            }
                        }

                        // 9. Actualizar estaciones de AC-02 en la misma transacción (SIN try-catch que oculte errores)
                        var estaciones = new List<SolicitudEstacionInspeccion>();
                        using (var cmdEstSel = new NpgsqlCommand(@"
                            SELECT id, solicitud_id, estacion_codigo, estacion_nombre, fecha_inicio, fecha_fin, inspector_id, inspector_nombre, inspeccion_id, estado, version, activo, observacion
                            FROM public.aocr_tbsolicitud_estacion
                            WHERE solicitud_id=@solicitud_id AND activo=TRUE
                            ORDER BY fecha_inicio ASC, id ASC;", cn, tx))
                        {
                            cmdEstSel.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            using (var rdEst = cmdEstSel.ExecuteReader())
                            {
                                while (rdEst.Read())
                                {
                                    estaciones.Add(new SolicitudEstacionInspeccion
                                    {
                                        Id = rdEst.GetInt32(0),
                                        SolicitudId = rdEst.GetInt32(1),
                                        EstacionCodigo = rdEst.IsDBNull(2) ? null : rdEst.GetString(2),
                                        EstacionNombre = rdEst.IsDBNull(3) ? null : rdEst.GetString(3),
                                        FechaInicio = rdEst.GetDateTime(4),
                                        FechaFin = rdEst.GetDateTime(5),
                                        InspectorId = rdEst.IsDBNull(6) ? (int?)null : rdEst.GetInt32(6),
                                        InspectorNombre = rdEst.IsDBNull(7) ? null : rdEst.GetString(7),
                                        InspeccionId = rdEst.IsDBNull(8) ? (int?)null : rdEst.GetInt32(8),
                                        Estado = rdEst.IsDBNull(9) ? null : rdEst.GetString(9),
                                        Version = rdEst.GetInt32(10),
                                        Activo = rdEst.GetBoolean(11),
                                        Observacion = rdEst.IsDBNull(12) ? null : rdEst.GetString(12)
                                    });
                                }
                            }
                        }

                        if (estaciones.Count > 0)
                        {
                            foreach (var est in estaciones)
                            {
                                if (!p.EstacionId.HasValue || est.Id == p.EstacionId.Value)
                                {
                                    est.InspectorId = p.InspectorId;
                                    est.InspectorNombre = p.InspectorNombre;
                                    est.Estado = "DESIGNADO";
                                    est.ActualizadoEn = DateTime.Now;
                                    est.ActualizadoPor = p.DircavUsuarioId;
                                }
                            }

                            var daoEst = estacionDao ?? new SolicitudEstacionDAO();
                            var guardadoEstOk = daoEst.GuardarEstacionesTransaccional(p.SolicitudId, estaciones, p.DircavUsuarioId, cn, tx);
                            if (!guardadoEstOk)
                            {
                                throw new InvalidOperationException("Fallo al guardar estaciones en la transacción de designación.");
                            }
                        }

                        // 10. Actualizar solicitud principal
                        int nuevaVersionSolicitud = versionActual + 1;
                        using (var cmdUpdSol = new NpgsqlCommand(@"
                            UPDATE public.aocr_tbsolicitud
                            SET codigo_tecnico=@inspector_id,
                                tecnico_responsable_id=@inspector_id,
                                tecnico_responsable_cedula=@inspector_cedula,
                                tecnico_responsable_nombre=@inspector_nombre,
                                tecnico_responsable_tipo=@inspector_tipo,
                                inspector_apoyo_cedula=@inspector_apoyo_cedula,
                                inspector_apoyo_nombre=@inspector_apoyo_nombre,
                                inspector_apoyo_tipo=@inspector_apoyo_tipo,
                                estado=@estado,
                                version=@version,
                                updated_at=NOW(),
                                updated_by=@updated_by
                            WHERE codigo_solicitud=@solicitud_id;", cn, tx))
                        {
                            cmdUpdSol.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            cmdUpdSol.Parameters.AddWithValue("@inspector_id", p.InspectorId);
                            cmdUpdSol.Parameters.AddWithValue("@inspector_cedula", (object)p.InspectorCedula ?? string.Empty);
                            cmdUpdSol.Parameters.AddWithValue("@inspector_nombre", (object)p.InspectorNombre ?? string.Empty);
                            cmdUpdSol.Parameters.AddWithValue("@inspector_tipo", (object)(p.InspectorTipo ?? "AIR"));
                            cmdUpdSol.Parameters.AddWithValue("@inspector_apoyo_cedula", (object)p.InspectorApoyoCedula ?? DBNull.Value);
                            cmdUpdSol.Parameters.AddWithValue("@inspector_apoyo_nombre", (object)p.InspectorApoyoNombre ?? DBNull.Value);
                            cmdUpdSol.Parameters.AddWithValue("@inspector_apoyo_tipo", (object)p.InspectorApoyoTipo ?? DBNull.Value);
                            cmdUpdSol.Parameters.AddWithValue("@estado", AocrEstadosProceso.DesignacionPendienteFirmaDircav);
                            cmdUpdSol.Parameters.AddWithValue("@version", nuevaVersionSolicitud);
                            cmdUpdSol.Parameters.AddWithValue("@updated_by", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                            cmdUpdSol.ExecuteNonQuery();
                        }

                        // 11. Inserción en historial documental
                        using (var cmdHist = new NpgsqlCommand(@"
                            INSERT INTO public.aocr_tbhistorial_documental
                            (codigo_solicitud, evento, detalle, codigo_usuario, fecha_evento, created_at, created_by)
                            VALUES
                            (@solicitud_id, @evento, @detalle, @usuario_id, NOW(), NOW(), @created_by);", cn, tx))
                        {
                            cmdHist.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                            cmdHist.Parameters.AddWithValue("@evento", "DESIGNACION_INSPECTOR_REGISTRADA");
                            cmdHist.Parameters.AddWithValue("@detalle", "Inspector " + p.InspectorNombre + " designado formalmente por DIRCAV (v" + versionNuevaDesignacion + ").");
                            cmdHist.Parameters.AddWithValue("@usuario_id", p.DircavUsuarioId);
                            cmdHist.Parameters.AddWithValue("@created_by", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                            cmdHist.ExecuteNonQuery();
                        }

                        // 12. Inserción en auditoría atómica
                        using (var cmdAudit = new NpgsqlCommand(@"
                            INSERT INTO public.aocr_tbauditoria
                            (entidad, accion, usuario, fecha, datos_previos, datos_nuevos)
                            VALUES
                            ('DIRCAV', 'DESIGNAR_INSPECTOR', @usuario, NOW(), @datos_previos, @datos_nuevos);", cn, tx))
                        {
                            cmdAudit.Parameters.AddWithValue("@usuario", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                            cmdAudit.Parameters.AddWithValue("@datos_previos", "Estado=" + estadoActual + "; Version=" + versionActual);
                            cmdAudit.Parameters.AddWithValue("@datos_nuevos", "Designado=" + p.InspectorNombre + "; Version=" + versionNuevaDesignacion);
                            cmdAudit.ExecuteNonQuery();
                        }

                        // 13. Encolar notificación provisional en email_queue (NO definitiva antes de AC-06)
                        var emailItem = new CapaDatos.Services.EmailQueueItem
                        {
                            Para = "coordinacion@dgac.gob.ec",
                            ParaNombre = "Coordinación AOCR",
                            Asunto = "AOCR - Designación de Inspector registrada #" + p.SolicitudId,
                            Cuerpo = "Se ha registrado la designación formal del Inspector " + p.InspectorNombre + ". Pendiente de firma digital DIRCAV.",
                            SolicitudId = p.SolicitudId,
                            TipoNotificacion = "SOLICITUD_DESIGNACION_INSPECTOR_REGISTRADA",
                            EventKey = "AOCR_DIRCAV_DESIG_" + p.SolicitudId + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                            Estado = "PENDIENTE"
                        };
                        bool duplicate;
                        new CapaDatos.Services.EmailQueueService().EncolarConAdjuntosEnTransaccion(cn, tx, emailItem, null, out duplicate);

                        tx.Commit();

                        return new DircavTransaccionResultado
                        {
                            Exitoso = true,
                            HttpStatusCode = 200,
                            DesignacionId = nuevaDesignacionId,
                            Version = versionNuevaDesignacion,
                            NuevoEstado = AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                            Mensaje = string.Format("Inspector '{0}' designado formalmente (Versión {1}). Proceda a la firma digital del oficio de designación.", p.InspectorNombre, versionNuevaDesignacion),
                            Designacion = new AocrDesignacionInspector
                            {
                                Id = nuevaDesignacionId,
                                SolicitudId = p.SolicitudId,
                                EstacionId = p.EstacionId,
                                InspectorId = p.InspectorId,
                                InspectorCedula = p.InspectorCedula,
                                InspectorNombre = p.InspectorNombre,
                                InspectorApoyoCedula = p.InspectorApoyoCedula,
                                InspectorApoyoNombre = p.InspectorApoyoNombre,
                                DircavUsuarioId = p.DircavUsuarioId,
                                DircavUsuarioNombre = p.DircavUsuarioNombre,
                                Estado = AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                                Motivo = p.Motivo,
                                Version = versionNuevaDesignacion,
                                Vigente = true,
                                FechaDesignacion = fechaDesignacion
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        Trace.TraceError("[DIRCAV][DESIGNAR_ERROR] SolicitudId=" + p.SolicitudId + "; Error=" + ex);
                        return new DircavTransaccionResultado
                        {
                            Exitoso = false,
                            HttpStatusCode = 500,
                            Mensaje = "No se pudo completar la designación del inspector. Ocurrió un error interno en la transacción."
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Registra transaccionalmente la firma digital del oficio de designación (AC-06).
        /// Actualiza aocr_tbdesignacion_inspector, aocr_tbsolicitud (DESIGNACION_FIRMADA_DIRCAV),
        /// genera historial documental, registra auditoría institucional y encola notificación outbox al Inspector.
        /// </summary>
        public DircavTransaccionResultado EjecutarFirmaDesignacionTransaccional(DircavFirmarDesignacionParams p, NpgsqlTransaction externalTx = null)
        {
            if (p == null || p.SolicitudId <= 0)
            {
                return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };
            }
            if (p.DircavUsuarioId <= 0)
            {
                return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 401, Mensaje = "Sesión no válida o expirada." };
            }
            if (string.IsNullOrWhiteSpace(p.RutaDocumentoFirmado) || string.IsNullOrWhiteSpace(p.HashDocumento))
            {
                return new DircavTransaccionResultado { Exitoso = false, HttpStatusCode = 400, Mensaje = "Los datos del documento firmado (ruta y hash) son obligatorios." };
            }

            var conExterno = externalTx != null;
            var cn = conExterno ? externalTx.Connection : CrearConexion();

            try
            {
                if (!conExterno && cn.State != ConnectionState.Open)
                {
                    cn.Open();
                }

                AsegurarEsquema(cn);

                var tx = conExterno ? externalTx : cn.BeginTransaction();
                try
                {
                    // 1. Bloqueo pesimista de designación vigente
                    const string sqlBuscarDesig = @"
SELECT id, solicitud_id, inspeccion_id, estacion_id,
       inspector_id, inspector_cedula, inspector_nombre,
       inspector_apoyo_cedula, inspector_apoyo_nombre,
       dircav_usuario_id, dircav_usuario_nombre,
       estado, motivo, version, vigente, fecha_designacion, fecha_firma,
       ruta_pdf, ruta_documento_firmado, hash_documento, firmado,
       usuario_firma, tamanio_bytes, mime_type, huella_certificado, codigo_verificacion
FROM public.aocr_tbdesignacion_inspector
WHERE solicitud_id = @solicitud_id
  AND COALESCE(estacion_id, 0) = COALESCE(@estacion_id, 0)
  AND vigente = TRUE
FOR UPDATE;";

                    AocrDesignacionInspector desigVigente = null;
                    using (var cmdBuscar = new NpgsqlCommand(sqlBuscarDesig, cn, tx))
                    {
                        cmdBuscar.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                        cmdBuscar.Parameters.AddWithValue("@estacion_id", (object)p.EstacionId ?? DBNull.Value);
                        using (var dr = cmdBuscar.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                desigVigente = Mapear(dr);
                            }
                        }
                    }

                    if (desigVigente == null)
                    {
                        if (!conExterno) tx.Rollback();
                        return new DircavTransaccionResultado
                        {
                            Exitoso = false,
                            HttpStatusCode = 404,
                            Mensaje = "No se encontró una designación formal vigente para la solicitud indicada."
                        };
                    }

                    // 2. Control de Idempotencia estricta
                    if (desigVigente.Firmado)
                    {
                        if (!conExterno) tx.Rollback();
                        return new DircavTransaccionResultado
                        {
                            Exitoso = true,
                            HttpStatusCode = 200,
                            EsIdempotente = true,
                            DesignacionId = desigVigente.Id,
                            Version = desigVigente.Version,
                            NuevoEstado = AocrEstadosProceso.DesignacionFirmadaDircav,
                            Mensaje = "El oficio de designación ya se encuentra firmado formalmente por DIRCAV.",
                            Designacion = desigVigente
                        };
                    }

                    // 3. Bloqueo pesimista de solicitud principal
                    string estadoSolActual = null;
                    int versionSolActual = 1;
                    using (var cmdSol = new NpgsqlCommand("SELECT estado, COALESCE(version, 1) FROM public.aocr_tbsolicitud WHERE codigo_solicitud=@solicitud_id FOR UPDATE;", cn, tx))
                    {
                        cmdSol.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                        using (var rd = cmdSol.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                estadoSolActual = rd.IsDBNull(0) ? string.Empty : rd.GetString(0).Trim();
                                versionSolActual = rd.GetInt32(1);
                            }
                            else
                            {
                                if (!conExterno) tx.Rollback();
                                return new DircavTransaccionResultado
                                {
                                    Exitoso = false,
                                    HttpStatusCode = 404,
                                    Mensaje = "Solicitud no encontrada en base de datos."
                                };
                            }
                        }
                    }

                    // 4. Actualizar tabla aocr_tbdesignacion_inspector
                    var fechaFirma = DateTime.Now;
                    const string sqlUpdDesig = @"
UPDATE public.aocr_tbdesignacion_inspector
SET estado = @estado,
    fecha_firma = @fecha_firma,
    ruta_pdf = @ruta_pdf,
    ruta_documento_firmado = @ruta_firmado,
    hash_documento = @hash,
    firmado = TRUE,
    usuario_firma = @usuario_firma,
    tamanio_bytes = @tamanio_bytes,
    mime_type = @mime_type,
    huella_certificado = @huella_certificado,
    codigo_verificacion = @codigo_verificacion,
    actualizado_en = NOW(),
    actualizado_por = @actualizado_por
WHERE id = @id;";

                    using (var cmdUpd = new NpgsqlCommand(sqlUpdDesig, cn, tx))
                    {
                        cmdUpd.Parameters.AddWithValue("@id", desigVigente.Id);
                        cmdUpd.Parameters.AddWithValue("@estado", AocrEstadosProceso.DesignacionFirmadaDircav);
                        cmdUpd.Parameters.AddWithValue("@fecha_firma", fechaFirma);
                        cmdUpd.Parameters.AddWithValue("@ruta_pdf", (object)(p.RutaPdf ?? p.RutaDocumentoFirmado) ?? DBNull.Value);
                        cmdUpd.Parameters.AddWithValue("@ruta_firmado", p.RutaDocumentoFirmado);
                        cmdUpd.Parameters.AddWithValue("@hash", p.HashDocumento);
                        cmdUpd.Parameters.AddWithValue("@usuario_firma", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                        cmdUpd.Parameters.AddWithValue("@tamanio_bytes", p.TamanioBytes);
                        cmdUpd.Parameters.AddWithValue("@mime_type", (object)(p.MimeType ?? "application/pdf"));
                        cmdUpd.Parameters.AddWithValue("@huella_certificado", (object)p.HuellaCertificado ?? DBNull.Value);
                        cmdUpd.Parameters.AddWithValue("@codigo_verificacion", (object)p.CodigoVerificacion ?? DBNull.Value);
                        cmdUpd.Parameters.AddWithValue("@actualizado_por", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                        cmdUpd.ExecuteNonQuery();
                    }

                    // 5. Actualizar solicitud principal al nuevo estado institucional
                    int nuevaVersionSol = versionSolActual + 1;
                    using (var cmdUpdSol = new NpgsqlCommand(@"
UPDATE public.aocr_tbsolicitud
SET estado = @estado,
    version = @version,
    updated_at = NOW(),
    updated_by = @updated_by
WHERE codigo_solicitud = @solicitud_id;", cn, tx))
                    {
                        cmdUpdSol.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                        cmdUpdSol.Parameters.AddWithValue("@estado", AocrEstadosProceso.DesignacionFirmadaDircav);
                        cmdUpdSol.Parameters.AddWithValue("@version", nuevaVersionSol);
                        cmdUpdSol.Parameters.AddWithValue("@updated_by", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                        cmdUpdSol.ExecuteNonQuery();
                    }

                    // 6. Actualizar estaciones de inspección si aplica
                    using (var cmdEst = new NpgsqlCommand(@"
UPDATE public.aocr_tbsolicitud_estacion
SET estado = 'DESIGNADO',
    actualizado_en = NOW(),
    actualizado_por = @actualizado_por
WHERE solicitud_id = @solicitud_id
  AND activo = TRUE
  AND (@estacion_id IS NULL OR id = @estacion_id);", cn, tx))
                    {
                        cmdEst.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                        cmdEst.Parameters.AddWithValue("@estacion_id", (object)p.EstacionId ?? DBNull.Value);
                        cmdEst.Parameters.AddWithValue("@actualizado_por", p.DircavUsuarioId);
                        cmdEst.ExecuteNonQuery();
                    }

                    // 7. Inserción en historial documental
                    var hashCorto = !string.IsNullOrEmpty(p.HashDocumento) && p.HashDocumento.Length > 16
                        ? p.HashDocumento.Substring(0, 16) + "..."
                        : p.HashDocumento;
                    using (var cmdHist = new NpgsqlCommand(@"
INSERT INTO public.aocr_tbhistorial_documental
(codigo_solicitud, evento, detalle, codigo_usuario, fecha_evento, created_at, created_by)
VALUES
(@solicitud_id, @evento, @detalle, @usuario_id, NOW(), NOW(), @created_by);", cn, tx))
                    {
                        cmdHist.Parameters.AddWithValue("@solicitud_id", p.SolicitudId);
                        cmdHist.Parameters.AddWithValue("@evento", "DESIGNACION_OFICIO_FIRMADO_DIRCAV");
                        cmdHist.Parameters.AddWithValue("@detalle", string.Format("Oficio de designación v{0} firmado digitalmente por DIRCAV ({1}). Código de verificación: {2}. Hash: {3}.", desigVigente.Version, p.DircavUsuarioNombre ?? "DIRCAV", p.CodigoVerificacion, hashCorto));
                        cmdHist.Parameters.AddWithValue("@usuario_id", p.DircavUsuarioId);
                        cmdHist.Parameters.AddWithValue("@created_by", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                        cmdHist.ExecuteNonQuery();
                    }

                    // 8. Inserción en auditoría atómica
                    using (var cmdAudit = new NpgsqlCommand(@"
INSERT INTO public.aocr_tbauditoria
(entidad, accion, usuario, fecha, datos_previos, datos_nuevos)
VALUES
('DIRCAV', 'FIRMAR_DESIGNACION_INSPECTOR', @usuario, NOW(), @datos_previos, @datos_nuevos);", cn, tx))
                    {
                        cmdAudit.Parameters.AddWithValue("@usuario", (object)(p.DircavUsuarioNombre ?? "DIRCAV"));
                        cmdAudit.Parameters.AddWithValue("@datos_previos", "Estado=" + estadoSolActual + "; Version=" + versionSolActual);
                        cmdAudit.Parameters.AddWithValue("@datos_nuevos", "Estado=" + AocrEstadosProceso.DesignacionFirmadaDircav + "; CodigoVerif=" + p.CodigoVerificacion + "; Hash=" + p.HashDocumento);
                        cmdAudit.ExecuteNonQuery();
                    }

                    // 9. Encolar notificación en email_queue (outbox post-commit)
                    var emailItem = new CapaDatos.Services.EmailQueueItem
                    {
                        Para = "inspector@dgac.gob.ec",
                        ParaNombre = desigVigente.InspectorNombre ?? "Inspector Asignado",
                        Asunto = "AOCR - Oficio de Designación Firmado Digitalmente #" + p.SolicitudId,
                        Cuerpo = string.Format("Se ha firmado digitalmente el oficio de designación (Versión {0}) para la solicitud #{1} por parte de la Autoridad DIRCAV.", desigVigente.Version, p.SolicitudId),
                        SolicitudId = p.SolicitudId,
                        TipoNotificacion = "SOLICITUD_DESIGNACION_FIRMADA_INSPECTOR",
                        EventKey = "AOCR_DESIG_FIRMADA_" + p.SolicitudId + "_v" + desigVigente.Version + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                        Estado = "PENDIENTE"
                    };
                    bool duplicate;
                    new CapaDatos.Services.EmailQueueService().EncolarConAdjuntosEnTransaccion(cn, tx, emailItem, null, out duplicate);

                    if (!conExterno)
                    {
                        tx.Commit();
                    }

                    desigVigente.Estado = AocrEstadosProceso.DesignacionFirmadaDircav;
                    desigVigente.Firmado = true;
                    desigVigente.FechaFirma = fechaFirma;
                    desigVigente.RutaDocumentoFirmado = p.RutaDocumentoFirmado;
                    desigVigente.HashDocumento = p.HashDocumento;
                    desigVigente.UsuarioFirma = p.DircavUsuarioNombre ?? "DIRCAV";
                    desigVigente.TamanioBytes = p.TamanioBytes;
                    desigVigente.HuellaCertificado = p.HuellaCertificado;
                    desigVigente.CodigoVerificacion = p.CodigoVerificacion;

                    return new DircavTransaccionResultado
                    {
                        Exitoso = true,
                        HttpStatusCode = 200,
                        DesignacionId = desigVigente.Id,
                        Version = desigVigente.Version,
                        NuevoEstado = AocrEstadosProceso.DesignacionFirmadaDircav,
                        Mensaje = string.Format("Oficio de designación v{0} firmado digitalmente y notificado formalmente.", desigVigente.Version),
                        Designacion = desigVigente
                    };
                }
                catch (Exception ex)
                {
                    if (!conExterno) tx.Rollback();
                    Trace.TraceError("[DIRCAV][FIRMAR_ERROR] SolicitudId=" + p.SolicitudId + "; Error=" + ex);
                    return new DircavTransaccionResultado
                    {
                        Exitoso = false,
                        HttpStatusCode = 500,
                        Mensaje = "Error interno en la transacción al registrar la firma digital de la designación: " + ex.Message
                    };
                }
            }
            finally
            {
                if (!conExterno && cn != null)
                {
                    cn.Dispose();
                }
            }
        }

        private static AocrDesignacionInspector Mapear(IDataRecord dr)
        {
            var d = new AocrDesignacionInspector
            {
                Id = dr.GetInt32(dr.GetOrdinal("id")),
                SolicitudId = dr.GetInt32(dr.GetOrdinal("solicitud_id")),
                InspeccionId = dr.IsDBNull(dr.GetOrdinal("inspeccion_id")) ? (int?)null : dr.GetInt32(dr.GetOrdinal("inspeccion_id")),
                EstacionId = dr.IsDBNull(dr.GetOrdinal("estacion_id")) ? (int?)null : dr.GetInt32(dr.GetOrdinal("estacion_id")),
                InspectorId = dr.GetInt32(dr.GetOrdinal("inspector_id")),
                InspectorCedula = dr.IsDBNull(dr.GetOrdinal("inspector_cedula")) ? null : dr.GetString(dr.GetOrdinal("inspector_cedula")),
                InspectorNombre = dr.IsDBNull(dr.GetOrdinal("inspector_nombre")) ? null : dr.GetString(dr.GetOrdinal("inspector_nombre")),
                InspectorApoyoCedula = dr.IsDBNull(dr.GetOrdinal("inspector_apoyo_cedula")) ? null : dr.GetString(dr.GetOrdinal("inspector_apoyo_cedula")),
                InspectorApoyoNombre = dr.IsDBNull(dr.GetOrdinal("inspector_apoyo_nombre")) ? null : dr.GetString(dr.GetOrdinal("inspector_apoyo_nombre")),
                DircavUsuarioId = dr.GetInt32(dr.GetOrdinal("dircav_usuario_id")),
                DircavUsuarioNombre = dr.IsDBNull(dr.GetOrdinal("dircav_usuario_nombre")) ? null : dr.GetString(dr.GetOrdinal("dircav_usuario_nombre")),
                Estado = dr.IsDBNull(dr.GetOrdinal("estado")) ? null : dr.GetString(dr.GetOrdinal("estado")),
                Motivo = dr.IsDBNull(dr.GetOrdinal("motivo")) ? null : dr.GetString(dr.GetOrdinal("motivo")),
                Version = dr.GetInt32(dr.GetOrdinal("version")),
                Vigente = dr.GetBoolean(dr.GetOrdinal("vigente")),
                FechaDesignacion = dr.GetDateTime(dr.GetOrdinal("fecha_designacion")),
                FechaFirma = dr.IsDBNull(dr.GetOrdinal("fecha_firma")) ? (DateTime?)null : dr.GetDateTime(dr.GetOrdinal("fecha_firma"))
            };

            // Columnas AC-06 con protección ordinal
            try { d.RutaPdf = dr.IsDBNull(dr.GetOrdinal("ruta_pdf")) ? null : dr.GetString(dr.GetOrdinal("ruta_pdf")); } catch { }
            try { d.RutaDocumentoFirmado = dr.IsDBNull(dr.GetOrdinal("ruta_documento_firmado")) ? null : dr.GetString(dr.GetOrdinal("ruta_documento_firmado")); } catch { }
            try { d.HashDocumento = dr.IsDBNull(dr.GetOrdinal("hash_documento")) ? null : dr.GetString(dr.GetOrdinal("hash_documento")); } catch { }
            try { d.Firmado = !dr.IsDBNull(dr.GetOrdinal("firmado")) && dr.GetBoolean(dr.GetOrdinal("firmado")); } catch { }
            try { d.UsuarioFirma = dr.IsDBNull(dr.GetOrdinal("usuario_firma")) ? null : dr.GetString(dr.GetOrdinal("usuario_firma")); } catch { }
            try { d.TamanioBytes = dr.IsDBNull(dr.GetOrdinal("tamanio_bytes")) ? (long?)null : dr.GetInt64(dr.GetOrdinal("tamanio_bytes")); } catch { }
            try { d.MimeType = dr.IsDBNull(dr.GetOrdinal("mime_type")) ? "application/pdf" : dr.GetString(dr.GetOrdinal("mime_type")); } catch { }
            try { d.HuellaCertificado = dr.IsDBNull(dr.GetOrdinal("huella_certificado")) ? null : dr.GetString(dr.GetOrdinal("huella_certificado")); } catch { }
            try { d.CodigoVerificacion = dr.IsDBNull(dr.GetOrdinal("codigo_verificacion")) ? null : dr.GetString(dr.GetOrdinal("codigo_verificacion")); } catch { }

            return d;
        }
    }
}
