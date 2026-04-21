using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using CapaDatos.Entidades;
using Npgsql;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// DAO de solo-lectura que consulta la vista consolidada
    /// v_aocr_trazabilidad_tramite y la tabla aocr_tbdocumento_subsanacion.
    /// Diseñado para ser tolerante a fallos: si la vista o la tabla no
    /// existen, devuelve listas vacías sin lanzar excepciones, para no
    /// romper la vista de detalle en ambientes donde la migración aún
    /// no se haya aplicado.
    /// </summary>
    public class TrazabilidadDAO
    {
        private string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString; }
        }

        // =================================================================
        // LÍNEA DE TIEMPO UNIFICADA
        // =================================================================
        public List<EventoTrazabilidad> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<EventoTrazabilidad>();
            if (codigoSolicitud <= 0) return lista;

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                if (!ExisteRelacion(cn, "v_aocr_trazabilidad_tramite"))
                {
                    return lista;
                }

                const string sql = @"
                    SELECT
                        codigo_solicitud,
                        fecha_evento,
                        usuario_id,
                        usuario_nombre,
                        rol,
                        modulo,
                        accion,
                        estado_anterior,
                        estado_nuevo,
                        observacion,
                        codigo_documento,
                        documento_afectado,
                        fuente
                    FROM v_aocr_trazabilidad_tramite
                    WHERE codigo_solicitud = @id
                    ORDER BY fecha_evento DESC NULLS LAST;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(MapearEvento(rd));
                        }
                    }
                }
            }

            return lista;
        }

        // =================================================================
        // DOCUMENTOS DE SUBSANACIÓN (RESPUESTAS DEL RT A OBSERVACIONES)
        // =================================================================
        public List<DocumentoSubsanacionRegistro> ObtenerDocumentosSubsanacionPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<DocumentoSubsanacionRegistro>();
            if (codigoSolicitud <= 0) return lista;

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                if (!ExisteRelacion(cn, "aocr_tbdocumento_subsanacion") ||
                    !ExisteRelacion(cn, "aocr_tbsubsanacion"))
                {
                    return lista;
                }

                const string sql = @"
                    SELECT
                        ds.codigo_documento,
                        ds.codigo_subsanacion,
                        s.codigo_solicitud,
                        ds.nombre_archivo,
                        ds.ruta_archivo,
                        ds.tipo_documento,
                        ds.tamanio_bytes,
                        ds.fecha_carga,
                        ds.codigo_usuario_carga,
                        COALESCE(u.nombres || ' ' || u.apellidos, u.usuario, 'RT') AS usuario_nombre,
                        s.observacion        AS observacion_motivo,
                        s.fecha_solicitud    AS fecha_subsanacion
                    FROM aocr_tbdocumento_subsanacion ds
                    JOIN aocr_tbsubsanacion s ON s.codigo_subsanacion = ds.codigo_subsanacion
                    LEFT JOIN usuario u ON u.idusuario = ds.codigo_usuario_carga
                    WHERE s.codigo_solicitud = @id
                    ORDER BY ds.fecha_carga DESC NULLS LAST, ds.codigo_documento DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(MapearDocSubsanacion(rd));
                        }
                    }
                }
            }

            return lista;
        }

        // =================================================================
        // HELPERS
        // =================================================================
        private static EventoTrazabilidad MapearEvento(IDataRecord rd)
        {
            return new EventoTrazabilidad
            {
                CodigoSolicitud   = rd["codigo_solicitud"]   == DBNull.Value ? 0  : Convert.ToInt32(rd["codigo_solicitud"]),
                FechaEvento       = rd["fecha_evento"]       == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(rd["fecha_evento"]),
                UsuarioId         = rd["usuario_id"]         == DBNull.Value ? (int?)null : Convert.ToInt32(rd["usuario_id"]),
                UsuarioNombre     = rd["usuario_nombre"]     == DBNull.Value ? null : rd["usuario_nombre"].ToString(),
                Rol               = rd["rol"]                == DBNull.Value ? null : rd["rol"].ToString(),
                Modulo            = rd["modulo"]             == DBNull.Value ? null : rd["modulo"].ToString(),
                Accion            = rd["accion"]             == DBNull.Value ? null : rd["accion"].ToString(),
                EstadoAnterior    = rd["estado_anterior"]    == DBNull.Value ? null : rd["estado_anterior"].ToString(),
                EstadoNuevo       = rd["estado_nuevo"]       == DBNull.Value ? null : rd["estado_nuevo"].ToString(),
                Observacion       = rd["observacion"]        == DBNull.Value ? null : rd["observacion"].ToString(),
                CodigoDocumento   = rd["codigo_documento"]   == DBNull.Value ? (int?)null : Convert.ToInt32(rd["codigo_documento"]),
                DocumentoAfectado = rd["documento_afectado"] == DBNull.Value ? null : rd["documento_afectado"].ToString(),
                Fuente            = rd["fuente"]             == DBNull.Value ? null : rd["fuente"].ToString()
            };
        }

        private static DocumentoSubsanacionRegistro MapearDocSubsanacion(IDataRecord rd)
        {
            return new DocumentoSubsanacionRegistro
            {
                CodigoDocumento             = rd["codigo_documento"]        == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_documento"]),
                CodigoSubsanacion           = rd["codigo_subsanacion"]      == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_subsanacion"]),
                CodigoSolicitud             = rd["codigo_solicitud"]        == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_solicitud"]),
                NombreArchivo               = rd["nombre_archivo"]          == DBNull.Value ? null : rd["nombre_archivo"].ToString(),
                RutaArchivo                 = rd["ruta_archivo"]            == DBNull.Value ? null : rd["ruta_archivo"].ToString(),
                TipoDocumento               = rd["tipo_documento"]          == DBNull.Value ? null : rd["tipo_documento"].ToString(),
                TamanioBytes                = rd["tamanio_bytes"]           == DBNull.Value ? (long?)null : Convert.ToInt64(rd["tamanio_bytes"]),
                FechaCarga                  = rd["fecha_carga"]             == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(rd["fecha_carga"]),
                CodigoUsuarioCarga          = rd["codigo_usuario_carga"]    == DBNull.Value ? (int?)null : Convert.ToInt32(rd["codigo_usuario_carga"]),
                UsuarioCargaNombre          = rd["usuario_nombre"]          == DBNull.Value ? null : rd["usuario_nombre"].ToString(),
                ObservacionMotivo           = rd["observacion_motivo"]      == DBNull.Value ? null : rd["observacion_motivo"].ToString(),
                FechaSubsanacionSolicitada  = rd["fecha_subsanacion"]       == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_subsanacion"])
            };
        }

        private static bool ExisteRelacion(NpgsqlConnection cn, string nombre)
        {
            try
            {
                using (var cmd = new NpgsqlCommand("SELECT to_regclass(@n) IS NOT NULL;", cn))
                {
                    cmd.Parameters.AddWithValue("@n", nombre);
                    var r = cmd.ExecuteScalar();
                    return r != null && r != DBNull.Value && Convert.ToBoolean(r);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
