using System;
using System.Collections.Generic;
using System.Configuration;
using CapaDatos.Models;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed class DocumentoPdfDAO
    {
        private readonly string _connectionString;

        public DocumentoPdfDAO()
        {
            var configured = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString = configured != null ? configured.ConnectionString : ConexionDAO.CadenaConexion;
        }

        public NpgsqlConnection CrearConexion() { return new NpgsqlConnection(_connectionString); }

        public DocumentoPdfOrigenValidacion ValidarOrigen(int solicitudId, int inspeccionId, int origenId, string tipo, int inspectorId)
        {
            const string sql = @"SELECT g.codigo_documento,
                    (s.deleted_at IS NULL) solicitud_activa,
                    (COALESCE(i.codigo_inspector,s.codigo_tecnico,0)=@usuario) inspector_asignado,
                    EXISTS(SELECT 1 FROM public.aocr_tbfirma_documento f
                           WHERE f.codigo_solicitud=s.codigo_solicitud
                             AND f.codigo_inspeccion=i.codigo_inspeccion
                             AND " + CanonicalFirma + @"=@tipo) firmado,
                    g.estado,g.version,g.codigo_compania
                FROM public.aocr_tbdocumento_generado g
                JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=g.codigo_solicitud
                JOIN public.aocr_tbinspeccion i ON i.codigo_inspeccion=g.codigo_inspeccion
                                              AND i.codigo_solicitud=s.codigo_solicitud
                WHERE g.codigo_documento=@origen AND g.codigo_solicitud=@solicitud
                  AND g.codigo_inspeccion=@inspeccion
                  AND " + CanonicalGenerado + @"=@tipo
                  AND g.vigente=TRUE AND g.eliminado=FALSE;";
            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@origen", origenId);
                cmd.Parameters.AddWithValue("@tipo", tipo);
                cmd.Parameters.AddWithValue("@usuario", inspectorId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return new DocumentoPdfOrigenValidacion();
                    return new DocumentoPdfOrigenValidacion
                    {
                        Existe = true,
                        SolicitudActiva = Convert.ToBoolean(rd["solicitud_activa"]),
                        InspectorAsignado = Convert.ToBoolean(rd["inspector_asignado"]),
                        Firmado = Convert.ToBoolean(rd["firmado"]),
                        Estado = Convert.ToString(rd["estado"]),
                        Version = Convert.ToInt32(rd["version"]),
                        CodigoCompania = Convert.ToString(rd["codigo_compania"])
                    };
                }
            }
        }

        public DocumentoPdfRegistro ObtenerPorId(int id)
        {
            return ConsultarUno(BaseSelect + " WHERE di.codigo_documento=@id", c => c.Parameters.AddWithValue("@id", id));
        }

        public DocumentoPdfRegistro ObtenerVigente(int solicitudId, int inspeccionId, string tipo)
        {
            return ConsultarUno(BaseSelect + @" WHERE i.codigo_solicitud=@solicitud AND di.codigo_inspeccion=@inspeccion
                    AND " + CanonicalInspeccion + @"=@tipo
                ORDER BY di.version DESC,di.codigo_documento DESC LIMIT 1", c =>
            {
                c.Parameters.AddWithValue("@solicitud", solicitudId);
                c.Parameters.AddWithValue("@inspeccion", inspeccionId);
                c.Parameters.AddWithValue("@tipo", tipo);
            });
        }

        public IList<DocumentoPdfRegistro> ObtenerVersiones(int solicitudId, int inspeccionId, string tipo)
        {
            var result = new List<DocumentoPdfRegistro>();
            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(BaseSelect + @" WHERE i.codigo_solicitud=@solicitud AND di.codigo_inspeccion=@inspeccion
                    AND " + CanonicalInspeccion + @"=@tipo
                ORDER BY di.version DESC,di.codigo_documento DESC", cn))
            {
                cn.Open();
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@tipo", tipo);
                using (var rd = cmd.ExecuteReader()) while (rd.Read()) result.Add(Map(rd));
            }
            return result;
        }

        public IList<DocumentoPdfRegistro> ObtenerTodosOficiales()
        {
            var result = new List<DocumentoPdfRegistro>();
            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(BaseSelect + @" WHERE UPPER(TRIM(di.tipo_documento)) IN
                ('RECONOCIMIENTO','AOCR','CONDICIONES','CONDICIONES_LIMITACIONES')
                ORDER BY di.codigo_inspeccion,di.tipo_documento,di.version", cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader()) while (rd.Read()) result.Add(Map(rd));
            }
            return result;
        }

        public void BloquearGeneracion(NpgsqlConnection cn, NpgsqlTransaction tx, string key)
        {
            using (var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@key));", cn, tx))
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.ExecuteNonQuery();
            }
        }

        public DocumentoPdfRegistro ObtenerPorIdempotencia(NpgsqlConnection cn, NpgsqlTransaction tx, int inspeccionId, string tipo, string clave)
        {
            const string sql = @"SELECT codigo_documento FROM public.aocr_tbdocumento_inspeccion
                WHERE codigo_inspeccion=@inspeccion
                  AND " + CanonicalSinAlias + @"=@tipo
                  AND observacion LIKE @clave ESCAPE E'\\'
                ORDER BY codigo_documento DESC LIMIT 1;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@tipo", tipo);
                cmd.Parameters.AddWithValue("@clave", "%IDEMPOTENCY=" + EscapeLike(clave) + ";%");
                var id = cmd.ExecuteScalar();
                return id == null || id == DBNull.Value ? null : ObtenerPorIdInterno(cn, tx, Convert.ToInt32(id));
            }
        }

        public int ObtenerSiguienteVersion(NpgsqlConnection cn, NpgsqlTransaction tx, int inspeccionId, string tipo)
        {
            const string sql = @"SELECT COALESCE(MAX(version),0)+1 FROM public.aocr_tbdocumento_inspeccion
                WHERE codigo_inspeccion=@inspeccion AND " + CanonicalSinAlias + "=@tipo;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@tipo", tipo);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public DocumentoPdfRegistro Registrar(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId,
            int inspeccionId, int origenId, string tipo, int version, int versionOrigen, int usuarioId,
            string rol, string ruta, string nombre, string hash, long size, string clave)
        {
            const string insert = @"INSERT INTO public.aocr_tbdocumento_inspeccion
                (codigo_inspeccion,codigo_documento_base,version,tipo_documento,nombre_archivo_original,
                 nombre_archivo_storage,ruta_archivo,hash_archivo,tamano_bytes,content_type,observacion,
                 subido_por_rol,codigo_usuario,created_at)
                VALUES(@inspeccion,@origen,@version,@tipo,@nombre,@nombre,@ruta,@hash,@size,'application/pdf',
                       @observacion,@rol,@usuario,NOW()) RETURNING codigo_documento;";
            int id;
            using (var cmd = new NpgsqlCommand(insert, cn, tx))
            {
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@origen", origenId);
                cmd.Parameters.AddWithValue("@version", version);
                cmd.Parameters.AddWithValue("@tipo", tipo);
                cmd.Parameters.AddWithValue("@nombre", nombre);
                cmd.Parameters.AddWithValue("@ruta", ruta);
                cmd.Parameters.AddWithValue("@hash", hash);
                cmd.Parameters.AddWithValue("@size", size);
                cmd.Parameters.AddWithValue("@observacion", "IDEMPOTENCY=" + clave + ";VERSION_ORIGEN=" + versionOrigen + ";ESTADO=GENERADO;");
                cmd.Parameters.AddWithValue("@rol", rol ?? string.Empty);
                cmd.Parameters.AddWithValue("@usuario", usuarioId);
                id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            const string update = @"UPDATE public.aocr_tbdocumento_generado
                SET ruta_documento=@ruta,nombre_archivo=@nombre,tamanio_pdf=@size,estado='GENERADO',
                    fecha_generacion=NOW(),fecha_actualizacion=NOW()
                WHERE codigo_documento=@origen AND version=@versionOrigen AND vigente=TRUE
                  AND eliminado=FALSE AND COALESCE(hash_pdf_firmado,'')='';";
            using (var cmd = new NpgsqlCommand(update, cn, tx))
            {
                cmd.Parameters.AddWithValue("@ruta", ruta);
                cmd.Parameters.AddWithValue("@nombre", nombre);
                cmd.Parameters.AddWithValue("@size", size);
                cmd.Parameters.AddWithValue("@origen", origenId);
                cmd.Parameters.AddWithValue("@versionOrigen", versionOrigen);
                if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Conflicto al activar la versión PDF.");
            }
            RegistrarAuditoria(cn, tx, "PDF_PERSISTIDO", solicitudId, usuarioId,
                "DocumentoPdfId=" + id + ";Hash=" + hash + ";Ruta=" + ruta + ";Version=" + version);
            return ObtenerPorIdInterno(cn, tx, id);
        }

        public void RegistrarAuditoria(NpgsqlConnection cn, NpgsqlTransaction tx, string accion, int solicitudId, int usuarioId, string datos)
        {
            const string sql = @"INSERT INTO public.aocr_tbauditoria
                (entidad,accion,usuario,fecha,datos_previos,datos_nuevos)
                VALUES('DOCUMENTO_PDF',@accion,@usuario,NOW(),NULL,@datos);";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@accion", accion);
                cmd.Parameters.AddWithValue("@usuario", usuarioId.ToString());
                cmd.Parameters.AddWithValue("@datos", "SolicitudId=" + solicitudId + ";" + (datos ?? string.Empty));
                cmd.ExecuteNonQuery();
            }
        }

        private DocumentoPdfRegistro ConsultarUno(string sql, Action<NpgsqlCommand> bind)
        {
            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open(); bind(cmd);
                using (var rd = cmd.ExecuteReader()) return rd.Read() ? Map(rd) : null;
            }
        }

        private static string EscapeLike(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }

        private DocumentoPdfRegistro ObtenerPorIdInterno(NpgsqlConnection cn, NpgsqlTransaction tx, int id)
        {
            using (var cmd = new NpgsqlCommand(BaseSelect + " WHERE di.codigo_documento=@id", cn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var rd = cmd.ExecuteReader()) return rd.Read() ? Map(rd) : null;
            }
        }

        private static DocumentoPdfRegistro Map(NpgsqlDataReader r)
        {
            return new DocumentoPdfRegistro
            {
                Id=Convert.ToInt32(r["id"]),SolicitudId=Convert.ToInt32(r["solicitud_id"]),InspeccionId=Convert.ToInt32(r["inspeccion_id"]),
                DocumentoOrigenId=r["origen_id"]==DBNull.Value?0:Convert.ToInt32(r["origen_id"]),TipoDocumento=Convert.ToString(r["tipo_documento"]),
                Version=Convert.ToInt32(r["version"]),Estado=Convert.ToString(r["estado"]),NombreArchivo=Convert.ToString(r["nombre_archivo"]),
                RutaLogica=Convert.ToString(r["ruta_logica"]),MimeType=Convert.ToString(r["mime_type"]),TamanoBytes=r["tamano_bytes"]==DBNull.Value?0:Convert.ToInt64(r["tamano_bytes"]),
                HashSha256=Convert.ToString(r["hash_sha256"]),Vigente=Convert.ToBoolean(r["vigente"]),Firmado=Convert.ToBoolean(r["firmado"]),
                FechaGeneracion=Convert.ToDateTime(r["fecha_generacion"]),UsuarioGeneradorId=r["usuario_id"]==DBNull.Value?0:Convert.ToInt32(r["usuario_id"]),
                FechaFirma=r["fecha_firma"]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(r["fecha_firma"]),UsuarioFirmaId=r["usuario_firma"]==DBNull.Value?(int?)null:Convert.ToInt32(r["usuario_firma"]),
                VersionRegistro=Convert.ToInt32(r["version"]),CodigoCompania=Convert.ToString(r["codigo_compania"]),ObservacionTecnica=Convert.ToString(r["observacion"])
            };
        }

        private const string BaseSelect = @"SELECT di.codigo_documento id,i.codigo_solicitud solicitud_id,
            di.codigo_inspeccion inspeccion_id,di.codigo_documento_base origen_id,di.tipo_documento,di.version,
            COALESCE(g.estado,'GENERADO') estado,di.nombre_archivo_storage nombre_archivo,di.ruta_archivo ruta_logica,
            COALESCE(di.content_type,'application/pdf') mime_type,di.tamano_bytes,di.hash_archivo hash_sha256,
            NOT EXISTS(SELECT 1 FROM public.aocr_tbdocumento_inspeccion nx
                WHERE nx.codigo_inspeccion=di.codigo_inspeccion
                  AND (CASE WHEN UPPER(TRIM(nx.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' WHEN UPPER(TRIM(nx.tipo_documento)) IN ('CONDICIONES','CONDICIONES_LIMITACIONES') THEN 'CONDICIONES_LIMITACIONES' ELSE UPPER(TRIM(nx.tipo_documento)) END)
                      =(CASE WHEN UPPER(TRIM(di.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' WHEN UPPER(TRIM(di.tipo_documento)) IN ('CONDICIONES','CONDICIONES_LIMITACIONES') THEN 'CONDICIONES_LIMITACIONES' ELSE UPPER(TRIM(di.tipo_documento)) END)
                  AND (nx.version>di.version OR (nx.version=di.version AND nx.codigo_documento>di.codigo_documento))) vigente,
            (f.codigo_firma IS NOT NULL) firmado,di.created_at fecha_generacion,di.codigo_usuario,
            f.fecha_firma,f.codigo_usuario usuario_firma,g.codigo_compania,di.observacion
            FROM public.aocr_tbdocumento_inspeccion di
            JOIN public.aocr_tbinspeccion i ON i.codigo_inspeccion=di.codigo_inspeccion
            LEFT JOIN public.aocr_tbdocumento_generado g ON g.codigo_documento=di.codigo_documento_base
            LEFT JOIN LATERAL(SELECT ff.* FROM public.aocr_tbfirma_documento ff
                WHERE ff.codigo_solicitud=i.codigo_solicitud AND ff.codigo_inspeccion=i.codigo_inspeccion
                  AND (CASE WHEN UPPER(TRIM(ff.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' WHEN UPPER(TRIM(ff.tipo_documento)) IN ('CONDICIONES','CONDICIONES_LIMITACIONES') THEN 'CONDICIONES_LIMITACIONES' ELSE UPPER(TRIM(ff.tipo_documento)) END)
                      =(CASE WHEN UPPER(TRIM(di.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' WHEN UPPER(TRIM(di.tipo_documento)) IN ('CONDICIONES','CONDICIONES_LIMITACIONES') THEN 'CONDICIONES_LIMITACIONES' ELSE UPPER(TRIM(di.tipo_documento)) END)
                ORDER BY ff.codigo_firma DESC LIMIT 1) f ON TRUE";

        private const string CanonicalSinAlias = "(CASE WHEN UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' WHEN UPPER(TRIM(tipo_documento)) IN ('CONDICIONES','CONDICIONES_LIMITACIONES') THEN 'CONDICIONES_LIMITACIONES' ELSE UPPER(TRIM(tipo_documento)) END)";
        private const string CanonicalInspeccion = "(CASE WHEN UPPER(TRIM(di.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' WHEN UPPER(TRIM(di.tipo_documento)) IN ('CONDICIONES','CONDICIONES_LIMITACIONES') THEN 'CONDICIONES_LIMITACIONES' ELSE UPPER(TRIM(di.tipo_documento)) END)";
        private const string CanonicalGenerado = "(CASE WHEN UPPER(TRIM(g.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' WHEN UPPER(TRIM(g.tipo_documento)) IN ('CONDICIONES','CONDICIONES_LIMITACIONES') THEN 'CONDICIONES_LIMITACIONES' ELSE UPPER(TRIM(g.tipo_documento)) END)";
        private const string CanonicalFirma = "(CASE WHEN UPPER(TRIM(f.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' WHEN UPPER(TRIM(f.tipo_documento)) IN ('CONDICIONES','CONDICIONES_LIMITACIONES') THEN 'CONDICIONES_LIMITACIONES' ELSE UPPER(TRIM(f.tipo_documento)) END)";
    }
}
