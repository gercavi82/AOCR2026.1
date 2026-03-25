using System;
using System.Collections.Generic;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Orquestador central del workflow de inspeccion.
    /// Reutiliza BL/DAO existentes y opera como capa incremental sin romper rutas ni controladores actuales.
    /// </summary>
    public class InspeccionWorkflowService
    {
        private readonly InspeccionDAO _inspeccionDAO;
        private readonly InspeccionBL _inspeccionBL;
        private readonly HallazgoBL _hallazgoBL;
        private readonly SolicitudAOCRDAO _solicitudDAO;
        private readonly AuditoriaService _auditoriaService;
        private readonly ValidacionDocumentalService _validacionDocumentalService;
        private readonly IntegracionInspeccionOrService _integracionInspeccionOrService;

        public InspeccionWorkflowService()
        {
            _inspeccionDAO = new InspeccionDAO();
            _inspeccionBL = new InspeccionBL();
            _hallazgoBL = new HallazgoBL();
            _solicitudDAO = new SolicitudAOCRDAO();
            _auditoriaService = new AuditoriaService();
            _validacionDocumentalService = new ValidacionDocumentalService();
            _integracionInspeccionOrService = new IntegracionInspeccionOrService();
        }

        public ResultadoOperacion RegistrarResultadoInspeccion(int inspeccionId, string resultado, string observacion, int usuarioId, string usuarioNombre)
        {
            return EvaluarInspeccion(inspeccionId, resultado, observacion, usuarioId, usuarioNombre);
        }

        public ResultadoOperacion EvaluarInspeccion(int inspeccionId, string resultado, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                if (inspeccionId <= 0)
                {
                    return ResultadoOperacion.Error("Inspección inválida");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                {
                    return ResultadoOperacion.Error("Inspección no encontrada");
                }

                var resultadoNormalizado = (resultado ?? string.Empty).Trim().ToUpperInvariant();
                var esSatisfactorio =
                    resultadoNormalizado == "SATISFACTORIO" ||
                    resultadoNormalizado == "APROBADO" ||
                    resultadoNormalizado == EstadosInspeccion.RESULTADO_SATISFACTORIO;

                var estadoDestino = esSatisfactorio
                    ? EstadosInspeccion.RESULTADO_SATISFACTORIO
                    : EstadosInspeccion.RESULTADO_NO_SATISFACTORIO;

                inspeccion.ResultadoEvaluacion = esSatisfactorio
                    ? EstadosInspeccion.RESULTADO_SATISFACTORIO
                    : EstadosInspeccion.RESULTADO_NO_SATISFACTORIO;
                inspeccion.Resultado = esSatisfactorio ? "APROBADO" : "RECHAZADO";
                inspeccion.EstadoDocumental = esSatisfactorio ? "ACEPTADA" : "OBSERVACION_DOCUMENTAL";
                if (!string.IsNullOrWhiteSpace(observacion))
                {
                    inspeccion.ObservacionesGenerales = observacion;
                }

                if (!_inspeccionBL.Actualizar(inspeccion, usuarioId))
                {
                    return ResultadoOperacion.Error("No se pudo actualizar la evaluación de inspección");
                }

                var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                if (!string.Equals(estadoActual, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase) &&
                    EstadosInspeccion.EsTransicionValida(estadoActual, EstadosInspeccion.INFORME_ELABORADO))
                {
                    _inspeccionBL.CambiarEstado(
                        inspeccionId,
                        EstadosInspeccion.INFORME_ELABORADO,
                        usuarioId,
                        "Resultado registrado con informe asociado.",
                        usuarioNombre,
                        "RESULTADO_INSPECCION");

                    estadoActual = EstadosInspeccion.INFORME_ELABORADO;
                }

                var cambioEstado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    estadoDestino,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    esSatisfactorio ? "EVALUACION_APROBADA" : "EVALUACION_CON_NC");

                if (!cambioEstado.Exitoso)
                {
                    return cambioEstado;
                }

                EmitirNotificacionEvento(inspeccionId, esSatisfactorio ? "APROBACION_INSPECCION" : "NC_GENERADAS", observacion);

                return ResultadoOperacion.Ok(null, "Resultado de inspección registrado correctamente");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al evaluar inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion SubsanarInspeccion(int inspeccionId, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                if (inspeccionId <= 0)
                {
                    return ResultadoOperacion.Error("Inspección inválida");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                {
                    return ResultadoOperacion.Error("Inspección no encontrada");
                }

                inspeccion.EstadoDocumental = "SUBSANADA";
                if (!string.IsNullOrWhiteSpace(observacion))
                {
                    inspeccion.Comentarios = string.IsNullOrWhiteSpace(inspeccion.Comentarios)
                        ? observacion
                        : (inspeccion.Comentarios + " | " + observacion);
                }

                if (!_inspeccionBL.Actualizar(inspeccion, usuarioId))
                {
                    return ResultadoOperacion.Error("No se pudo actualizar la información de subsanación");
                }

                var resultado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    EstadosInspeccion.SUBSANADA,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    "SUBSANACION");

                if (resultado.Exitoso)
                {
                    EmitirNotificacionEvento(inspeccionId, "DOCUMENTOS_SUBSANADOS", observacion);
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al subsanar inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion RevalidarInspeccion(int inspeccionId, bool aprobada, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                var estadoDestino = aprobada
                    ? EstadosInspeccion.EN_INSPECCION
                    : EstadosInspeccion.OBSERVADA;

                var resultado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    estadoDestino,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    "REVALIDACION");

                if (resultado.Exitoso)
                {
                    EmitirNotificacionEvento(inspeccionId, aprobada ? "REVALIDACION_OK" : "REVALIDACION_RECHAZADA", observacion);
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al revalidar inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion SolicitarNuevaInspeccion(int inspeccionId, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                var resultado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    EstadosInspeccion.OBSERVADA,
                    usuarioId,
                    usuarioNombre,
                    string.IsNullOrWhiteSpace(observacion) ? "Se solicita una nueva inspección." : observacion,
                    "NUEVA_INSPECCION");

                if (resultado.Exitoso)
                {
                    EmitirNotificacionEvento(inspeccionId, "DEVOLUCION_INSPECCION", observacion);
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al solicitar nueva inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion RegistrarNoConformidad(int inspeccionId, string descripcion, string criticidad, int usuarioId, string usuarioNombre)
        {
            try
            {
                if (inspeccionId <= 0)
                {
                    return ResultadoOperacion.Error("Inspección inválida");
                }

                if (string.IsNullOrWhiteSpace(descripcion))
                {
                    return ResultadoOperacion.Error("La descripción de la no conformidad es obligatoria");
                }

                var hallazgo = new Hallazgo
                {
                    CodigoInspeccion = inspeccionId,
                    Descripcion = descripcion,
                    Criticidad = string.IsNullOrWhiteSpace(criticidad) ? "MEDIA" : criticidad,
                    Estado = "ABIERTO"
                };

                var hallazgoId = _hallazgoBL.Crear(hallazgo, usuarioNombre);
                if (hallazgoId <= 0)
                {
                    return ResultadoOperacion.Error("No se pudo registrar la no conformidad");
                }

                _auditoriaService.RegistrarAccionInspeccion(
                    inspeccionId,
                    "REGISTRO_NO_CONFORMIDAD",
                    usuarioId,
                    usuarioNombre,
                    descripcion,
                    null,
                    "Criticidad: " + hallazgo.Criticidad + ", HallazgoId: " + hallazgoId);

                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion != null)
                {
                    var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                    if (EstadosInspeccion.EsTransicionValida(estadoActual, EstadosInspeccion.OBSERVADA))
                    {
                        CambiarEstadoConNotificacion(
                            inspeccionId,
                            EstadosInspeccion.OBSERVADA,
                            usuarioId,
                            usuarioNombre,
                            "No conformidad registrada: " + descripcion,
                            "NC");
                    }
                }

                EmitirNotificacionEvento(inspeccionId, "NC_GENERADAS", descripcion);

                return ResultadoOperacion.Ok(hallazgoId, "No conformidad registrada correctamente");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al registrar no conformidad: " + ex.Message);
            }
        }

        private ResultadoOperacion CambiarEstadoConNotificacion(
            int inspeccionId,
            string estadoDestino,
            int usuarioId,
            string usuarioNombre,
            string observacion,
            string origen)
        {
            if (inspeccionId <= 0)
            {
                return ResultadoOperacion.Error("Inspección inválida");
            }

            try
            {
                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                {
                    return ResultadoOperacion.Error("Inspección no encontrada");
                }

                var estadoAnterior = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                var estadoDestinoNormalizado = EstadosInspeccion.NormalizarEstado(estadoDestino);

                if (string.Equals(estadoDestinoNormalizado, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase))
                {
                    var validacionDocs = _validacionDocumentalService.PuedeAvanzarEtapa(inspeccion.CodigoSolicitud, "APROBACION_INSPECCION");
                    if (!validacionDocs.EsValido)
                    {
                        return ResultadoOperacion.Error("No se puede cerrar la inspección. " + ConstruirMensajeFaltantes(validacionDocs));
                    }
                }

                var ok = _inspeccionBL.CambiarEstado(
                    inspeccionId,
                    estadoDestinoNormalizado,
                    usuarioId,
                    observacion,
                    usuarioNombre,
                    origen);

                if (!ok)
                {
                    return ResultadoOperacion.Error("No se pudo cambiar el estado de la inspección");
                }

                _auditoriaService.RegistrarCambioEstadoInspeccion(
                    inspeccionId,
                    estadoAnterior,
                    estadoDestinoNormalizado,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    null,
                    "Origen: " + (origen ?? "N/A") + ", Core: " + EstadosInspeccionCore.ObtenerEstadoCore(estadoDestinoNormalizado, inspeccion.Resultado));

                NotificarCambioEstado(inspeccion, estadoDestinoNormalizado, observacion);

                if (string.Equals(estadoDestinoNormalizado, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase))
                {
                    EvaluarGeneracionOrSiCorresponde(inspeccion.CodigoSolicitud, usuarioNombre, usuarioId);
                }

                return ResultadoOperacion.Ok(null, "Estado actualizado correctamente");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error(ex.Message);
            }
        }

        private void NotificarCambioEstado(Inspeccion inspeccion, string estadoDestino, string observacion)
        {
            try
            {
                if (inspeccion == null)
                {
                    return;
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                if (solicitud == null)
                {
                    return;
                }

                var titulo = "Actualización de inspección";
                var mensaje = "La inspección #" + inspeccion.CodigoInspeccion + " cambió a estado " + estadoDestino + ".";
                if (!string.IsNullOrWhiteSpace(observacion))
                {
                    mensaje += " Observación: " + observacion;
                }

                var url = "/Inspeccion/Detalle/" + inspeccion.CodigoInspeccion;
                var destinatarios = ConstruirDestinatarios(inspeccion, solicitud, estadoDestino);

                foreach (var codigoUsuario in destinatarios)
                {
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario,
                        titulo,
                        mensaje,
                        TiposNotificacion.INFO,
                        url,
                        TiposNotificacion.CATEGORIA_INSPECCION,
                        inspeccion.CodigoInspeccion,
                        "aocr_tbinspeccion");
                }
            }
            catch
            {
                // No bloquear el flujo principal por fallo de notificación.
            }
        }

        private void EmitirNotificacionEvento(int inspeccionId, string evento, string observacion)
        {
            try
            {
                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                {
                    return;
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                if (solicitud == null)
                {
                    return;
                }

                var titulo = "Evento de inspección";
                var mensaje = "Inspección #" + inspeccion.CodigoInspeccion + ": " + evento + ".";
                var tipo = TiposNotificacion.INFO;

                switch ((evento ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "NC_GENERADAS":
                        titulo = "No conformidades generadas";
                        mensaje = "Se registraron no conformidades en la inspección #" + inspeccion.CodigoInspeccion + ".";
                        tipo = TiposNotificacion.WARNING;
                        break;
                    case "DOCUMENTOS_SUBSANADOS":
                        titulo = "Documentación subsanada";
                        mensaje = "La documentación técnica de la inspección #" + inspeccion.CodigoInspeccion + " fue subsanada y requiere revisión.";
                        tipo = TiposNotificacion.INFO;
                        break;
                    case "DEVOLUCION_INSPECCION":
                        titulo = "Inspección devuelta";
                        mensaje = "La inspección #" + inspeccion.CodigoInspeccion + " fue devuelta para ajustes.";
                        tipo = TiposNotificacion.WARNING;
                        break;
                    case "APROBACION_INSPECCION":
                        titulo = "Inspección aprobada";
                        mensaje = "La inspección #" + inspeccion.CodigoInspeccion + " fue aprobada y continúa al siguiente paso.";
                        tipo = TiposNotificacion.SUCCESS;
                        break;
                    case "REVALIDACION_OK":
                        titulo = "Revalidación satisfactoria";
                        mensaje = "La revalidación de la inspección #" + inspeccion.CodigoInspeccion + " fue satisfactoria.";
                        tipo = TiposNotificacion.SUCCESS;
                        break;
                    case "REVALIDACION_RECHAZADA":
                        titulo = "Revalidación con observaciones";
                        mensaje = "La revalidación de la inspección #" + inspeccion.CodigoInspeccion + " mantiene observaciones pendientes.";
                        tipo = TiposNotificacion.WARNING;
                        break;
                }

                if (!string.IsNullOrWhiteSpace(observacion))
                {
                    mensaje += " Observación: " + observacion;
                }

                var url = "/Inspeccion/Detalle/" + inspeccion.CodigoInspeccion;
                foreach (var codigoUsuario in ConstruirDestinatarios(inspeccion, solicitud, evento))
                {
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario,
                        titulo,
                        mensaje,
                        tipo,
                        url,
                        TiposNotificacion.CATEGORIA_INSPECCION,
                        inspeccion.CodigoInspeccion,
                        "aocr_tbinspeccion");
                }
            }
            catch
            {
                // Notificación no bloqueante.
            }
        }

        private HashSet<int> ConstruirDestinatarios(Inspeccion inspeccion, SolicitudAOCR solicitud, string contexto)
        {
            var destinatarios = new HashSet<int>();
            if (inspeccion == null || solicitud == null)
            {
                return destinatarios;
            }

            var contextoNormalizado = (contexto ?? string.Empty).Trim().ToUpperInvariant();

            if (solicitud.CodigoUsuario > 0)
            {
                destinatarios.Add(solicitud.CodigoUsuario);
            }

            if (solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0)
            {
                destinatarios.Add(solicitud.CodigoTecnico.Value);
            }

            if (inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
            {
                destinatarios.Add(inspeccion.CodigoInspector.Value);
            }

            if (contextoNormalizado == "NC_GENERADAS" || contextoNormalizado == "DEVOLUCION_INSPECCION")
            {
                return destinatarios;
            }

            return destinatarios;
        }

        private void EvaluarGeneracionOrSiCorresponde(int codigoSolicitud, string usuarioNombre, int usuarioId)
        {
            if (codigoSolicitud <= 0)
            {
                return;
            }

            try
            {
                var resultadoOr = _integracionInspeccionOrService.GenerarORSiCorresponde(codigoSolicitud, usuarioNombre);
                _auditoriaService.RegistrarEvento(
                    modulo: "IntegracionInspeccionOR",
                    accion: resultadoOr.Exitoso ? "OR_EVALUADA_OK" : "OR_EVALUADA_BLOQUEADA",
                    entidad: "aocr_tbsolicitud",
                    entidadId: codigoSolicitud,
                    estadoAnterior: null,
                    estadoNuevo: resultadoOr.Exitoso ? "OR_HABILITADA" : "NO_HABILITADA",
                    usuarioId: usuarioId,
                    usuarioNombre: usuarioNombre,
                    observacion: resultadoOr.Mensaje,
                    ip: null,
                    datosResumen: "Evaluación de OR tras cierre de inspección.");
            }
            catch (Exception ex)
            {
                _auditoriaService.RegistrarEvento(
                    modulo: "IntegracionInspeccionOR",
                    accion: "OR_EVALUADA_ERROR",
                    entidad: "aocr_tbsolicitud",
                    entidadId: codigoSolicitud,
                    estadoAnterior: null,
                    estadoNuevo: "ERROR",
                    usuarioId: usuarioId,
                    usuarioNombre: usuarioNombre,
                    observacion: ex.Message,
                    ip: null,
                    datosResumen: "Error no bloqueante al evaluar OR.");
            }
        }

        private static string ConstruirMensajeFaltantes(ResultadoValidacionDocumental validacion)
        {
            if (validacion == null)
            {
                return "Validación documental inválida.";
            }

            var faltantes = validacion.DocumentosFaltantes ?? new List<string>();
            if (faltantes.Count > 0)
            {
                return "Faltan documentos: " + string.Join(", ", faltantes) + ".";
            }

            var errores = validacion.Errores ?? new List<string>();
            if (errores.Count > 0)
            {
                return string.Join(" ", errores.ToArray());
            }

            return "Existen observaciones documentales pendientes.";
        }
    }
}