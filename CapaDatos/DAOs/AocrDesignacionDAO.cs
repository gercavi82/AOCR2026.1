using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
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
       usuario_firma, tamanio_bytes, mime_type
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
       usuario_firma, tamanio_bytes, mime_type
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
       usuario_firma, tamanio_bytes, mime_type
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

            return d;
        }
    }
}
