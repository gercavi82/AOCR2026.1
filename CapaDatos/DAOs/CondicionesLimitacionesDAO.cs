using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// AC-10: DAO transaccional para la persistencia de Condiciones y Limitaciones (CL).
    /// Asegura control de concurrencia con pg_advisory_xact_lock, consultas parametrizadas,
    /// inmutabilidad tras firma y sincronización con documentos generados y firmas institucionales.
    /// </summary>
    public class CondicionesLimitacionesDAO
    {
        private readonly string _cs;
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        public CondicionesLimitacionesDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public CondicionesLimitacionesDAO(string connectionString)
        {
            _cs = connectionString;
        }

        public void EnsureSchemaPublic()
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);
            }
        }

        private static void EnsureSchema(NpgsqlConnection cn)
        {
            if (_schemaReady) return;
            lock (SyncLock)
            {
                if (_schemaReady) return;

                const string sql = @"
                    CREATE TABLE IF NOT EXISTS public.aocr_tbcondiciones_limitaciones (
                        id SERIAL PRIMARY KEY,
                        codigo_solicitud INTEGER NOT NULL,
                        codigo_inspeccion INTEGER NULL,
                        codigo_informe INTEGER NULL,
                        numero_aocr VARCHAR(100) NULL,
                        version INTEGER NOT NULL DEFAULT 1,
                        estado VARCHAR(50) NOT NULL DEFAULT 'CL_BORRADOR',
                        vigente BOOLEAN NOT NULL DEFAULT TRUE,
                        compania VARCHAR(250) NULL,
                        operador_extranjero VARCHAR(250) NULL,
                        representante_tecnico VARCHAR(250) NULL,
                        tipo_operacion VARCHAR(100) NULL,
                        rutas_autorizadas TEXT NULL,
                        alcance_autorizado TEXT NULL,
                        condiciones_aprobadas TEXT NULL,
                        limitaciones TEXT NULL,
                        observaciones TEXT NULL,
                        inspector_usuario_id INTEGER NULL,
                        inspector_nombre VARCHAR(200) NULL,
                        fecha_generacion TIMESTAMP NOT NULL DEFAULT NOW(),
                        coordinador_usuario_id INTEGER NULL,
                        coordinador_nombre VARCHAR(200) NULL,
                        observacion_coordinador TEXT NULL,
                        fecha_revision_coordinador TIMESTAMP NULL,
                        dircav_usuario_id INTEGER NULL,
                        dircav_nombre VARCHAR(200) NULL,
                        observacion_dircav TEXT NULL,
                        fecha_firma_dircav TIMESTAMP NULL,
                        ruta_pdf_borrador VARCHAR(500) NULL,
                        ruta_pdf_firmado VARCHAR(500) NULL,
                        hash_pdf VARCHAR(128) NULL,
                        hash_pdf_firmado VARCHAR(128) NULL,
                        tamanio_pdf BIGINT NULL,
                        codigo_verificacion VARCHAR(64) NULL,
                        version_concurrencia BIGINT NOT NULL DEFAULT 1,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        updated_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS ux_cl_solicitud_vigente
                        ON public.aocr_tbcondiciones_limitaciones(codigo_solicitud)
                        WHERE vigente = TRUE;

                    CREATE UNIQUE INDEX IF NOT EXISTS ux_cl_solicitud_version
                        ON public.aocr_tbcondiciones_limitaciones(codigo_solicitud, version);

                    CREATE INDEX IF NOT EXISTS ix_cl_estado_vigente
                        ON public.aocr_tbcondiciones_limitaciones(estado, vigente);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }

        public CondicionesLimitaciones ObtenerPorSolicitudVigente(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0) return null;

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT id, codigo_solicitud, codigo_inspeccion, codigo_informe, numero_aocr,
                           version, estado, vigente, compania, operador_extranjero, representante_tecnico,
                           tipo_operacion, rutas_autorizadas, alcance_autorizado, condiciones_aprobadas,
                           limitaciones, observaciones, inspector_usuario_id, inspector_nombre, fecha_generacion,
                           coordinador_usuario_id, coordinador_nombre, observacion_coordinador, fecha_revision_coordinador,
                           dircav_usuario_id, dircav_nombre, observacion_dircav, fecha_firma_dircav,
                           ruta_pdf_borrador, ruta_pdf_firmado, hash_pdf, hash_pdf_firmado, tamanio_pdf,
                           codigo_verificacion, version_concurrencia, created_at, updated_at
                    FROM public.aocr_tbcondiciones_limitaciones
                    WHERE codigo_solicitud = @codigo_solicitud AND vigente = TRUE
                    ORDER BY version DESC, id DESC
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return null;
                        return Mapear(rd);
                    }
                }
            }
        }

        public CondicionesLimitaciones ObtenerPorId(int id)
        {
            if (id <= 0) return null;

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT id, codigo_solicitud, codigo_inspeccion, codigo_informe, numero_aocr,
                           version, estado, vigente, compania, operador_extranjero, representante_tecnico,
                           tipo_operacion, rutas_autorizadas, alcance_autorizado, condiciones_aprobadas,
                           limitaciones, observaciones, inspector_usuario_id, inspector_nombre, fecha_generacion,
                           coordinador_usuario_id, coordinador_nombre, observacion_coordinador, fecha_revision_coordinador,
                           dircav_usuario_id, dircav_nombre, observacion_dircav, fecha_firma_dircav,
                           ruta_pdf_borrador, ruta_pdf_firmado, hash_pdf, hash_pdf_firmado, tamanio_pdf,
                           codigo_verificacion, version_concurrencia, created_at, updated_at
                    FROM public.aocr_tbcondiciones_limitaciones
                    WHERE id = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return null;
                        return Mapear(rd);
                    }
                }
            }
        }

        public int GuardarBorrador(CondicionesLimitaciones cl)
        {
            if (cl == null) throw new ArgumentNullException(nameof(cl));

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        // Bloqueo pesimista por solicitud para prevenir condiciones de carrera
                        using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@solicitud::bigint);", cn, tx))
                        {
                            lockCmd.Parameters.AddWithValue("@solicitud", cl.CodigoSolicitud);
                            lockCmd.ExecuteNonQuery();
                        }

                        // Verificar si existe borrador vigente
                        const string sqlExiste = @"
                            SELECT id, version, estado
                            FROM public.aocr_tbcondiciones_limitaciones
                            WHERE codigo_solicitud = @solicitud AND vigente = TRUE
                            FOR UPDATE;";

                        int idExistente = 0;
                        int versionActual = 1;
                        string estadoActual = null;

                        using (var cmdCheck = new NpgsqlCommand(sqlExiste, cn, tx))
                        {
                            cmdCheck.Parameters.AddWithValue("@solicitud", cl.CodigoSolicitud);
                            using (var rd = cmdCheck.ExecuteReader())
                            {
                                if (rd.Read())
                                {
                                    idExistente = Convert.ToInt32(rd["id"]);
                                    versionActual = Convert.ToInt32(rd["version"]);
                                    estadoActual = rd["estado"]?.ToString();
                                }
                            }
                        }

                        if (idExistente > 0)
                        {
                            // Si el estado actual es borrador o devuelta al inspector, actualizamos el mismo registro
                            if (string.Equals(estadoActual, AocrEstadoCl.ClBorrador, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(estadoActual, AocrEstadoCl.ClDevueltaInspector, StringComparison.OrdinalIgnoreCase))
                            {
                                const string sqlUpdate = @"
                                    UPDATE public.aocr_tbcondiciones_limitaciones
                                    SET codigo_inspeccion = COALESCE(@inspeccion, codigo_inspeccion),
                                        codigo_informe = COALESCE(@informe, codigo_informe),
                                        numero_aocr = COALESCE(@numero_aocr, numero_aocr),
                                        compania = COALESCE(@compania, compania),
                                        operador_extranjero = COALESCE(@operador, operador_extranjero),
                                        representante_tecnico = COALESCE(@rt, representante_tecnico),
                                        tipo_operacion = COALESCE(@tipo_op, tipo_operacion),
                                        rutas_autorizadas = @rutas,
                                        alcance_autorizado = @alcance,
                                        condiciones_aprobadas = @condiciones,
                                        limitaciones = @limitaciones,
                                        observaciones = @observaciones,
                                        inspector_usuario_id = COALESCE(@inspector_id, inspector_usuario_id),
                                        inspector_nombre = COALESCE(@inspector_nombre, inspector_nombre),
                                        estado = @estado,
                                        ruta_pdf_borrador = COALESCE(@ruta_pdf_borrador, ruta_pdf_borrador),
                                        hash_pdf = COALESCE(@hash_pdf, hash_pdf),
                                        version_concurrencia = version_concurrencia + 1,
                                        updated_at = NOW()
                                    WHERE id = @id;";

                                using (var cmdUpd = new NpgsqlCommand(sqlUpdate, cn, tx))
                                {
                                    cmdUpd.Parameters.AddWithValue("@id", idExistente);
                                    cmdUpd.Parameters.AddWithValue("@inspeccion", (object)cl.CodigoInspeccion ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@informe", (object)cl.CodigoInforme ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@numero_aocr", (object)cl.NumeroAocr ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@compania", (object)cl.Compania ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@operador", (object)cl.OperadorExtranjero ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@rt", (object)cl.RepresentanteTecnico ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@tipo_op", (object)cl.TipoOperacion ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@rutas", (object)cl.RutasAutorizadas ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@alcance", (object)cl.AlcanceAutorizado ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@condiciones", (object)cl.CondicionesAprobadas ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@limitaciones", (object)cl.Limitaciones ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@observaciones", (object)cl.Observaciones ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@inspector_id", (object)cl.InspectorUsuarioId ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@inspector_nombre", (object)cl.InspectorNombre ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@estado", AocrEstadoCl.ClBorrador);
                                    cmdUpd.Parameters.AddWithValue("@ruta_pdf_borrador", (object)cl.RutaPdfBorrador ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@hash_pdf", (object)cl.HashPdf ?? DBNull.Value);
                                    cmdUpd.ExecuteNonQuery();
                                }

                                tx.Commit();
                                cl.Id = idExistente;
                                cl.Version = versionActual;
                                return idExistente;
                            }
                            else
                            {
                                // Si estaba en otro estado, marcamos anterior como no vigente y creamos nueva versión
                                using (var cmdArchivar = new NpgsqlCommand("UPDATE public.aocr_tbcondiciones_limitaciones SET vigente = FALSE, updated_at = NOW() WHERE id = @id;", cn, tx))
                                {
                                    cmdArchivar.Parameters.AddWithValue("@id", idExistente);
                                    cmdArchivar.ExecuteNonQuery();
                                }
                                versionActual++;
                            }
                        }

                        // Insertar nuevo registro
                        const string sqlInsert = @"
                            INSERT INTO public.aocr_tbcondiciones_limitaciones (
                                codigo_solicitud, codigo_inspeccion, codigo_informe, numero_aocr,
                                version, estado, vigente, compania, operador_extranjero, representante_tecnico,
                                tipo_operacion, rutas_autorizadas, alcance_autorizado, condiciones_aprobadas,
                                limitaciones, observaciones, inspector_usuario_id, inspector_nombre,
                                fecha_generacion, ruta_pdf_borrador, hash_pdf, version_concurrencia,
                                created_at, updated_at
                            ) VALUES (
                                @solicitud, @inspeccion, @informe, @numero_aocr,
                                @version, @estado, TRUE, @compania, @operador, @rt,
                                @tipo_op, @rutas, @alcance, @condiciones,
                                @limitaciones, @observaciones, @inspector_id, @inspector_nombre,
                                NOW(), @ruta_pdf_borrador, @hash_pdf, 1,
                                NOW(), NOW()
                            ) RETURNING id;";

                        int nuevoId;
                        using (var cmdIns = new NpgsqlCommand(sqlInsert, cn, tx))
                        {
                            cmdIns.Parameters.AddWithValue("@solicitud", cl.CodigoSolicitud);
                            cmdIns.Parameters.AddWithValue("@inspeccion", (object)cl.CodigoInspeccion ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@informe", (object)cl.CodigoInforme ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@numero_aocr", (object)cl.NumeroAocr ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@version", versionActual);
                            cmdIns.Parameters.AddWithValue("@estado", AocrEstadoCl.ClBorrador);
                            cmdIns.Parameters.AddWithValue("@compania", (object)cl.Compania ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@operador", (object)cl.OperadorExtranjero ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@rt", (object)cl.RepresentanteTecnico ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@tipo_op", (object)cl.TipoOperacion ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@rutas", (object)cl.RutasAutorizadas ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@alcance", (object)cl.AlcanceAutorizado ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@condiciones", (object)cl.CondicionesAprobadas ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@limitaciones", (object)cl.Limitaciones ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@observaciones", (object)cl.Observaciones ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@inspector_id", (object)cl.InspectorUsuarioId ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@inspector_nombre", (object)cl.InspectorNombre ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@ruta_pdf_borrador", (object)cl.RutaPdfBorrador ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@hash_pdf", (object)cl.HashPdf ?? DBNull.Value);

                            nuevoId = Convert.ToInt32(cmdIns.ExecuteScalar());
                        }

                        tx.Commit();
                        cl.Id = nuevoId;
                        cl.Version = versionActual;
                        return nuevoId;
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public bool ActualizarEstado(int id, string nuevoEstado, int usuarioId, string usuarioNombre, string rol, string observacion)
        {
            if (id <= 0 || !AocrEstadoCl.EsEstadoValido(nuevoEstado)) return false;

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        string campoObservacion = "";
                        string campoUsuario = "";
                        string campoNombre = "";
                        string campoFecha = "";

                        if (nuevoEstado == AocrEstadoCl.ClDevueltaInspector || nuevoEstado == AocrEstadoCl.ClPendienteDircav)
                        {
                            campoObservacion = ", observacion_coordinador = @obs";
                            campoUsuario = ", coordinador_usuario_id = @usr";
                            campoNombre = ", coordinador_nombre = @nom";
                            campoFecha = ", fecha_revision_coordinador = NOW()";
                        }
                        else if (nuevoEstado == AocrEstadoCl.ClDevueltaCoordinador)
                        {
                            campoObservacion = ", observacion_dircav = @obs";
                            campoUsuario = ", dircav_usuario_id = @usr";
                            campoNombre = ", dircav_nombre = @nom";
                        }

                        string sql = $@"
                            UPDATE public.aocr_tbcondiciones_limitaciones
                            SET estado = @estado,
                                version_concurrencia = version_concurrencia + 1,
                                updated_at = NOW()
                                {campoObservacion}
                                {campoUsuario}
                                {campoNombre}
                                {campoFecha}
                            WHERE id = @id;";

                        using (var cmd = new NpgsqlCommand(sql, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                            cmd.Parameters.AddWithValue("@id", id);
                            if (!string.IsNullOrWhiteSpace(campoObservacion))
                            {
                                cmd.Parameters.AddWithValue("@obs", (object)observacion ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@usr", usuarioId > 0 ? (object)usuarioId : DBNull.Value);
                                cmd.Parameters.AddWithValue("@nom", (object)usuarioNombre ?? DBNull.Value);
                            }

                            var filas = cmd.ExecuteNonQuery();
                            if (filas <= 0)
                            {
                                tx.Rollback();
                                return false;
                            }
                        }

                        tx.Commit();
                        return true;
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public bool RegistrarFirmaDircav(
            int id,
            string rutaPdfFirmado,
            string hashPdfFirmado,
            long tamanioPdf,
            int dircavId,
            string dircavNombre,
            string codigoVerificacion)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(rutaPdfFirmado) || string.IsNullOrWhiteSpace(hashPdfFirmado))
                return false;

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        // 1. Actualizar tabla específica de CL
                        const string sqlCl = @"
                            UPDATE public.aocr_tbcondiciones_limitaciones
                            SET estado = @estado,
                                ruta_pdf_firmado = @ruta,
                                hash_pdf_firmado = @hash,
                                tamanio_pdf = @tamanio,
                                dircav_usuario_id = @dircav_id,
                                dircav_nombre = @dircav_nombre,
                                fecha_firma_dircav = NOW(),
                                codigo_verificacion = @codigo_verificacion,
                                version_concurrencia = version_concurrencia + 1,
                                updated_at = NOW()
                            WHERE id = @id AND estado = @estado_pendiente;";

                        using (var cmd = new NpgsqlCommand(sqlCl, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@estado", AocrEstadoCl.ClFirmadaDircav);
                            cmd.Parameters.AddWithValue("@ruta", rutaPdfFirmado);
                            cmd.Parameters.AddWithValue("@hash", hashPdfFirmado);
                            cmd.Parameters.AddWithValue("@tamanio", tamanioPdf);
                            cmd.Parameters.AddWithValue("@dircav_id", dircavId);
                            cmd.Parameters.AddWithValue("@dircav_nombre", (object)dircavNombre ?? "DIRCAV");
                            cmd.Parameters.AddWithValue("@codigo_verificacion", (object)codigoVerificacion ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.Parameters.AddWithValue("@estado_pendiente", AocrEstadoCl.ClPendienteFirmaDircav);

                            var filas = cmd.ExecuteNonQuery();
                            if (filas <= 0)
                            {
                                // Intentar también si el estado era CL_PENDIENTE_DIRCAV
                                cmd.Parameters["@estado_pendiente"].Value = AocrEstadoCl.ClPendienteDircav;
                                filas = cmd.ExecuteNonQuery();
                                if (filas <= 0)
                                {
                                    tx.Rollback();
                                    return false;
                                }
                            }
                        }

                        // Obtener solicitud_id para sincronizar con documento_generado y firmas
                        int solicitudId;
                        string numeroAocr;
                        using (var cmdSol = new NpgsqlCommand("SELECT codigo_solicitud, numero_aocr FROM public.aocr_tbcondiciones_limitaciones WHERE id = @id;", cn, tx))
                        {
                            cmdSol.Parameters.AddWithValue("@id", id);
                            using (var rd = cmdSol.ExecuteReader())
                            {
                                if (!rd.Read()) { tx.Rollback(); return false; }
                                solicitudId = Convert.ToInt32(rd["codigo_solicitud"]);
                                numeroAocr = rd["numero_aocr"]?.ToString();
                            }
                        }

                        // 2. Sincronizar en public.aocr_tbdocumento_generado
                        const string sqlDocGen = @"
                            UPDATE public.aocr_tbdocumento_generado
                            SET estado = 'CONDICIONES_FIRMADAS_DCAV',
                                ruta_pdf_firmado = @ruta,
                                hash_pdf_firmado = @hash,
                                tamanio_pdf_firmado = @tamanio,
                                codigo_usuario_firma = @dircav_id,
                                rol_firma = 'DIRCAV',
                                fecha_firma = NOW(),
                                version_concurrencia = version_concurrencia + 1
                            WHERE codigo_solicitud = @solicitud
                              AND UPPER(tipo_documento) IN ('CONDICIONES_LIMITACIONES', 'CONDICIONES')
                              AND vigente = TRUE;";

                        using (var cmdDoc = new NpgsqlCommand(sqlDocGen, cn, tx))
                        {
                            cmdDoc.Parameters.AddWithValue("@ruta", rutaPdfFirmado);
                            cmdDoc.Parameters.AddWithValue("@hash", hashPdfFirmado);
                            cmdDoc.Parameters.AddWithValue("@tamanio", tamanioPdf);
                            cmdDoc.Parameters.AddWithValue("@dircav_id", dircavId);
                            cmdDoc.Parameters.AddWithValue("@solicitud", solicitudId);
                            cmdDoc.ExecuteNonQuery();
                        }

                        // 3. Registrar en public.aocr_tbfirma_documento
                        const string sqlFirma = @"
                            INSERT INTO public.aocr_tbfirma_documento (
                                codigo_solicitud, tipo_documento, numero_aocr,
                                nombre_archivo, ruta_documento, hash_documento,
                                tamanio_pdf_firmado, firmado_por_rol, nombre_firmante,
                                cargo_firmante, fecha_firma, codigo_usuario, usuario_nombre, created_at
                            ) VALUES (
                                @solicitud, 'CONDICIONES_LIMITACIONES', @numero_aocr,
                                @nombre, @ruta, @hash,
                                @tamanio, 'DIRCAV', @dircav_nombre,
                                'Director de Certificación Aeronáutica y Vigilancia Continua', NOW(), @dircav_id, @dircav_nombre, NOW()
                            ) ON CONFLICT DO NOTHING;";

                        using (var cmdFirma = new NpgsqlCommand(sqlFirma, cn, tx))
                        {
                            cmdFirma.Parameters.AddWithValue("@solicitud", solicitudId);
                            cmdFirma.Parameters.AddWithValue("@numero_aocr", (object)numeroAocr ?? DBNull.Value);
                            cmdFirma.Parameters.AddWithValue("@nombre", System.IO.Path.GetFileName(rutaPdfFirmado));
                            cmdFirma.Parameters.AddWithValue("@ruta", rutaPdfFirmado);
                            cmdFirma.Parameters.AddWithValue("@hash", hashPdfFirmado);
                            cmdFirma.Parameters.AddWithValue("@tamanio", tamanioPdf);
                            cmdFirma.Parameters.AddWithValue("@dircav_nombre", (object)dircavNombre ?? "DIRCAV");
                            cmdFirma.Parameters.AddWithValue("@dircav_id", dircavId);
                            cmdFirma.ExecuteNonQuery();
                        }

                        tx.Commit();
                        return true;
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        private static CondicionesLimitaciones Mapear(NpgsqlDataReader rd)
        {
            return new CondicionesLimitaciones
            {
                Id = rd["id"] != DBNull.Value ? Convert.ToInt32(rd["id"]) : 0,
                CodigoSolicitud = rd["codigo_solicitud"] != DBNull.Value ? Convert.ToInt32(rd["codigo_solicitud"]) : 0,
                CodigoInspeccion = rd["codigo_inspeccion"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_inspeccion"]) : null,
                CodigoInforme = rd["codigo_informe"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_informe"]) : null,
                NumeroAocr = rd["numero_aocr"] != DBNull.Value ? rd["numero_aocr"].ToString() : null,
                Version = rd["version"] != DBNull.Value ? Convert.ToInt32(rd["version"]) : 1,
                Estado = rd["estado"] != DBNull.Value ? rd["estado"].ToString() : AocrEstadoCl.ClBorrador,
                Vigente = rd["vigente"] != DBNull.Value && Convert.ToBoolean(rd["vigente"]),
                Compania = rd["compania"] != DBNull.Value ? rd["compania"].ToString() : null,
                OperadorExtranjero = rd["operador_extranjero"] != DBNull.Value ? rd["operador_extranjero"].ToString() : null,
                RepresentanteTecnico = rd["representante_tecnico"] != DBNull.Value ? rd["representante_tecnico"].ToString() : null,
                TipoOperacion = rd["tipo_operacion"] != DBNull.Value ? rd["tipo_operacion"].ToString() : null,
                RutasAutorizadas = rd["rutas_autorizadas"] != DBNull.Value ? rd["rutas_autorizadas"].ToString() : null,
                AlcanceAutorizado = rd["alcance_autorizado"] != DBNull.Value ? rd["alcance_autorizado"].ToString() : null,
                CondicionesAprobadas = rd["condiciones_aprobadas"] != DBNull.Value ? rd["condiciones_aprobadas"].ToString() : null,
                Limitaciones = rd["limitaciones"] != DBNull.Value ? rd["limitaciones"].ToString() : null,
                Observaciones = rd["observaciones"] != DBNull.Value ? rd["observaciones"].ToString() : null,
                InspectorUsuarioId = rd["inspector_usuario_id"] != DBNull.Value ? (int?)Convert.ToInt32(rd["inspector_usuario_id"]) : null,
                InspectorNombre = rd["inspector_nombre"] != DBNull.Value ? rd["inspector_nombre"].ToString() : null,
                FechaGeneracion = rd["fecha_generacion"] != DBNull.Value ? Convert.ToDateTime(rd["fecha_generacion"]) : DateTime.Now,
                CoordinadorUsuarioId = rd["coordinador_usuario_id"] != DBNull.Value ? (int?)Convert.ToInt32(rd["coordinador_usuario_id"]) : null,
                CoordinadorNombre = rd["coordinador_nombre"] != DBNull.Value ? rd["coordinador_nombre"].ToString() : null,
                ObservacionCoordinador = rd["observacion_coordinador"] != DBNull.Value ? rd["observacion_coordinador"].ToString() : null,
                FechaRevisionCoordinador = rd["fecha_revision_coordinador"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fecha_revision_coordinador"]) : null,
                DircavUsuarioId = rd["dircav_usuario_id"] != DBNull.Value ? (int?)Convert.ToInt32(rd["dircav_usuario_id"]) : null,
                DircavNombre = rd["dircav_nombre"] != DBNull.Value ? rd["dircav_nombre"].ToString() : null,
                ObservacionDircav = rd["observacion_dircav"] != DBNull.Value ? rd["observacion_dircav"].ToString() : null,
                FechaFirmaDircav = rd["fecha_firma_dircav"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fecha_firma_dircav"]) : null,
                RutaPdfBorrador = rd["ruta_pdf_borrador"] != DBNull.Value ? rd["ruta_pdf_borrador"].ToString() : null,
                RutaPdfFirmado = rd["ruta_pdf_firmado"] != DBNull.Value ? rd["ruta_pdf_firmado"].ToString() : null,
                HashPdf = rd["hash_pdf"] != DBNull.Value ? rd["hash_pdf"].ToString() : null,
                HashPdfFirmado = rd["hash_pdf_firmado"] != DBNull.Value ? rd["hash_pdf_firmado"].ToString() : null,
                TamanioPdf = rd["tamanio_pdf"] != DBNull.Value ? (long?)Convert.ToInt64(rd["tamanio_pdf"]) : null,
                CodigoVerificacion = rd["codigo_verificacion"] != DBNull.Value ? rd["codigo_verificacion"].ToString() : null,
                VersionConcurrencia = rd["version_concurrencia"] != DBNull.Value ? Convert.ToInt64(rd["version_concurrencia"]) : 1L,
                CreatedAt = rd["created_at"] != DBNull.Value ? Convert.ToDateTime(rd["created_at"]) : DateTime.Now,
                UpdatedAt = rd["updated_at"] != DBNull.Value ? Convert.ToDateTime(rd["updated_at"]) : DateTime.Now
            };
        }
    }
}
