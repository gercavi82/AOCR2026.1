using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using CapaDatos.Constants;
using CapaDatos.Services;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// Persistencia autoritativa del flujo final. Estados, auditoria y outbox se
    /// confirman en la misma transaccion PostgreSQL; nunca envia SMTP.
    /// </summary>
    public sealed class DocumentosFinalesWorkflowDAO
    {
        private const string TipoAocr = "RECONOCIMIENTO";
        private const string TipoCondiciones = "CONDICIONES_LIMITACIONES";
        private readonly string _cs;

        public DocumentosFinalesWorkflowDAO()
        {
            var configured = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = configured != null && !string.IsNullOrWhiteSpace(configured.ConnectionString)
                ? configured.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public AocrDocumentoGenerado ObtenerVigente(int solicitudId, string tipoDocumento)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn, null);
                const string sql = @"SELECT codigo_documento,codigo_solicitud,codigo_inspeccion,tipo_documento,
numero_aocr,nombre_archivo,ruta_documento,tamanio_pdf,estado,fecha_generacion,codigo_usuario,
usuario_nombre,created_at,version_documento,vigente,completo,bloqueado,hash_pdf,ruta_pdf_firmado,
hash_pdf_firmado,tamanio_pdf_firmado,codigo_usuario_firma,rol_firma,fecha_firma,version_concurrencia
FROM public.aocr_tbdocumento_generado
WHERE codigo_solicitud=@solicitud AND UPPER(tipo_documento)=UPPER(@tipo) AND vigente=TRUE
ORDER BY version_documento DESC,codigo_documento DESC LIMIT 1;";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                    cmd.Parameters.AddWithValue("@tipo", NormalizarTipo(tipoDocumento));
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return null;
                        return MapDocumento(rd);
                    }
                }
            }
        }

        public DocumentosFinalesResultado FinalizarYEncolar(DocumentoFinalEnvioRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        EnsureSchema(cn, tx);
                        LockSolicitud(cn, tx, request.SolicitudId);
                        var expediente = CargarExpediente(cn, tx, request.SolicitudId, request.InspeccionId);
                        ValidarInspectorYInforme(expediente, request);

                        var aocr = CargarDocumentoParaUpdate(cn, tx, request.SolicitudId, TipoAocr);
                        var condiciones = CargarDocumentoParaUpdate(cn, tx, request.SolicitudId, TipoCondiciones);

                        if (EsEnvioYaConfirmado(expediente.EstadoProceso, aocr, condiciones, request.RequiereAocr, request.RequiereCondiciones))
                        {
                            tx.Commit();
                            return ResultadoEnvio(true, true, request.RequiereAocr, request.RequiereCondiciones);
                        }

                        ValidarEstadoParaEnvio(expediente.EstadoProceso);
                        ValidarSinObservacionesPendientes(cn, tx, request.SolicitudId, request.InspeccionId);
                        if (request.RequiereAocr) ValidarEvidencia(aocr, request.Aocr, TipoAocr);
                        if (request.RequiereCondiciones) ValidarEvidencia(condiciones, request.Condiciones, TipoCondiciones);

                        if (request.RequiereAocr) ActualizarDocumentoEnviado(cn, tx, aocr.CodigoDocumento, AocrEstadosProceso.PendienteFirmaAocrDirdac, request.InspectorId);
                        if (request.RequiereCondiciones) ActualizarDocumentoEnviado(cn, tx, condiciones.CodigoDocumento, AocrEstadosProceso.PendienteFirmaCondicionesDcav, request.InspectorId);
                        var descripcionEnvio = request.RequiereAocr
                            ? "AOCR y Condiciones enviados conjuntamente para firmas independientes."
                            : "Condiciones y Limitaciones enviadas para firma DCAV según Módulo 8.";
                        CambiarEstadoExpediente(cn, tx, request.SolicitudId, request.InspeccionId,
                            AocrEstadosProceso.DocumentosFinalesEnFirma, "FIRMAS_INSTITUCIONALES", request.RequiereAocr ? "DIRDAC_DCAV" : "DCAV",
                            request.InspectorId, descripcionEnvio);
                        ActualizarEstadoSolicitud(cn, tx, request.SolicitudId, AocrEstadosProceso.DocumentosFinalesEnFirma, request.InspectorId);
                        RegistrarEvento(cn, tx, "DOCUMENTOS_FINALES_ENVIADOS_FIRMA:" + request.SolicitudId + ":" + (aocr != null ? aocr.VersionDocumento : 0) + ":" + (condiciones != null ? condiciones.VersionDocumento : 0),
                            "DOCUMENTOS_FINALES_ENVIADOS_FIRMA", request.SolicitudId, request.InspeccionId,
                            request.InspectorId, request.InspectorNombre, AocrEstadosProceso.DocumentosFinalesPorGenerar,
                            AocrEstadosProceso.DocumentosFinalesEnFirma, "AOCR=" + (aocr != null ? aocr.VersionDocumento : 0) + ";CONDICIONES=" + (condiciones != null ? condiciones.VersionDocumento : 0));

                        var dirdac = request.RequiereAocr ? ResolverDestinatarios(cn, tx, AocrRolesInstitucionales.DirdacSqlTokens) : new List<Destinatario>();
                        var dcav = request.RequiereCondiciones ? ResolverDestinatarios(cn, tx, AocrRolesInstitucionales.DcavSqlTokens) : new List<Destinatario>();
                        if (request.RequiereAocr && dirdac.Count == 0) throw new InvalidOperationException("No existen usuarios DIRDAC activos y autorizados con correo institucional.");
                        if (request.RequiereCondiciones && dcav.Count == 0) throw new InvalidOperationException("No existen usuarios DCAV activos y autorizados con correo institucional.");

                        if (request.RequiereAocr) EncolarRama(cn, tx, expediente, request, aocr, dirdac, true);
                        if (request.RequiereCondiciones) EncolarRama(cn, tx, expediente, request, condiciones, dcav, false);
                        tx.Commit();
                        return ResultadoEnvio(true, false, request.RequiereAocr, request.RequiereCondiciones);
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public DocumentosFinalesResultado RegistrarFirmaYFinalizar(DocumentoFinalFirmaRequest request, DocumentoFinalEvidencia otraFirmaVerificada)
        {
            if (request == null) throw new ArgumentNullException("request");
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        EnsureSchema(cn, tx);
                        LockSolicitud(cn, tx, request.SolicitudId);
                        var expediente = CargarExpediente(cn, tx, request.SolicitudId, request.InspeccionId);
                        if (expediente == null) throw new InvalidOperationException("Solicitud o inspeccion no encontrada.");
                        var tipo = NormalizarTipo(request.TipoDocumento);
                        ValidarRolFirma(tipo, request.Rol);
                        var doc = CargarDocumentoParaUpdate(cn, tx, request.SolicitudId, tipo);
                        var estadoFirmado = tipo == TipoAocr ? AocrEstadosProceso.AocrFirmadoDirdac : AocrEstadosProceso.CondicionesFirmadasDcav;
                        var estadoPendiente = tipo == TipoAocr ? AocrEstadosProceso.PendienteFirmaAocrDirdac : AocrEstadosProceso.PendienteFirmaCondicionesDcav;

                        if (doc != null && string.Equals(doc.Estado, estadoFirmado, StringComparison.OrdinalIgnoreCase))
                        {
                            tx.Commit();
                            return new DocumentosFinalesResultado { Exitoso = true, Idempotente = true, EstadoExpediente = AocrEstadosProceso.DocumentosFinalesEnFirma, EstadoAocr = tipo == TipoAocr ? estadoFirmado : null, EstadoCondiciones = tipo == TipoCondiciones ? estadoFirmado : null, Mensaje = "La firma ya fue registrada." };
                        }
                        if (doc == null || !string.Equals(doc.Estado, estadoPendiente, StringComparison.OrdinalIgnoreCase) || !doc.Bloqueado)
                            throw new InvalidOperationException("El documento no se encuentra en el estado pendiente de firma autorizado.");
                        if (doc.CodigoInspeccion.GetValueOrDefault() != request.InspeccionId)
                            throw new InvalidOperationException("La firma no corresponde a la inspeccion vigente.");

                        InsertarFirma(cn, tx, request);
                        const string update = @"UPDATE public.aocr_tbdocumento_generado SET estado=@estado,ruta_pdf_firmado=@ruta,
hash_pdf_firmado=@hash,tamanio_pdf_firmado=@bytes,codigo_usuario_firma=@usuario,rol_firma=@rol,
fecha_firma=NOW(),version_concurrencia=version_concurrencia+1 WHERE codigo_documento=@id;";
                        using (var cmd = new NpgsqlCommand(update, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@estado", estadoFirmado);
                            cmd.Parameters.AddWithValue("@ruta", request.RutaPdfFirmado);
                            cmd.Parameters.AddWithValue("@hash", request.HashPdfFirmado);
                            cmd.Parameters.AddWithValue("@bytes", request.TamanioPdfFirmado);
                            cmd.Parameters.AddWithValue("@usuario", request.UsuarioId);
                            cmd.Parameters.AddWithValue("@rol", request.Rol ?? string.Empty);
                            cmd.Parameters.AddWithValue("@id", doc.CodigoDocumento);
                            if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("No se pudo persistir el estado documental firmado.");
                        }

                        RegistrarEvento(cn, tx, "FIRMA_FINAL:" + request.SolicitudId + ":" + tipo + ":" + doc.VersionDocumento,
                            tipo == TipoAocr ? "AOCR_FIRMADO_DIRDAC" : "CONDICIONES_FIRMADAS_DCAV", request.SolicitudId,
                            request.InspeccionId, request.UsuarioId, request.UsuarioNombre, estadoPendiente, estadoFirmado,
                            request.HashPdfFirmado);

                        var otroTipo = tipo == TipoAocr ? TipoCondiciones : TipoAocr;
                        var otro = CargarDocumentoParaUpdate(cn, tx, request.SolicitudId, otroTipo);
                        var otroEstadoFirmado = tipo == TipoAocr ? AocrEstadosProceso.CondicionesFirmadasDcav : AocrEstadosProceso.AocrFirmadoDirdac;
                        var modificacionSoloCondiciones = expediente.TipoSolicitud == 3 && tipo == TipoCondiciones;
                        var documentosCompletos = modificacionSoloCondiciones
                            || (otro != null && string.Equals(otro.Estado, otroEstadoFirmado, StringComparison.OrdinalIgnoreCase));
                        if (!documentosCompletos)
                        {
                            tx.Commit();
                            return new DocumentosFinalesResultado
                            {
                                Exitoso = true,
                                Finalizado = false,
                                EstadoExpediente = AocrEstadosProceso.DocumentosFinalesEnFirma,
                                EstadoAocr = tipo == TipoAocr ? estadoFirmado : (otro != null ? otro.Estado : null),
                                EstadoCondiciones = tipo == TipoCondiciones ? estadoFirmado : (otro != null ? otro.Estado : null),
                                Mensaje = "Firma registrada. El expediente continua pendiente de la otra firma institucional."
                            };
                        }

                        if (!modificacionSoloCondiciones) ValidarOtraFirma(otro, otraFirmaVerificada);
                        ValidarSinObservacionesPendientes(cn, tx, request.SolicitudId, request.InspeccionId);
                        var actualFirmado = new DocumentoFinalEvidencia { DocumentoId = doc.CodigoDocumento, InspeccionId = request.InspeccionId, Version = doc.VersionDocumento, TipoDocumento = tipo, RutaPdf = request.RutaPdfFirmado, HashPdf = request.HashPdfFirmado, TamanioPdf = request.TamanioPdfFirmado };
                        var aocrFirmado = modificacionSoloCondiciones ? null : (tipo == TipoAocr ? actualFirmado : otraFirmaVerificada);
                        var condicionesFirmadas = tipo == TipoCondiciones ? actualFirmado : otraFirmaVerificada;
                        FinalizarExpedienteYEncolarRt(cn, tx, request, aocrFirmado, condicionesFirmadas);
                        tx.Commit();
                        return new DocumentosFinalesResultado
                        {
                            Exitoso = true,
                            Finalizado = true,
                            EstadoExpediente = AocrEstadosProceso.Finalizado,
                            EstadoAocr = modificacionSoloCondiciones ? "NO_APLICA" : AocrEstadosProceso.AocrFirmadoDirdac,
                            EstadoCondiciones = AocrEstadosProceso.CondicionesFirmadasDcav,
                            Mensaje = modificacionSoloCondiciones
                                ? "La firma DCAV fue verificada. La modificación finalizó y el correo único al RT quedó en cola."
                                : "Ambas firmas fueron verificadas. El expediente finalizo y el correo unico al RT quedo en cola."
                        };
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        private static void FinalizarExpedienteYEncolarRt(NpgsqlConnection cn, NpgsqlTransaction tx, DocumentoFinalFirmaRequest request, DocumentoFinalEvidencia aocr, DocumentoFinalEvidencia condiciones)
        {
            if (condiciones == null) throw new InvalidOperationException("Las Condiciones y Limitaciones firmadas son obligatorias para finalizar el expediente.");
            var soloCondiciones = aocr == null;
            var rt = ResolverRt(cn, tx, request.SolicitudId);
            if (rt == null) throw new InvalidOperationException("No fue posible resolver el RT real con correo valido para la entrega final.");
            CambiarEstadoExpediente(cn, tx, request.SolicitudId, request.InspeccionId, AocrEstadosProceso.Finalizado,
                "ENTREGA_DOCUMENTOS_FINALES", "RT", request.UsuarioId, soloCondiciones ? "Condiciones firmadas verificadas para modificación." : "Ambos documentos cuentan con la firma institucional requerida.");
            ActualizarEstadoSolicitud(cn, tx, request.SolicitudId, AocrEstadosProceso.Finalizado, request.UsuarioId);
            RegistrarEvento(cn, tx, "EXPEDIENTE_FINALIZADO:" + request.SolicitudId + ":" + (aocr != null ? aocr.Version : 0) + ":" + condiciones.Version,
                "EXPEDIENTE_FINALIZADO", request.SolicitudId, request.InspeccionId, request.UsuarioId, request.UsuarioNombre,
                AocrEstadosProceso.DocumentosFinalesEnFirma, AocrEstadosProceso.Finalizado, soloCondiciones ? "PDF de Condiciones firmado verificado." : "Dos PDF firmados verificados.");

            var eventKey = "DOCUMENTOS_FINALES_RT:" + request.SolicitudId + ":" + (aocr != null ? aocr.Version : 0) + ":" + condiciones.Version + ":" + rt.Email.ToLowerInvariant();
            var queue = new EmailQueueService();
            bool duplicado;
            var adjuntos = new List<EmailAttachmentItem>();
            if (aocr != null)
                adjuntos.Add(new EmailAttachmentItem { FileName = "AOCR_firmado.pdf", ContentType = "application/pdf", FilePath = aocr.RutaPdf, FileSize = aocr.TamanioPdf });
            adjuntos.Add(new EmailAttachmentItem { FileName = "Condiciones_y_Limitaciones_firmadas.pdf", ContentType = "application/pdf", FilePath = condiciones.RutaPdf, FileSize = condiciones.TamanioPdf });
            queue.EncolarConAdjuntosEnTransaccion(cn, tx, new EmailQueueItem
            {
                Para = rt.Email,
                ParaNombre = rt.Nombre,
                Asunto = "Sistema AOCR - documentos finales firmados",
                Cuerpo = soloCondiciones
                    ? "<p>La modificación AOCR ha finalizado. Se adjuntan las Condiciones y Limitaciones firmadas por DCAV.</p>"
                    : "<p>El expediente AOCR ha finalizado. Se adjuntan el AOCR firmado por DIRDAC y las Condiciones y Limitaciones firmadas por DCAV.</p>",
                Estado = EstadoEmail.Pendiente,
                SolicitudId = request.SolicitudId,
                EventKey = eventKey,
                TipoNotificacion = "DOCUMENTOS_FINALES_RT",
                CorrelationId = "FINAL-" + request.SolicitudId,
                EsHtml = true,
                MaxIntentos = 5
            }, adjuntos, out duplicado);
            InsertarNotificacionInterna(cn, tx, rt.UsuarioId, "DOCUMENTOS_FINALES_DISPONIBLES", "Documentos finales disponibles",
                soloCondiciones ? "Las Condiciones y Limitaciones firmadas están disponibles." : "El AOCR y las Condiciones y Limitaciones firmadas están disponibles.", "/SolicitudAOCR/Detalle/" + request.SolicitudId, eventKey + ":INTERNA", request.SolicitudId);
        }

        private static void EncolarRama(NpgsqlConnection cn, NpgsqlTransaction tx, ExpedienteRow expediente, DocumentoFinalEnvioRequest request, AocrDocumentoGenerado doc, IList<Destinatario> destinatarios, bool esAocr)
        {
            var tipoVisible = esAocr ? "AOCR" : "Condiciones y Limitaciones";
            var tipoEvento = esAocr ? "AOCR_PENDIENTE_FIRMA_DIRDAC" : "CONDICIONES_PENDIENTES_FIRMA_DCAV";
            var url = esAocr ? "/SolicitudAOCR/GeneradasFirmadas?DocumentoPendiente=AOCR" : "/SolicitudAOCR/GeneradasFirmadas?DocumentoPendiente=CONDICIONES";
            var asunto = esAocr ? "Sistema AOCR - AOCR pendiente de firma" : "Sistema AOCR - Condiciones y Limitaciones pendientes de firma";
            foreach (var destinatario in destinatarios)
            {
                var key = request.SolicitudId + ":" + request.InspeccionId + ":" + (esAocr ? "AOCR" : "CONDICIONES") + ":" + doc.VersionDocumento + ":PENDIENTE_FIRMA:" + destinatario.UsuarioId;
                var mensaje = "Solicitud " + expediente.NumeroSolicitud + "; compania " + expediente.Compania + "; inspeccion " + expediente.NumeroInspeccion + "; documento " + tipoVisible + "; enviado por " + request.InspectorNombre + ".";
                InsertarNotificacionInterna(cn, tx, destinatario.UsuarioId, tipoEvento, asunto, mensaje, url, key + ":INTERNA", request.SolicitudId);
                var queue = new EmailQueueService();
                bool duplicado;
                queue.EncolarConAdjuntosEnTransaccion(cn, tx, new EmailQueueItem
                {
                    Para = destinatario.Email,
                    ParaNombre = destinatario.Nombre,
                    Asunto = asunto,
                    Cuerpo = ConstruirCuerpoPendiente(expediente, request, tipoVisible, url),
                    Estado = EstadoEmail.Pendiente,
                    SolicitudId = request.SolicitudId,
                    EventKey = key + ":EMAIL",
                    TipoNotificacion = tipoEvento,
                    CorrelationId = "ENVIO-FIRMA-" + request.SolicitudId,
                    EsHtml = true,
                    MaxIntentos = 5
                }, null, out duplicado);
            }
        }

        private static string ConstruirCuerpoPendiente(ExpedienteRow expediente, DocumentoFinalEnvioRequest request, string tipo, string url)
        {
            var sb = new StringBuilder();
            sb.Append("<p>Existe un documento pendiente de revision y firma.</p><ul>")
              .Append("<li>Solicitud: ").Append(Html(expediente.NumeroSolicitud)).Append("</li>")
              .Append("<li>Compania: ").Append(Html(expediente.Compania)).Append("</li>")
              .Append("<li>Inspeccion: ").Append(Html(expediente.NumeroInspeccion)).Append("</li>")
              .Append("<li>Inspector: ").Append(Html(request.InspectorNombre)).Append("</li>")
              .Append("<li>Documento: ").Append(Html(tipo)).Append("</li>")
              .Append("<li>Fecha de envio: ").Append(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)).Append("</li></ul>")
              .Append("<p><a href=\"").Append(Html(CombinarUrl(request.BaseUrl, url))).Append("\">Abrir bandeja autenticada</a></p>");
            return sb.ToString();
        }

        private static void InsertarNotificacionInterna(NpgsqlConnection cn, NpgsqlTransaction tx, int usuarioId, string tipo, string titulo, string mensaje, string url, string eventKey, int solicitudId)
        {
            if (usuarioId <= 0) throw new InvalidOperationException("No se puede crear una notificacion interna con UsuarioId=0.");
            const string sql = @"INSERT INTO public.aocr_tbnotificacion
(codigousuario,titulo,mensaje,tipo,url,fechacreacion,leida,modulo,entidad_id,tipo_entidad,event_key,correlation_id,updated_at)
VALUES(@usuario,@titulo,@mensaje,@tipo,@url,NOW(),FALSE,'DOCUMENTOS_FINALES',@solicitud,'SolicitudAOCR',@key,@corr,NOW())
ON CONFLICT (event_key) WHERE event_key IS NOT NULL DO NOTHING;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@usuario", usuarioId);
                cmd.Parameters.AddWithValue("@titulo", titulo);
                cmd.Parameters.AddWithValue("@mensaje", mensaje);
                cmd.Parameters.AddWithValue("@tipo", tipo);
                cmd.Parameters.AddWithValue("@url", url);
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@key", eventKey);
                cmd.Parameters.AddWithValue("@corr", "DOCFINAL-" + solicitudId);
                cmd.ExecuteNonQuery();
            }
        }

        private static void ValidarEvidencia(AocrDocumentoGenerado actual, DocumentoFinalEvidencia enviada, string tipo)
        {
            if (actual == null || enviada == null) throw new InvalidOperationException("Deben existir los dos documentos finales vigentes.");
            if (actual.CodigoDocumento != enviada.DocumentoId || actual.VersionDocumento != enviada.Version || !actual.Vigente
                || actual.CodigoInspeccion.GetValueOrDefault() != enviada.InspeccionId
                || !string.Equals(NormalizarTipo(actual.TipoDocumento), NormalizarTipo(enviada.TipoDocumento), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La version vigente de " + tipo + " cambio. Recargue el expediente.");
            if (actual.Bloqueado || !actual.Completo) throw new InvalidOperationException("El documento " + tipo + " no esta completo o ya fue bloqueado.");
            if (!string.Equals(actual.HashPdf, enviada.HashPdf, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actual.RutaDocumento, enviada.RutaPdf, StringComparison.OrdinalIgnoreCase)
                || actual.TamanioPdf.GetValueOrDefault() != enviada.TamanioPdf)
                throw new InvalidOperationException("La integridad persistida de " + tipo + " no coincide con el PDF fisico vigente.");
        }

        private static void ValidarOtraFirma(AocrDocumentoGenerado otro, DocumentoFinalEvidencia evidencia)
        {
            if (otro == null || evidencia == null || otro.CodigoDocumento != evidencia.DocumentoId || otro.VersionDocumento != evidencia.Version
                || otro.CodigoInspeccion.GetValueOrDefault() != evidencia.InspeccionId
                || !string.Equals(otro.RutaPdfFirmado, evidencia.RutaPdf, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(otro.HashPdfFirmado, evidencia.HashPdf, StringComparison.OrdinalIgnoreCase)
                || otro.TamanioPdfFirmado.GetValueOrDefault() != evidencia.TamanioPdf)
                throw new InvalidOperationException("La otra firma institucional no supera la validacion de version e integridad.");
            ValidarRolFirma(NormalizarTipo(otro.TipoDocumento), otro.RolFirma);
        }

        private static void ValidarSinObservacionesPendientes(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId, int inspeccionId)
        {
            const string sql = @"SELECT COUNT(*) FROM public.aocr_tbnoconformidad
WHERE (codigo_solicitud=@solicitud OR codigo_inspeccion=@inspeccion OR codigo_inspeccion_nueva=@inspeccion)
  AND fecha_cierre IS NULL
  AND UPPER(COALESCE(estado,'')) NOT IN ('CERRADA','CERRADO','ANULADA');";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("Existen observaciones o no conformidades pendientes; no es posible continuar con los documentos finales.");
            }
        }

        private static void ValidarInspectorYInforme(ExpedienteRow expediente, DocumentoFinalEnvioRequest request)
        {
            if (expediente == null) throw new InvalidOperationException("Solicitud o inspeccion no encontrada.");
            if (request.InspectorId <= 0 || expediente.InspectorId != request.InspectorId)
                throw new UnauthorizedAccessException("Solo el Inspector asignado puede finalizar los documentos.");
            if (!expediente.InformeFinalizado || !expediente.InformeFirmadoInspector || !EsInformeAprobado(expediente.EstadoInforme))
                throw new InvalidOperationException("El Informe Tecnico vigente debe estar firmado y aprobado por DIRDAC.");
            if (string.IsNullOrWhiteSpace(expediente.RutaInformeFirmado) || string.IsNullOrWhiteSpace(expediente.HashInforme))
                throw new InvalidOperationException("La evidencia firmada del Informe Tecnico vigente es incompleta.");
        }

        private static bool EsInformeAprobado(string estado)
        {
            var e = (estado ?? string.Empty).Trim().ToUpperInvariant();
            return e == "APROBADO_DIRECCION" || e == "INFORME_TECNICO_APROBADO_DIRDAC" || e == "INFORME_TECNICO_APROBADO_DCAV";
        }

        private static void ValidarEstadoParaEnvio(string estado)
        {
            var e = (estado ?? string.Empty).Trim().ToUpperInvariant();
            if (e != AocrEstadosProceso.DocumentosFinalesPorGenerar && e != AocrEstadosProceso.InformeTecnicoAprobadoDirdac
                && e != AocrEstadosProceso.InformeTecnicoAprobadoDcav)
                throw new InvalidOperationException("El expediente no se encuentra habilitado para enviar documentos finales.");
        }

        private static bool EsEnvioYaConfirmado(string estado, AocrDocumentoGenerado aocr, AocrDocumentoGenerado condiciones, bool requiereAocr, bool requiereCondiciones)
        {
            return string.Equals(estado, AocrEstadosProceso.DocumentosFinalesEnFirma, StringComparison.OrdinalIgnoreCase)
                && (!requiereAocr || (aocr != null && (string.Equals(aocr.Estado, AocrEstadosProceso.PendienteFirmaAocrDirdac, StringComparison.OrdinalIgnoreCase) || string.Equals(aocr.Estado, AocrEstadosProceso.AocrFirmadoDirdac, StringComparison.OrdinalIgnoreCase))))
                && (!requiereCondiciones || (condiciones != null && (string.Equals(condiciones.Estado, AocrEstadosProceso.PendienteFirmaCondicionesDcav, StringComparison.OrdinalIgnoreCase) || string.Equals(condiciones.Estado, AocrEstadosProceso.CondicionesFirmadasDcav, StringComparison.OrdinalIgnoreCase))));
        }

        private static DocumentosFinalesResultado ResultadoEnvio(bool ok, bool idempotente, bool requiereAocr, bool requiereCondiciones)
        {
            return new DocumentosFinalesResultado
            {
                Exitoso = ok,
                Idempotente = idempotente,
                EstadoExpediente = AocrEstadosProceso.DocumentosFinalesEnFirma,
                EstadoAocr = requiereAocr ? AocrEstadosProceso.PendienteFirmaAocrDirdac : "NO_APLICA",
                EstadoCondiciones = requiereCondiciones ? AocrEstadosProceso.PendienteFirmaCondicionesDcav : "NO_APLICA",
                Mensaje = idempotente
                    ? "Los documentos requeridos ya fueron enviados; no se duplicaron estados ni notificaciones."
                    : (requiereAocr ? "Los dos documentos fueron bloqueados y enviados a sus firmantes. Las notificaciones quedaron pendientes en la cola." : "Condiciones y Limitaciones fueron bloqueadas y enviadas a DCAV. La notificación quedó pendiente en la cola.")
            };
        }

        private static void ActualizarDocumentoEnviado(NpgsqlConnection cn, NpgsqlTransaction tx, int documentoId, string estado, int usuarioId)
        {
            const string sql = @"UPDATE public.aocr_tbdocumento_generado SET estado=@estado,bloqueado=TRUE,
version_concurrencia=version_concurrencia+1,codigo_usuario_liberacion=@usuario WHERE codigo_documento=@id AND vigente=TRUE AND bloqueado=FALSE;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@estado", estado); cmd.Parameters.AddWithValue("@usuario", usuarioId); cmd.Parameters.AddWithValue("@id", documentoId);
                if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Conflicto de concurrencia al bloquear el documento final.");
            }
        }

        private static void InsertarFirma(NpgsqlConnection cn, NpgsqlTransaction tx, DocumentoFinalFirmaRequest r)
        {
            const string sql = @"INSERT INTO public.aocr_tbfirma_documento
(codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,nombre_archivo,ruta_documento,hash_documento,
tamanio_pdf_firmado,firmado_por_rol,codigo_qr,sujeto_certificado,nombre_firmante,cargo_firmante,fecha_firma,codigo_usuario,usuario_nombre,created_at)
VALUES(@solicitud,@inspeccion,@tipo,@numero,@nombre,@ruta,@hash,@bytes,@rol,@qr,@sujeto,@firmante,@cargo,NOW(),@usuario,@usuario_nombre,NOW())
ON CONFLICT DO NOTHING;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", r.SolicitudId); cmd.Parameters.AddWithValue("@inspeccion", r.InspeccionId);
                cmd.Parameters.AddWithValue("@tipo", NormalizarTipo(r.TipoDocumento)); cmd.Parameters.AddWithValue("@numero", (object)r.NumeroAocr ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nombre", (object)r.NombreArchivo ?? DBNull.Value); cmd.Parameters.AddWithValue("@ruta", r.RutaPdfFirmado);
                cmd.Parameters.AddWithValue("@hash", r.HashPdfFirmado); cmd.Parameters.AddWithValue("@bytes", r.TamanioPdfFirmado);
                cmd.Parameters.AddWithValue("@rol", r.Rol ?? string.Empty); cmd.Parameters.AddWithValue("@qr", (object)r.CodigoQr ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sujeto", (object)r.SujetoCertificado ?? DBNull.Value); cmd.Parameters.AddWithValue("@firmante", (object)r.NombreFirmante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cargo", (object)r.CargoFirmante ?? DBNull.Value); cmd.Parameters.AddWithValue("@usuario", r.UsuarioId);
                cmd.Parameters.AddWithValue("@usuario_nombre", (object)r.UsuarioNombre ?? DBNull.Value); cmd.ExecuteNonQuery();
            }
        }

        private static void ValidarRolFirma(string tipo, string rol)
        {
            if (tipo == TipoAocr && !AocrRolesInstitucionales.EsDirdac(rol))
                throw new UnauthorizedAccessException("DIRDAC firma exclusivamente el AOCR.");
            if (tipo == TipoCondiciones && !AocrRolesInstitucionales.EsDcav(rol))
                throw new UnauthorizedAccessException("DCAV firma exclusivamente Condiciones y Limitaciones.");
        }

        private static ExpedienteRow CargarExpediente(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId, int inspeccionId)
        {
            const string sql = @"SELECT s.codigo_solicitud,COALESCE(NULLIF(s.numero_solicitud,''),s.codigo_solicitud::text) numero_solicitud,
COALESCE(NULLIF(s.razon_social,''),NULLIF(s.nombre_operador,''),'No registrada') compania,
s.codigo_usuario,s.tipo_solicitud,i.codigo_inspeccion,COALESCE(NULLIF(i.numero_inspeccion,''),i.codigo_inspeccion::text) numero_inspeccion,
i.codigo_inspector,inf.finalizado,inf.firmado_inspector,inf.estado_informe,inf.ruta_documento_firmado,inf.hash_documento,
COALESCE((SELECT pe.estado_actual FROM public.aocr_proceso_estado pe WHERE pe.solicitud_id=s.codigo_solicitud AND pe.activo=TRUE ORDER BY pe.id DESC LIMIT 1),'') estado_proceso
FROM public.aocr_tbsolicitud s JOIN public.aocr_tbinspeccion i ON i.codigo_solicitud=s.codigo_solicitud
JOIN LATERAL (SELECT x.* FROM public.aocr_tbinforme_inspeccion x WHERE x.codigo_inspeccion=i.codigo_inspeccion ORDER BY x.version DESC,x.codigo_informe DESC LIMIT 1) inf ON TRUE
WHERE s.codigo_solicitud=@solicitud AND i.codigo_inspeccion=@inspeccion AND s.deleted_at IS NULL FOR UPDATE OF s,i,inf;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId); cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;
                    return new ExpedienteRow { SolicitudId = solicitudId, InspeccionId = inspeccionId, NumeroSolicitud = S(rd,"numero_solicitud"), Compania = S(rd,"compania"), NumeroInspeccion = S(rd,"numero_inspeccion"), RtUsuarioId = I(rd,"codigo_usuario"), TipoSolicitud = I(rd,"tipo_solicitud"), InspectorId = I(rd,"codigo_inspector"), InformeFinalizado = B(rd,"finalizado"), InformeFirmadoInspector = B(rd,"firmado_inspector"), EstadoInforme = S(rd,"estado_informe"), RutaInformeFirmado = S(rd,"ruta_documento_firmado"), HashInforme = S(rd,"hash_documento"), EstadoProceso = S(rd,"estado_proceso") };
                }
            }
        }

        private static AocrDocumentoGenerado CargarDocumentoParaUpdate(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId, string tipo)
        {
            const string sql = @"SELECT codigo_documento,codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,nombre_archivo,
ruta_documento,tamanio_pdf,estado,fecha_generacion,codigo_usuario,usuario_nombre,created_at,version_documento,vigente,completo,
bloqueado,hash_pdf,ruta_pdf_firmado,hash_pdf_firmado,tamanio_pdf_firmado,codigo_usuario_firma,rol_firma,fecha_firma,version_concurrencia
FROM public.aocr_tbdocumento_generado WHERE codigo_solicitud=@solicitud AND UPPER(tipo_documento)=UPPER(@tipo) AND vigente=TRUE
ORDER BY version_documento DESC,codigo_documento DESC LIMIT 1 FOR UPDATE;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId); cmd.Parameters.AddWithValue("@tipo", tipo);
                using (var rd = cmd.ExecuteReader()) { return rd.Read() ? MapDocumento(rd) : null; }
            }
        }

        private static AocrDocumentoGenerado MapDocumento(NpgsqlDataReader rd)
        {
            return new AocrDocumentoGenerado { CodigoDocumento=I(rd,"codigo_documento"),CodigoSolicitud=I(rd,"codigo_solicitud"),CodigoInspeccion=NI(rd,"codigo_inspeccion"),TipoDocumento=S(rd,"tipo_documento"),NumeroAocr=S(rd,"numero_aocr"),NombreArchivo=S(rd,"nombre_archivo"),RutaDocumento=S(rd,"ruta_documento"),TamanioPdf=NL(rd,"tamanio_pdf"),Estado=S(rd,"estado"),FechaGeneracion=D(rd,"fecha_generacion"),CodigoUsuario=NI(rd,"codigo_usuario"),UsuarioNombre=S(rd,"usuario_nombre"),CreatedAt=ND(rd,"created_at"),VersionDocumento=I(rd,"version_documento"),Vigente=B(rd,"vigente"),Completo=B(rd,"completo"),Bloqueado=B(rd,"bloqueado"),HashPdf=S(rd,"hash_pdf"),HashPdfFirmado=S(rd,"hash_pdf_firmado"),TamanioPdfFirmado=NL(rd,"tamanio_pdf_firmado"),RutaPdfFirmado=S(rd,"ruta_pdf_firmado"),CodigoUsuarioFirma=NI(rd,"codigo_usuario_firma"),RolFirma=S(rd,"rol_firma"),FechaFirma=ND(rd,"fecha_firma"),VersionConcurrencia=L(rd,"version_concurrencia") };
        }

        private static IList<Destinatario> ResolverDestinatarios(NpgsqlConnection cn, NpgsqlTransaction tx, string[] roles)
        {
            const string sql = @"SELECT DISTINCT u.idusuario,TRIM(u.correo) correo,
TRIM(COALESCE(NULLIF(u.nombreusuario,''),'')||' '||COALESCE(NULLIF(u.apellidousuario,''),'')) nombre
FROM public.usuario u JOIN public.usuario_rol ur ON u.codigousuario::text=ur.codigousuario::text
JOIN public.rol r ON r.codigorol=ur.codigorol
WHERE regexp_replace(UPPER(TRIM(COALESCE(r.descripcion,''))),'[^A-Z0-9]+','_','g')=ANY(@roles)
AND COALESCE(ur.activo,TRUE)=TRUE AND COALESCE(r.activo,TRUE)=TRUE AND COALESCE(u.estadoactividad::text,'1')='1';";
            var result = new List<Destinatario>();
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@roles", roles);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) { var email=S(rd,"correo"); if (EmailValido(email)) result.Add(new Destinatario { UsuarioId=I(rd,"idusuario"),Email=email,Nombre=S(rd,"nombre") }); }
            }
            return result.Where(x=>x.UsuarioId>0).GroupBy(x=>x.UsuarioId).Select(x=>x.First()).ToList();
        }

        private static Destinatario ResolverRt(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId)
        {
            const string sql = @"SELECT u.idusuario,TRIM(u.correo) correo,TRIM(COALESCE(u.nombreusuario,'')||' '||COALESCE(u.apellidousuario,'')) nombre
FROM public.aocr_tbsolicitud s JOIN public.usuario u ON u.idusuario=s.codigo_usuario
WHERE s.codigo_solicitud=@solicitud AND COALESCE(u.estadoactividad::text,'1')='1' LIMIT 1;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                using (var rd=cmd.ExecuteReader()) { if (!rd.Read()) return null; var email=S(rd,"correo"); return EmailValido(email) ? new Destinatario { UsuarioId=I(rd,"idusuario"),Email=email,Nombre=S(rd,"nombre") } : null; }
            }
        }

        private static void CambiarEstadoExpediente(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId,int inspeccionId,string estado,string etapa,string rol,int usuarioId,string observacion)
        {
            const string sql=@"UPDATE public.aocr_proceso_estado SET activo=FALSE,updated_at=NOW(),updated_by=@usuario WHERE solicitud_id=@solicitud AND activo=TRUE AND estado_actual<>@estado;
INSERT INTO public.aocr_proceso_estado(solicitud_id,inspeccion_id,estado_actual,etapa_actual,rol_responsable,observacion,activo,version,created_at,created_by,updated_at,updated_by)
SELECT @solicitud,@inspeccion,@estado,@etapa,@rol,@observacion,TRUE,COALESCE(MAX(version),0)+1,NOW(),@usuario,NOW(),@usuario FROM public.aocr_proceso_estado WHERE solicitud_id=@solicitud
HAVING NOT EXISTS(SELECT 1 FROM public.aocr_proceso_estado WHERE solicitud_id=@solicitud AND activo=TRUE AND estado_actual=@estado);";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);cmd.Parameters.AddWithValue("@estado",estado);cmd.Parameters.AddWithValue("@etapa",etapa);cmd.Parameters.AddWithValue("@rol",rol);cmd.Parameters.AddWithValue("@observacion",observacion);cmd.Parameters.AddWithValue("@usuario",usuarioId);cmd.ExecuteNonQuery();}
        }

        private static void ActualizarEstadoSolicitud(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId,string estado,int usuarioId)
        { using(var cmd=new NpgsqlCommand("UPDATE public.aocr_tbsolicitud SET estado=@estado,updated_at=NOW(),updated_by=@usuario WHERE codigo_solicitud=@solicitud;",cn,tx)){cmd.Parameters.AddWithValue("@estado",estado);cmd.Parameters.AddWithValue("@usuario",usuarioId);cmd.Parameters.AddWithValue("@solicitud",solicitudId);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("No se pudo actualizar el expediente.");} }

        private static void RegistrarEvento(NpgsqlConnection cn,NpgsqlTransaction tx,string key,string evento,int solicitudId,int inspeccionId,int usuarioId,string usuario,string anterior,string nuevo,string observacion)
        { const string sql=@"INSERT INTO public.aocr_evento_workflow(evento,event_key,correlation_id,modulo,accion,entidad,entidad_id,solicitud_id,inspeccion_id,estado_anterior,estado_nuevo,usuario_id,usuario,observacion,resultado,intentos,fecha,updated_at)
VALUES(@evento,@key,@corr,'DOCUMENTOS_FINALES',@evento,'aocr_tbsolicitud',@solicitud,@solicitud,@inspeccion,@anterior,@nuevo,@usuario_id,@usuario,@observacion,'REGISTRADO',1,NOW(),NOW()) ON CONFLICT(event_key) DO NOTHING;";using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@evento",evento);cmd.Parameters.AddWithValue("@key",key);cmd.Parameters.AddWithValue("@corr","DOCFINAL-"+solicitudId);cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);cmd.Parameters.AddWithValue("@anterior",(object)anterior??DBNull.Value);cmd.Parameters.AddWithValue("@nuevo",nuevo);cmd.Parameters.AddWithValue("@usuario_id",usuarioId);cmd.Parameters.AddWithValue("@usuario",(object)usuario??DBNull.Value);cmd.Parameters.AddWithValue("@observacion",(object)observacion??DBNull.Value);cmd.ExecuteNonQuery();} }

        private static void LockSolicitud(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId)
        { using(var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(@id::bigint);",cn,tx)){cmd.Parameters.AddWithValue("@id",solicitudId);cmd.ExecuteNonQuery();} }

        private static void EnsureSchema(NpgsqlConnection cn,NpgsqlTransaction tx)
        {
            const string sql=@"ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS version_documento INTEGER NOT NULL DEFAULT 1;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS vigente BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS completo BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS bloqueado BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS hash_pdf VARCHAR(128);
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS ruta_pdf_firmado VARCHAR(500);
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS tamanio_pdf_firmado BIGINT;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS codigo_usuario_firma INTEGER;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS rol_firma VARCHAR(100);
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS fecha_firma TIMESTAMP;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS version_concurrencia BIGINT NOT NULL DEFAULT 1;
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS modulo VARCHAR(100);
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS entidad_id INTEGER;
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS tipo_entidad VARCHAR(100);
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS event_key VARCHAR(300);
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(80);
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;
CREATE TABLE IF NOT EXISTS public.aocr_evento_workflow (
 id BIGSERIAL PRIMARY KEY,
 evento VARCHAR(80) NOT NULL,
 event_key VARCHAR(300) NOT NULL,
 correlation_id VARCHAR(80) NOT NULL,
 modulo VARCHAR(80),
 accion VARCHAR(100),
 entidad VARCHAR(100),
 entidad_id INTEGER,
 solicitud_id INTEGER,
 inspeccion_id INTEGER,
 informe_id INTEGER,
 nc_id INTEGER,
 documento_id INTEGER,
 estado_anterior VARCHAR(100),
 estado_nuevo VARCHAR(100),
 usuario_id INTEGER,
 usuario VARCHAR(150),
 rol VARCHAR(100),
 ip VARCHAR(64),
 observacion TEXT,
 version INTEGER,
 hash VARCHAR(128),
 resultado VARCHAR(40) NOT NULL DEFAULT 'REGISTRADO',
 detalle_error TEXT,
 intentos INTEGER NOT NULL DEFAULT 1,
 fecha TIMESTAMP NOT NULL DEFAULT NOW(),
 created_at TIMESTAMP NOT NULL DEFAULT NOW(),
 updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
 CONSTRAINT uq_aocr_evento_workflow_event_key UNIQUE(event_key),
 CONSTRAINT ck_aocr_evento_intentos CHECK(intentos>0)
);
ALTER TABLE public.aocr_evento_workflow ADD COLUMN IF NOT EXISTS created_at TIMESTAMP NOT NULL DEFAULT NOW();
CREATE INDEX IF NOT EXISTS ix_aocr_evento_workflow_correlation ON public.aocr_evento_workflow(correlation_id,fecha);
CREATE INDEX IF NOT EXISTS ix_aocr_evento_workflow_solicitud ON public.aocr_evento_workflow(solicitud_id,fecha);
WITH repetidos AS (
  SELECT codigo_documento,ROW_NUMBER() OVER(PARTITION BY codigo_solicitud,UPPER(tipo_documento) ORDER BY version_documento DESC,codigo_documento DESC) rn
  FROM public.aocr_tbdocumento_generado WHERE vigente=TRUE
)
UPDATE public.aocr_tbdocumento_generado d SET vigente=FALSE
FROM repetidos r WHERE d.codigo_documento=r.codigo_documento AND r.rn>1;
CREATE UNIQUE INDEX IF NOT EXISTS ux_documento_final_vigente ON public.aocr_tbdocumento_generado(codigo_solicitud,UPPER(tipo_documento)) WHERE vigente=TRUE;
CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tbnotificacion_event_key ON public.aocr_tbnotificacion(event_key) WHERE event_key IS NOT NULL;";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.ExecuteNonQuery();}
        }

        private static bool EmailValido(string email){if(string.IsNullOrWhiteSpace(email)||email.EndsWith("@invalid.local",StringComparison.OrdinalIgnoreCase))return false;try{return string.Equals(new MailAddress(email.Trim()).Address,email.Trim(),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        private static string NormalizarTipo(string tipo){var t=NormalizarToken(tipo);return t=="AOCR"||t=="RECONOCIMIENTO"?TipoAocr:TipoCondiciones;}
        private static string NormalizarToken(string value){return (value??string.Empty).Trim().ToUpperInvariant().Replace(" ","_").Replace("-","_");}
        private static string CombinarUrl(string baseUrl,string relative){return string.IsNullOrWhiteSpace(baseUrl)?relative:(baseUrl.TrimEnd('/')+"/"+relative.TrimStart('/'));}
        private static string Html(string value){return System.Net.WebUtility.HtmlEncode(value??string.Empty);}
        private static string S(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?null:r[n].ToString();}
        private static int I(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt32(r[n]);}
        private static int? NI(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?(int?)null:Convert.ToInt32(r[n]);}
        private static long L(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0L:Convert.ToInt64(r[n]);}
        private static long? NL(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?(long?)null:Convert.ToInt64(r[n]);}
        private static bool B(NpgsqlDataReader r,string n){return r[n]!=DBNull.Value&&Convert.ToBoolean(r[n]);}
        private static DateTime D(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?DateTime.MinValue:Convert.ToDateTime(r[n]);}
        private static DateTime? ND(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(r[n]);}

        private sealed class Destinatario { public int UsuarioId; public string Email; public string Nombre; }
        private sealed class ExpedienteRow { public int SolicitudId;public int InspeccionId;public int InspectorId;public int RtUsuarioId;public int TipoSolicitud;public string NumeroSolicitud;public string NumeroInspeccion;public string Compania;public bool InformeFinalizado;public bool InformeFirmadoInspector;public string EstadoInforme;public string RutaInformeFirmado;public string HashInforme;public string EstadoProceso; }
    }
}
