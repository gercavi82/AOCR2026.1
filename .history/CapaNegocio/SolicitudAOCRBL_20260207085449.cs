using System;
using System.Collections.Generic;
using CapaModelo;
using CapaDatos.DAOs;

namespace CapaNegocio
{
    public static class SolicitudAOCRBL
    {
        // 1. Crear Solicitud
        public static int CrearSolicitud(SolicitudAOCR solicitud, out string mensaje)
        {
            try
            {
                solicitud.FechaSolicitud = DateTime.Now;
                solicitud.Estado = "PENDIENTE";
                int id = new SolicitudAOCRDAO().InsertarConReturn(solicitud);
                mensaje = "Solicitud creada exitosamente.";
                return id;
            }
            catch (Exception ex)
            {
                mensaje = "Error al crear solicitud: " + ex.Message;
                return 0;
            }
        }

        // 2. Obtener solicitudes por usuario
        public static List<SolicitudAOCR> ListarPorUsuario(int codigoUsuario)
        {
            return new SolicitudAOCRDAO().ObtenerPorUsuario(codigoUsuario);
        }

        // 3. Obtener por ID
        public static SolicitudAOCR ObtenerPorId(int id)
        {
            return new SolicitudAOCRDAO().ObtenerPorId(id);
        }

        // 4. Actualizar solicitud
        public static bool ActualizarSolicitud(SolicitudAOCR solicitud, out string mensaje)
        {
            try
            {
                bool ok = new SolicitudAOCRDAO().ActualizarGeneral(solicitud);
                mensaje = ok ? "Solicitud actualizada correctamente." : "No se pudo actualizar la solicitud.";
                return ok;
            }
            catch (Exception ex)
            {
                mensaje = "Error al actualizar: " + ex.Message;
                return false;
            }
        }

        // 5. Cambiar estado
        public static bool CambiarEstado(int idSolicitud, string nuevoEstado, int codigoUsuario, string observaciones, out string mensaje)
        {
            try
            {
                bool ok = new SolicitudAOCRDAO().CambiarEstado(idSolicitud, nuevoEstado, codigoUsuario, observaciones);
                mensaje = ok ? "Estado actualizado correctamente." : "No fue posible cambiar el estado.";
                return ok;
            }
            catch (Exception ex)
            {
                mensaje = "Error cambiando estado: " + ex.Message;
                return false;
            }
        }

        public static List<SolicitudAOCR> ListarActivas() => new SolicitudAOCRDAO().ListarActivas();

        public static List<SolicitudAOCR> ListarPorEstado(string estado) => new SolicitudAOCRDAO().ObtenerPorEstado(estado);

        public static List<SolicitudAOCR> ListarPendientesRevision() => new SolicitudAOCRDAO().ObtenerPendientesRevision();

        public static List<SolicitudAOCR> ListarParaValidacionJefatura() => new SolicitudAOCRDAO().ObtenerParaValidacionJefatura();

        // 11. Marcar Para Inspeccion
        public static bool MarcarParaInspeccion(int idSolicitud)
        {
            try
            {
                return new SolicitudAOCRDAO().CambiarEstado(idSolicitud, "INSPECCION_SOLICITADA", 0, "Cambio automático por sistema");
            }
            catch { return false; }
        }

        // 12. Asignar inspectores (CORREGIDO: Delega al DAO)
        public static bool AsignarInspectores(int id, int principal, int? apoyo, DateTime fecha, string obs, out string mensaje)
        {
            // Ya no hay código SQL aquí, se movió al DAO para evitar errores de Npgsql en esta capa
            return new SolicitudAOCRDAO().AsignarInspectores(id, principal, apoyo, fecha, obs, out mensaje);
        }
        
        // ==========================================
        // WORKFLOW SUBSANACIÓN
        // ==========================================
        
        /// <summary>
        /// Solicita subsanación de documentos - Marca SUBSANACION y registra observaciones
        /// </summary>
        public static bool SolicitarSubsanacion(int idSolicitud, string observaciones, int codigoUsuario, out string mensaje)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(observaciones))
                {
                    mensaje = "Debe especificar las observaciones para la subsanación.";
                    return false;
                }
                
                var solicitud = ObtenerPorId(idSolicitud);
                if (solicitud == null)
                {
                    mensaje = "Solicitud no encontrada.";
                    return false;
                }
                
                // Verificar que el estado permite subsanación
                var estadosPermitidos = new[] { "ANALISIS_REQUISITOS", "EN_EVALUACION_TECNICA", "EN_EVALUACION_LEGAL", "EN_EVALUACION_FINANCIERA", "EN_APROBACION_COORDINADOR", "EN_APROBACION_DIRECTOR" };
                if (!System.Array.Exists(estadosPermitidos, e => e == solicitud.Estado))
                {
                    mensaje = $"No se puede solicitar subsanación desde el estado {solicitud.Estado}.";
                    return false;
                }
                
                // Cambiar estado a SUBSANACION
                bool resultado = CambiarEstado(idSolicitud, "SUBSANACION", codigoUsuario, observaciones, out mensaje);
                
                if (resultado)
                {
                    // Enviar notificación al solicitante
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario: solicitud.CodigoUsuario,
                        titulo: "Subsanación solicitada",
                        mensaje: $"Su solicitud AOCR #{idSolicitud} requiere correcciones: {observaciones}",
                        tipo: CapaDatos.Constants.TiposNotificacion.WARNING,
                        url: $"/SolicitudAOCR/Detalle/{idSolicitud}"
                    );
                    
                    LogBL.RegistrarInfo($"Subsanación solicitada para solicitud {idSolicitud} por usuario {codigoUsuario}", "SolicitudAOCR");
                }
                
                return resultado;
            }
            catch (Exception ex)
            {
                mensaje = "Error al solicitar subsanación: " + ex.Message;
                LogBL.RegistrarError("Error en SolicitarSubsanacion", ex.ToString(), "SolicitudAOCR");
                return false;
            }
        }
        
        /// <summary>
        /// Marca solicitud como SUBSANADO - El solicitante ha cargado documentos corregidos
        /// </summary>
        public static bool MarcarSubsanado(int idSolicitud, int codigoUsuario, string comentarios, out string mensaje)
        {
            try
            {
                var solicitud = ObtenerPorId(idSolicitud);
                if (solicitud == null)
                {
                    mensaje = "Solicitud no encontrada.";
                    return false;
                }
                
                if (solicitud.Estado != "SUBSANACION")
                {
                    mensaje = "La solicitud no está en estado SUBSANACION.";
                    return false;
                }
                
                // Cambiar estado a SUBSANADO
                bool resultado = CambiarEstado(idSolicitud, "SUBSANADO", codigoUsuario, comentarios ?? "Documentos corregidos cargados", out mensaje);
                
                if (resultado)
                {
                    // Notificar a operadores para revisión
                    var operadores = UsuarioBL.ObtenerPorRol("Operador");
                    foreach (var op in operadores)
                    {
                        NotificacionBL.EnviarNotificacion(
                            codigoUsuario: op.CodigoUsuario,
                            titulo: "Solicitud subsanada para revisión",
                            mensaje: $"Solicitud AOCR #{idSolicitud} ha sido subsanada y requiere revisión.",
                            tipo: CapaDatos.Constants.TiposNotificacion.INFO,
                            url: $"/SolicitudAOCR/Detalle/{idSolicitud}"
                        );
                    }
                    
                    LogBL.RegistrarInfo($"Solicitud {idSolicitud} marcada como SUBSANADO por usuario {codigoUsuario}", "SolicitudAOCR");
                }
                
                return resultado;
            }
            catch (Exception ex)
            {
                mensaje = "Error al marcar subsanado: " + ex.Message;
                LogBL.RegistrarError("Error en MarcarSubsanado", ex.ToString(), "SolicitudAOCR");
                return false;
            }
        }
        
        // ==========================================
        // WORKFLOW APROBACIÓN
        // ==========================================
        
        /// <summary>
        /// Aprobación por Coordinador - Pasa a EN_APROBACION_DIRECTOR
        /// </summary>
        public static bool AprobarCoordinador(int idSolicitud, int codigoUsuario, string observaciones, out string mensaje)
        {
            try
            {
                var solicitud = ObtenerPorId(idSolicitud);
                if (solicitud == null)
                {
                    mensaje = "Solicitud no encontrada.";
                    return false;
                }
                
                if (solicitud.Estado != "EN_APROBACION_COORDINADOR")
                {
                    mensaje = "La solicitud no está en estado EN_APROBACION_COORDINADOR.";
                    return false;
                }
                
                // Cambiar a EN_APROBACION_DIRECTOR
                bool resultado = CambiarEstado(idSolicitud, "EN_APROBACION_DIRECTOR", codigoUsuario, observaciones ?? "Aprobado por Coordinador", out mensaje);
                
                if (resultado)
                {
                    // Notificar a Director Financiero
                    var directores = UsuarioBL.ObtenerPorRol("DirectorFinanciero");
                    foreach (var dir in directores)
                    {
                        NotificacionBL.EnviarNotificacion(
                            codigoUsuario: dir.CodigoUsuario,
                            titulo: "Solicitud requiere aprobación final",
                            mensaje: $"Solicitud AOCR #{idSolicitud} requiere su aprobación como Director.",
                            tipo: CapaDatos.Constants.TiposNotificacion.WARNING,
                            url: $"/SolicitudAOCR/Detalle/{idSolicitud}"
                        );
                    }
                    
                    LogBL.RegistrarInfo($"Solicitud {idSolicitud} aprobada por Coordinador, usuario {codigoUsuario}", "SolicitudAOCR");
                }
                
                return resultado;
            }
            catch (Exception ex)
            {
                mensaje = "Error al aprobar por Coordinador: " + ex.Message;
                LogBL.RegistrarError("Error en AprobarCoordinador", ex.ToString(), "SolicitudAOCR");
                return false;
            }
        }
        
        /// <summary>
        /// Aprobación FINAL por Director - Pasa a APROBADO
        /// </summary>
        public static bool AprobarDirector(int idSolicitud, int codigoUsuario, string observaciones, out string mensaje)
        {
            try
            {
                var solicitud = ObtenerPorId(idSolicitud);
                if (solicitud == null)
                {
                    mensaje = "Solicitud no encontrada.";
                    return false;
                }
                
                if (solicitud.Estado != "EN_APROBACION_DIRECTOR")
                {
                    mensaje = "La solicitud no está en estado EN_APROBACION_DIRECTOR.";
                    return false;
                }
                
                // Cambiar a APROBADO
                bool resultado = CambiarEstado(idSolicitud, "APROBADO", codigoUsuario, observaciones ?? "Aprobado por Director", out mensaje);
                
                if (resultado)
                {
                    // Notificar al solicitante
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario: solicitud.CodigoUsuario,
                        titulo: "Solicitud AOCR aprobada",
                        mensaje: $"Su solicitud AOCR #{idSolicitud} ha sido aprobada por el Director. Se procederá a la emisión del certificado.",
                        tipo: CapaDatos.Constants.TiposNotificacion.SUCCESS,
                        url: $"/SolicitudAOCR/Detalle/{idSolicitud}"
                    );
                    
                    LogBL.RegistrarInfo($"Solicitud {idSolicitud} aprobada por Director, usuario {codigoUsuario}", "SolicitudAOCR");
                }
                
                return resultado;
            }
            catch (Exception ex)
            {
                mensaje = "Error al aprobar por Director: " + ex.Message;
                LogBL.RegistrarError("Error en AprobarDirector", ex.ToString(), "SolicitudAOCR");
                return false;
            }
        }
        
        /// <summary>
        /// Rechaza una solicitud desde cualquier estado
        /// </summary>
        public static bool Rechazar(int idSolicitud, int codigoUsuario, string motivoRechazo, out string mensaje)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(motivoRechazo))
                {
                    mensaje = "Debe especificar el motivo del rechazo.";
                    return false;
                }
                
                var solicitud = ObtenerPorId(idSolicitud);
                if (solicitud == null)
                {
                    mensaje = "Solicitud no encontrada.";
                    return false;
                }
                
                // No se puede rechazar si ya está en estado final
                if (solicitud.Estado == "RECHAZADO" || solicitud.Estado == "AOCR_ENTREGADO")
                {
                    mensaje = "La solicitud ya está en un estado final.";
                    return false;
                }
                
                // Cambiar a RECHAZADO
                bool resultado = CambiarEstado(idSolicitud, "RECHAZADO", codigoUsuario, motivoRechazo, out mensaje);
                
                if (resultado)
                {
                    // Notificar al solicitante
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario: solicitud.CodigoUsuario,
                        titulo: "Solicitud AOCR rechazada",
                        mensaje: $"Su solicitud AOCR #{idSolicitud} ha sido rechazada. Motivo: {motivoRechazo}",
                        tipo: CapaDatos.Constants.TiposNotificacion.ERROR,
                        url: $"/SolicitudAOCR/Detalle/{idSolicitud}"
                    );
                    
                    LogBL.RegistrarInfo($"Solicitud {idSolicitud} rechazada por usuario {codigoUsuario}. Motivo: {motivoRechazo}", "SolicitudAOCR");
                }
                
                return resultado;
            }
            catch (Exception ex)
            {
                mensaje = "Error al rechazar solicitud: " + ex.Message;
                LogBL.RegistrarError("Error en Rechazar", ex.ToString(), "SolicitudAOCR");
                return false;
            }
        }
        
        // ==========================================
        // EMISIÓN DE CERTIFICADO AOCR
        // ==========================================
        
        /// <summary>
        /// Marca solicitud como AOCR_EMITIDO después de generar el PDF
        /// </summary>
        public static bool MarcarAOCREmitido(int idSolicitud, string numeroAOCR, string rutaPDF, int codigoUsuario, out string mensaje)
        {
            try
            {
                var solicitud = ObtenerPorId(idSolicitud);
                if (solicitud == null)
                {
                    mensaje = "Solicitud no encontrada.";
                    return false;
                }
                
                if (solicitud.Estado != "APROBADO")
                {
                    mensaje = "La solicitud debe estar en estado APROBADO para emitir el certificado.";
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(numeroAOCR))
                {
                    mensaje = "Debe especificar el número de AOCR.";
                    return false;
                }
                
                // Actualizar número AOCR y ruta PDF
                solicitud.NumeroAOCR = numeroAOCR;
                solicitud.FechaEmision = DateTime.Now;
                
                // Cambiar a AOCR_EMITIDO
                bool resultado = CambiarEstado(idSolicitud, "AOCR_EMITIDO", codigoUsuario, $"AOCR emitido. Número: {numeroAOCR}", out mensaje);
                
                if (resultado)
                {
                    // Actualizar datos adicionales
                    new SolicitudAOCRDAO().ActualizarGeneral(solicitud);
                    
                    // Notificar al solicitante
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario: solicitud.CodigoUsuario,
                        titulo: "Certificado AOCR emitido",
                        mensaje: $"Su certificado AOCR #{numeroAOCR} ha sido emitido correctamente. Solicitud #{idSolicitud}",
                        tipo: CapaDatos.Constants.TiposNotificacion.SUCCESS,
                        url: $"/SolicitudAOCR/DescargarCertificado/{idSolicitud}"
                    );
                    
                    LogBL.RegistrarInfo($"AOCR emitido para solicitud {idSolicitud}. Número: {numeroAOCR}", "SolicitudAOCR");
                }
                
                return resultado;
            }
            catch (Exception ex)
            {
                mensaje = "Error al marcar AOCR emitido: " + ex.Message;
                LogBL.RegistrarError("Error en MarcarAOCREmitido", ex.ToString(), "SolicitudAOCR");
                return false;
            }
        }
        
        /// <summary>
        /// Marca certificado como AOCR_ENTREGADO (estado final)
        /// </summary>
        public static bool MarcarAOCREntregado(int idSolicitud, int codigoUsuario, DateTime? fechaEntrega, string observaciones, out string mensaje)
        {
            try
            {
                var solicitud = ObtenerPorId(idSolicitud);
                if (solicitud == null)
                {
                    mensaje = "Solicitud no encontrada.";
                    return false;
                }
                
                if (solicitud.Estado != "AOCR_EMITIDO")
                {
                    mensaje = "El certificado debe estar en estado AOCR_EMITIDO para marcarlo como entregado.";
                    return false;
                }
                
                // Cambiar a AOCR_ENTREGADO
                bool resultado = CambiarEstado(idSolicitud, "AOCR_ENTREGADO", codigoUsuario, observaciones ?? "Certificado AOCR entregado al solicitante", out mensaje);
                
                if (resultado)
                {
                    // Actualizar fecha de entrega
                    solicitud.FechaEntrega = fechaEntrega ?? DateTime.Now;
                    new SolicitudAOCRDAO().ActualizarGeneral(solicitud);
                    
                    // Notificar al solicitante
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario: solicitud.CodigoUsuario,
                        titulo: "Certificado AOCR entregado",
                        mensaje: $"Su certificado AOCR #{solicitud.NumeroAOCR} ha sido entregado. Proceso completado.",
                        tipo: CapaDatos.Constants.TiposNotificacion.SUCCESS,
                        url: $"/SolicitudAOCR/Detalle/{idSolicitud}"
                    );
                    
                    LogBL.RegistrarInfo($"AOCR entregado para solicitud {idSolicitud}", "SolicitudAOCR");
                }
                
                return resultado;
            }
            catch (Exception ex)
            {
                mensaje = "Error al marcar AOCR entregado: " + ex.Message;
                LogBL.RegistrarError("Error en MarcarAOCREntregado", ex.ToString(), "SolicitudAOCR");
                return false;
            }
        }
    }
}