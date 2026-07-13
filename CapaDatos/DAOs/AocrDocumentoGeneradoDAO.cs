using System;
using System.Configuration;
using System.Collections.Generic;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class AocrDocumentoGeneradoDAO
    {
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;
        private readonly string _cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;

        public void Registrar(AocrDocumentoGenerado documento)
        {
            if (documento == null)
            {
                throw new ArgumentNullException(nameof(documento));
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sqlUpdateBorrador = @"
                    UPDATE public.aocr_tbdocumento_generado
                    SET numero_aocr = @numero_aocr,
                        nombre_archivo = @nombre_archivo,
                        ruta_documento = @ruta_documento,
                        tamanio_pdf = @tamanio_pdf,
                        estado = @estado,
                        fecha_generacion = @fecha_generacion,
                        codigo_usuario = @codigo_usuario,
                        usuario_nombre = @usuario_nombre,
                        fecha_actualizacion = NOW()
                    WHERE codigo_documento = (
                        SELECT codigo_documento
                        FROM public.aocr_tbdocumento_generado
                        WHERE codigo_solicitud = @codigo_solicitud
                          AND UPPER(tipo_documento) = UPPER(@tipo_documento)
                          AND vigente = TRUE
                          AND eliminado = FALSE
                          AND estado IN ('EN_REVISION_INSPECTOR', 'BORRADOR_INSPECTOR', 'GENERADO')
                        ORDER BY version DESC, codigo_documento DESC
                        LIMIT 1
                    );";

                var tipoNormalizado = (documento.TipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
                if (tipoNormalizado == "RECONOCIMIENTO" || tipoNormalizado == "CONDICIONES_LIMITACIONES")
                {
                    using (var update = new NpgsqlCommand(sqlUpdateBorrador, cn))
                    {
                        BindDocumento(update, documento);
                        if (update.ExecuteNonQuery() > 0)
                        {
                            return;
                        }
                    }
                }

                const string sql = @"
                    INSERT INTO public.aocr_tbdocumento_generado
                    (
                        codigo_solicitud,
                        codigo_inspeccion,
                        tipo_documento,
                        numero_aocr,
                        nombre_archivo,
                        ruta_documento,
                        tamanio_pdf,
                        estado,
                        fecha_generacion,
                        codigo_usuario,
                        usuario_nombre, codigo_compania, codigo_inspector, version,
                        vigente, eliminado, usuario_creador_id, fecha_actualizacion,
                        created_at
                    )
                    VALUES
                    (
                        @codigo_solicitud,
                        @codigo_inspeccion,
                        @tipo_documento,
                        @numero_aocr,
                        @nombre_archivo,
                        @ruta_documento,
                        @tamanio_pdf,
                        @estado,
                        @fecha_generacion,
                        @codigo_usuario,
                        @usuario_nombre, @codigo_compania, @codigo_inspector, @version,
                        @vigente, @eliminado, @usuario_creador_id, NOW(),
                        NOW()
                    );";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    BindDocumento(cmd, documento);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public AocrDocumentoGenerado ObtenerUltimoPorSolicitudTipo(int codigoSolicitud, string tipoDocumento)
        {
            if (codigoSolicitud <= 0 || string.IsNullOrWhiteSpace(tipoDocumento))
            {
                return null;
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT codigo_documento,
                           codigo_solicitud,
                           codigo_inspeccion,
                           tipo_documento,
                           numero_aocr,
                           nombre_archivo,
                           ruta_documento,
                           tamanio_pdf,
                           estado,
                           fecha_generacion,
                           codigo_usuario,
                           usuario_nombre,
                           created_at, codigo_compania, codigo_inspector, version,
                           vigente, eliminado, usuario_creador_id, fecha_actualizacion
                    FROM public.aocr_tbdocumento_generado
                    WHERE codigo_solicitud = @codigo_solicitud
                      AND UPPER(COALESCE(tipo_documento, '')) = UPPER(@tipo_documento)
                    ORDER BY fecha_generacion DESC, codigo_documento DESC
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_documento", tipoDocumento.Trim());
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            return null;
                        }

                        return new AocrDocumentoGenerado
                        {
                            CodigoDocumento = rd["codigo_documento"] != DBNull.Value ? Convert.ToInt32(rd["codigo_documento"]) : 0,
                            CodigoSolicitud = rd["codigo_solicitud"] != DBNull.Value ? Convert.ToInt32(rd["codigo_solicitud"]) : 0,
                            CodigoInspeccion = rd["codigo_inspeccion"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_inspeccion"]) : null,
                            TipoDocumento = rd["tipo_documento"] != DBNull.Value ? rd["tipo_documento"].ToString() : null,
                            NumeroAocr = rd["numero_aocr"] != DBNull.Value ? rd["numero_aocr"].ToString() : null,
                            NombreArchivo = rd["nombre_archivo"] != DBNull.Value ? rd["nombre_archivo"].ToString() : null,
                            RutaDocumento = rd["ruta_documento"] != DBNull.Value ? rd["ruta_documento"].ToString() : null,
                            TamanioPdf = rd["tamanio_pdf"] != DBNull.Value ? (long?)Convert.ToInt64(rd["tamanio_pdf"]) : null,
                            Estado = rd["estado"] != DBNull.Value ? rd["estado"].ToString() : null,
                            FechaGeneracion = rd["fecha_generacion"] != DBNull.Value ? Convert.ToDateTime(rd["fecha_generacion"]) : DateTime.MinValue,
                            CodigoUsuario = rd["codigo_usuario"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_usuario"]) : null,
                            UsuarioNombre = rd["usuario_nombre"] != DBNull.Value ? rd["usuario_nombre"].ToString() : null,
                            CreatedAt = rd["created_at"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["created_at"]) : null,
                            CodigoCompania = rd["codigo_compania"] != DBNull.Value ? rd["codigo_compania"].ToString() : null,
                            CodigoInspector = rd["codigo_inspector"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_inspector"]) : null,
                            Version = rd["version"] != DBNull.Value ? Convert.ToInt32(rd["version"]) : 1,
                            Vigente = rd["vigente"] != DBNull.Value && Convert.ToBoolean(rd["vigente"]),
                            Eliminado = rd["eliminado"] != DBNull.Value && Convert.ToBoolean(rd["eliminado"]),
                            UsuarioCreadorId = rd["usuario_creador_id"] != DBNull.Value ? (int?)Convert.ToInt32(rd["usuario_creador_id"]) : null,
                            FechaActualizacion = rd["fecha_actualizacion"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fecha_actualizacion"]) : null
                        };
                    }
                }
            }
        }

        public AocrDocumentoGenerado ObtenerOCrearBorrador(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int solicitudId,
            int inspeccionId,
            string codigoCompania,
            int inspectorId,
            string tipoDocumento,
            int usuarioCreadorId,
            out bool creado)
        {
            if (cn == null) throw new ArgumentNullException(nameof(cn));
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            if (solicitudId <= 0 || inspeccionId <= 0 || inspectorId <= 0 || string.IsNullOrWhiteSpace(codigoCompania) || string.IsNullOrWhiteSpace(tipoDocumento))
            {
                throw new ArgumentException("Los datos del borrador no son validos.");
            }

            creado = false;
            EnsureSchema(cn);
            using (var advisory = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@clave));", cn, tx))
            {
                advisory.Parameters.AddWithValue("@clave", solicitudId + ":" + inspeccionId + ":" + codigoCompania.Trim().ToUpperInvariant() + ":" + tipoDocumento.Trim().ToUpperInvariant());
                advisory.ExecuteNonQuery();
            }

            const string buscar = @"
                SELECT codigo_documento, codigo_solicitud, codigo_inspeccion, tipo_documento,
                       numero_aocr, nombre_archivo, ruta_documento, tamanio_pdf, estado,
                       fecha_generacion, codigo_usuario, usuario_nombre, created_at,
                       codigo_compania, codigo_inspector, version, vigente, eliminado,
                       usuario_creador_id, fecha_actualizacion
                FROM public.aocr_tbdocumento_generado
                WHERE codigo_solicitud=@solicitud
                  AND codigo_inspeccion=@inspeccion
                  AND UPPER(TRIM(codigo_compania))=UPPER(TRIM(@compania))
                  AND UPPER(TRIM(tipo_documento))=UPPER(TRIM(@tipo))
                  AND vigente=TRUE AND eliminado=FALSE
                ORDER BY version DESC, codigo_documento DESC
                FOR UPDATE;";

            AocrDocumentoGenerado existente = null;
            using (var cmd = new NpgsqlCommand(buscar, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@compania", codigoCompania.Trim());
                cmd.Parameters.AddWithValue("@tipo", tipoDocumento.Trim());
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read()) existente = MapDocumento(rd);
                    if (rd.Read()) throw new InvalidOperationException("Existen multiples documentos vigentes para el mismo expediente y tipo.");
                }
            }

            if (existente != null)
            {
                if (!EsEditable(existente.Estado) || !string.IsNullOrWhiteSpace(existente.HashPdfFirmado))
                {
                    throw new InvalidOperationException("El documento vigente ya fue enviado, aprobado o firmado y no puede sobrescribirse.");
                }

                const string actualizar = @"
                    UPDATE public.aocr_tbdocumento_generado
                    SET codigo_inspector=@inspector, codigo_usuario=@inspector,
                        estado='EN_REVISION_INSPECTOR', fecha_actualizacion=NOW()
                    WHERE codigo_documento=@id;";
                using (var cmd = new NpgsqlCommand(actualizar, cn, tx))
                {
                    cmd.Parameters.AddWithValue("@inspector", inspectorId);
                    cmd.Parameters.AddWithValue("@id", existente.CodigoDocumento);
                    cmd.ExecuteNonQuery();
                }
                existente.CodigoInspector = inspectorId;
                existente.CodigoUsuario = inspectorId;
                existente.Estado = "EN_REVISION_INSPECTOR";
                return existente;
            }

            const string insertar = @"
                INSERT INTO public.aocr_tbdocumento_generado
                (codigo_solicitud, codigo_inspeccion, tipo_documento, numero_aocr,
                 nombre_archivo, ruta_documento, estado, fecha_generacion,
                 codigo_usuario, usuario_nombre, codigo_compania, codigo_inspector,
                 version, vigente, eliminado, usuario_creador_id, created_at, fecha_actualizacion)
                SELECT @solicitud, @inspeccion, @tipo, 'BORRADOR', '', '',
                       'EN_REVISION_INSPECTOR', NOW(), @inspector, 'Inspector',
                       @compania, @inspector,
                       COALESCE(MAX(version),0)+1, TRUE, FALSE, @creador, NOW(), NOW()
                FROM public.aocr_tbdocumento_generado
                WHERE codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion
                  AND UPPER(TRIM(tipo_documento))=UPPER(TRIM(@tipo))
                RETURNING codigo_documento;";
            int id;
            using (var cmd = new NpgsqlCommand(insertar, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@tipo", tipoDocumento.Trim());
                cmd.Parameters.AddWithValue("@inspector", inspectorId);
                cmd.Parameters.AddWithValue("@compania", codigoCompania.Trim());
                cmd.Parameters.AddWithValue("@creador", usuarioCreadorId);
                id = Convert.ToInt32(cmd.ExecuteScalar());
            }
            creado = true;
            return new AocrDocumentoGenerado
            {
                CodigoDocumento=id, CodigoSolicitud=solicitudId, CodigoInspeccion=inspeccionId,
                TipoDocumento=tipoDocumento.Trim(), NumeroAocr="BORRADOR", Estado="EN_REVISION_INSPECTOR",
                FechaGeneracion=DateTime.Now, CodigoUsuario=inspectorId, CodigoInspector=inspectorId,
                CodigoCompania=codigoCompania.Trim(), Vigente=true, Eliminado=false, UsuarioCreadorId=usuarioCreadorId
            };
        }

        public void PrepararEsquema(NpgsqlConnection cn)
        {
            if (cn == null) throw new ArgumentNullException(nameof(cn));
            EnsureSchema(cn);
        }

        public bool TieneParDocumentosInspector(int solicitudId,int inspeccionId,int inspectorId)
        {
            using(var cn=new NpgsqlConnection(_cs))
            {
                cn.Open();EnsureSchema(cn);
                const string sql=@"SELECT COUNT(DISTINCT UPPER(tipo_documento))
                    FROM public.aocr_tbdocumento_generado
                    WHERE codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion
                      AND codigo_inspector=@inspector AND vigente=TRUE AND eliminado=FALSE
                      AND estado IN ('EN_REVISION_INSPECTOR','GENERADO')
                      AND UPPER(tipo_documento) IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES');";
                using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);cmd.Parameters.AddWithValue("@inspector",inspectorId);return Convert.ToInt32(cmd.ExecuteScalar())==2;}
            }
        }

        public AocrDocumentoGenerado ObtenerPorExpedienteTipo(int solicitudId,int inspeccionId,string tipoDocumento)
        {
            using(var cn=new NpgsqlConnection(_cs))
            {
                cn.Open();EnsureSchema(cn);
                const string sql=@"SELECT codigo_documento,codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,nombre_archivo,ruta_documento,tamanio_pdf,estado,fecha_generacion,codigo_usuario,usuario_nombre,created_at,codigo_compania,codigo_inspector,version,vigente,eliminado,usuario_creador_id,fecha_actualizacion
                    FROM public.aocr_tbdocumento_generado
                    WHERE codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion
                      AND UPPER(TRIM(tipo_documento))=UPPER(TRIM(@tipo)) AND vigente=TRUE AND eliminado=FALSE
                    ORDER BY version DESC,codigo_documento DESC LIMIT 1;";
                using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);cmd.Parameters.AddWithValue("@tipo",tipoDocumento);using(var rd=cmd.ExecuteReader()){return rd.Read()?MapDocumento(rd):null;}}
            }
        }

        public IList<AocrDocumentoGenerado> ListarVersionesPorExpediente(int solicitudId,int inspeccionId,string tipoDocumento)
        {
            var result=new List<AocrDocumentoGenerado>();
            using(var cn=new NpgsqlConnection(_cs))
            {
                cn.Open();EnsureSchema(cn);
                const string sql=@"SELECT codigo_documento,codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,nombre_archivo,ruta_documento,tamanio_pdf,estado,fecha_generacion,codigo_usuario,usuario_nombre,created_at,codigo_compania,codigo_inspector,version,vigente,eliminado,usuario_creador_id,fecha_actualizacion
                    FROM public.aocr_tbdocumento_generado
                    WHERE codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion
                      AND UPPER(TRIM(tipo_documento))=UPPER(TRIM(@tipo)) AND eliminado=FALSE
                    ORDER BY version DESC,codigo_documento DESC;";
                using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);cmd.Parameters.AddWithValue("@tipo",tipoDocumento);using(var rd=cmd.ExecuteReader()){while(rd.Read())result.Add(MapDocumento(rd));}}
            }
            return result;
        }

        public int ActualizarEdicionOptimista(int documentoId,int solicitudId,int inspeccionId,int inspectorId,int versionEsperada,string estadoNuevo)
        {
            using(var cn=new NpgsqlConnection(_cs))
            {
                cn.Open();EnsureSchema(cn);
                using(var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    const string lockSql=@"SELECT codigo_documento FROM public.aocr_tbdocumento_generado
                        WHERE codigo_documento=@documento AND codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion
                          AND codigo_inspector=@inspector AND version=@version AND vigente=TRUE AND eliminado=FALSE
                          AND COALESCE(hash_pdf_firmado,'')=''
                          AND estado IN ('BORRADOR','EN_REVISION_INSPECTOR','GENERADO','OBSERVADO_DCAV','CORREGIDO_INSPECTOR') FOR UPDATE;";
                    using(var cmd=new NpgsqlCommand(lockSql,cn,tx)){BindVersion(cmd,documentoId,solicitudId,inspeccionId,inspectorId,versionEsperada);if(cmd.ExecuteScalar()==null){tx.Rollback();return 0;}}
                    using(var cmd=new NpgsqlCommand("UPDATE public.aocr_tbdocumento_generado SET vigente=FALSE,fecha_actualizacion=NOW() WHERE codigo_documento=@documento;",cn,tx)){cmd.Parameters.AddWithValue("@documento",documentoId);if(cmd.ExecuteNonQuery()!=1){tx.Rollback();return 0;}}
                    const string cloneSql=@"INSERT INTO public.aocr_tbdocumento_generado
                        (codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,nombre_archivo,ruta_documento,tamanio_pdf,estado,fecha_generacion,codigo_usuario,usuario_nombre,hash_pdf_firmado,codigo_compania,codigo_inspector,version,vigente,eliminado,usuario_creador_id,fecha_actualizacion,created_at)
                        SELECT codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,NULL,NULL,NULL,@estado,NOW(),@inspector,usuario_nombre,NULL,codigo_compania,@inspector,version+1,TRUE,FALSE,usuario_creador_id,NOW(),NOW()
                        FROM public.aocr_tbdocumento_generado WHERE codigo_documento=@documento
                        RETURNING version;";
                    int nuevaVersion;
                    using(var cmd=new NpgsqlCommand(cloneSql,cn,tx)){cmd.Parameters.AddWithValue("@estado",estadoNuevo);cmd.Parameters.AddWithValue("@inspector",inspectorId);cmd.Parameters.AddWithValue("@documento",documentoId);nuevaVersion=Convert.ToInt32(cmd.ExecuteScalar());}
                    tx.Commit();return nuevaVersion;
                }
            }
        }

        public bool RegistrarPdfGeneradoOptimista(int documentoId,int solicitudId,int inspeccionId,int inspectorId,int versionEsperada,string ruta,string nombre,long tamanio)
        {
            using(var cn=new NpgsqlConnection(_cs))
            {
                cn.Open();EnsureSchema(cn);
                const string sql=@"UPDATE public.aocr_tbdocumento_generado
                    SET ruta_documento=@ruta,nombre_archivo=@nombre,tamanio_pdf=@tamanio,estado='GENERADO',
                        codigo_usuario=@inspector,usuario_nombre='Inspector',fecha_generacion=NOW(),fecha_actualizacion=NOW()
                    WHERE codigo_documento=@documento AND codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion
                      AND codigo_inspector=@inspector AND version=@version AND vigente=TRUE AND eliminado=FALSE
                      AND COALESCE(hash_pdf_firmado,'')='' AND estado IN ('BORRADOR','EN_REVISION_INSPECTOR','GENERADO','OBSERVADO_DCAV','CORREGIDO_INSPECTOR');";
                using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@ruta",ruta);cmd.Parameters.AddWithValue("@nombre",(object)nombre??DBNull.Value);cmd.Parameters.AddWithValue("@tamanio",tamanio);cmd.Parameters.AddWithValue("@inspector",inspectorId);cmd.Parameters.AddWithValue("@documento",documentoId);cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);cmd.Parameters.AddWithValue("@version",versionEsperada);return cmd.ExecuteNonQuery()==1;}
            }
        }

        public void MarcarLiberadoRt(
            int codigoSolicitud,
            string tipoDocumento,
            string rutaPdfFirmado,
            string hashPdfFirmado,
            long tamanioPdf,
            int? codigoUsuarioLiberacion,
            string nombreArchivo = null)
        {
            if (codigoSolicitud <= 0 || string.IsNullOrWhiteSpace(tipoDocumento) || string.IsNullOrWhiteSpace(rutaPdfFirmado))
            {
                return;
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sqlUpdate = @"
                    UPDATE public.aocr_tbdocumento_generado
                    SET ruta_documento = @ruta_documento,
                        nombre_archivo = COALESCE(NULLIF(@nombre_archivo, ''), nombre_archivo),
                        tamanio_pdf = @tamanio_pdf,
                        estado = 'LIBERADO_RT',
                        hash_pdf_firmado = @hash_pdf_firmado,
                        fecha_liberacion = COALESCE(fecha_liberacion, NOW()),
                        disponible_rt = TRUE,
                        fecha_disponible_rt = COALESCE(fecha_disponible_rt, NOW()),
                        codigo_usuario_liberacion = @codigo_usuario_liberacion
                    WHERE codigo_documento = (
                        SELECT codigo_documento
                        FROM public.aocr_tbdocumento_generado
                        WHERE codigo_solicitud = @codigo_solicitud
                          AND UPPER(COALESCE(tipo_documento, '')) = UPPER(@tipo_documento)
                        ORDER BY fecha_generacion DESC, codigo_documento DESC
                        LIMIT 1
                    );";

                int filas;
                using (var cmd = new NpgsqlCommand(sqlUpdate, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_documento", tipoDocumento.Trim());
                    cmd.Parameters.AddWithValue("@ruta_documento", rutaPdfFirmado.Trim());
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)(nombreArchivo ?? string.Empty));
                    cmd.Parameters.AddWithValue("@tamanio_pdf", tamanioPdf);
                    cmd.Parameters.AddWithValue("@hash_pdf_firmado", (object)hashPdfFirmado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_usuario_liberacion", (object)codigoUsuarioLiberacion ?? DBNull.Value);
                    filas = cmd.ExecuteNonQuery();
                }

                if (filas > 0)
                {
                    return;
                }

                const string sqlInsert = @"
                    INSERT INTO public.aocr_tbdocumento_generado
                    (
                        codigo_solicitud,
                        tipo_documento,
                        nombre_archivo,
                        ruta_documento,
                        tamanio_pdf,
                        estado,
                        fecha_generacion,
                        hash_pdf_firmado,
                        fecha_liberacion,
                        disponible_rt,
                        fecha_disponible_rt,
                        codigo_usuario,
                        codigo_usuario_liberacion,
                        created_at
                    )
                    VALUES
                    (
                        @codigo_solicitud,
                        @tipo_documento,
                        @nombre_archivo,
                        @ruta_documento,
                        @tamanio_pdf,
                        'LIBERADO_RT',
                        NOW(),
                        @hash_pdf_firmado,
                        NOW(),
                        TRUE,
                        NOW(),
                        @codigo_usuario,
                        @codigo_usuario_liberacion,
                        NOW()
                    );";

                using (var cmd = new NpgsqlCommand(sqlInsert, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_documento", tipoDocumento.Trim());
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)nombreArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_documento", rutaPdfFirmado.Trim());
                    cmd.Parameters.AddWithValue("@tamanio_pdf", tamanioPdf);
                    cmd.Parameters.AddWithValue("@hash_pdf_firmado", (object)hashPdfFirmado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_usuario", (object)codigoUsuarioLiberacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_usuario_liberacion", (object)codigoUsuarioLiberacion ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void EnsureSchema(NpgsqlConnection cn)
        {
            if (_schemaReady)
            {
                return;
            }

            lock (SyncLock)
            {
                if (_schemaReady)
                {
                    return;
                }

                const string sql = @"
                    CREATE TABLE IF NOT EXISTS public.aocr_tbdocumento_generado
                    (
                        codigo_documento SERIAL PRIMARY KEY,
                        codigo_solicitud INTEGER NOT NULL,
                        codigo_inspeccion INTEGER NULL,
                        tipo_documento VARCHAR(80) NOT NULL,
                        numero_aocr VARCHAR(120) NULL,
                        nombre_archivo VARCHAR(260) NULL,
                        ruta_documento VARCHAR(500) NOT NULL,
                        tamanio_pdf BIGINT NULL,
                        estado VARCHAR(80) NOT NULL DEFAULT 'GENERADO',
                        fecha_generacion TIMESTAMP NOT NULL DEFAULT NOW(),
                        codigo_usuario INTEGER NULL,
                        usuario_nombre VARCHAR(160) NULL,
                        hash_pdf_firmado VARCHAR(128) NULL,
                        fecha_liberacion TIMESTAMP NULL,
                        codigo_usuario_liberacion INTEGER NULL,
                        disponible_rt BOOLEAN NOT NULL DEFAULT FALSE,
                        fecha_disponible_rt TIMESTAMP NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );

                    ALTER TABLE public.aocr_tbdocumento_generado ALTER COLUMN ruta_documento DROP NOT NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS hash_pdf_firmado VARCHAR(128) NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS fecha_liberacion TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS codigo_usuario_liberacion INTEGER NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS disponible_rt BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS fecha_disponible_rt TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS codigo_compania VARCHAR(160) NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS codigo_inspector INTEGER NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS version INTEGER NOT NULL DEFAULT 1;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS vigente BOOLEAN NOT NULL DEFAULT TRUE;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS eliminado BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS usuario_creador_id INTEGER NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS fecha_actualizacion TIMESTAMP NULL;

                    CREATE INDEX IF NOT EXISTS idx_aocr_documento_generado_solicitud_tipo
                        ON public.aocr_tbdocumento_generado(codigo_solicitud, tipo_documento, fecha_generacion DESC);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }

        private static void BindDocumento(NpgsqlCommand cmd, AocrDocumentoGenerado documento)
        {
            cmd.Parameters.AddWithValue("@codigo_solicitud", documento.CodigoSolicitud);
            cmd.Parameters.AddWithValue("@codigo_inspeccion", (object)documento.CodigoInspeccion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tipo_documento", (object)documento.TipoDocumento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@numero_aocr", (object)documento.NumeroAocr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nombre_archivo", (object)documento.NombreArchivo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ruta_documento", (object)documento.RutaDocumento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tamanio_pdf", (object)documento.TamanioPdf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@estado", (object)documento.Estado ?? "GENERADO");
            cmd.Parameters.AddWithValue("@fecha_generacion", documento.FechaGeneracion == DateTime.MinValue ? DateTime.Now : documento.FechaGeneracion);
            cmd.Parameters.AddWithValue("@codigo_usuario", (object)documento.CodigoUsuario ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@usuario_nombre", (object)documento.UsuarioNombre ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@codigo_compania", (object)documento.CodigoCompania ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@codigo_inspector", (object)documento.CodigoInspector ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@version", documento.Version > 0 ? documento.Version : 1);
            cmd.Parameters.AddWithValue("@vigente", documento.Version <= 0 || documento.Vigente);
            cmd.Parameters.AddWithValue("@eliminado", documento.Eliminado);
            cmd.Parameters.AddWithValue("@usuario_creador_id", (object)documento.UsuarioCreadorId ?? DBNull.Value);
        }

        private static void BindVersion(NpgsqlCommand cmd,int documentoId,int solicitudId,int inspeccionId,int inspectorId,int version)
        {
            cmd.Parameters.AddWithValue("@documento",documentoId);
            cmd.Parameters.AddWithValue("@solicitud",solicitudId);
            cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);
            cmd.Parameters.AddWithValue("@inspector",inspectorId);
            cmd.Parameters.AddWithValue("@version",version);
        }

        private static AocrDocumentoGenerado MapDocumento(NpgsqlDataReader rd)
        {
            return new AocrDocumentoGenerado
            {
                CodigoDocumento=Convert.ToInt32(rd["codigo_documento"]), CodigoSolicitud=Convert.ToInt32(rd["codigo_solicitud"]),
                CodigoInspeccion=rd["codigo_inspeccion"]==DBNull.Value?(int?)null:Convert.ToInt32(rd["codigo_inspeccion"]),
                TipoDocumento=Convert.ToString(rd["tipo_documento"]), NumeroAocr=Convert.ToString(rd["numero_aocr"]),
                NombreArchivo=Convert.ToString(rd["nombre_archivo"]), RutaDocumento=Convert.ToString(rd["ruta_documento"]),
                TamanioPdf=rd["tamanio_pdf"]==DBNull.Value?(long?)null:Convert.ToInt64(rd["tamanio_pdf"]), Estado=Convert.ToString(rd["estado"]),
                FechaGeneracion=Convert.ToDateTime(rd["fecha_generacion"]), CodigoUsuario=rd["codigo_usuario"]==DBNull.Value?(int?)null:Convert.ToInt32(rd["codigo_usuario"]),
                UsuarioNombre=Convert.ToString(rd["usuario_nombre"]), CreatedAt=rd["created_at"]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(rd["created_at"]),
                CodigoCompania=Convert.ToString(rd["codigo_compania"]), CodigoInspector=rd["codigo_inspector"]==DBNull.Value?(int?)null:Convert.ToInt32(rd["codigo_inspector"]),
                Version=Convert.ToInt32(rd["version"]), Vigente=Convert.ToBoolean(rd["vigente"]), Eliminado=Convert.ToBoolean(rd["eliminado"]),
                UsuarioCreadorId=rd["usuario_creador_id"]==DBNull.Value?(int?)null:Convert.ToInt32(rd["usuario_creador_id"]),
                FechaActualizacion=rd["fecha_actualizacion"]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(rd["fecha_actualizacion"])
            };
        }

        private static bool EsEditable(string estado)
        {
            var token=(estado??string.Empty).Trim().ToUpperInvariant();
            return token=="EN_REVISION_INSPECTOR" || token=="BORRADOR_INSPECTOR" || token=="GENERADO";
        }
    }
}
